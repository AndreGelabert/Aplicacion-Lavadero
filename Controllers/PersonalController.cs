using Firebase.Auth;
using Firebase.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

/// <summary>
/// Controlador para la gestión de personal.
/// Incluye filtros, paginación, búsqueda, acciones AJAX y ordenamiento.
/// </summary>
[Authorize(Roles = "Administrador")]
public class PersonalController : Controller
{
  #region Dependencias
    private readonly PersonalService _personalService;
    private readonly AuditService _auditService;

    /// <summary>
 /// Crea una nueva instancia del controlador de personal.
    /// </summary>
    public PersonalController(PersonalService personalService, AuditService auditService)
    {
_personalService = personalService;
        _auditService = auditService;
    }
    #endregion

    #region Vistas Principales

    /// <summary>
 /// Página principal de personal con filtros, orden y paginación.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
 List<string> estados,
        List<string> roles,
   int pageNumber = 1,
        int pageSize = 10,
     string sortBy = null,
        string sortOrder = null)
    {
 estados = ConfigurarEstadosDefecto(estados);

        sortBy ??= "NombreCompleto";
 sortOrder ??= "asc";

        var (empleados, currentPage, totalPages, visiblePages) = await ObtenerDatosEmpleados(
      estados, roles, pageNumber, pageSize, sortBy, sortOrder);

        var rolesUnicos = await _personalService.ObtenerRolesUnicos();

        ConfigurarViewBag(estados, roles, rolesUnicos, pageSize, currentPage, totalPages, visiblePages, sortBy, sortOrder);

        return View(empleados);
    }
    #endregion

    #region Búsqueda y Tabla Parcial

 /// <summary>
    /// Busca empleados por término de búsqueda (parcial para actualización dinámica).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> SearchPartial(
     string searchTerm,
 List<string> estados,
        List<string> roles,
        int pageNumber = 1,
        int pageSize = 10,
  string sortBy = null,
    string sortOrder = null)
    {
estados = ConfigurarEstadosDefecto(estados);

        sortBy ??= "NombreCompleto";
        sortOrder ??= "asc";

        var empleados = await _personalService.BuscarEmpleados(
            searchTerm, estados, roles, pageNumber, pageSize, sortBy, sortOrder);

var totalEmpleados = await _personalService.ObtenerTotalEmpleadosBusqueda(
        searchTerm, estados, roles);

        var totalPages = Math.Max((int)Math.Ceiling(totalEmpleados / (double)pageSize), 1);

        ViewBag.CurrentPage = pageNumber;
      ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = GetVisiblePages(pageNumber, totalPages);
   ViewBag.Estados = estados;
        ViewBag.Roles = roles;
        ViewBag.SortBy = sortBy;
  ViewBag.SortOrder = sortOrder;
      ViewBag.SearchTerm = searchTerm;

        return PartialView("_PersonalTable", empleados);
    }

 /// <summary>
 /// Devuelve la tabla parcial (sin búsqueda) with filters and order.
    /// </summary>
  [HttpGet]
    public async Task<IActionResult> TablePartial(
        List<string> estados,
        List<string> roles,
  int pageNumber = 1,
   int pageSize = 10,
string sortBy = null,
        string sortOrder = null)
    {
        estados = ConfigurarEstadosDefecto(estados);

        sortBy ??= "NombreCompleto";
 sortOrder ??= "asc";

     var empleados = await _personalService.ObtenerEmpleados(estados, roles, pageNumber, pageSize, sortBy, sortOrder);
        var totalPages = await _personalService.ObtenerTotalPaginas(estados, roles, pageSize);
        totalPages = Math.Max(totalPages, 1);

        ViewBag.CurrentPage = pageNumber;
        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = GetVisiblePages(pageNumber, totalPages);
   ViewBag.Estados = estados;
        ViewBag.Roles = roles;
   ViewBag.SortBy = sortBy;
        ViewBag.SortOrder = sortOrder;

        return PartialView("_PersonalTable", empleados);
    }
    #endregion

    #region Operaciones CRUD

    /// <summary>
    /// Actualiza el rol de un empleado.
    /// </summary>
[HttpPost]
    public async Task<IActionResult> UpdateRole(string id, string newRole)
    {
        try
  {
    await _personalService.ActualizarRol(id, newRole);

            // Auditoría
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
          var userEmail = User.FindFirstValue(ClaimTypes.Email);
     await _auditService.LogEvent(userId, userEmail, "Modificación de rol", id, "Empleado");

            // ✅ Si es petición AJAX, devolver JSON sin tocar TempData
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
    return Json(new { success = true, message = "Rol actualizado correctamente." });
         }

            // Solo establecer TempData para navegación tradicional
            TempData["RoleChangeEvent_UserId"] = id;
            TempData["RoleChangeEvent_NewRole"] = newRole;
       TempData["Success"] = "Rol actualizado correctamente.";
        }
  catch (Exception ex)
     {
         // Si es petición AJAX, devolver error sin tocar TempData
    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
     {
                return Json(new { success = false, message = $"Error al actualizar el rol: {ex.Message}" });
          }

       TempData["Error"] = $"Error al actualizar el rol: {ex.Message}";
   }

      return RedirectToAction("Index");
    }

    /// <summary>
    /// Desactiva un empleado.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> DeactivateEmployee(string id)
    {
        try
     {
            await _personalService.CambiarEstadoEmpleado(id, "Inactivo");

      // Auditoría
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
         await _auditService.LogEvent(userId, userEmail, "Desactivación de empleado", id, "Empleado");

      // ✅ Si es petición AJAX, devolver JSON sin tocar TempData
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
         {
             return Json(new { success = true, message = "Empleado desactivado correctamente." });
            }

            // Solo establecer TempData para navegación tradicional
            TempData["StateChangeEvent_UserId"] = id;
            TempData["StateChangeEvent_NewState"] = "Inactivo";
            TempData["Success"] = "Empleado desactivado correctamente.";
   }
        catch (Exception ex)
    {
       // Si es petición AJAX, devolver error sin tocar TempData
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
           return Json(new { success = false, message = $"Error al desactivar: {ex.Message}" });
            }

    TempData["Error"] = $"Error al desactivar el empleado: {ex.Message}";
        }

        return RedirectToAction("Index");
    }

    /// <summary>
    /// Reactiva un empleado.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ReactivateEmployee(string id)
    {
      try
   {
            await _personalService.CambiarEstadoEmpleado(id, "Activo");

       // Auditoría
         var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
      var userEmail = User.FindFirstValue(ClaimTypes.Email);
   await _auditService.LogEvent(userId, userEmail, "Reactivación de empleado", id, "Empleado");

            // ✅ Si es petición AJAX, devolver JSON sin tocar TempData
    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
  {
      return Json(new { success = true, message = "Empleado reactivado correctamente." });
            }

            // Solo establecer TempData para navegación tradicional
            TempData["StateChangeEvent_UserId"] = id;
     TempData["StateChangeEvent_NewState"] = "Activo";
         TempData["Success"] = "Empleado reactivado correctamente.";
  }
catch (Exception ex)
        {
   // Si es petición AJAX, devolver error sin tocar TempData
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
         return Json(new { success = false, message = $"Error al reactivar: {ex.Message}" });
}

       TempData["Error"] = $"Error al reactivar el empleado: {ex.Message}";
        }

        return RedirectToAction("Index");
    }
    #endregion

    #region Métodos Privados - Procesamiento de Negocio

    /// <summary>
    /// Configura los estados por defecto cuando no se recibe ninguno desde la vista.
    /// </summary>
    private static List<string> ConfigurarEstadosDefecto(List<string> estados)
    {
        estados ??= new List<string>();
    if (!estados.Any())
   estados.Add("Activo");
        return estados;
    }

    /// <summary>
    /// Obtiene los empleados según los filtros y orden actual, calcula la paginación y las páginas visibles.
    /// </summary>
    private async Task<(List<Empleado> empleados, int currentPage, int totalPages, List<int> visiblePages)>
        ObtenerDatosEmpleados(List<string> estados, List<string> roles,
        int pageNumber, int pageSize, string sortBy, string sortOrder)
    {
        var empleados = await _personalService.ObtenerEmpleados(estados, roles, pageNumber, pageSize, sortBy, sortOrder);
   var totalPages = Math.Max(await _personalService.ObtenerTotalPaginas(estados, roles, pageSize), 1);
        var currentPage = Math.Clamp(pageNumber, 1, totalPages);
        var visiblePages = GetVisiblePages(currentPage, totalPages);

        return (empleados, currentPage, totalPages, visiblePages);
    }
    #endregion

    #region Métodos Privados - Utilidades

    /// <summary>
    /// Calcula el conjunto de páginas visibles alrededor de la página actual.
  /// </summary>
    private List<int> GetVisiblePages(int currentPage, int totalPages, int range = 2)
    {
  var start = Math.Max(1, currentPage - range);
        var end = Math.Min(totalPages, currentPage + range);
        return Enumerable.Range(start, end - start + 1).ToList();
    }

  /// <summary>
    /// Copia a ViewBag la información común necesaria para renderizar la vista Index.
    /// </summary>
    private void ConfigurarViewBag(
   List<string> estados, List<string> roles, List<string> rolesUnicos,
    int pageSize, int currentPage, int totalPages, List<int> visiblePages,
   string sortBy, string sortOrder)
    {
        ViewBag.TotalPages = totalPages;
   ViewBag.VisiblePages = visiblePages;
        ViewBag.CurrentPage = currentPage;
   ViewBag.Estados = estados;
   ViewBag.Roles = roles;
        ViewBag.TodosLosRoles = rolesUnicos;
        ViewBag.PageSize = pageSize;
    ViewBag.SortBy = sortBy;
        ViewBag.SortOrder = sortOrder;
    }
  #endregion
}
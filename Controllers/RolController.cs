using Firebase.Models;
using Firebase.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

/// <summary>
/// Controlador para la gestión de roles y permisos.
/// Incluye filtros, paginación, búsqueda y acciones AJAX de creación, edición y eliminación.
/// </summary>
[Authorize]
public class RolController : Controller
{
    #region Dependencias
    private readonly RolService _rolService;
    private readonly AuditService _auditService;

    /// <summary>
    /// Crea una nueva instancia del controlador de roles.
    /// </summary>
    /// <param name="rolService">Servicio para operaciones de roles.</param>
    /// <param name="auditService">Servicio para registro de auditoría.</param>
    public RolController(RolService rolService, AuditService auditService)
    {
        _rolService = rolService;
        _auditService = auditService;
    }
    #endregion
    #region Vistas Principales

    /// <summary>
    /// Página principal de roles con filtros, orden y paginación.
    /// </summary>
    /// <param name="estado">Estado a filtrar ("Activo" o "Inactivo").</param>
    /// <param name="pageNumber">Número de página actual.</param>
    /// <param name="pageSize">Tamaño de página.</param>
    /// <param name="sortBy">Campo por el cual ordenar.</param>
    /// <param name="sortOrder">Dirección del ordenamiento.</param>
    /// <returns>Vista con la lista de roles.</returns>
    [HttpGet]
    [Permiso("Roles.Consultar", "Roles.Crear")]
    public async Task<IActionResult> Index(
        string? estado,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = "Nombre",
        string sortOrder = "asc")
    {
        if (!User.TienePermiso("Roles.Consultar"))
            return View(new List<Rol>());

        await _rolService.AsegurarRolesBaseAsync();

        if (!Request.Query.ContainsKey("estado")) estado = "Activo";

        var totalPages = Math.Max(await _rolService.ObtenerTotalPaginas(estado, pageSize), 1);
        pageNumber = Math.Clamp(pageNumber, 1, totalPages);

        var roles = await _rolService.ObtenerRoles(estado, pageNumber, pageSize, sortBy, sortOrder);
        var conteoUsuarios = await _rolService.ObtenerConteoUsuariosPorRol();

        ConfigurarViewBag(estado, pageNumber, totalPages, sortBy, sortOrder, null, conteoUsuarios);

        return View(roles);
    }
    #endregion

    #region Búsqueda y Tabla Parcial

    /// <summary>
    /// Busca roles por término de búsqueda (parcial para actualización dinámica).
    /// </summary>
    /// <param name="searchTerm">Término de búsqueda.</param>
    /// <param name="estado">Estado a filtrar.</param>
    /// <param name="pageNumber">Número de página actual.</param>
    /// <param name="pageSize">Tamaño de página.</param>
    /// <param name="sortBy">Campo por el cual ordenar.</param>
    /// <param name="sortOrder">Dirección del ordenamiento.</param>
    /// <returns>Vista parcial con la tabla de roles.</returns>
    [HttpGet]
    [Permiso("Roles.Consultar")]
    public async Task<IActionResult> SearchPartial(
        string? searchTerm,
        string? estado,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = "Nombre",
        string sortOrder = "asc")
    {
        var totalRoles = await _rolService.ObtenerTotalRolesBusqueda(searchTerm, estado);
        var totalPages = Math.Max((int)Math.Ceiling(totalRoles / (double)Math.Max(1, pageSize)), 1);
        pageNumber = Math.Clamp(pageNumber, 1, totalPages);

        var roles = await _rolService.BuscarRoles(searchTerm, estado, pageNumber, pageSize, sortBy, sortOrder);
        var conteoUsuarios = await _rolService.ObtenerConteoUsuariosPorRol();

        ConfigurarViewBag(estado, pageNumber, totalPages, sortBy, sortOrder, searchTerm, conteoUsuarios);

        return PartialView("_RolTable", roles);
    }

    /// <summary>
    /// Devuelve la tabla parcial (sin búsqueda) con filtros y orden.
    /// </summary>
    /// <param name="estado">Estado a filtrar.</param>
    /// <param name="pageNumber">Número de página actual.</param>
    /// <param name="pageSize">Tamaño de página.</param>
    /// <param name="sortBy">Campo por el cual ordenar.</param>
    /// <param name="sortOrder">Dirección del ordenamiento.</param>
    /// <returns>Vista parcial con la tabla de roles.</returns>
    [HttpGet]
    [Permiso("Roles.Consultar")]
    public async Task<IActionResult> TablePartial(
        string? estado,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = "Nombre",
        string sortOrder = "asc")
    {
        var totalPages = Math.Max(await _rolService.ObtenerTotalPaginas(estado, pageSize), 1);
        pageNumber = Math.Clamp(pageNumber, 1, totalPages);

        var roles = await _rolService.ObtenerRoles(estado, pageNumber, pageSize, sortBy, sortOrder);
        var conteoUsuarios = await _rolService.ObtenerConteoUsuariosPorRol();

        ConfigurarViewBag(estado, pageNumber, totalPages, sortBy, sortOrder, null, conteoUsuarios);

        return PartialView("_RolTable", roles);
    }
    #endregion

    #region Vistas Parciales

    /// <summary>
    /// Devuelve el formulario parcial para crear o editar un rol.
    /// </summary>
    /// <param name="id">ID del rol a editar; null para creación.</param>
    /// <returns>Vista parcial del formulario.</returns>
    [HttpGet]
    [Permiso("Roles.Consultar", "Roles.Crear")]
    public async Task<IActionResult> FormPartial(string? id = null)
    {
        if (!string.IsNullOrWhiteSpace(id) && !User.TienePermiso("Roles.Consultar"))
            return Forbid();

        if (string.IsNullOrWhiteSpace(id))
        {
            return PartialView("_RolForm", new Rol());
        }

        var rol = await _rolService.ObtenerRol(id);
        if (rol == null)
        {
            return NotFound();
        }

        return PartialView("_RolForm", rol);
    }
    #endregion

    #region Operaciones CRUD - AJAX

    /// <summary>
    /// Crea un rol vía AJAX.
    /// </summary>
    /// <param name="rol">Datos del rol a crear.</param>
    /// <returns>Resultado de la operación en JSON.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Permiso("Roles.Crear")]
    public async Task<IActionResult> CrearRolAjax(Rol rol)
    {
        await _rolService.AsegurarRolesBaseAsync();

        rol.Nombre = RolReglas.NormalizarNombre(rol.Nombre);
        rol.Descripcion = (rol.Descripcion ?? string.Empty).Trim();

        var errores = new Dictionary<string, string>();

        var errorNombre = RolReglas.ValidarNombre(rol.Nombre);
        if (errorNombre != null)
        {
            errores["Nombre"] = errorNombre;
        }

        var errorDescripcion = RolReglas.ValidarDescripcion(rol.Descripcion);
        if (errorDescripcion != null)
        {
            errores["Descripcion"] = errorDescripcion;
        }

        var errorPermisos = RolReglas.ValidarPermisos(rol.Permisos);
        if (errorPermisos != null)
        {
            errores["Permisos"] = errorPermisos;
        }

        if (errorNombre == null && await _rolService.ExisteRolConNombre(rol.Nombre))
        {
            errores["Nombre"] = RolReglas.MensajeRolDuplicado;
        }

        if (errores.Count > 0)
        {
            return Json(new { success = false, message = "Revise los errores del formulario.", errors = errores });
        }

        rol.Permisos = RolCatalogo.NormalizarPermisos(rol.Permisos).ToList();
        rol.Estado = "Activo";
        rol.FechaCreacion = DateTime.UtcNow;

        var id = await _rolService.CrearRol(rol);
        await RegistrarEvento("Alta Rol", id);

        return Json(new { success = true, message = "Rol creado exitosamente" });
    }

    /// <summary>
    /// Actualiza un rol vía AJAX.
    /// </summary>
    /// <param name="rol">Datos del rol a actualizar.</param>
    /// <returns>Resultado de la operación en JSON.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Permiso("Roles.Modificar")]
    public async Task<IActionResult> ActualizarRolAjax(Rol rol)
    {
        await _rolService.AsegurarRolesBaseAsync();

        var rolActual = await _rolService.ObtenerRol(rol.Id);
        if (rolActual == null)
        {
            return Json(new { success = false, message = "No se encontró el rol a actualizar." });
        }

        if (!string.Equals(rolActual.Estado, "Activo", StringComparison.OrdinalIgnoreCase))
        {
            return Json(new { success = false, message = "No se puede editar un rol inactivo." });
        }

        rol.Nombre = RolReglas.NormalizarNombre(rol.Nombre);
        rol.Descripcion = (rol.Descripcion ?? string.Empty).Trim();

        var errores = new Dictionary<string, string>();

        var errorNombre = RolReglas.ValidarNombre(rol.Nombre);
        if (errorNombre != null)
        {
            errores["Nombre"] = errorNombre;
        }

        var errorDescripcion = RolReglas.ValidarDescripcion(rol.Descripcion);
        if (errorDescripcion != null)
        {
            errores["Descripcion"] = errorDescripcion;
        }

        var errorPermisos = RolReglas.ValidarPermisos(rol.Permisos);
        if (errorPermisos != null)
        {
            errores["Permisos"] = errorPermisos;
        }

        var errorEdicion = RolReglas.ValidarEdicion(rolActual, rol.Nombre, rol.Permisos);
        if (errorEdicion != null)
        {
            if (string.Equals(errorEdicion, RolReglas.MensajeRolProtegidoNoEditable, StringComparison.Ordinal))
            {
                errores["Nombre"] = errorEdicion;
            }
            else
            {
                errores["Permisos"] = errorEdicion;
            }
        }

        if (errorNombre == null && await _rolService.ExisteRolConNombre(rol.Nombre, rol.Id))
        {
            errores["Nombre"] = RolReglas.MensajeRolDuplicado;
        }

        if (errores.Count > 0)
        {
            return Json(new { success = false, message = "Revise los errores del formulario.", errors = errores });
        }

        rol.Nombre = RolCatalogo.EsRolProtegido(rolActual.Nombre) ? rolActual.Nombre : rol.Nombre;
        rol.Descripcion = rol.Descripcion.Trim();
        rol.Permisos = RolCatalogo.NormalizarPermisos(rol.Permisos).ToList();
        rol.Estado = rolActual.Estado;
        rol.FechaCreacion = rolActual.FechaCreacion;

        await _rolService.ActualizarRolConReasignacion(rol, rolActual.Nombre);
        await RegistrarEvento("Modificacion Rol", rol.Id);

        return Json(new { success = true, message = "Rol actualizado exitosamente" });
    }

    /// <summary>
    /// Desactiva un rol vía AJAX sin eliminar sus datos.
    /// </summary>
    /// <param name="id">ID del rol a eliminar.</param>
    /// <returns>Resultado de la operación en JSON.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Permiso("Roles.Eliminar")]
    public async Task<IActionResult> EliminarRolAjax(string? id)
    {
        await _rolService.AsegurarRolesBaseAsync();

        var rol = await _rolService.ObtenerRol(id ?? string.Empty);
        if (rol == null)
        {
            return Json(new { success = false, message = "No se encontró el rol a eliminar." });
        }

        var conteoUsuarios = await _rolService.ObtenerConteoUsuariosPorRol();
        conteoUsuarios.TryGetValue(rol.Nombre, out var usuariosAsignados);

        var errorEliminacion = RolReglas.ValidarEliminacion(rol, usuariosAsignados);
        if (errorEliminacion != null)
        {
            return Json(new { success = false, message = errorEliminacion });
        }

        await _rolService.DesactivarRol(rol.Id);
        await RegistrarEvento("Desactivacion Rol", rol.Id);

        return Json(new { success = true, message = "Rol desactivado exitosamente" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Permiso("Roles.Modificar")]
    public async Task<IActionResult> ReactivarRolAjax(string? id)
    {
        var rol = await _rolService.ObtenerRol(id ?? string.Empty);
        if (rol == null)
            return Json(new { success = false, message = "No se encontró el rol a reactivar." });

        await _rolService.ReactivarRol(rol.Id);
        await RegistrarEvento("Reactivacion Rol", rol.Id);
        return Json(new { success = true, message = "Rol reactivado exitosamente" });
    }
    #endregion

    #region Métodos Privados - Utilidades

    /// <summary>
    /// Copia a ViewBag la información común necesaria para renderizar la tabla de roles.
    /// </summary>
    private void ConfigurarViewBag(
        string? estado,
        int currentPage,
        int totalPages,
        string sortBy,
        string sortOrder,
        string? searchTerm,
        Dictionary<string, int> conteoUsuarios)
    {
        ViewBag.CurrentPage = currentPage;
        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = GetVisiblePages(currentPage, totalPages);
        ViewBag.Estado = estado;
        ViewBag.SortBy = sortBy;
        ViewBag.SortOrder = sortOrder;
        ViewBag.SearchTerm = searchTerm;
        ViewBag.ConteoUsuarios = conteoUsuarios;
    }

    /// <summary>
    /// Calcula el conjunto de páginas visibles alrededor de la página actual.
    /// </summary>
    private static List<int> GetVisiblePages(int currentPage, int totalPages, int range = 2)
    {
        var start = Math.Max(1, currentPage - range);
        var end = Math.Min(totalPages, currentPage + range);
        return Enumerable.Range(start, end - start + 1).ToList();
    }

    /// <summary>
    /// Registra un evento de auditoría asociado al usuario actual.
    /// </summary>
    private async Task RegistrarEvento(string accion, string targetId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        await _auditService.LogEvent(userId, userEmail, accion, targetId, "Rol");
    }
    #endregion
}

using Firebase.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[Authorize(Roles = "Administrador")]
public class PersonalController : Controller
{
    private readonly PersonalService _personalService;
    private readonly AuditService _auditService;

    public PersonalController(PersonalService personalService, AuditService auditService)
    {
        _personalService = personalService;
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        List<string> estados,
        string firstDocId = null,
        string lastDocId = null,
        int pageNumber = 1,
        int pageSize = 10)
    {
        var empleados = await _personalService.ObtenerEmpleados(estados, firstDocId, lastDocId, pageNumber, pageSize);
        var totalPages = await _personalService.ObtenerTotalPaginas(estados, pageSize);
        totalPages = Math.Max(totalPages, 1);
        var currentPage = Math.Clamp(pageNumber, 1, totalPages);
        var visiblePages = GetVisiblePages(currentPage, totalPages);

        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = visiblePages;
        ViewBag.CurrentPage = currentPage;
        ViewBag.Estados = estados;
        ViewBag.PageSize = pageSize;
        ViewBag.FirstDocId = empleados.FirstOrDefault()?.Id;
        ViewBag.LastDocId = empleados.LastOrDefault()?.Id;

        return View(empleados);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateRole(string id, string newRole)
    {
        try
        {
            await _personalService.ActualizarRol(id, newRole);

            // Configurar TempData para analytics
            TempData["RoleChangeEvent_UserId"] = id;
            TempData["RoleChangeEvent_NewRole"] = newRole;

            // Configurar mensaje de éxito
            TempData["Success"] = "Rol actualizado correctamente.";

            // Auditoría
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            await _auditService.LogEvent(userId, userEmail, "Modificación de rol", id, "Empleado");

            // Si es petición AJAX, devolver JSON
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = true, message = "Rol actualizado correctamente." });
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error al actualizar el rol: {ex.Message}";

            // Si es petición AJAX, devolver error
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = false, message = $"Error al actualizar el rol: {ex.Message}" });
            }
        }

        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> DeactivateEmployee(string id)
    {
        try
        {
            await _personalService.CambiarEstadoEmpleado(id, "Inactivo");

            // Configurar TempData para analytics
            TempData["StateChangeEvent_UserId"] = id;
            TempData["StateChangeEvent_NewState"] = "Inactivo";

            // Configurar mensaje de éxito
            TempData["Success"] = "Empleado desactivado correctamente.";

            // Auditoría
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            await _auditService.LogEvent(userId, userEmail, "Desactivación de empleado", id, "Empleado");

            // Si es petición AJAX, devolver JSON
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = true, message = "Empleado desactivado correctamente." });
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error al desactivar el empleado: {ex.Message}";

            // Si es petición AJAX, devolver error
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = false, message = $"Error al desactivar el empleado: {ex.Message}" });
            }
        }

        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> ReactivateEmployee(string id)
    {
        try
        {
            await _personalService.CambiarEstadoEmpleado(id, "Activo");

            // Configurar TempData para analytics
            TempData["StateChangeEvent_UserId"] = id;
            TempData["StateChangeEvent_NewState"] = "Activo";

            // Configurar mensaje de éxito
            TempData["Success"] = "Empleado reactivado correctamente.";

            // Auditoría
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            await _auditService.LogEvent(userId, userEmail, "Reactivación de empleado", id, "Empleado");

            // Si es petición AJAX, devolver JSON
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = true, message = "Empleado reactivado correctamente." });
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error al reactivar el empleado: {ex.Message}";

            // Si es petición AJAX, devolver error
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = false, message = $"Error al reactivar el empleado: {ex.Message}" });
            }
        }

        return RedirectToAction("Index");
    }

    private List<int> GetVisiblePages(int currentPage, int totalPages, int range = 2)
    {
        var start = Math.Max(1, currentPage - range);
        var end = Math.Min(totalPages, currentPage + range);
        return Enumerable.Range(start, end - start + 1).ToList();
    }
}
using Firebase.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

/// <summary>
/// Controlador para la gestión de tipos de documento.
/// </summary>
[Authorize(Roles = "Administrador,Empleado")]
public class TipoDocumentoController : Controller
{
    private readonly TipoDocumentoService _tipoDocumentoService;
    private readonly AuditService _auditService;

    public TipoDocumentoController(TipoDocumentoService tipoDocumentoService, AuditService auditService)
    {
        _tipoDocumentoService = tipoDocumentoService;
        _auditService = auditService;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearTipoDocumento(string nombreTipo)
    {
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nombreTipo))
                {
                    return Json(new { success = false, message = "El nombre del tipo de documento es obligatorio." });
                }

                if (await _tipoDocumentoService.ExisteTipoDocumento(nombreTipo))
                {
                    return Json(new { success = false, message = "Ya existe un tipo de documento con el mismo nombre." });
                }

                var docId = await _tipoDocumentoService.CrearTipoDocumento(nombreTipo);
                await RegistrarEvento("Creacion de tipo de documento", docId, "TipoDocumento");

                var tiposActualizados = await _tipoDocumentoService.ObtenerTiposDocumento();

                return Json(new
                {
                    success = true,
                    message = "Tipo de documento creado correctamente.",
                    tipos = tiposActualizados
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al crear el tipo de documento: {ex.Message}" });
            }
        }
        return BadRequest();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarTipoDocumento(string nombreTipo)
    {
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nombreTipo))
                {
                    return Json(new { success = false, message = "Debe seleccionar un tipo de documento." });
                }

                // TODO: Validar si hay clientes usando este tipo antes de eliminar (si es requerido)
                // Por ahora permitimos eliminar, pero idealmente deberíamos chequear.

                var idsEliminados = await _tipoDocumentoService.EliminarTipoDocumento(nombreTipo);
                foreach (var id in idsEliminados)
                {
                    await RegistrarEvento("Eliminacion de tipo de documento", id, "TipoDocumento");
                }

                var tiposActualizados = await _tipoDocumentoService.ObtenerTiposDocumento();

                return Json(new
                {
                    success = true,
                    message = "Tipo de documento eliminado correctamente.",
                    tipos = tiposActualizados
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al eliminar el tipo de documento: {ex.Message}" });
            }
        }
        return BadRequest();
    }

    private async Task RegistrarEvento(string accion, string targetId, string entidad)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        await _auditService.LogEvent(userId, userEmail, accion, targetId, entidad);
    }
}

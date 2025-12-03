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
    private readonly ClienteService _clienteService;

    public TipoDocumentoController(
        TipoDocumentoService tipoDocumentoService,
        AuditService auditService,
        ClienteService clienteService)
    {
        _tipoDocumentoService = tipoDocumentoService;
        _auditService = auditService;
        _clienteService = clienteService;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearTipoDocumento(string nombreTipo, string? formato = null)
    {
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nombreTipo))
                {
                    return Json(new { success = false, message = "El nombre del tipo de documento es obligatorio." });
                }

                if (nombreTipo.Length < 3)
                {
                    return Json(new { success = false, message = "El nombre debe tener al menos 3 caracteres." });
                }

                // ✅ NUEVO: Validar que el formato sea obligatorio
                if (string.IsNullOrWhiteSpace(formato))
                {
                    return Json(new { success = false, message = "El formato del documento es obligatorio." });
                }

                if (formato.Length < 3)
                {
                    return Json(new { success = false, message = "El formato debe tener al menos 3 caracteres." });
                }

                // ✅ NUEVO: Validar que el formato solo contenga caracteres permitidos
                if (!System.Text.RegularExpressions.Regex.IsMatch(formato, @"^[nl.\-]{3,}$"))
                {
                    return Json(new { success = false, message = "El formato solo puede contener 'n' (números), 'l' (letras), '.' y '-'. Mínimo 3 caracteres." });
                }

                if (await _tipoDocumentoService.ExisteTipoDocumento(nombreTipo))
                {
                    return Json(new { success = false, message = "Ya existe un tipo de documento con el mismo nombre." });
                }

                var docId = await _tipoDocumentoService.CrearTipoDocumento(nombreTipo, formato);
                await RegistrarEvento("Creacion de tipo de documento", docId, "TipoDocumento");

                var tiposActualizados = await _tipoDocumentoService.ObtenerTiposDocumentoCompletos();

                return Json(new
                {
                    success = true,
                    message = "Tipo de documento creado correctamente.",
                    tipos = tiposActualizados.Select(t => t.Nombre).ToList(),
                    tiposCompletos = tiposActualizados
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al crear el tipo de documento: {ex.Message}" });
            }
        }
        return BadRequest();
    }

    /// <summary>
    /// Obtiene el formato de un tipo de documento específico.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ObtenerFormato(string nombreTipo)
    {
        if (string.IsNullOrWhiteSpace(nombreTipo))
        {
            return Json(new { success = false, formato = (string?)null, regex = (string?)null });
        }

        var tipo = await _tipoDocumentoService.ObtenerTipoDocumentoPorNombre(nombreTipo);
        if (tipo == null)
        {
            return Json(new { success = false, formato = (string?)null, regex = (string?)null });
        }

        return Json(new
        {
            success = true,
            formato = tipo.Formato,
            regex = tipo.ObtenerRegexPattern()
        });
    }

    /// <summary>
    /// Obtiene todos los tipos de documento con sus formatos.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ObtenerTiposConFormatos()
    {
        var tipos = await _tipoDocumentoService.ObtenerTiposDocumentoCompletos();
        return Json(tipos.Select(t => new
        {
            nombre = t.Nombre,
            formato = t.Formato,
            regex = t.ObtenerRegexPattern()
        }));
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

                // ✅ NUEVO: Verificar si está en uso antes de eliminar
                var enUso = await _clienteService.ExisteTipoDocumentoEnUso(nombreTipo);
                if (enUso)
                {
                    return Json(new {
                        success = false,
                        message = $"No se puede eliminar el tipo '{nombreTipo}' porque está en uso por uno o más clientes."
                    });
                }

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

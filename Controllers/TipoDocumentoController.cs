using Firebase.Services;
using Firebase.Models;
using Firebase.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

/// <summary>
/// Controlador para la gestión de tipos de documento.
/// </summary>
[Authorize]
public class TipoDocumentoController : Controller
{
    private readonly TipoDocumentoService _tipoDocumentoService;

    public TipoDocumentoController(TipoDocumentoService tipoDocumentoService)
    {
        _tipoDocumentoService = tipoDocumentoService;
    }

    public sealed class TipoDocumentoModalInput
    {
        public string? Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Formato { get; set; } = string.Empty;
    }

    private ActorAuditoria Actor() => new(
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
        User.FindFirstValue(ClaimTypes.Email) ?? string.Empty);

    [HttpGet]
    [Permiso("Clientes.Crear", "Clientes.Modificar")]
    public async Task<IActionResult> ModalDatos() =>
        Json(await _tipoDocumentoService.ObtenerTodos(incluirInactivos: true));

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Permiso("Clientes.Crear", "Clientes.Modificar")]
    public async Task<IActionResult> ModalGuardar(TipoDocumentoModalInput input, bool crear)
    {
        if (!User.TienePermiso(crear ? "Clientes.Crear" : "Clientes.Modificar")) return Forbid();
        if (!crear && string.IsNullOrWhiteSpace(input.Id))
            return BadRequest(new { mensaje = "El tipo de documento no existe." });

        var resultado = await _tipoDocumentoService.GuardarConAuditoria(
            new TipoDocumento
            {
                Id = crear ? string.Empty : input.Id!,
                Nombre = input.Nombre,
                Formato = input.Formato
            },
            crear,
            Actor());

        if (!resultado.Resultado.EsExitoso)
            return BadRequest(new { mensaje = resultado.Resultado.MensajeError });

        var tiposActivos = await _tipoDocumentoService.ObtenerTiposDocumentoCompletos();
        return Json(new
        {
            mensaje = resultado.Resultado.MensajeExito,
            id = resultado.TipoDocumentoId,
            tipos = tiposActivos.Select(t => t.Nombre).ToList(),
            tiposCompletos = tiposActivos
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Permiso("Clientes.Modificar", "Clientes.Eliminar")]
    public async Task<IActionResult> ModalEstado(string id, string estado)
    {
        if (estado != "Activo" && estado != "Inactivo")
            return BadRequest(new { mensaje = "Estado inválido." });

        var resultado = await _tipoDocumentoService.CambiarEstadoConAuditoria(id, estado, Actor());
        if (!resultado.Resultado.EsExitoso)
            return BadRequest(new { mensaje = resultado.Resultado.MensajeError });

        var tiposActivos = await _tipoDocumentoService.ObtenerTiposDocumentoCompletos();
        return Json(new
        {
            mensaje = resultado.Resultado.MensajeExito,
            tipos = tiposActivos.Select(t => t.Nombre).ToList(),
            tiposCompletos = tiposActivos
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Permiso("Clientes.Crear")]
    public async Task<IActionResult> CrearTipoDocumento(string nombreTipo, string? formato = null)
    {
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            try
            {
                var resultado = await _tipoDocumentoService.GuardarConAuditoria(
                    new TipoDocumento
                    {
                        Id = string.Empty,
                        Nombre = nombreTipo ?? string.Empty,
                        Formato = formato ?? string.Empty
                    },
                    crear: true,
                    Actor());

                if (!resultado.Resultado.EsExitoso)
                    return Json(new { success = false, message = resultado.Resultado.MensajeError });

                var tiposActualizados = await _tipoDocumentoService.ObtenerTiposDocumentoCompletos();

                return Json(new
                {
                    success = true,
                    message = resultado.Resultado.MensajeExito,
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
    [Permiso("Clientes.Consultar")]
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
    [Permiso("Clientes.Consultar")]
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
    [Permiso("Clientes.Modificar", "Clientes.Eliminar")]
    public async Task<IActionResult> DesactivarTipoDocumento(string nombreTipo)
    {
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nombreTipo))
                {
                    return Json(new { success = false, message = "Debe seleccionar un tipo de documento." });
                }

                var tipo = await _tipoDocumentoService.ObtenerTipoDocumentoPorNombre(nombreTipo);
                if (tipo == null)
                {
                    return Json(new { success = false, message = "El tipo de documento no existe." });
                }

                var resultado = await _tipoDocumentoService.CambiarEstadoConAuditoria(tipo.Id, "Inactivo", Actor());
                if (!resultado.Resultado.EsExitoso)
                {
                    return Json(new { success = false, message = resultado.Resultado.MensajeError });
                }

                var tiposActualizados = await _tipoDocumentoService.ObtenerTiposDocumento();

                return Json(new
                {
                    success = true,
                    message = resultado.Resultado.MensajeExito,
                    tipos = tiposActualizados
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al desactivar el tipo de documento: {ex.Message}" });
            }
        }
        return BadRequest();
    }
}

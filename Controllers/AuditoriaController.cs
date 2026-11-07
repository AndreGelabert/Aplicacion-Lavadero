using Firebase.Models;
using Firebase.Security;
using Firebase.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>HTTP endpoints for audit listings and record details.</summary>
[Authorize]
public class AuditoriaController : Controller
{
    private readonly AuditService _auditService;
    private readonly AuditQueryService _queries;

    public AuditoriaController(AuditService auditService, AuditQueryService queries)
    {
        _auditService = auditService;
        _queries = queries;
    }

    [HttpGet]
    [Permiso("Auditoria.Consultar")]
    public async Task<IActionResult> Index([FromQuery] AuditoriaFilter filters)
        => RangoFechasInvertido(filters)
            ? BadRequest("La fecha Desde no puede ser posterior a la fecha Hasta.")
            : View(await _queries.ObtenerPantalla(filters));

    [HttpGet]
    [Permiso("Auditoria.Consultar")]
    public async Task<IActionResult> TablePartial([FromQuery] AuditoriaFilter filters)
        => RangoFechasInvertido(filters)
            ? BadRequest("La fecha Desde no puede ser posterior a la fecha Hasta.")
            : PartialView("_AuditoriaTable", await _queries.ObtenerTabla(filters));

    [HttpGet]
    [Permiso("Auditoria.Consultar")]
    public async Task<IActionResult> SearchPartial([FromQuery] AuditoriaFilter filters)
        => RangoFechasInvertido(filters)
            ? BadRequest("La fecha Desde no puede ser posterior a la fecha Hasta.")
            : PartialView("_AuditoriaTable", await _queries.Buscar(filters));

    private static bool RangoFechasInvertido(AuditoriaFilter filters)
        => filters.FechaInicio.HasValue && filters.FechaFin.HasValue
            && filters.FechaInicio.Value.Date > filters.FechaFin.Value.Date;

    #region Detalle de Registro

    /// <summary>
    /// Devuelve el detalle (antes/después) de un registro de auditoría por su ID de documento.
    /// Requiere permiso Auditoria.Consultar. Retorna 404 si no existe.
    /// </summary>
    [HttpGet]
    [Permiso("Auditoria.Consultar")]
    public async Task<IActionResult> Detalle(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Contains('/') || id == "." || id == ".." || System.Text.Encoding.UTF8.GetByteCount(id) > 1500)
            return NotFound();

        try
        {
        var registro = await _auditService.ObtenerDetalle(id);
        if (registro == null)
            return NotFound();

        return Json(new
        {
            docId = registro.DocId ?? "",
            action = registro.Action ?? "",
            targetType = registro.TargetType ?? "",
            targetId = registro.TargetId ?? "",
            nombreHistorico = registro.NombreHistorico ?? "",
            userEmail = registro.UserEmail ?? "",
            userId = registro.UserId ?? "",
            antes = registro.Antes,
            despues = registro.Despues,
            comparacionNoAplica = registro.ComparacionNoAplica,
            timestamp = registro.Timestamp.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss")
        });
        }
        catch (Exception)
        {
            return StatusCode(500, "No se pudo cargar el detalle de auditoría.");
        }
    }

    #endregion

}

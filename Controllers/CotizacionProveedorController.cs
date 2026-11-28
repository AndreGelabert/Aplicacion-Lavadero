using Firebase.Services;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Firebase.Controllers;

[AllowAnonymous]
[Route("api/cotizacion-proveedor")]
public class CotizacionProveedorController : ControllerBase
{
    private readonly Func<string, string, CancellationToken, Task<DetalleCotizacionProveedor>> obtenerDetalle;
    private readonly Func<string, string, RespuestaCotizacionProveedorRequest, CancellationToken, Task> responder;
    private readonly ILogger<CotizacionProveedorController> logger;

    public CotizacionProveedorController(FirestoreDb db, ILogger<CotizacionProveedorController> logger)
        : this(new CotizacionProveedorCapability(db), logger) { }

    internal CotizacionProveedorController(CotizacionProveedorCapability capability, ILogger<CotizacionProveedorController> logger)
        : this(capability.ObtenerDetalleAsync, capability.ResponderAsync, logger) { }

    internal CotizacionProveedorController(
        Func<string, string, CancellationToken, Task<DetalleCotizacionProveedor>> obtenerDetalle,
        Func<string, string, RespuestaCotizacionProveedorRequest, CancellationToken, Task> responder,
        ILogger<CotizacionProveedorController>? logger = null)
    {
        this.obtenerDetalle = obtenerDetalle;
        this.responder = responder;
        this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<CotizacionProveedorController>.Instance;
    }

    [HttpPost("detalle")]
    public async Task<IActionResult> Detalle([FromBody] CotizacionProveedorTokenRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.SolicitudId) || string.IsNullOrWhiteSpace(request.Token))
            return BadRequest();
        try
        {
            var detalle = await obtenerDetalle(request.SolicitudId, request.Token, HttpContext?.RequestAborted ?? CancellationToken.None);
            return Ok(detalle);
        }
        catch (CotizacionProveedorInvalidaException)
        {
            return BadRequest();
        }
        catch (OperationCanceledException) when (HttpContext?.RequestAborted.IsCancellationRequested != false)
        {
            throw;
        }
        catch (Exception)
        {
            logger.LogError(new EventId(1, "SupplierQuoteDetailUnavailable"), "Supplier quote detail unavailable");
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpPost("responder")]
    public async Task<IActionResult> Responder([FromBody] RespuestaCotizacionProveedorRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.SolicitudId) || string.IsNullOrWhiteSpace(request.Token) || request.Items == null || request.Items.Count == 0)
            return BadRequest();
        try
        {
            await responder(request.SolicitudId, request.Token, request, HttpContext?.RequestAborted ?? CancellationToken.None);
            return Ok();
        }
        catch (CotizacionProveedorDatosInvalidosException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (CotizacionProveedorInvalidaException)
        {
            return BadRequest();
        }
        catch (OperationCanceledException) when (HttpContext?.RequestAborted.IsCancellationRequested != false)
        {
            throw;
        }
        catch (Exception)
        {
            logger.LogError(new EventId(2, "SupplierQuoteResponseUnavailable"), "Supplier quote response unavailable");
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }
}

using Firebase.Services;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FirebaseLoginCustom.Controllers;

[AllowAnonymous]
[Route("api/turno-oferta-email")]
public class TurnoOfertaEmailController : ControllerBase
{
    private readonly Func<string, string, string, string, DateTime, bool, CancellationToken, Task> respond;
    private readonly ILogger<TurnoOfertaEmailController> logger;

    public TurnoOfertaEmailController(FirestoreDb db, ILogger<TurnoOfertaEmailController> logger) : this(new TurnoOfertaEmailCapability(db).RespondHttp, logger) { }

    internal TurnoOfertaEmailController(Func<string, string, string, string, DateTime, bool, Task> respond, ILogger<TurnoOfertaEmailController>? logger = null)
        : this((c, o, i, t, n, a, _) => respond(c, o, i, t, n, a), logger) { }

    internal TurnoOfertaEmailController(Func<string, string, string, string, DateTime, bool, CancellationToken, Task> respond, ILogger<TurnoOfertaEmailController>? logger = null)
    {
        this.respond = respond;
        this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<TurnoOfertaEmailController>.Instance;
    }

    [HttpPost("responder")]
    public async Task<IActionResult> Responder([FromBody] TurnoOfertaEmailResponse? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.CascadeId) || string.IsNullOrWhiteSpace(request.OfferId) ||
            string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.Token) || request.Accept == null)
            return BadRequest();
        try
        {
            var now = DateTime.UtcNow;
            now = now.AddTicks(-(now.Ticks % 10));
            await respond(request.CascadeId, request.OfferId, request.ClientId, request.Token, now, request.Accept.Value, HttpContext?.RequestAborted ?? CancellationToken.None);
            return Ok();
        }
        catch (TurnoOfertaEmailInvalidException)
        {
            return BadRequest();
        }
        catch (OperationCanceledException) when (HttpContext?.RequestAborted.IsCancellationRequested != false)
        {
            throw;
        }
        catch (Exception)
        {
            logger.LogError(new EventId(1, "EmailOfferResponseUnavailable"), "Email offer response unavailable");
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }
}

public sealed class TurnoOfertaEmailResponse
{
    public string? CascadeId { get; set; }
    public string? OfferId { get; set; }
    public string? ClientId { get; set; }
    public string? Token { get; set; }
    public bool? Accept { get; set; }
}

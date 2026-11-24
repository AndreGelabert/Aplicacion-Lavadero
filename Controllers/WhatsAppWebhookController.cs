using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Firebase.Models;
using Firebase.Models.WhatsApp;
using Firebase.Services;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace Firebase.Controllers;

/// <summary>
/// Controlador que maneja los webhooks de WhatsApp Cloud API
/// </summary>
[ApiController]
[Route("api/whatsapp")]
public class WhatsAppWebhookController : ControllerBase
{
    // Operational memory guard, not a documented Meta payload maximum.
    private const int MaxWebhookBodyBytes = 1024 * 1024;
    private readonly WhatsAppSettings _settings;
    private readonly WhatsAppFlowService _flowService;
    private readonly MetaWhatsAppService _whatsAppService;
    private readonly ILogger<WhatsAppWebhookController> _logger;

    public WhatsAppWebhookController(
        IOptions<WhatsAppSettings> settings,
        WhatsAppFlowService flowService,
        MetaWhatsAppService whatsAppService,
        ILogger<WhatsAppWebhookController> logger)
    {
        _settings = settings.Value;
        _flowService = flowService;
        _whatsAppService = whatsAppService;
        _logger = logger;
    }

    /// <summary>
    /// Endpoint GET para verificación del webhook por parte de Meta
    /// </summary>
    [HttpGet("webhook")]
    public IActionResult VerifyWebhook(
        [FromQuery(Name = "hub.mode")] string mode,
        [FromQuery(Name = "hub.verify_token")] string token,
        [FromQuery(Name = "hub.challenge")] string challenge)
    {
        _logger.LogInformation("WhatsApp webhook verification received");

        if (mode == "subscribe" && token == _settings.VerifyToken)
        {
            _logger.LogInformation("WhatsApp webhook verification succeeded");
            return Ok(challenge); // Devolver el challenge tal cual (sin JSON)
        }

        _logger.LogWarning("❌ Verificación fallida. Token no coincide.");
        return Forbid();
    }

    /// <summary>
    /// Endpoint POST para recibir mensajes de WhatsApp
    /// </summary>
    [HttpPost("webhook")]
    public async Task<IActionResult> ReceiveMessage()
    {
        using var requestScope = _logger.BeginScope(new Dictionary<string, object> { ["WhatsAppRequestId"] = Guid.NewGuid().ToString("N") });
        try
        {
            var signatures = Request.Headers["X-Hub-Signature-256"];
            if (string.IsNullOrWhiteSpace(_settings.AppSecret) || signatures.Count != 1)
            {
                _logger.LogWarning("WhatsApp webhook no autorizado: AppSecret configurado: {HasSecret}, encabezados de firma: {SigCount}",
                    !string.IsNullOrWhiteSpace(_settings.AppSecret), signatures.Count);
                return Unauthorized();
            }

            var signature = signatures[0];
            if (signature == null || signature.Length != 71 || !signature.StartsWith("sha256=", StringComparison.Ordinal))
            {
                _logger.LogWarning("WhatsApp webhook no autorizado: formato de firma inválido");
                return Unauthorized();
            }

            byte[] supplied;
            try { supplied = Convert.FromHexString(signature[7..]); }
            catch (FormatException)
            {
                _logger.LogWarning("WhatsApp webhook no autorizado: firma hexadecimal inválida");
                return Unauthorized();
            }

            if (Request.ContentLength > MaxWebhookBodyBytes)
                return StatusCode(StatusCodes.Status413PayloadTooLarge);

            using var raw = new MemoryStream();
            var buffer = new byte[8192];
            while (true)
            {
                // Read at most one byte past the limit, even without a trustworthy length.
                var remaining = MaxWebhookBodyBytes + 1 - (int)raw.Length;
                var count = await Request.Body.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), HttpContext.RequestAborted);
                if (count == 0) break;
                if (raw.Length + count > MaxWebhookBodyBytes)
                    return StatusCode(StatusCodes.Status413PayloadTooLarge);
                raw.Write(buffer, 0, count);
            }
            var bytes = raw.ToArray();
            var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(_settings.AppSecret), bytes);
            if (!CryptographicOperations.FixedTimeEquals(expected, supplied))
            {
                _logger.LogWarning("WhatsApp webhook no autorizado: la firma HMAC calculada no coincide con la recibida");
                return Unauthorized();
            }

            JsonDocument document;
            try { document = JsonDocument.Parse(bytes); }
            catch (JsonException) { return BadRequest(); }
            using var parsed = document;
            var body = parsed.RootElement;
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var messageValues = new List<WhatsAppValue>();

            // Validate the entire batch before performing any message side effects.
            if (body.ValueKind != JsonValueKind.Object ||
                !body.TryGetProperty("entry", out var entries) ||
                entries.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("Invalid WhatsApp webhook envelope: expected an entry array");
                return BadRequest();
            }

            // Procesar cada entrada
            foreach (var entry in entries.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object ||
                    !entry.TryGetProperty("changes", out var changes) ||
                    changes.ValueKind != JsonValueKind.Array)
                {
                    _logger.LogWarning("Invalid WhatsApp webhook entry: expected a changes array");
                    return BadRequest();
                }

                foreach (var change in changes.EnumerateArray())
                {
                    if (change.ValueKind != JsonValueKind.Object ||
                        !change.TryGetProperty("field", out var field) ||
                        field.ValueKind != JsonValueKind.String)
                    {
                        _logger.LogWarning("Invalid WhatsApp webhook change: expected a field name");
                        return BadRequest();
                    }

                    if (field.GetString() != "messages")
                    {
                        _logger.LogDebug("Ignoring non-message WhatsApp webhook event");
                        continue;
                    }

                    WhatsAppValue? value;
                    try
                    {
                        if (!change.TryGetProperty("value", out var rawValue) ||
                            rawValue.ValueKind != JsonValueKind.Object)
                        {
                            _logger.LogWarning("Invalid WhatsApp messages event: expected a value object");
                            return BadRequest();
                        }

                        value = rawValue.Deserialize<WhatsAppValue>(options);
                    }
                    catch (JsonException)
                    {
                        _logger.LogWarning("Invalid WhatsApp messages event: message schema validation failed");
                        return BadRequest();
                    }

                    if (value == null) return BadRequest();
                    messageValues.Add(value);
                }
            }

            foreach (var value in messageValues)
            {
                // Procesar mensajes entrantes
                if (value.Messages != null && value.Messages.Any())
                {
                    foreach (var message in value.Messages)
                    {
                        await ProcessIncomingMessage(message);
                    }
                }

                // Procesar cambios de estado (opcional: logging)
                if (value.Statuses != null && value.Statuses.Any())
                {
                    foreach (var status in value.Statuses)
                    {
                        _logger.LogInformation("WhatsApp message status event received");
                    }
                }
            }

            return Ok();
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (WhatsAppOperationException ex)
        {
            _logger.LogError("WhatsApp webhook failed: {Operation} {Classification} {StatusCode} {MetaErrorCode} {MetaErrorSubcode}", ex.Operation, ex.Classification, ex.StatusCode, ex.MetaErrorCode, ex.MetaErrorSubcode);
            return StatusCode(500, new { message = "WhatsApp processing failed", operation = ex.Operation, classification = ex.Classification, upstreamStatus = ex.StatusCode, metaErrorCode = ex.MetaErrorCode, metaErrorSubcode = ex.MetaErrorSubcode });
        }
        catch (Exception ex)
        {
            _logger.LogError("Error procesando webhook: {ExceptionType}", ex.GetType().Name);
            return StatusCode(500);
        }
    }

    /// <summary>
    /// Procesa un mensaje entrante individual
    /// </summary>
    private async Task ProcessIncomingMessage(WhatsAppIncomingMessage message)
    {
        var correlationId = string.IsNullOrEmpty(message.Id)
            ? Guid.NewGuid().ToString("N")
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(message.Id)));
        using var messageScope = _logger.BeginScope(new Dictionary<string, object> { ["WhatsAppMessageId"] = correlationId });
        try
        {
            var phoneNumber = message.From;
            string messageBody = null;

            // Extraer el contenido según el tipo de mensaje
            if (message.Type == "text" && message.Text != null)
            {
                messageBody = message.Text.Body;
            }
            else if (message.Type == "interactive" && message.Interactive != null)
            {
                // Usuario hizo click en un botón o lista
                if (message.Interactive.Type == "button_reply" && message.Interactive.ButtonReply != null)
                {
                    // Usar el ID del botón como contenido del mensaje
                    messageBody = message.Interactive.ButtonReply.Id;
                }
                else if (message.Interactive.Type == "list_reply" && message.Interactive.ListReply != null)
                {
                    // Usuario seleccionó una opción de lista
                    messageBody = message.Interactive.ListReply.Id;
                }
            }
            else if (message.Type == "button" && message.Button != null)
            {
                // Formato alternativo de botones
                messageBody = message.Button.Payload ?? message.Button.Text;
            }

            if (string.IsNullOrEmpty(messageBody))
            {
                _logger.LogInformation("WhatsApp message unsupported or empty");
                return;
            }

            _logger.LogInformation("WhatsApp message received");

            // A failed read receipt must not prevent the reply flow.
            try
            {
                await _whatsAppService.MarkMessageAsRead(message.Id);
            }
            catch (WhatsAppOperationException ex)
            {
                _logger.LogWarning("WhatsApp read receipt failed: {Classification} {StatusCode} {MetaErrorCode} {MetaErrorSubcode}", ex.Classification, ex.StatusCode, ex.MetaErrorCode, ex.MetaErrorSubcode);
            }

            // Procesar el mensaje con el servicio de flujos
            await _flowService.ProcessMessage(phoneNumber, messageBody);
        }
        catch (WhatsAppOperationException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError("WhatsApp message processing failed: {ExceptionType}", ex.GetType().Name);
            throw;
        }
    }
}
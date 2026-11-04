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
    public async Task<IActionResult> ReceiveMessage([FromBody] JsonElement body)
    {
        using var requestScope = _logger.BeginScope(new Dictionary<string, object> { ["WhatsAppRequestId"] = Guid.NewGuid().ToString("N") });
        try
        {
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
                _logger.LogWarning("WhatsApp read receipt failed: {Classification} {StatusCode}", ex.Classification, ex.StatusCode);
            }

            // Procesar el mensaje con el servicio de flujos
            await _flowService.ProcessMessage(phoneNumber, messageBody);
        }
        catch (Exception ex)
        {
            _logger.LogError("WhatsApp message processing failed: {ExceptionType}", ex.GetType().Name);
            throw;
        }
    }
}
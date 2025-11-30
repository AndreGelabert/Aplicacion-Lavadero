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
        _logger.LogInformation("🔍 Verificación de webhook recibida");
        _logger.LogInformation("   Mode: {Mode}", mode);
        _logger.LogInformation("   Token recibido: {Token}", token);
        _logger.LogInformation("   Token esperado: {ExpectedToken}", _settings.VerifyToken);

        if (mode == "subscribe" && token == _settings.VerifyToken)
        {
            _logger.LogInformation("✅ Verificación exitosa. Challenge: {Challenge}", challenge);
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
        try
        {
            var bodyJson = body.GetRawText();
            _logger.LogInformation("📨 Webhook recibido:");
            _logger.LogInformation(bodyJson);

            // Deserializar el payload
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var payload = JsonSerializer.Deserialize<WhatsAppWebhookPayload>(bodyJson, options);

            if (payload?.Entry == null || !payload.Entry.Any())
            {
                _logger.LogWarning("⚠️ Payload vacío o sin entries");
                return Ok();
            }

            // Procesar cada entrada
            foreach (var entry in payload.Entry)
            {
                foreach (var change in entry.Changes ?? new List<WhatsAppChange>())
                {
                    if (change.Field != "messages") continue;

                    var value = change.Value;

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
                            _logger.LogInformation(
                                "📊 Estado de mensaje {MessageId}: {Status}",
                                status.Id,
                                status.Status);
                        }
                    }
                }
            }

            return Ok(); // SIEMPRE responder 200 OK
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error procesando webhook");
            return Ok(); // Aún con error, responder 200 para evitar reintentos de Meta
        }
    }

    /// <summary>
    /// Procesa un mensaje entrante individual
    /// </summary>
    private async Task ProcessIncomingMessage(WhatsAppIncomingMessage message)
    {
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
                    _logger.LogInformation("🔘 Botón presionado: {ButtonId} - {ButtonTitle}", 
                        message.Interactive.ButtonReply.Id, 
                        message.Interactive.ButtonReply.Title);
                }
                else if (message.Interactive.Type == "list_reply" && message.Interactive.ListReply != null)
                {
                    // Usuario seleccionó una opción de lista
                    messageBody = message.Interactive.ListReply.Id;
                    _logger.LogInformation("📋 Opción de lista seleccionada: {ListId} - {ListTitle}",
                        message.Interactive.ListReply.Id,
                        message.Interactive.ListReply.Title);
                }
            }
            else if (message.Type == "button" && message.Button != null)
            {
                // Formato alternativo de botones
                messageBody = message.Button.Payload ?? message.Button.Text;
                _logger.LogInformation("🔘 Botón presionado (formato alternativo): {Payload}", messageBody);
            }

            if (string.IsNullOrEmpty(messageBody))
            {
                _logger.LogInformation("⏭️ Mensaje de tipo {Type} no soportado o sin contenido", message.Type);
                return;
            }

            _logger.LogInformation("💬 Mensaje recibido:");
            _logger.LogInformation("   📱 Número original: {PhoneNumber}", phoneNumber);
            _logger.LogInformation("   📝 Tipo: {Type}", message.Type);
            _logger.LogInformation("   💬 Contenido: {Message}", messageBody);

            // Marcar como leído (mejora UX)
            await _whatsAppService.MarkMessageAsRead(message.Id);

            // Procesar el mensaje con el servicio de flujos
            await _flowService.ProcessMessage(phoneNumber, messageBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error procesando mensaje individual");
        }
    }
}
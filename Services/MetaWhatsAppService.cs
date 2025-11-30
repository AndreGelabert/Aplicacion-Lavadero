using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Firebase.Models;
using Firebase.Models.WhatsApp;
using Microsoft.Extensions.Options;

namespace Firebase.Services;

/// <summary>
/// Servicio para enviar mensajes de WhatsApp mediante Meta Cloud API
/// </summary>
public class MetaWhatsAppService
{
    private readonly HttpClient _httpClient;
    private readonly WhatsAppSettings _settings;
    private readonly ILogger<MetaWhatsAppService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public MetaWhatsAppService(
        HttpClient httpClient,
        IOptions<WhatsAppSettings> settings,
        ILogger<MetaWhatsAppService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _settings.AccessToken);
    }

    /// <summary>
    /// Envía un mensaje de texto simple
    /// </summary>
    public async Task<bool> SendTextMessage(string to, string messageBody)
    {
        try
        {
            // Preparar número para API de Meta (remover el 9 de Argentina si existe)
            var apiFormat = PhoneNumberHelper.PrepareForMetaAPI(to);
            
            _logger.LogInformation("📤 Enviando mensaje de texto:");
            _logger.LogInformation("   📱 Número recibido: {Original}", to);
            _logger.LogInformation("   📱 Formato API Meta: {API}", apiFormat);
            _logger.LogInformation("   💬 Preview: {Message}", messageBody.Length > 80 ? messageBody.Substring(0, 80) + "..." : messageBody);

            var payload = new WhatsAppTextOutgoingMessage
            {
                To = apiFormat,
                Text = new WhatsAppText { Body = messageBody }
            };

            var url = $"{_settings.ApiBaseUrl}/messages";
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("❌ Error enviando mensaje: {StatusCode} - {Response}",
                    response.StatusCode, responseBody);
                return false;
            }

            _logger.LogInformation("✅ Mensaje enviado exitosamente a {PhoneNumber}", apiFormat);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Excepción al enviar mensaje de WhatsApp");
            return false;
        }
    }

    /// <summary>
    /// Envía un mensaje con botones de respuesta rápida (máximo 3)
    /// </summary>
    public async Task<bool> SendButtonMessage(string to, string bodyText, List<(string id, string title)> buttons)
    {
        try
        {
            // Preparar número para API de Meta
            var apiFormat = PhoneNumberHelper.PrepareForMetaAPI(to);
            
            _logger.LogInformation("📤 Enviando mensaje con botones:");
            _logger.LogInformation("   📱 Formato API: {To}", apiFormat);

            if (buttons.Count > 3)
            {
                _logger.LogWarning("⚠️ WhatsApp solo admite máximo 3 botones. Se tomarán los primeros 3.");
                buttons = buttons.Take(3).ToList();
            }

            var payload = new WhatsAppInteractiveMessage
            {
                To = apiFormat,
                Interactive = new WhatsAppInteractive
                {
                    Type = "button",
                    Body = new WhatsAppInteractiveBody { Text = bodyText },
                    Action = new WhatsAppInteractiveAction
                    {
                        Buttons = buttons.Select(b => new WhatsAppButton
                        {
                            Type = "reply",
                            Reply = new WhatsAppReply
                            {
                                Id = b.id,
                                Title = b.title.Length > 20 ? b.title[..20] : b.title
                            }
                        }).ToList()
                    }
                }
            };

            var url = $"{_settings.ApiBaseUrl}/messages";
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("❌ Error enviando mensaje con botones: {StatusCode} - {Response}",
                    response.StatusCode, responseBody);
                return false;
            }

            _logger.LogInformation("✅ Mensaje con botones enviado a {PhoneNumber}", to);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Excepción al enviar mensaje con botones");
            return false;
        }
    }

    /// <summary>
    /// Envía un mensaje con lista desplegable (hasta 10 opciones)
    /// </summary>
    public async Task<bool> SendListMessage(
        string to,
        string bodyText,
        string buttonText,
        string sectionTitle,
        List<(string id, string title, string description)> options)
    {
        try
        {
            // Preparar número para API de Meta
            var apiFormat = PhoneNumberHelper.PrepareForMetaAPI(to);
            
            _logger.LogInformation("📤 Enviando mensaje con lista:");
            _logger.LogInformation("   📱 Formato API: {To}", apiFormat);

            if (options.Count > 10)
            {
                _logger.LogWarning("⚠️ WhatsApp solo admite máximo 10 opciones en lista. Se tomarán las primeras 10.");
                options = options.Take(10).ToList();
            }

            var payload = new WhatsAppInteractiveMessage
            {
                To = apiFormat,
                Interactive = new WhatsAppInteractive
                {
                    Type = "list",
                    Body = new WhatsAppInteractiveBody { Text = bodyText },
                    Action = new WhatsAppInteractiveAction
                    {
                        Button = buttonText,
                        Sections = new List<WhatsAppSection>
                        {
                            new()
                            {
                                Title = sectionTitle,
                                Rows = options.Select(opt => new WhatsAppRow
                                {
                                    Id = opt.id,
                                    Title = opt.title.Length > 24 ? opt.title[..24] : opt.title,
                                    Description = opt.description.Length > 72 ? opt.description[..72] : opt.description
                                }).ToList()
                            }
                        }
                    }
                }
            };

            var url = $"{_settings.ApiBaseUrl}/messages";
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("❌ Error enviando mensaje con lista: {StatusCode} - {Response}",
                    response.StatusCode, responseBody);
                return false;
            }

            _logger.LogInformation("✅ Mensaje con lista enviado a {PhoneNumber}", to);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Excepción al enviar mensaje con lista");
            return false;
        }
    }

    /// <summary>
    /// Marca un mensaje como leído
    /// </summary>
    public async Task<bool> MarkMessageAsRead(string messageId)
    {
        try
        {
            var payload = new
            {
                messaging_product = "whatsapp",
                status = "read",
                message_id = messageId
            };

            var url = $"{_settings.ApiBaseUrl}/messages";
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marcando mensaje como leído");
            return false;
        }
    }
}

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Firebase.Models;
using Firebase.Models.WhatsApp;
using Microsoft.Extensions.Options;

namespace Firebase.Services;

public class MetaWhatsAppService
{
    private readonly HttpClient _httpClient;
    private readonly WhatsAppSettings _settings;
    private readonly ILogger<MetaWhatsAppService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public MetaWhatsAppService(HttpClient httpClient, IOptions<WhatsAppSettings> settings, ILogger<MetaWhatsAppService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.AccessToken);
    }

    public Task<bool> SendTextMessage(string to, string messageBody) => Send("text", new WhatsAppTextOutgoingMessage
    {
        To = PhoneNumberHelper.PrepareForMetaAPI(to), Text = new WhatsAppText { Body = messageBody }
    });

    public Task<bool> SendButtonMessage(string to, string bodyText, List<(string id, string title)> buttons) => Send("buttons", new WhatsAppInteractiveMessage
    {
        To = PhoneNumberHelper.PrepareForMetaAPI(to),
        Interactive = new WhatsAppInteractive
        {
            Type = "button", Body = new WhatsAppInteractiveBody { Text = bodyText },
            Action = new WhatsAppInteractiveAction { Buttons = buttons.Take(3).Select(b => new WhatsAppButton
            {
                Type = "reply", Reply = new WhatsAppReply { Id = b.id, Title = b.title.Length > 20 ? b.title[..20] : b.title }
            }).ToList() }
        }
    });

    public Task<bool> SendListMessage(string to, string bodyText, string buttonText, string sectionTitle, List<(string id, string title, string description)> options) => Send("list", new WhatsAppInteractiveMessage
    {
        To = PhoneNumberHelper.PrepareForMetaAPI(to),
        Interactive = new WhatsAppInteractive
        {
            Type = "list", Body = new WhatsAppInteractiveBody { Text = bodyText },
            Action = new WhatsAppInteractiveAction { Button = buttonText, Sections = new List<WhatsAppSection> { new()
            {
                Title = sectionTitle, Rows = options.Take(10).Select(o => new WhatsAppRow
                {
                    Id = o.id, Title = o.title.Length > 24 ? o.title[..24] : o.title,
                    Description = o.description.Length > 72 ? o.description[..72] : o.description
                }).ToList()
            } } }
        }
    });

    public Task<bool> MarkMessageAsRead(string messageId) => Send("read", new { messaging_product = "whatsapp", status = "read", message_id = messageId });

    private async Task<bool> Send(string operation, object payload)
    {
        try
        {
            using var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync($"{_settings.ApiBaseUrl}/messages", content);
            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;
                var classification = status is 401 or 403 ? "auth" : status == 429 || status >= 500 ? "transient" : "permanent";
                var (code, subcode) = await ReadDiagnostics(response.Content);
                throw new WhatsAppOperationException(operation, classification, status, code, subcode);
            }
            _logger.LogInformation("WhatsApp operation {Operation} succeeded", operation);
            return true;
        }
        catch (WhatsAppOperationException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning("WhatsApp operation {Operation} failed: uncertain transport ({ExceptionType})", operation, ex.GetType().Name);
            throw new WhatsAppOperationException(operation, "uncertain transport");
        }
    }

    private static async Task<(int? Code, int? Subcode)> ReadDiagnostics(HttpContent content)
    {
        const int limit = 16384;
        try
        {
            if (content.Headers.ContentLength > limit) return (null, null);
            await using var stream = await content.ReadAsStreamAsync();
            var buffer = new byte[limit + 1];
            var count = 0;
            while (count < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(count));
                if (read == 0) break;
                count += read;
            }
            if (count > limit) return (null, null);
            using var json = JsonDocument.Parse(buffer.AsMemory(0, count));
            if (json.RootElement.ValueKind != JsonValueKind.Object ||
                !json.RootElement.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object)
                return (null, null);
            static int? Number(JsonElement element, string name) =>
                element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;
            return (Number(error, "code"), Number(error, "error_subcode"));
        }
        catch (Exception)
        {
            // Diagnostics must never replace the classified HTTP failure.
            return (null, null);
        }
    }
}

using System.Net.Http.Headers;
using System.Net.Http.Json;
using Firebase.Models;
using Microsoft.Extensions.Options;

namespace Firebase.Services;

public interface IEmailSender
{
    Task<ResultadoEnvioEmail> EnviarAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

public sealed record ResultadoEnvioEmail(bool Enviado, string Mensaje, string? MessageId = null);

public sealed class ResendEmailSender(
    HttpClient httpClient,
    IOptions<EmailSettings> options,
    IWebHostEnvironment environment,
    ILogger<ResendEmailSender> logger) : IEmailSender
{
    public async Task<ResultadoEnvioEmail> EnviarAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            return new(false, "El transporte de correo no está configurado.");
        if (string.IsNullOrWhiteSpace(settings.From))
            return new(false, "El remitente de correo no está configurado.");
        if (string.IsNullOrWhiteSpace(message.To))
            return new(false, "El destinatario es obligatorio.");

        if (environment.IsDevelopment() &&
            !string.Equals(settings.DevelopmentAllowedRecipient, message.To, StringComparison.OrdinalIgnoreCase))
            return new(false, "El destinatario no está permitido por la allowlist de desarrollo.");

        var payload = new
        {
            from = settings.From,
            to = new[] { message.To },
            subject = message.Subject,
            html = message.Html,
            reply_to = message.ReplyTo ?? settings.ReplyTo
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Resend rechazó el envío. StatusCode={StatusCode}", response.StatusCode);
            return new(false, "Resend rechazó el envío del correo.");
        }

        var result = System.Text.Json.JsonSerializer.Deserialize<ResendResponse>(body);
        return new(true, "Resend aceptó el envío. La aceptación no confirma entrega ni lectura.", result?.Id);
    }

    private sealed record ResendResponse(string Id);
}

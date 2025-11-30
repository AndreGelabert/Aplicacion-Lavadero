namespace Firebase.Models;

/// <summary>
/// Configuración para WhatsApp Cloud API
/// </summary>
public class WhatsAppSettings
{
    public required string AccessToken { get; set; }
    public required string PhoneNumberId { get; set; }
    public required string VerifyToken { get; set; }
    public string ApiVersion { get; set; } = "v24.0";
    
    /// <summary>
    /// Código de país por defecto (Argentina = 54)
    /// </summary>
    public string DefaultCountryCode { get; set; } = "54";
    
    public string ApiBaseUrl => $"https://graph.facebook.com/{ApiVersion}/{PhoneNumberId}";
}
namespace Firebase.Models;

/// <summary>
/// Configuraci�n para WhatsApp Cloud API
/// </summary>
public class WhatsAppSettings
{
    public required string AccessToken { get; set; }
    public required string PhoneNumberId { get; set; }
    public required string VerifyToken { get; set; }
    // Supplied externally; never reuse the access or verification token.
    public string? AppSecret { get; set; }
    public string ApiVersion { get; set; } = "v24.0";
    
    /// <summary>
    /// C�digo de pa�s por defecto (Argentina = 54)
    /// </summary>
    public string DefaultCountryCode { get; set; } = "54";
    
    public string ApiBaseUrl => $"https://graph.facebook.com/{ApiVersion}/{PhoneNumberId}";
}
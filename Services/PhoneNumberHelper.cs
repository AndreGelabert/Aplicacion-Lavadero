using System.Text.RegularExpressions;

namespace Firebase.Services;

/// <summary>
/// Clase helper para normalización y validación de números de teléfono
/// </summary>
public static class PhoneNumberHelper
{
    /// <summary>
    /// Normaliza un número de teléfono al formato internacional de WhatsApp
    /// Ejemplo: +54 3751 59-0586 → 543751590586
    /// </summary>
    public static string NormalizePhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return phoneNumber;

        // Remover todos los caracteres no numéricos excepto el + inicial
        var normalized = Regex.Replace(phoneNumber, @"[^\d+]", "");

        // Si empieza con +, removerlo (WhatsApp usa el número sin +)
        if (normalized.StartsWith("+"))
            normalized = normalized.Substring(1);

        return normalized;
    }

    /// <summary>
    /// Convierte un número de DB (sin código de país) a formato WhatsApp (con código de país)
    /// Ejemplo: 3751590586 → 543751590586
    /// </summary>
    public static string AddCountryCode(string phoneNumber, string countryCode = "54")
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return phoneNumber;

        var normalized = NormalizePhoneNumber(phoneNumber);

        // Si ya tiene el código de país, no agregarlo de nuevo
        if (normalized.StartsWith(countryCode))
            return normalized;

        return countryCode + normalized;
    }

    /// <summary>
    /// Remueve el código de país de un número (para guardar en DB)
    /// Ejemplo: 5493751590586 → 3751590586 (también remueve el 9 de Argentina)
    /// </summary>
    public static string RemoveCountryCode(string phoneNumber, string countryCode = "54")
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return phoneNumber;

        var normalized = NormalizePhoneNumber(phoneNumber);

        // Si empieza con el código de país, removerlo
        if (normalized.StartsWith(countryCode))
        {
            var sinCodigo = normalized.Substring(countryCode.Length);
            
            // Para Argentina: remover el 9 adicional si existe
            // 549XXXXXXXXXX → XXXXXXXXXX (sin 54 ni 9)
            if (countryCode == "54" && sinCodigo.StartsWith("9") && sinCodigo.Length >= 10)
            {
                return sinCodigo.Substring(1); // Remover el 9
            }
            
            return sinCodigo;
        }

        return normalized;
    }
    
    /// <summary>
    /// Convierte un número de DB (sin código de país) a formato WhatsApp
    /// Ejemplo: 3751590586 → 543751590586 (sin el 9 para envío API)
    /// </summary>
    public static string ToWhatsAppFormat(string phoneNumber, string countryCode = "54")
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return phoneNumber;

        var normalized = NormalizePhoneNumber(phoneNumber);

        // Si ya tiene código de país, devolverlo
        if (normalized.StartsWith(countryCode))
            return normalized;

        // Para Argentina: agregar código de país SIN el 9
        // El 9 solo se usa en llamadas, no en la API de WhatsApp
        return countryCode + normalized;
    }
    
    /// <summary>
    /// Prepara un número para envío a través de la API de Meta
    /// Remueve el 9 adicional de Argentina si existe
    /// Ejemplo: 5493751590586 → 543751590586
    /// </summary>
    public static string PrepareForMetaAPI(string phoneNumber, string countryCode = "54")
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return phoneNumber;

        var normalized = NormalizePhoneNumber(phoneNumber);

        // Para Argentina: si tiene el 9 después del código de país, removerlo
        // 5493751590586 → 543751590586
        if (countryCode == "54" && normalized.StartsWith($"{countryCode}9"))
        {
            // Remover el 9
            return countryCode + normalized.Substring(3); // "54" + resto sin el 9
        }

        return normalized;
    }

    /// <summary>
    /// Formatea un número normalizado para mostrar (con +)
    /// Ejemplo: 5437515905886 → +5437515905886
    /// </summary>
    public static string FormatForDisplay(string normalizedNumber)
    {
        if (string.IsNullOrWhiteSpace(normalizedNumber))
            return normalizedNumber;

        if (!normalizedNumber.StartsWith("+"))
            return "+" + normalizedNumber;

        return normalizedNumber;
    }

    /// <summary>
    /// Valida que un número tenga un formato válido
    /// </summary>
    public static bool IsValidPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return false;

        var normalized = NormalizePhoneNumber(phoneNumber);

        // Un número válido debe tener entre 10 y 15 dígitos
        return normalized.Length >= 10 && normalized.Length <= 15 && Regex.IsMatch(normalized, @"^\d+$");
    }

    /// <summary>
    /// Compara dos números de teléfono ignorando formato
    /// </summary>
    public static bool AreEqual(string phone1, string phone2)
    {
        if (string.IsNullOrWhiteSpace(phone1) || string.IsNullOrWhiteSpace(phone2))
            return false;

        var normalized1 = NormalizePhoneNumber(phone1);
        var normalized2 = NormalizePhoneNumber(phone2);

        // Remover códigos de país para comparación
        var clean1 = RemoveCountryCode(normalized1);
        var clean2 = RemoveCountryCode(normalized2);

        // Comparar sin códigos de país
        if (clean1 == clean2)
            return true;

        // Comparar los últimos 10 dígitos (fallback)
        if (normalized1.Length >= 10 && normalized2.Length >= 10)
        {
            var last10_1 = normalized1.Substring(normalized1.Length - 10);
            var last10_2 = normalized2.Substring(normalized2.Length - 10);

            if (last10_1 == last10_2)
                return true;
        }

        // Comparación exacta
        return normalized1 == normalized2;
    }
}

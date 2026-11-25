using System;

namespace Firebase.Utils;

/// <summary>
/// Proporciona utilidades para la conversión y formateo de fechas en la zona horaria del sistema (Argentina).
/// </summary>
public static class TimeZoneHelper
{
    public const string IanaTimeZoneId = "America/Argentina/Buenos_Aires";
    public const string WindowsTimeZoneId = "Argentina Standard Time";

    private static readonly Lazy<TimeZoneInfo> _argentinaZone = new(ObtenerZonaArgentina);

    /// <summary>
    /// Zona horaria oficial de la aplicación (Argentina).
    /// </summary>
    public static TimeZoneInfo ArgentinaZone => _argentinaZone.Value;

    private static TimeZoneInfo ObtenerZonaArgentina()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(IanaTimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(WindowsTimeZoneId);
        }
    }

    /// <summary>
    /// Convierte una fecha y hora UTC a la zona horaria de la aplicación (Argentina UTC-3).
    /// Maneja de forma segura DateTimeKind.Utc, DateTimeKind.Local y DateTimeKind.Unspecified.
    /// </summary>
    public static DateTime ConvertirUtcAZonaApp(DateTime dateTime)
    {
        var utc = dateTime.Kind switch
        {
            DateTimeKind.Utc => dateTime,
            DateTimeKind.Local => dateTime.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
        };

        return TimeZoneInfo.ConvertTimeFromUtc(utc, ArgentinaZone);
    }

    /// <summary>
    /// Formatea una fecha UTC a string en la zona horaria de la aplicación.
    /// Devuelve string.Empty si la fecha es nula.
    /// </summary>
    public static string FormatearFechaHoraApp(DateTime? utcDateTime, string formato = "dd/MM/yyyy HH:mm")
    {
        if (!utcDateTime.HasValue)
            return string.Empty;

        return ConvertirUtcAZonaApp(utcDateTime.Value).ToString(formato);
    }
}

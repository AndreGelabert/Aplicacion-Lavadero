using Firebase.Models;

namespace Firebase.Services;

internal readonly record struct TerminosCancelacion(bool Elegible, decimal Porcentaje, int ValidezDias);

/// <summary>Evaluación pura; no emite ni canjea descuentos.</summary>
internal static class TurnoCancelacionPolicy
{
    internal static TerminosCancelacion Evaluar(DateTime inicioUtc, DateTime cancelacionUtc, bool noShow, Configuracion configuracion)
    {
        ArgumentNullException.ThrowIfNull(configuracion);
        if (inicioUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("El inicio debe ser UTC.", nameof(inicioUtc));
        if (cancelacionUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("La cancelación debe ser UTC.", nameof(cancelacionUtc));

        var horas = configuracion.CancelacionAnticipadaHorasMinimas;
        var porcentaje = configuracion.CancelacionAnticipadaDescuento;
        var dias = configuracion.CancelacionAnticipadaValidezDias;
        if (horas < 1 || horas > 168) throw new ArgumentOutOfRangeException(nameof(configuracion), "Horas fuera de rango.");
        if (porcentaje < 0 || porcentaje > 100) throw new ArgumentOutOfRangeException(nameof(configuracion), "Porcentaje fuera de rango.");
        if (dias < 1 || dias > 365) throw new ArgumentOutOfRangeException(nameof(configuracion), "Validez fuera de rango.");

        return new(!noShow && inicioUtc.Ticks - cancelacionUtc.Ticks >= (long)horas * TimeSpan.TicksPerHour, porcentaje, dias);
    }
}

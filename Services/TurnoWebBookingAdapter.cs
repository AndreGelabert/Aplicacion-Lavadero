using Firebase.Utils;

namespace Firebase.Services;

internal sealed record TurnoWebBookingSelection(string ServicioId, string? PaqueteId);
internal sealed record TurnoWebBookingRequest(string Id, string ClaveIdempotencia, string ClienteId,
    string VehiculoId, DateTime InicioLocal, bool AceptaAdelanto, IReadOnlyList<TurnoWebBookingSelection> Servicios,
    string? ClienteTrajoId = null, string? ClienteRetiraId = null, decimal Descuento = 0, string? Notas = null, string? CuponId = null);

/// <summary>Internal adapter only; neither an HTTP boundary nor an authorization decision.</summary>
internal sealed class TurnoWebBookingAdapter(TurnoService turnos)
{
    internal async Task<bool> Reservar(TurnoWebBookingRequest request) =>
        (await ReservarConDetalle(request)).Success;

    internal Task<TurnoBookingResult> ReservarConDetalle(TurnoWebBookingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.InicioLocal == default || request.InicioLocal.Kind != DateTimeKind.Unspecified ||
            string.IsNullOrWhiteSpace(request.ClaveIdempotencia) || request.Servicios == null ||
            request.Servicios.Count is < 1 or > 100 || request.Servicios.Any(s => s == null))
            throw new ArgumentException("Datos de turno inválidos.", nameof(request));
        var zone = TimeZoneHelper.ArgentinaZone;
        if (zone.IsInvalidTime(request.InicioLocal) || zone.IsAmbiguousTime(request.InicioLocal))
            throw new ArgumentException("Horario local inválido o ambiguo.", nameof(request));
        var utc = TimeZoneInfo.ConvertTimeToUtc(request.InicioLocal, zone);
        return turnos.ReservarConDetalle(new TurnoService.SnapshotRequest(request.Id, request.ClienteId, request.VehiculoId,
            "Web", request.ClaveIdempotencia, utc, request.AceptaAdelanto,
            request.Servicios.Select(s => new TurnoService.SnapshotSelection(s.ServicioId, s.PaqueteId)).ToArray(),
            request.ClienteTrajoId, request.ClienteRetiraId, request.Descuento, request.Notas, request.CuponId));
    }
}

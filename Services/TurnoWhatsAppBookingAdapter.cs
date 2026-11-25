using Firebase.Models.WhatsApp;
using Firebase.Utils;
using System.Text.Json;

namespace Firebase.Services;

internal sealed record TurnoWhatsAppBookingSelection(string ServicioId, string? PaqueteId);
internal sealed record TurnoWhatsAppBookingRequest(string Id, string ClaveIdempotencia, string VehiculoId,
    DateTime InicioLocal, bool AceptaAdelanto, IReadOnlyList<TurnoWhatsAppBookingSelection> Servicios);

/// <summary>Internal adapter; the caller must supply an authenticated, trusted session.</summary>
internal sealed class TurnoWhatsAppBookingAdapter
{
    private readonly Func<TurnoService.SnapshotRequest, TurnoService.BookingAuthorization, Task<TurnoBookingResult>> reservarConDetalle;

    internal TurnoWhatsAppBookingAdapter(TurnoService turnos) : this(turnos.ReservarWhatsAppConDetalle) { }
    internal TurnoWhatsAppBookingAdapter(Func<TurnoService.SnapshotRequest, TurnoService.BookingAuthorization, Task<TurnoBookingResult>> reservarConDetalle) =>
        this.reservarConDetalle = reservarConDetalle ?? throw new ArgumentNullException(nameof(reservarConDetalle));
    internal TurnoWhatsAppBookingAdapter(Func<TurnoService.SnapshotRequest, TurnoService.BookingAuthorization, Task<bool>> legacyReservar)
    {
        ArgumentNullException.ThrowIfNull(legacyReservar);
        this.reservarConDetalle = async (req, auth) => new TurnoBookingResult(await legacyReservar(req, auth));
    }

    internal async Task<bool> Reservar(WhatsAppSession session, TurnoWhatsAppBookingRequest request) =>
        (await ReservarConDetalle(session, request)).Success;

    internal Task<TurnoBookingResult> ReservarConDetalle(WhatsAppSession session, TurnoWhatsAppBookingRequest request)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(session.ClienteId) || string.IsNullOrWhiteSpace(session.Id) ||
            string.IsNullOrWhiteSpace(session.IncarnationId) || string.IsNullOrWhiteSpace(session.SelectionId) ||
            session.Revision < 0 || session.Revision == long.MaxValue || session.CurrentState != "TURNO_RESERVA_CONFIRMAR" ||
            session.BookingTolerance is null or < 0 or > 525600 || string.IsNullOrWhiteSpace(session.BookingRequest) ||
            string.IsNullOrWhiteSpace(request.Id) ||
            string.IsNullOrWhiteSpace(request.ClaveIdempotencia) || request.InicioLocal == default ||
            request.InicioLocal.Kind != DateTimeKind.Unspecified || request.Servicios == null ||
            request.Servicios.Count is < 1 or > 100 || request.Servicios.Any(s => s == null))
            throw new ArgumentException("Datos de turno inválidos.");
        var zone = TimeZoneHelper.ArgentinaZone;
        if (zone.IsInvalidTime(request.InicioLocal) || zone.IsAmbiguousTime(request.InicioLocal))
            throw new ArgumentException("Horario local inválido o ambiguo.", nameof(request));
        var utc = TimeZoneInfo.ConvertTimeToUtc(request.InicioLocal, zone);
        var command = new TurnoService.SnapshotRequest(request.Id, session.ClienteId, request.VehiculoId,
            "WhatsApp", request.ClaveIdempotencia, utc, request.AceptaAdelanto,
            request.Servicios.Select(s => new TurnoService.SnapshotSelection(s.ServicioId, s.PaqueteId)).ToArray());
        if (session.BookingRequest != JsonSerializer.Serialize(command))
            throw new ArgumentException("Datos de turno inválidos.");
        return reservarConDetalle(command, new TurnoService.BookingAuthorization(session.Id, session.IncarnationId,
            session.Revision, session.SelectionId, session.BookingTolerance.Value));
    }
}

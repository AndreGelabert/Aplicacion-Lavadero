using Firebase.Models.WhatsApp;
using Firebase.Services;
using Firebase.Utils;
using System.Text.Json;

namespace Firebase.Tests.Services;

public class TurnoWhatsAppBookingAdapterTests
{
    private static TurnoWhatsAppBookingRequest Request() => new("appointment", "key", "vehicle", new DateTime(2030, 1, 1, 9, 0, 0), true, [new("service", "package"), new("other", null)]);
    private static TurnoService.SnapshotRequest Snapshot() => new("appointment", "client", "vehicle", "WhatsApp", "key", new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc), true, [new("service", "package"), new("other", null)]);
    private static WhatsAppSession Session() => new() { Id = "5491112345678", CurrentState = "TURNO_RESERVA_CONFIRMAR", ClienteId = "client", IncarnationId = "inc", Revision = 4, SelectionId = "token", BookingTolerance = 10, BookingRequest = JsonSerializer.Serialize(Snapshot()) };

    [Fact]
    public async Task DelegatesExactCommandAndCapabilityOnRepeat()
    {
        var calls = new List<(TurnoService.SnapshotRequest, TurnoService.BookingAuthorization)>();
        var adapter = new TurnoWhatsAppBookingAdapter((command, auth) => { calls.Add((command, auth)); return Task.FromResult(true); });
        var session = Session(); var request = Request();
        Assert.True(await adapter.Reservar(session, request));
        Assert.True(await adapter.Reservar(session, request));
        Assert.Equal(2, calls.Count);
        foreach (var (command, auth) in calls)
        {
            Assert.Equal(JsonSerializer.Serialize(Snapshot()), JsonSerializer.Serialize(command));
            Assert.Equal(new TurnoService.BookingAuthorization(session.Id, "inc", 4, "token", 10), auth);
        }
    }

    [Fact]
    public async Task InvalidCapabilityOrDraftNeverDelegates()
    {
        var count = 0; var adapter = new TurnoWhatsAppBookingAdapter((_, _) => { count++; return Task.FromResult(true); });
        await Assert.ThrowsAsync<ArgumentNullException>(() => adapter.Reservar(null!, Request()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => adapter.Reservar(Session(), null!));
        var sessions = new List<WhatsAppSession>();
        var s = Session(); s.ClienteId = null; sessions.Add(s);
        s = Session(); s.Id = ""; sessions.Add(s);
        s = Session(); s.IncarnationId = null; sessions.Add(s);
        s = Session(); s.SelectionId = null; sessions.Add(s);
        s = Session(); s.Revision = -1; sessions.Add(s);
        s = Session(); s.Revision = long.MaxValue; sessions.Add(s);
        s = Session(); s.CurrentState = "other"; sessions.Add(s);
        s = Session(); s.BookingTolerance = null; sessions.Add(s);
        s = Session(); s.BookingTolerance = -1; sessions.Add(s);
        s = Session(); s.BookingTolerance = 525601; sessions.Add(s);
        s = Session(); s.BookingRequest = null; sessions.Add(s);
        s = Session(); s.BookingRequest = "{}"; sessions.Add(s);
        foreach (var invalid in sessions) await Assert.ThrowsAsync<ArgumentException>(() => adapter.Reservar(invalid, Request()));
        foreach (var request in new[] { Request() with { Id = "" }, Request() with { ClaveIdempotencia = "" }, Request() with { VehiculoId = "other" } })
            await Assert.ThrowsAsync<ArgumentException>(() => adapter.Reservar(Session(), request));
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task InvalidAndAmbiguousLocalTimesNeverDelegate()
    {
        var count = 0; var adapter = new TurnoWhatsAppBookingAdapter((_, _) => { count++; return Task.FromResult(true); });
        var gap = new DateTime(2008, 10, 19, 0, 30, 0); var overlap = new DateTime(2009, 3, 14, 23, 30, 0);
        Assert.True(TimeZoneHelper.ArgentinaZone.IsInvalidTime(gap)); Assert.True(TimeZoneHelper.ArgentinaZone.IsAmbiguousTime(overlap));
        foreach (var time in new[] { default, gap, overlap, DateTime.SpecifyKind(Request().InicioLocal, DateTimeKind.Utc), DateTime.SpecifyKind(Request().InicioLocal, DateTimeKind.Local) })
            await Assert.ThrowsAsync<ArgumentException>(() => adapter.Reservar(Session(), Request() with { InicioLocal = time }));
        Assert.Equal(0, count);
    }
}

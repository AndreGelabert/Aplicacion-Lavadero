using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;

namespace Firebase.Tests.Integration;

public class TurnoOfertaEmailRespuestaIntegrationTests
{
    private static readonly DateTime T = new(2030, 1, 8, 12, 0, 0, DateTimeKind.Utc);
    private static async Task<(FirestoreDb db, TurnoOfertaPersistence offers, TurnoOfertaEmailCapability email)> Setup()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-email-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var booking = new Turno { Id = "appointment", ClienteId = "owner", Estado = "Pendiente", Origen = "test", ClaveIdempotencia = "appointment", AceptaAdelanto = true, InicioUtc = T.AddDays(2), FinUtc = T.AddDays(2).AddMinutes(30), Vehiculos = [new() { VehiculoId = "vehicle", CantidadEmpleadosRequeridos = 1, Servicios = [new() { ServicioId = "service", TiempoEstimado = 30 }] }] };
        await db.Collection("turnos").Document("appointment").SetAsync(booking);
        await db.Collection("empleados").Document("one").SetAsync(new { Estado = "Activo" });
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion { CapacidadMaximaConcurrente = 1, HorariosOperacion = new() { ["Miércoles"] = "00:00-23:59", ["Jueves"] = "00:00-23:59", ["Viernes"] = "00:00-23:59", ["Sábado"] = "00:00-23:59", ["Domingo"] = "00:00-23:59", ["Lunes"] = "00:00-23:59", ["Martes"] = "00:00-23:59" } });
        var offers = new TurnoOfertaPersistence(db);
        await offers.Create(new("cascade", "offer", "appointment", T, T.AddMinutes(10), T.AddDays(2), T.AddDays(1)));
        return (db, offers, new TurnoOfertaEmailCapability(db));
    }
    [LocalFirestoreFact]
    public async Task CapabilityIsHashedSingleUseAndBoundToOfferAndClient()
    {
        foreach (var accept in new[] { true, false })
        {
            var (db, offers, email) = await Setup();
            var token = await email.Issue("cascade", "offer", "owner", T, T.AddMinutes(5));
            var stored = await db.Collection("turno_ofertas_email_capabilities").Document("offer").GetSnapshotAsync();
            Assert.DoesNotContain(token, stored.ToDictionary().Values.OfType<string>());
            await Assert.ThrowsAsync<InvalidOperationException>(() => email.Respond("cascade", "offer", "other", token, T.AddMinutes(1), accept));
            await email.Respond("cascade", "offer", "owner", token, T.AddMinutes(1), accept);
            Assert.Equal(accept ? "Aceptada" : "Rechazada", (await db.Collection("turno_ofertas").Document("offer").GetSnapshotAsync()).GetValue<string>("Estado"));
            var consumed = await stored.Reference.GetSnapshotAsync();
            await email.Respond("cascade", "offer", "owner", token, T.AddMinutes(1), accept);
            Assert.Equal(consumed.UpdateTime, (await stored.Reference.GetSnapshotAsync()).UpdateTime);
        }
    }
    [LocalFirestoreFact]
    public async Task InvalidAndExpiredCapabilitiesDoNotChangeOffer()
    {
        var (db, _, email) = await Setup();
        await Assert.ThrowsAsync<InvalidOperationException>(() => email.Issue("cascade", "offer", "owner", T, T.AddMinutes(11)));
        var token = await email.Issue("cascade", "offer", "owner", T, T.AddMinutes(5));
        var before = await db.Collection("turno_ofertas").Document("offer").GetSnapshotAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => email.Respond("cascade", "offer", "owner", "invalid", T.AddMinutes(1), false));
        await Assert.ThrowsAsync<InvalidOperationException>(() => email.Respond("cascade", "offer", "owner", token, T.AddMinutes(5), false));
        Assert.Equal(before.UpdateTime, (await before.Reference.GetSnapshotAsync()).UpdateTime);
    }
    [LocalFirestoreFact]
    public async Task ExistingRejectReplayRemainsIdempotent()
    {
        var (db, offers, _) = await Setup();
        await offers.Reject("cascade", "offer", T.AddMinutes(1));
        var before = await db.Collection("turno_ofertas").Document("offer").GetSnapshotAsync();
        await offers.Reject("cascade", "offer", T.AddMinutes(1));
        Assert.Equal(before.UpdateTime, (await before.Reference.GetSnapshotAsync()).UpdateTime);
    }
}

using Firebase.Models;
using Firebase.Services;
using Firebase.Tests.Integration;
using Google.Api.Gax;
using Google.Cloud.Firestore;

namespace Firebase.Tests.Services;

public class WhatsAppOfferPaginationTests
{
    [LocalFirestoreFact]
    public async Task TerminalHistoryDoesNotHideOwnedPendingOffer()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-offers-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var now = DateTime.UtcNow;
        var batch = db.StartBatch();
        for (var i = 0; i < 101; i++)
            batch.Set(db.Collection("turno_ofertas").Document($"a{i:D3}"), new { ClienteId = "owner", Estado = "Rechazada" });
        batch.Set(db.Collection("turno_ofertas").Document("z-pending"), ValidOffer(now));
        batch.Set(db.Collection("turnos").Document("appointment"), ValidBooking(now));
        batch.Set(db.Collection("turno_ofertas_pendientes").Document("appointment"), new { OfferId = "z-pending", CascadeId = "cascade" });
        batch.Set(db.Collection("turno_cascadas").Document("cascade"), new { PendingId = "z-pending" });
        await batch.CommitAsync();

        var persistence = new TurnoOfertaPersistence(db);
        var offers = await persistence.PendingForOwner("owner", now);
        Assert.Equal("z-pending", Assert.Single(offers).Id);
        Assert.Empty(await persistence.PendingForOwner("other", now));
        await db.Collection("turnos").Document("appointment").UpdateAsync("ClienteId", "other");
        Assert.Empty(await persistence.PendingForOwner("owner", now));
    }

    private static Turno ValidBooking(DateTime now) => new()
    {
        Id = "appointment", ClienteId = "owner", Estado = "Pendiente", Origen = "test", ClaveIdempotencia = "appointment",
        InicioUtc = now.AddDays(2), FinUtc = now.AddDays(2).AddMinutes(30), AceptaAdelanto = true,
        Vehiculos = [new() { VehiculoId = "vehicle", CantidadEmpleadosRequeridos = 1,
            Servicios = [new() { ServicioId = "service", TiempoEstimado = 30 }] }]
    };

    private static Dictionary<string, object> ValidOffer(DateTime now) => new()
    {
        ["ClienteId"] = "owner", ["Estado"] = "Pendiente", ["AppointmentId"] = "appointment", ["CascadeId"] = "cascade",
        ["CreatedUtc"] = now.AddMinutes(-1), ["ExpiresUtc"] = now.AddHours(1), ["ProposedUtc"] = now.AddDays(1),
        ["ProposedEndUtc"] = now.AddDays(1).AddMinutes(30), ["OriginalUtc"] = now.AddDays(2), ["AnchorUtc"] = now.AddDays(2)
    };

    [LocalFirestoreFact]
    public async Task InvalidSnapshotsAreNeverReturned()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-offers-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var now = DateTime.UtcNow;
        var offer = db.Collection("turno_ofertas").Document("offer");
        var appointment = db.Collection("turnos").Document("appointment");
        await appointment.SetAsync(ValidBooking(now));
        await db.Collection("turno_ofertas_pendientes").Document("appointment").SetAsync(new { OfferId = "offer", CascadeId = "cascade" });
        await db.Collection("turno_cascadas").Document("cascade").SetAsync(new { PendingId = "offer" });
        var persistence = new TurnoOfertaPersistence(db);
        foreach (var field in new[] { "CreatedUtc", "AnchorUtc", "OriginalUtc", "ProposedEndUtc" })
        {
            var missing = ValidOffer(now);
            missing.Remove(field);
            await offer.SetAsync(missing);
            Assert.Empty(await persistence.PendingForOwner("owner", now));
            var wrongType = ValidOffer(now);
            wrongType[field] = "invalid";
            await offer.SetAsync(wrongType);
            Assert.Empty(await persistence.PendingForOwner("owner", now));
        }
        var future = ValidOffer(now);
        future["CreatedUtc"] = now.AddMinutes(1);
        await offer.SetAsync(future);
        Assert.Empty(await persistence.PendingForOwner("owner", now));
        await offer.SetAsync(ValidOffer(now));
        Assert.Single(await persistence.PendingForOwner("owner", now));
        await appointment.UpdateAsync("Id", "different");
        Assert.Empty(await persistence.PendingForOwner("owner", now));
    }

    [LocalFirestoreFact]
    public async Task StaleOrMalformedBookingIsNeverReturned()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-offers-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var now = DateTime.UtcNow;
        var appointment = db.Collection("turnos").Document("appointment");
        await db.Collection("turno_ofertas").Document("offer").SetAsync(ValidOffer(now));
        await db.Collection("turno_ofertas_pendientes").Document("appointment").SetAsync(new { OfferId = "offer", CascadeId = "cascade" });
        await db.Collection("turno_cascadas").Document("cascade").SetAsync(new { PendingId = "offer" });
        var persistence = new TurnoOfertaPersistence(db);
        await appointment.SetAsync(ValidBooking(now));
        Assert.Single(await persistence.PendingForOwner("owner", now));
        foreach (var field in new[] { "InicioUtc", "FinUtc" })
        {
            await appointment.SetAsync(ValidBooking(now));
            await appointment.UpdateAsync(field, now.AddDays(2).AddHours(1));
            Assert.Empty(await persistence.PendingForOwner("owner", now));
            await appointment.UpdateAsync(field, "invalid");
            Assert.Empty(await persistence.PendingForOwner("owner", now));
        }
        await appointment.SetAsync(ValidBooking(now));
        await appointment.UpdateAsync("FinUtc", now.AddDays(2).AddMinutes(-1));
        Assert.Empty(await persistence.PendingForOwner("owner", now));
    }

    [LocalFirestoreFact]
    public async Task ExhaustedScanDoesNotReturnPartialOrEmptySuccess()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-offers-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var now = DateTime.UtcNow;
        for (var page = 0; page < 10; page++)
        {
            var batch = db.StartBatch();
            for (var i = 0; i < 400; i++)
                batch.Set(db.Collection("turno_ofertas").Document($"a{page * 400 + i:D4}"), new { ClienteId = "owner", Estado = "Rechazada" });
            await batch.CommitAsync();
        }
        await db.Collection("turno_ofertas").Document("z-pending").SetAsync(ValidOffer(now));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new TurnoOfertaPersistence(db).PendingForOwner("owner", now));
    }
}

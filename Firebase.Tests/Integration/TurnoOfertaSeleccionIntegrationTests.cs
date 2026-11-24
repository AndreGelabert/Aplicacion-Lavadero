using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;

namespace Firebase.Tests.Integration;

public class TurnoOfertaSeleccionIntegrationTests
{
    private static readonly DateTime Vacancy = new(2030, 1, 8, 12, 0, 0, DateTimeKind.Utc);

    private static Turno Booking(string id, DateTime start) => new() { Id = id, ClienteId = id, Estado = "Pendiente", Origen = "test", ClaveIdempotencia = id, AceptaAdelanto = true, InicioUtc = start, FinUtc = start.AddMinutes(30), Vehiculos = [new() { VehiculoId = "vehicle", CantidadEmpleadosRequeridos = 1, Servicios = [new() { ServicioId = "service", TiempoEstimado = 30 }] }] };

    private static async Task<(FirestoreDb Db, string Cascade)> Setup()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-select-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("turnos").Document("cancelled").SetAsync(Booking("cancelled", Vacancy));
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion());
        await new TurnoService(db).Cancelar("cancelled");
        return (db, Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents).Id);
    }

    [LocalFirestoreFact]
    public async Task CorruptTerminalMarkerFailsClosedWithoutWrites()
    {
        var (db, cascade) = await Setup();
        await db.Collection("turnos").Document("near").SetAsync(Booking("near", Vacancy.AddDays(1)));
        await db.Collection("turnos").Document("later").SetAsync(Booking("later", Vacancy.AddDays(2)));
        var service = new TurnoOfertaPersistence(db);
        var created = Vacancy.AddHours(-1);
        var expires = Vacancy.AddMinutes(-50);
        Assert.Equal("near", await service.SelectAndCreate(cascade, "first", created, expires));
        await service.Reject(cascade, "first", created.AddMinutes(1));
        var marker = Assert.Single((await db.Collection("turno_ofertas_terminales").GetSnapshotAsync()).Documents);
        await marker.Reference.UpdateAsync("Estado", "corrupto");
        var before = await db.Collection("turno_cascadas").Document(cascade).GetSnapshotAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SelectAndCreate(cascade, "second", created, expires));
        Assert.False((await db.Collection("turno_ofertas").Document("second").GetSnapshotAsync()).Exists);
        Assert.Equal(before.UpdateTime, (await before.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal("corrupto", (await marker.Reference.GetSnapshotAsync()).GetValue<string>("Estado"));
    }

    [LocalFirestoreFact]
    public async Task MalformedEligibleBookingFailsClosedWithoutWrites()
    {
        var (db, cascade) = await Setup();
        await db.Collection("turnos").Document("bad").SetAsync(Booking("different", Vacancy.AddDays(1)));
        await db.Collection("turnos").Document("good").SetAsync(Booking("good", Vacancy.AddDays(2)));
        var before = await db.Collection("turno_cascadas").Document(cascade).GetSnapshotAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new TurnoOfertaPersistence(db).SelectAndCreate(cascade, "offer", Vacancy.AddHours(-1), Vacancy.AddMinutes(-50)));
        Assert.False((await db.Collection("turno_ofertas").Document("offer").GetSnapshotAsync()).Exists);
        Assert.Equal(before.UpdateTime, (await before.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Empty((await db.Collection("turno_adelanto_anclas").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task EmptyAndAnchorBoundsDoNotCreateAnOffer()
    {
        var (db, cascade) = await Setup();
        var service = new TurnoOfertaPersistence(db);
        Assert.Null(await service.SelectAndCreate(cascade, "empty", Vacancy.AddHours(-1), Vacancy.AddMinutes(-50)));
        await db.Collection("turnos").Document("outside").SetAsync(Booking("outside", Vacancy.AddDays(7).AddMinutes(1)));
        await db.Collection("turnos").Document("anchored").SetAsync(Booking("anchored", Vacancy.AddDays(1)));
        await db.Collection("turno_adelanto_anclas").Document("anchored").SetAsync(new Dictionary<string, object> { ["AnchorUtc"] = Vacancy.AddDays(7).AddMinutes(1) });
        Assert.Null(await service.SelectAndCreate(cascade, "bounded", Vacancy.AddHours(-1), Vacancy.AddMinutes(-50)));
        Assert.Empty((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
        Assert.Equal("", (await db.Collection("turno_cascadas").Document(cascade).GetSnapshotAsync()).GetValue<string>("PendingId"));
    }

    [LocalFirestoreFact]
    public async Task SameStartUsesOldestCreatedDocument()
    {
        var (db, cascade) = await Setup();
        await db.Collection("turnos").Document("z-old").CreateAsync(Booking("z-old", Vacancy.AddDays(1)));
        await db.Collection("turnos").Document("a-new").CreateAsync(Booking("a-new", Vacancy.AddDays(1)));
        var old = await db.Collection("turnos").Document("z-old").GetSnapshotAsync();
        var newer = await db.Collection("turnos").Document("a-new").GetSnapshotAsync();
        Assert.True(old.CreateTime < newer.CreateTime);
        Assert.Equal("z-old", await new TurnoOfertaPersistence(db).SelectAndCreate(cascade, "offer", Vacancy.AddHours(-1), Vacancy.AddMinutes(-50)));
    }

    [LocalFirestoreFact]
    public async Task DistinctCascadesExcludePendingAppointmentAndReleaseOnRejection()
    {
        var (db, first) = await Setup();
        await db.Collection("turnos").Document("cancelled-two").SetAsync(Booking("cancelled-two", Vacancy));
        await new TurnoService(db).Cancelar("cancelled-two");
        var second = Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents.Where(x => x.Id != first)).Id;
        await db.Collection("turnos").Document("near").SetAsync(Booking("near", Vacancy.AddDays(1)));
        var service = new TurnoOfertaPersistence(db);
        var created = Vacancy.AddHours(-1);
        var expires = Vacancy.AddMinutes(-50);
        Assert.Equal("near", await service.SelectAndCreate(first, "one", created, expires));
        Assert.Null(await service.SelectAndCreate(second, "two", created, expires));
        Assert.False((await db.Collection("turno_ofertas").Document("two").GetSnapshotAsync()).Exists);
        await service.Reject(first, "one", created.AddMinutes(1));
        Assert.Equal("near", await service.SelectAndCreate(second, "two", created, expires));
    }

    [LocalFirestoreFact]
    public async Task ConcurrentCascadesWithDifferentVacanciesChooseDifferentAppointments()
    {
        var (db, first) = await Setup();
        await db.Collection("turnos").Document("cancelled-two").SetAsync(Booking("cancelled-two", Vacancy.AddMinutes(30)));
        await new TurnoService(db).Cancelar("cancelled-two");
        var second = Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents.Where(x => x.Id != first)).Id;
        await db.Collection("turnos").Document("near").SetAsync(Booking("near", Vacancy.AddDays(1)));
        await db.Collection("turnos").Document("later").SetAsync(Booking("later", Vacancy.AddDays(2)));
        var service = new TurnoOfertaPersistence(db);
        var results = await Task.WhenAll(service.SelectAndCreate(first, "one", Vacancy.AddHours(-1), Vacancy.AddMinutes(-50)),
            service.SelectAndCreate(second, "two", Vacancy.AddHours(-1), Vacancy.AddMinutes(-50)));
        Assert.Equal(new[] { "later", "near" }, results.OrderBy(x => x).ToArray());
        Assert.Equal(2, (await db.Collection("turno_ofertas_pendientes").GetSnapshotAsync()).Count);
    }

    [LocalFirestoreFact]
    public async Task ConcurrentDistinctOffersCreateAtMostOnePendingOffer()
    {
        var (db, cascade) = await Setup();
        await db.Collection("turnos").Document("near").SetAsync(Booking("near", Vacancy.AddDays(1)));
        var service = new TurnoOfertaPersistence(db);
        async Task<bool> Attempt(string id)
        {
            try { return await service.SelectAndCreate(cascade, id, Vacancy.AddHours(-1), Vacancy.AddMinutes(-50)) == "near"; }
            catch (InvalidOperationException) { return false; }
        }
        var results = await Task.WhenAll(Attempt("one"), Attempt("two"));
        Assert.Single(results.Where(x => x));
        var offer = Assert.Single((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
        Assert.Equal(offer.Id, (await db.Collection("turno_cascadas").Document(cascade).GetSnapshotAsync()).GetValue<string>("PendingId"));
        Assert.Equal("near", offer.GetValue<string>("AppointmentId"));
    }

    [LocalFirestoreFact]
    public async Task SelectsClosestEligibleAndCreatesOfferAtomically()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-select-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var t = new DateTime(2030, 1, 8, 12, 0, 0, DateTimeKind.Utc);
        Turno Booking(string id, DateTime start) => new() { Id = id, ClienteId = id, Estado = "Pendiente", Origen = "test", ClaveIdempotencia = id, AceptaAdelanto = true, InicioUtc = start, FinUtc = start.AddMinutes(30), Vehiculos = [new() { VehiculoId = "vehicle", CantidadEmpleadosRequeridos = 1, Servicios = [new() { ServicioId = "service", TiempoEstimado = 30 }] }] };
        await db.Collection("turnos").Document("cancelled").SetAsync(Booking("cancelled", t));
        await db.Collection("turnos").Document("later").SetAsync(Booking("later", t.AddDays(2)));
        await db.Collection("turnos").Document("near").SetAsync(Booking("near", t.AddDays(1)));
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion());
        await new TurnoService(db).Cancelar("cancelled");
        var cascade = Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);
        var result = await new TurnoOfertaPersistence(db).SelectAndCreate(cascade.Id, "offer", t.AddHours(-1), t.AddMinutes(-50));
        Assert.Equal("near", result);
        var offer = await db.Collection("turno_ofertas").Document("offer").GetSnapshotAsync();
        Assert.Equal("near", offer.GetValue<string>("AppointmentId"));
        Assert.Equal(t, offer.GetValue<DateTime>("ProposedUtc"));
        Assert.Equal("offer", (await cascade.Reference.GetSnapshotAsync()).GetValue<string>("PendingId"));
        var before = await offer.Reference.GetSnapshotAsync();
        Assert.Equal("near", await new TurnoOfertaPersistence(db).SelectAndCreate(cascade.Id, "offer", t.AddHours(-1), t.AddMinutes(-50)));
        Assert.Equal(before.UpdateTime, (await offer.Reference.GetSnapshotAsync()).UpdateTime);
    }
}

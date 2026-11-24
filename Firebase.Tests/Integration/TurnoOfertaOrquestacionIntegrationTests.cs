using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;

namespace Firebase.Tests.Integration;

public class TurnoOfertaOrquestacionIntegrationTests
{
    private static readonly DateTime Vacancy = new(2030, 1, 8, 12, 0, 0, DateTimeKind.Utc);
    private static Turno Booking(string id, DateTime start) => new() { Id = id, ClienteId = id, Estado = "Pendiente", Origen = "test", ClaveIdempotencia = id, AceptaAdelanto = true, InicioUtc = start, FinUtc = start.AddMinutes(30), Vehiculos = [new() { VehiculoId = "vehicle", CantidadEmpleadosRequeridos = 1, Servicios = [new() { ServicioId = "service", TiempoEstimado = 30 }] }] };

    private static async Task<(FirestoreDb Db, string Cascade)> Setup(bool candidate = true)
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-orch-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("turnos").Document("cancelled").SetAsync(Booking("cancelled", Vacancy));
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion());
        await new TurnoService(db).Cancelar("cancelled");
        if (candidate) await db.Collection("turnos").Document("near").SetAsync(Booking("near", Vacancy.AddDays(1)));
        return (db, Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents).Id);
    }

    [LocalFirestoreFact]
    public async Task CorruptExpiryFailsClosedWithoutWrites()
    {
        foreach (var offset in new[] { 0, -1 })
        {
            var (db, cascade) = await Setup();
            var now = Vacancy.AddDays(-1);
            var orchestration = new TurnoOfertaOrquestacion(db, () => now);
            Assert.Equal("near", await orchestration.Resume(cascade));
            var offer = Assert.Single((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
            await offer.Reference.UpdateAsync("ExpiresUtc", now.AddMinutes(offset));
            var refs = new[] { offer.Reference, db.Collection("turno_cascadas").Document(cascade), db.Collection("turno_ofertas_pendientes").Document("near"), db.Collection("turnos").Document("near"), db.Collection("coordinacion").Document("turnos") };
            var before = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
            await Assert.ThrowsAsync<InvalidOperationException>(() => orchestration.Resume(cascade));
            var after = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
            for (var i = 0; i < refs.Length; i++) Assert.Equal(before[i].UpdateTime, after[i].UpdateTime);
            Assert.Empty((await db.Collection("turno_ofertas_terminales").GetSnapshotAsync()).Documents);
            Assert.Single((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
        }
    }

    [LocalFirestoreFact]
    public async Task ResumeContinuesAfterStaleRejectedOffer()
    {
        var (db, cascade) = await Setup();
        await db.Collection("turnos").Document("later").SetAsync(Booking("later", Vacancy.AddDays(2)));
        var now = Vacancy.AddDays(-1);
        var initial = new TurnoOfertaOrquestacion(db, () => now);
        Assert.Equal("near", await initial.Resume(cascade));
        var offer = Assert.Single((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
        var deadline = offer.GetValue<DateTime>("ExpiresUtc");
        var resume = new TurnoOfertaOrquestacion(db, () => deadline) { AfterPendingRead = () => new TurnoOfertaPersistence(db).Reject(cascade, offer.Id, deadline.AddTicks(-10)) };
        Assert.Equal("later", await resume.Resume(cascade));
        Assert.Equal("Rechazada", (await offer.Reference.GetSnapshotAsync()).GetValue<string>("Estado"));
        Assert.False((await db.Collection("turno_ofertas_pendientes").Document("near").GetSnapshotAsync()).Exists);
        Assert.Equal(2, (await db.Collection("turno_ofertas").GetSnapshotAsync()).Count);
    }

    [LocalFirestoreFact]
    public async Task ResumeAfterRejectionOrExpirySelectsNextAndReplaysPendingWithoutWrites()
    {
        foreach (var expire in new[] { false, true })
        {
            var (db, cascade) = await Setup();
            await db.Collection("turnos").Document("later").SetAsync(Booking("later", Vacancy.AddDays(2)));
            var orchestration = new TurnoOfertaOrquestacion(db);
            Assert.Equal("near", await orchestration.SelectAndCreate(cascade));
            var first = Assert.Single((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
            var persistence = new TurnoOfertaPersistence(db);
            var created = first.GetValue<DateTime>("CreatedUtc");
            if (expire) await persistence.Expire(cascade, first.Id, first.GetValue<DateTime>("ExpiresUtc"));
            else await persistence.Reject(cascade, first.Id, created);
            Assert.Equal("later", await new TurnoOfertaOrquestacion(db).Resume(cascade));
            var offers = await db.Collection("turno_ofertas").GetSnapshotAsync();
            Assert.Equal(2, offers.Count);
            var pending = Assert.Single(offers.Documents.Where(x => x.GetValue<string>("Estado") == "Pendiente"));
            var cascadeBefore = await db.Collection("turno_cascadas").Document(cascade).GetSnapshotAsync();
            var claimBefore = await db.Collection("turno_ofertas_pendientes").Document("later").GetSnapshotAsync();
            Assert.Equal("later", await new TurnoOfertaOrquestacion(db).Resume(cascade));
            Assert.Equal(pending.UpdateTime, (await pending.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(cascadeBefore.UpdateTime, (await cascadeBefore.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(claimBefore.UpdateTime, (await claimBefore.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(2, (await db.Collection("turno_ofertas").GetSnapshotAsync()).Count);
        }
    }

    [LocalFirestoreFact]
    public async Task ResumeAfterAcceptanceUsesReleasedOriginalVacancy()
    {
        var (db, cascade) = await Setup();
        await db.Collection("turnos").Document("later").SetAsync(Booking("later", Vacancy.AddDays(2)));
        await db.Collection("empleados").Document("one").SetAsync(new { Estado = "Activo" });
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion { CapacidadMaximaConcurrente = 1, HorariosOperacion = new() { ["Miércoles"] = "00:00-23:59", ["Jueves"] = "00:00-23:59", ["Viernes"] = "00:00-23:59", ["Sábado"] = "00:00-23:59", ["Domingo"] = "00:00-23:59", ["Lunes"] = "00:00-23:59", ["Martes"] = "00:00-23:59" } });
        Assert.Equal("near", await new TurnoOfertaOrquestacion(db).SelectAndCreate(cascade));
        var first = Assert.Single((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
        await new TurnoOfertaPersistence(db).Accept(cascade, first.Id, first.GetValue<DateTime>("CreatedUtc"));
        Assert.Equal("later", await new TurnoOfertaOrquestacion(db).Resume(cascade));
        var pending = Assert.Single((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents.Where(x => x.GetValue<string>("Estado") == "Pendiente"));
        Assert.Equal(Vacancy.AddDays(1), pending.GetValue<DateTime>("ProposedUtc"));
        Assert.Equal(Vacancy, (await db.Collection("turnos").Document("near").GetSnapshotAsync()).GetValue<DateTime>("InicioUtc"));
    }

    [LocalFirestoreFact]
    public async Task ResumeWithCorruptPendingClaimFailsWithoutWrites()
    {
        var (db, cascade) = await Setup();
        Assert.Equal("near", await new TurnoOfertaOrquestacion(db).SelectAndCreate(cascade));
        var claim = db.Collection("turno_ofertas_pendientes").Document("near");
        await claim.UpdateAsync("CascadeId", "other");
        var refs = new[] { claim, db.Collection("turno_cascadas").Document(cascade), db.Collection("turnos").Document("near"), db.Collection("coordinacion").Document("turnos") };
        var before = await Task.WhenAll(refs.Select(x => x.GetSnapshotAsync()));
        var offer = Assert.Single((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new TurnoOfertaOrquestacion(db).Resume(cascade));
        var after = await Task.WhenAll(refs.Select(x => x.GetSnapshotAsync()));
        for (var i = 0; i < refs.Length; i++) Assert.Equal(before[i].UpdateTime, after[i].UpdateTime);
        Assert.Equal(offer.UpdateTime, (await offer.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Single((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task ConfiguredDurationAndRestartReplayPreservePendingOffer()
    {
        var (db, cascade) = await Setup();
        await db.Collection("configuracion").Document("system_config").UpdateAsync("RespuestaOfertaAdelantoMinutos", 23);
        var before = DateTime.UtcNow.AddSeconds(-1);
        Assert.Equal("near", await new TurnoOfertaOrquestacion(db).SelectAndCreate(cascade));
        var offer = Assert.Single((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
        Assert.True(offer.GetValue<DateTime>("CreatedUtc") >= before);
        Assert.True(offer.GetValue<DateTime>("CreatedUtc") <= DateTime.UtcNow);
        Assert.Equal(TimeSpan.FromMinutes(23), offer.GetValue<DateTime>("ExpiresUtc") - offer.GetValue<DateTime>("CreatedUtc"));
        await db.Collection("configuracion").Document("system_config").UpdateAsync("RespuestaOfertaAdelantoMinutos", 3);
        Assert.Equal("near", await new TurnoOfertaOrquestacion(db).SelectAndCreate(cascade));
        Assert.Equal(offer.UpdateTime, (await offer.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Single((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task MissingDurationDefaultsToTenMinutes()
    {
        var (db, cascade) = await Setup();
        await db.Collection("configuracion").Document("system_config").UpdateAsync("RespuestaOfertaAdelantoMinutos", FieldValue.Delete);
        Assert.Equal("near", await new TurnoOfertaOrquestacion(db).SelectAndCreate(cascade));
        var offer = Assert.Single((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
        Assert.Equal(TimeSpan.FromMinutes(10), offer.GetValue<DateTime>("ExpiresUtc") - offer.GetValue<DateTime>("CreatedUtc"));
    }

    [LocalFirestoreFact]
    public async Task InvalidDurationFailsWithoutWrites()
    {
        var (db, cascade) = await Setup();
        await db.Collection("configuracion").Document("system_config").UpdateAsync("RespuestaOfertaAdelantoMinutos", 0);
        var before = await db.Collection("turno_cascadas").Document(cascade).GetSnapshotAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new TurnoOfertaOrquestacion(db).SelectAndCreate(cascade));
        Assert.Empty((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_adelanto_anclas").GetSnapshotAsync()).Documents);
        Assert.Equal(before.UpdateTime, (await before.Reference.GetSnapshotAsync()).UpdateTime);
    }

    [LocalFirestoreFact]
    public async Task NoCandidatesLeavesCascadeUntouched()
    {
        var (db, cascade) = await Setup(false);
        var before = await db.Collection("turno_cascadas").Document(cascade).GetSnapshotAsync();
        Assert.Null(await new TurnoOfertaOrquestacion(db).SelectAndCreate(cascade));
        Assert.Empty((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
        Assert.Equal(before.UpdateTime, (await before.Reference.GetSnapshotAsync()).UpdateTime);
    }
}

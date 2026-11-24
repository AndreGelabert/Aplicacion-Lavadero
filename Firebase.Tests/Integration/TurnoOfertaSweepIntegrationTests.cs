using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Firebase.Tests.Integration;

public class TurnoOfertaSweepIntegrationTests
{
    private static readonly DateTime Vacancy = new(2030, 1, 8, 12, 0, 0, DateTimeKind.Utc);
    private static Turno Booking(string id, DateTime start) => new() { Id = id, ClienteId = id, Estado = "Pendiente", Origen = "test", ClaveIdempotencia = id, AceptaAdelanto = true, InicioUtc = start, FinUtc = start.AddMinutes(30), Vehiculos = [new() { VehiculoId = "vehicle", CantidadEmpleadosRequeridos = 1, Servicios = [new() { ServicioId = "service", TiempoEstimado = 30 }] }] };

    [LocalFirestoreFact]
    public async Task SweepExpiresAtDeadlineAndContinuesWithoutPrematureOrReplayWrites()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-sweep-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("turnos").Document("cancelled").SetAsync(Booking("cancelled", Vacancy));
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion());
        await new TurnoService(db).Cancelar("cancelled");
        await db.Collection("turnos").Document("near").SetAsync(Booking("near", Vacancy.AddDays(1)));
        await db.Collection("turnos").Document("later").SetAsync(Booking("later", Vacancy.AddDays(2)));
        var now = Vacancy.AddDays(-1);
        var sweep = new TurnoOfertaSweep(db, NullLogger<TurnoOfertaSweep>.Instance, () => now);
        await sweep.RunBatchAsync(10, null, CancellationToken.None);
        var first = Assert.Single((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
        var cascade = Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);
        var claim = await db.Collection("turno_ofertas_pendientes").Document("near").GetSnapshotAsync();
        Assert.True(claim.Exists);
        now = first.GetValue<DateTime>("ExpiresUtc").AddTicks(-10);
        await sweep.RunBatchAsync(10, null, CancellationToken.None);
        Assert.Equal(first.UpdateTime, (await first.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(cascade.UpdateTime, (await cascade.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(claim.UpdateTime, (await claim.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Single((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_ofertas_terminales").GetSnapshotAsync()).Documents);
        now = first.GetValue<DateTime>("ExpiresUtc");
        await sweep.RunBatchAsync(10, null, CancellationToken.None);
        Assert.Equal("Expirada", (await first.Reference.GetSnapshotAsync()).GetValue<string>("Estado"));
        Assert.False((await claim.Reference.GetSnapshotAsync()).Exists);
        var terminal = Assert.Single((await db.Collection("turno_ofertas_terminales").GetSnapshotAsync()).Documents);
        Assert.Equal("Expirada", terminal.GetValue<string>("Estado"));
        var offers = await db.Collection("turno_ofertas").GetSnapshotAsync();
        Assert.Equal(2, offers.Count);
        var next = Assert.Single(offers.Documents.Where(x => x.GetValue<string>("Estado") == "Pendiente"));
        Assert.Equal("later", next.GetValue<string>("AppointmentId"));
        var nextClaim = await db.Collection("turno_ofertas_pendientes").Document("later").GetSnapshotAsync();
        Assert.True(nextClaim.Exists);
        var cascadeAfter = await cascade.Reference.GetSnapshotAsync();
        var expiredAfter = await first.Reference.GetSnapshotAsync();
        await sweep.RunBatchAsync(10, null, CancellationToken.None);
        Assert.Equal(expiredAfter.UpdateTime, (await first.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(terminal.UpdateTime, (await terminal.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(next.UpdateTime, (await next.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(nextClaim.UpdateTime, (await nextClaim.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(cascadeAfter.UpdateTime, (await cascade.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(2, (await db.Collection("turno_ofertas").GetSnapshotAsync()).Count);
    }

    [LocalFirestoreFact]
    public async Task TwoSweepsAtDeadlineLeaveOneNextOffer()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-sweep-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("turnos").Document("cancelled").SetAsync(Booking("cancelled", Vacancy));
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion());
        await new TurnoService(db).Cancelar("cancelled");
        await db.Collection("turnos").Document("near").SetAsync(Booking("near", Vacancy.AddDays(1)));
        await db.Collection("turnos").Document("later").SetAsync(Booking("later", Vacancy.AddDays(2)));
        var now = Vacancy.AddDays(-1);
        var sweep = new TurnoOfertaSweep(db, NullLogger<TurnoOfertaSweep>.Instance, () => now);
        await sweep.RunBatchAsync(10, null, CancellationToken.None);
        var first = Assert.Single((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
        now = first.GetValue<DateTime>("ExpiresUtc");
        await Task.WhenAll(sweep.RunBatchAsync(10, null, CancellationToken.None), sweep.RunBatchAsync(10, null, CancellationToken.None));
        await sweep.RunBatchAsync(10, null, CancellationToken.None);
        var offers = await db.Collection("turno_ofertas").GetSnapshotAsync();
        Assert.Equal(2, offers.Count);
        Assert.Equal("Expirada", (await first.Reference.GetSnapshotAsync()).GetValue<string>("Estado"));
        Assert.Equal("later", Assert.Single(offers.Documents.Where(x => x.GetValue<string>("Estado") == "Pendiente")).GetValue<string>("AppointmentId"));
        Assert.False((await db.Collection("turno_ofertas_pendientes").Document("near").GetSnapshotAsync()).Exists);
        Assert.True((await db.Collection("turno_ofertas_pendientes").Document("later").GetSnapshotAsync()).Exists);
        Assert.Single((await db.Collection("turno_ofertas_terminales").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task BatchReportsFailuresAndStillProcessesLaterCascadesAcrossWraps()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-sweep-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion());
        await db.Collection("turnos").Document("cancelled").SetAsync(Booking("cancelled", Vacancy));
        await new TurnoService(db).Cancelar("cancelled");
        await db.Collection("turnos").Document("candidate").SetAsync(Booking("candidate", Vacancy.AddDays(1)));
        var bad = db.Collection("turno_cascadas").Document("!");
        await bad.SetAsync(new { ContinuationState = "Pendiente", CancelledAppointmentId = "missing" });
        var before = await bad.GetSnapshotAsync();
        var sweep = new TurnoOfertaSweep(db, NullLogger<TurnoOfertaSweep>.Instance, () => Vacancy.AddDays(-1));

        var first = await sweep.RunBatchWithFailuresAsync(1, null, CancellationToken.None);
        Assert.Equal("!", first.Cursor);
        Assert.Equal(new[] { "!" }, first.FailedCascadeIds);
        Assert.Empty((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
        var second = await sweep.RunBatchWithFailuresAsync(1, first.Cursor, CancellationToken.None);
        Assert.Empty(second.FailedCascadeIds);
        Assert.NotNull(second.Cursor);
        var offer = Assert.Single((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
        Assert.Equal("candidate", offer.GetValue<string>("AppointmentId"));
        var wrap = await sweep.RunBatchWithFailuresAsync(1, second.Cursor, CancellationToken.None);
        Assert.Null(wrap.Cursor);
        Assert.Empty(wrap.FailedCascadeIds);
        var retry = await sweep.RunBatchWithFailuresAsync(1, wrap.Cursor, CancellationToken.None);
        Assert.Equal("!", retry.Cursor);
        Assert.Equal(new[] { "!" }, retry.FailedCascadeIds);
        Assert.Equal(before.UpdateTime, (await bad.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(offer.UpdateTime, (await offer.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Single((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);

        var finalPage = await sweep.RunBatchWithFailuresAsync(10, null, CancellationToken.None);
        Assert.Null(finalPage.Cursor);
        Assert.Equal(new[] { "!" }, finalPage.FailedCascadeIds);
        Assert.Equal(offer.UpdateTime, (await offer.Reference.GetSnapshotAsync()).UpdateTime);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sweep.RunBatchWithFailuresAsync(1, null, cts.Token));
    }

    [LocalFirestoreFact]
    public async Task PartialRootedCascadeIsReportedWhileLegacyOfferOnlyCascadeIsUntouched()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-sweep-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var partial = db.Collection("turno_cascadas").Document("partial");
        var legacy = db.Collection("turno_cascadas").Document("legacy");
        await partial.SetAsync(new { ContinuationState = "Pendiente", VacancyStartUtc = Vacancy, PendingId = "" });
        await legacy.SetAsync(new { PendingId = "legacy-offer" });
        var beforePartial = await partial.GetSnapshotAsync();
        var beforeLegacy = await legacy.GetSnapshotAsync();
        var sweep = new TurnoOfertaSweep(db, NullLogger<TurnoOfertaSweep>.Instance);
        var result = await sweep.RunBatchWithFailuresAsync(10, null, CancellationToken.None);
        Assert.Null(result.Cursor);
        Assert.Equal(new[] { "partial" }, result.FailedCascadeIds);
        Assert.Equal(beforePartial.UpdateTime, (await partial.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(beforeLegacy.UpdateTime, (await legacy.GetSnapshotAsync()).UpdateTime);
        Assert.Empty((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_ofertas_pendientes").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_ofertas_terminales").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("coordinacion").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task CursorWrapperReportsFailures()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-sweep-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("turno_cascadas").Document("bad").SetAsync(new { ContinuationState = "Pendiente", CancelledAppointmentId = "missing" });
        var sweep = new TurnoOfertaSweep(db, NullLogger<TurnoOfertaSweep>.Instance);
        var before = await db.Collection("turno_cascadas").Document("bad").GetSnapshotAsync();
        var ex = await Assert.ThrowsAsync<TurnoOfertaSweep.BatchFailedException>(() => sweep.RunBatchAsync(1, null, CancellationToken.None));
        Assert.Equal("bad", ex.Cursor);
        Assert.Equal(new[] { "bad" }, ex.FailedCascadeIds);
        Assert.Contains("bad", ex.Message);
        Assert.Equal(before.UpdateTime, (await before.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Null(await sweep.RunBatchAsync(1, ex.Cursor, CancellationToken.None));
        var final = await Assert.ThrowsAsync<TurnoOfertaSweep.BatchFailedException>(() => sweep.RunBatchAsync(10, null, CancellationToken.None));
        Assert.Null(final.Cursor);
        Assert.Equal(new[] { "bad" }, final.FailedCascadeIds);
    }

    [LocalFirestoreFact]
    public async Task SweepWrapsRevisitsFailuresAndReplaysWithoutDuplicates()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-sweep-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("turnos").Document("cancelled").SetAsync(Booking("cancelled", Vacancy));
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion { RespuestaOfertaAdelantoMinutos = 0 });
        await new TurnoService(db).Cancelar("cancelled");
        await db.Collection("turnos").Document("candidate").SetAsync(Booking("candidate", Vacancy.AddDays(1)));
        var cascade = Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);
        var sweep = new TurnoOfertaSweep(db, NullLogger<TurnoOfertaSweep>.Instance);
        var failure = await Assert.ThrowsAsync<TurnoOfertaSweep.BatchFailedException>(() => sweep.RunBatchAsync(1, null, CancellationToken.None));
        Assert.Equal(new[] { cascade.Id }, failure.FailedCascadeIds);
        var cursor = failure.Cursor;
        Assert.Equal(cascade.Id, cursor);
        Assert.Empty((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
        Assert.Equal(cascade.UpdateTime, (await cascade.Reference.GetSnapshotAsync()).UpdateTime);
        await db.Collection("configuracion").Document("system_config").UpdateAsync("RespuestaOfertaAdelantoMinutos", 10);
        cursor = await sweep.RunBatchAsync(1, cursor, CancellationToken.None);
        Assert.Null(cursor);
        await sweep.RunBatchAsync(1, cursor, CancellationToken.None);
        var offer = Assert.Single((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
        await sweep.RunBatchAsync(1, null, CancellationToken.None);
        Assert.Equal(offer.UpdateTime, (await offer.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Single((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
    }
}

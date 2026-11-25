using Firebase.BackgroundServices;
using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Firebase.Tests.Integration;

public class TurnoOfertaHostIntegrationTests
{
    [LocalFirestoreFact]
    public async Task FailedPageDoesNotStarveLaterCascadeAndIsRevisitedAfterWrap()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-host-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var vacancy = new DateTime(2030, 1, 8, 12, 0, 0, DateTimeKind.Utc);
        Turno Booking(string id, DateTime start) => new()
        {
            Id = id, ClienteId = id, Estado = "Pendiente", Origen = "test", ClaveIdempotencia = id,
            AceptaAdelanto = true, InicioUtc = start, FinUtc = start.AddMinutes(30),
            Vehiculos = [new() { VehiculoId = "vehicle", CantidadEmpleadosRequeridos = 1, Servicios = [new() { ServicioId = "service", TiempoEstimado = 30 }] }]
        };
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion());
        await db.Collection("turnos").Document("cancelled").SetAsync(Booking("cancelled", vacancy));
        await new TurnoService(db).Cancelar("cancelled");
        await db.Collection("turnos").Document("candidate").SetAsync(Booking("candidate", vacancy.AddDays(1)));
        var cascade = Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);
        var batch = db.StartBatch();
        for (var i = 0; i < 100; i++)
            batch.Set(db.Collection("turno_cascadas").Document($"!{i:D3}"), new { ContinuationState = "Pendiente", CancelledAppointmentId = "missing" });
        await batch.CommitAsync();
        var bad = db.Collection("turno_cascadas").Document("!000");
        var before = await bad.GetSnapshotAsync();
        var sweep = new TurnoOfertaSweep(db, NullLogger<TurnoOfertaSweep>.Instance, () => vacancy.AddDays(-1));
        var cursors = new List<string?>();
        var failures = new List<TurnoOfertaSweep.BatchFailedException>();
        using var stop = new CancellationTokenSource();
        var host = new TurnoOfertaHost(async (size, cursor, token) =>
        {
            cursors.Add(cursor);
            try { return await sweep.RunBatchAsync(size, cursor, token); }
            catch (TurnoOfertaSweep.BatchFailedException ex) { failures.Add(ex); throw; }
        }, (delay, token) =>
        {
            Assert.Equal(TimeSpan.FromMinutes(1), delay);
            if (cursors.Count == 3) stop.Cancel();
            return Task.CompletedTask;
        }, NullLogger<TurnoOfertaHost>.Instance);

        await host.RunAsync(stop.Token);

        Assert.Equal(new string?[] { null, "!099", null }, cursors);
        Assert.Equal(2, failures.Count);
        Assert.All(failures, failure =>
        {
            Assert.Equal("!099", failure.Cursor);
            Assert.Equal(100, failure.FailedCascadeIds.Count);
            Assert.Contains("!000", failure.FailedCascadeIds);
        });
        Assert.Equal(before.UpdateTime, (await bad.GetSnapshotAsync()).UpdateTime);
        var offer = Assert.Single((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
        Assert.Equal("candidate", offer.GetValue<string>("AppointmentId"));
        Assert.True((await db.Collection("turno_ofertas_pendientes").Document("candidate").GetSnapshotAsync()).Exists);
        Assert.True((await cascade.Reference.GetSnapshotAsync()).Exists);
    }
}

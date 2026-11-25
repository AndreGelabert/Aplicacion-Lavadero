using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Firebase.Tests.Integration;

public class TurnoNoShowSchedulerIntegrationTests
{
    [Fact]
    public void ProductionRegistrationActivatesSchedulerAndPreservesEmulatorGate()
    {
        var source = File.ReadAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(SourcePath())!, "../../Program.cs")));
        var start = source.IndexOf("if (!localEmulators)\n{", StringComparison.Ordinal);
        if (start < 0) start = source.IndexOf("if (!localEmulators)\r\n{", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = source.IndexOf("var app = builder.Build();", start, StringComparison.Ordinal);
        Assert.True(end > start);
        var registration = source[start..end];
        Assert.Contains("TurnoHostRegistrations.AddBackgroundServices(builder.Services);", registration);
        Assert.Equal(1, source.Split("TurnoHostRegistrations.AddBackgroundServices(builder.Services);", StringSplitOptions.None).Length - 1);
        Assert.Contains("TurnoHostRegistrations.AddWhatsAppFlow(builder.Services);", source);
        Assert.Contains("services.AddHostedService<Firebase.BackgroundServices.WhatsAppSessionCleanupService>()", source);
        Assert.Contains("services.AddHostedService<Firebase.BackgroundServices.TurnoNoShowService>()", source);
        Assert.Contains(".OfType<Firebase.BackgroundServices.TurnoNoShowService>().Any(service => service.ExecuteTask is { IsCompleted: false })", source);
    }

    private static string SourcePath([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;

    [LocalFirestoreFact]
    public async Task SweepPagesAndIsolatesFailuresWithoutEarlyWrites()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-sweep-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var now = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        await db.Collection("configuracion").Document("system_config").SetAsync(new { ToleranciaLlegadaTurnosMinutos = 15 });
        foreach (var id in new[] { "a", "b", "c", "d" })
            await db.Collection("turnos").Document(id).SetAsync(new { Estado = "Pendiente", InicioUtc = now.AddMinutes(-16) });
        await db.Collection("turnos").Document("b").UpdateAsync("LavadoId", "incompatible");
        await db.Collection("turnos").Document("started").SetAsync(new { Estado = "EnProceso", InicioUtc = now.AddHours(-1), LavadoId = "wash" });
        await db.Collection("turnos").Document("early").SetAsync(new { Estado = "Pendiente", InicioUtc = now.AddMinutes(-15) });
        var early = await db.Collection("turnos").Document("early").GetSnapshotAsync();
        var started = await db.Collection("turnos").Document("started").GetSnapshotAsync();
        var sweep = new TurnoNoShowSweep(db, new TurnoService(db), NullLogger<TurnoNoShowSweep>.Instance);
        string? cursor = null;
        do { cursor = await sweep.RunBatchAsync(() => now, 2, cursor, CancellationToken.None); } while (cursor != null);
        foreach (var id in new[] { "a", "c", "d" }) Assert.Equal("Ausente", (await db.Collection("turnos").Document(id).GetSnapshotAsync()).GetValue<string>("Estado"));
        Assert.Equal("Pendiente", (await db.Collection("turnos").Document("b").GetSnapshotAsync()).GetValue<string>("Estado"));
        Assert.Equal(early.UpdateTime, (await early.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(started.UpdateTime, (await started.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Empty((await db.Collection("turno_cupones").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task BatchIsBoundedMakesProgressAndUsesFreshClock()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-batch-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var now = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        await db.Collection("configuracion").Document("system_config").SetAsync(new { ToleranciaLlegadaTurnosMinutos = 0 });
        foreach (var id in new[] { "a", "b", "c" })
            await db.Collection("turnos").Document(id).SetAsync(new { Estado = "Pendiente", InicioUtc = now });
        var sweep = new TurnoNoShowSweep(db, new TurnoService(db), NullLogger<TurnoNoShowSweep>.Instance);
        var calls = 0;
        DateTime Clock() => now.AddSeconds(++calls);
        var cursor = await sweep.RunBatchAsync(Clock, 2, null, CancellationToken.None);
        Assert.NotNull(cursor);
        Assert.Equal("Ausente", (await db.Collection("turnos").Document("a").GetSnapshotAsync()).GetValue<string>("Estado"));
        Assert.Equal("Ausente", (await db.Collection("turnos").Document("b").GetSnapshotAsync()).GetValue<string>("Estado"));
        Assert.Equal("Pendiente", (await db.Collection("turnos").Document("c").GetSnapshotAsync()).GetValue<string>("Estado"));
        Assert.True(calls >= 2);
        Assert.Null(await sweep.RunBatchAsync(Clock, 2, cursor, CancellationToken.None));
        Assert.Equal("Ausente", (await db.Collection("turnos").Document("c").GetSnapshotAsync()).GetValue<string>("Estado"));
    }

    [LocalFirestoreFact]
    public async Task DiscoverySkipsNonPendingAndWrapRevisitsChangedAndFailedRecords()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-discovery-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var now = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        await db.Collection("configuracion").Document("system_config").SetAsync(new { ToleranciaLlegadaTurnosMinutos = 0 });
        var turns = db.Collection("turnos");
        await turns.Document("a").SetAsync(new { Estado = "EnProceso", InicioUtc = now.AddHours(-1) });
        await turns.Document("b").SetAsync(new { Estado = "Pendiente", InicioUtc = now.AddHours(-1), LavadoId = "incompatible" });
        await turns.Document("c").SetAsync(new { Estado = "Cancelado", InicioUtc = now.AddHours(-1) });
        await turns.Document("d").SetAsync(new { Estado = "Pendiente", InicioUtc = now.AddHours(-1) });
        var sweep = new TurnoNoShowSweep(db, new TurnoService(db), NullLogger<TurnoNoShowSweep>.Instance);
        var cursor = await sweep.RunBatchAsync(() => now, 1, null, CancellationToken.None);
        Assert.Equal("b", cursor);
        await turns.Document("a").UpdateAsync("Estado", "Pendiente");
        Assert.Equal("d", await sweep.RunBatchAsync(() => now, 1, cursor, CancellationToken.None));
        Assert.Null(await sweep.RunBatchAsync(() => now, 1, "d", CancellationToken.None));
        Assert.Equal("Pendiente", (await turns.Document("a").GetSnapshotAsync()).GetValue<string>("Estado"));
        await turns.Document("b").UpdateAsync("LavadoId", FieldValue.Delete);
        Assert.Equal("a", await sweep.RunBatchAsync(() => now, 1, null, CancellationToken.None));
        Assert.Equal("b", await sweep.RunBatchAsync(() => now, 1, "a", CancellationToken.None));
        Assert.Equal("Ausente", (await turns.Document("a").GetSnapshotAsync()).GetValue<string>("Estado"));
        Assert.Equal("Ausente", (await turns.Document("b").GetSnapshotAsync()).GetValue<string>("Estado"));
        Assert.Equal("Cancelado", (await turns.Document("c").GetSnapshotAsync()).GetValue<string>("Estado"));
    }

    [LocalFirestoreFact]
    public async Task MissingOrInvalidConfigurationFailsClosedWithoutWrites()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-config-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var now = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var turn = db.Collection("turnos").Document("a");
        await turn.SetAsync(new { Estado = "Pendiente", InicioUtc = now.AddHours(-1) });
        var before = await turn.GetSnapshotAsync();
        var sweep = new TurnoNoShowSweep(db, new TurnoService(db), NullLogger<TurnoNoShowSweep>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sweep.RunBatchAsync(() => now, 2, null, CancellationToken.None));
        await db.Collection("configuracion").Document("system_config").SetAsync(new { ToleranciaLlegadaTurnosMinutos = -1 });
        await Assert.ThrowsAsync<InvalidOperationException>(() => sweep.RunBatchAsync(() => now, 2, null, CancellationToken.None));
        Assert.Equal(before.UpdateTime, (await turn.GetSnapshotAsync()).UpdateTime);
        Assert.Empty((await db.Collection("coordinacion").GetSnapshotAsync()).Documents);
    }
}

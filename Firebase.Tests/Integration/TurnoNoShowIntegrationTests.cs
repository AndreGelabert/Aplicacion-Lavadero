using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Caching.Memory;

namespace Firebase.Tests.Integration;

public class TurnoNoShowIntegrationTests
{
    private static readonly DateTime Start = new(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static async Task<FirestoreDb> Setup()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-noshow-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("turnos").Document("t").SetAsync(new Turno { Id = "t", ClienteId = "c", Estado = "Pendiente", Origen = "web", ClaveIdempotencia = "k", InicioUtc = Start, FinUtc = Start.AddMinutes(10), Vehiculos = [new() { VehiculoId = "v", CantidadEmpleadosRequeridos = 1, Servicios = [new() { ServicioId = "s", TiempoEstimado = 10 }] }], RecetaOperativa = new() { Version = 1, Servicios = [new() { ServicioId = "s", Nombre = "Servicio", Tipo = "Lavado" }] } });
        await db.Collection("coordinacion").Document("turnos").SetAsync(new { Version = 7L });
        await db.Collection("configuracion").Document("system_config").SetAsync(new { ToleranciaLlegadaTurnosMinutos = 15, CapacidadMaximaConcurrente = 10 });
        await db.Collection("empleados").Document("e").SetAsync(new { Estado = "Activo" });
        return db;
    }
    private static async Task<string[]> Snapshot(FirestoreDb db)
    {
        var result = new List<string>();
        foreach (var name in new[] { "turnos", "coordinacion", "turno_cupones", "turno_cascadas", "lavados", "configuracion", "empleados", "employee_membership" })
            result.AddRange((await db.Collection(name).GetSnapshotAsync()).Documents.Select(d => $"{name}/{d.Id}:{d.UpdateTime}:{System.Text.Json.JsonSerializer.Serialize(d.ToDictionary())}"));
        return result.OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }
    [LocalFirestoreFact]
    public async Task DeadlineAndReplayAreAtomicWithoutCouponOrCascade()
    {
        var db = await Setup(); var service = new TurnoService(db);
        var before = await Snapshot(db);
        Assert.False(await service.MarcarAusencia("t", Start.AddMinutes(15).AddTicks(-10)));
        Assert.Equal(before, await Snapshot(db));
        Assert.False(await service.MarcarAusencia("t", Start.AddMinutes(15)));
        Assert.Equal(before, await Snapshot(db));
        Assert.True(await service.MarcarAusencia("t", Start.AddMinutes(15).AddTicks(10)));
        Assert.Equal("Ausente", (await db.Collection("turnos").Document("t").GetSnapshotAsync()).GetValue<string>("Estado"));
        Assert.Equal(8L, (await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).GetValue<long>("Version"));
        Assert.Empty((await db.Collection("turno_cupones").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);
        var after = await Snapshot(db);
        Assert.True(await service.MarcarAusencia("t", Start.AddMinutes(16)));
        Assert.Equal(after, await Snapshot(db));
        var db2 = await Setup();
        Assert.True(await new TurnoService(db2).MarcarAusencia("t", Start.AddMinutes(16)));
    }
    [LocalFirestoreFact]
    public async Task ZeroUsesNoExtraGraceAndNullFailsClosed()
    {
        var db = await Setup(); var config = db.Collection("configuracion").Document("system_config");
        await config.UpdateAsync("ToleranciaLlegadaTurnosMinutos", 0);
        var service = new TurnoService(db); var before = await Snapshot(db);
        Assert.False(await service.MarcarAusencia("t", Start));
        Assert.Equal(before, await Snapshot(db));
        Assert.True(await service.MarcarAusencia("t", Start.AddTicks(10)));
        Assert.Equal("Ausente", (await db.Collection("turnos").Document("t").GetSnapshotAsync()).GetValue<string>("Estado"));
        Assert.Empty((await db.Collection("turno_cupones").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);
        var after = await Snapshot(db);
        Assert.True(await service.MarcarAusencia("t", Start.AddMinutes(1)));
        Assert.Equal(after, await Snapshot(db));
        var db2 = await Setup();
        await db2.Collection("configuracion").Document("system_config").UpdateAsync("ToleranciaLlegadaTurnosMinutos", null);
        var unchanged = await Snapshot(db2);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new TurnoService(db2).MarcarAusencia("t", Start.AddMinutes(16)));
        Assert.Equal("Tolerancia de llegada no disponible.", error.Message);
        Assert.Equal(unchanged, await Snapshot(db2));
    }
    [LocalFirestoreFact]
    public async Task StartedCompletedAndMalformedInputsNeverWrite()
    {
        foreach (var state in new[] { "EnProceso", "Completado" })
        {
            var db = await Setup(); await db.Collection("turnos").Document("t").UpdateAsync("Estado", state);
            var before = await Snapshot(db);
            Assert.False(await new TurnoService(db).MarcarAusencia("t", Start.AddMinutes(16)));
            Assert.Equal(before, await Snapshot(db));
        }
        var db2 = await Setup(); var original = await Snapshot(db2);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => new TurnoService(db2).MarcarAusencia("t", DateTime.SpecifyKind(Start, DateTimeKind.Unspecified)));
        Assert.Equal(original, await Snapshot(db2));
    }
    [LocalFirestoreFact]
    public async Task ConversionAndAbsenceRaceHaveOnlyOneWinner()
    {
        var db = await Setup(); using var cache = new MemoryCache(new MemoryCacheOptions());
        var services = new ServicioService(db);
        var washes = new LavadoService(db, services, new PaqueteServicioService(db, services, cache), new PersonalService(db), new ConfiguracionService(db, cache));
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Absent() { await gate.Task; return await new TurnoService(db).MarcarAusencia("t", Start.AddMinutes(16)); }
        async Task<string?> Convert() { await gate.Task; try { return await washes.ConvertirTurnoEnLavado("t", null); } catch (InvalidOperationException) { return null; } }
        var absence = Absent(); var conversion = Convert(); gate.SetResult(); await Task.WhenAll(absence, conversion);
        var turn = await db.Collection("turnos").Document("t").GetSnapshotAsync(); var created = await db.Collection("lavados").GetSnapshotAsync();
        if (absence.Result) { Assert.Null(conversion.Result); Assert.Equal("Ausente", turn.GetValue<string>("Estado")); Assert.Empty(created.Documents); }
        else { Assert.NotNull(conversion.Result); Assert.Equal("EnProceso", turn.GetValue<string>("Estado")); Assert.Equal(conversion.Result, turn.GetValue<string>("LavadoId")); Assert.Single(created.Documents); }
        Assert.Empty((await db.Collection("turno_cupones").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);
    }
}

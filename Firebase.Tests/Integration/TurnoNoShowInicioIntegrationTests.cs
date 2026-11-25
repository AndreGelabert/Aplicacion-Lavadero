using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Firebase.Tests.Integration;

public class TurnoNoShowInicioIntegrationTests
{
    private static readonly DateTime Start = new(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static async Task<(FirestoreDb db, LavadoService washes)> Setup(MemoryCache cache, object? grace)
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-expiry-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var services = new ServicioService(db);
        var washes = new LavadoService(db, services, new PaqueteServicioService(db, services, cache), new PersonalService(db), new ConfiguracionService(db, cache));
        await db.Collection("turnos").Document("t").SetAsync(new Turno { Id = "t", ClienteId = "c", Estado = "Pendiente", Origen = "web", ClaveIdempotencia = "k", InicioUtc = Start, FinUtc = Start.AddMinutes(10), PrecioAcordado = 0, DesglosePrecio = new() { Version = 1, Subtotal = 0, Total = 0 }, Vehiculos = [new() { VehiculoId = "v", CantidadEmpleadosRequeridos = 1, Servicios = [new() { ServicioId = "s", TiempoEstimado = 10 }] }], RecetaOperativa = new() { Version = 1, Servicios = [new() { ServicioId = "s", Nombre = "Servicio", Tipo = "Lavado" }] } });
        await db.Collection("coordinacion").Document("turnos").SetAsync(new { Version = 7L });
        await db.Collection("configuracion").Document("system_config").SetAsync(new Dictionary<string, object?> { ["ToleranciaLlegadaTurnosMinutos"] = grace, ["CapacidadMaximaConcurrente"] = 10 });
        await db.Collection("empleados").Document("e").SetAsync(new { Estado = "Activo" });
        return (db, washes);
    }

    private static async Task<string[]> Snapshot(FirestoreDb db)
    {
        var result = new List<string>();
        foreach (var name in new[] { "turnos", "coordinacion", "lavados", "turno_cupones", "turno_cascadas", "insumos", "movimientosStock", "reservasHerramientas", "reservasHerramientasTotales", "configuracion", "empleados" })
            result.AddRange((await db.Collection(name).GetSnapshotAsync()).Documents.Select(d => $"{name}/{d.Id}:{d.UpdateTime}:{System.Text.Json.JsonSerializer.Serialize(d.ToDictionary())}"));
        return result.OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    [LocalFirestoreFact]
    public async Task LimitesYCeroYReplayIniciado()
    {
        foreach (var (grace, delta, expired) in new[] { (15, -10L, false), (15, 0L, false), (15, 10L, true), (0, 0L, false), (0, 10L, true) })
        {
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var (db, washes) = await Setup(cache, grace);
            var now = Start.AddMinutes(grace).AddTicks(delta);
            if (expired)
            {
                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => washes.ConvertirTurnoEnLavado("t", null, () => now));
                Assert.Equal("El turno no está disponible para iniciar.", error.Message);
                Assert.Equal("Ausente", (await db.Collection("turnos").Document("t").GetSnapshotAsync()).GetValue<string>("Estado"));
                Assert.Equal  (8L, (await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).GetValue<long>("Version"));
                foreach (var name in new[] { "lavados", "turno_cupones", "turno_cascadas", "movimientosStock", "reservasHerramientas", "reservasHerramientasTotales" })
                    Assert.Empty((await db.Collection(name).GetSnapshotAsync()).Documents);
            }
            else
            {
                var id = await washes.ConvertirTurnoEnLavado("t", null, () => now);
                var before = await Snapshot(db);
                Assert.Equal(id, await washes.ConvertirTurnoEnLavado("t", null, () => Start.AddDays(1)));
                Assert.Equal(before, await Snapshot(db));
            }
        }
    }

    [LocalFirestoreFact]
    public async Task ToleranciaAusenteONoValidaNoEscribe()
    {
        foreach (var grace in new object?[] { null, "15", -1, 525601 })
        {
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var (db, washes) = await Setup(cache, grace);
            var before = await Snapshot(db);
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => washes.ConvertirTurnoEnLavado("t", null, () => Start.AddDays(1)));
            Assert.Equal("Tolerancia de llegada no disponible.", error.Message);
            Assert.Equal(before, await Snapshot(db));
        }
    }

    [LocalFirestoreFact]
    public async Task AusenciaEInicioConcurrentesNoProducenDosResultados()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var (db, washes) = await Setup(cache, 15);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Absent() { await gate.Task; return await new TurnoService(db).MarcarAusencia("t", Start.AddMinutes(16)); }
        async Task<string?> StartWash() { await gate.Task; try { return await washes.ConvertirTurnoEnLavado("t", null, () => Start.AddMinutes(15)); } catch (InvalidOperationException) { return null; } }
        var a = Absent(); var b = StartWash(); gate.SetResult(); await Task.WhenAll(a, b);
        var turn = await db.Collection("turnos").Document("t").GetSnapshotAsync();
        var washesCreated = await db.Collection("lavados").GetSnapshotAsync();
        if (a.Result) { Assert.Null(b.Result); Assert.Equal("Ausente", turn.GetValue<string>("Estado")); Assert.Empty(washesCreated.Documents); }
        else { Assert.NotNull(b.Result); Assert.Equal("EnProceso", turn.GetValue<string>("Estado")); Assert.Equal(b.Result, turn.GetValue<string>("LavadoId")); Assert.Single(washesCreated.Documents); }
        Assert.Empty((await db.Collection("turno_cupones").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task ConversionVencidaConfirmaAusenciaSinCrearLavado()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-expiry-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var services = new ServicioService(db);
        var washes = new LavadoService(db, services, new PaqueteServicioService(db, services, cache), new PersonalService(db), new ConfiguracionService(db, cache));
        var start = DateTime.UtcNow.AddMinutes(-2);
        await db.Collection("turnos").Document("t").SetAsync(new Turno { Id = "t", ClienteId = "c", Estado = "Pendiente", Origen = "web", ClaveIdempotencia = "k", InicioUtc = start, FinUtc = start.AddMinutes(10), Vehiculos = [new() { VehiculoId = "v", CantidadEmpleadosRequeridos = 1, Servicios = [new() { ServicioId = "s", TiempoEstimado = 10 }] }], RecetaOperativa = new() { Version = 1, Servicios = [new() { ServicioId = "s", Nombre = "Servicio", Tipo = "Lavado" }] } });
        await db.Collection("coordinacion").Document("turnos").SetAsync(new { Version = 7L });
        await db.Collection("configuracion").Document("system_config").SetAsync(new { ToleranciaLlegadaTurnosMinutos = 0, CapacidadMaximaConcurrente = 10 });
        await db.Collection("empleados").Document("e").SetAsync(new { Estado = "Activo" });
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => washes.ConvertirTurnoEnLavado("t", null));
        Assert.Equal("El turno no está disponible para iniciar.", error.Message);
        Assert.Equal("Ausente", (await db.Collection("turnos").Document("t").GetSnapshotAsync()).GetValue<string>("Estado"));
        Assert.Equal(8L, (await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).GetValue<long>("Version"));
        foreach (var name in new[] { "lavados", "turno_cupones", "turno_cascadas", "movimientosStock" })
            Assert.Empty((await db.Collection(name).GetSnapshotAsync()).Documents);
    }
}

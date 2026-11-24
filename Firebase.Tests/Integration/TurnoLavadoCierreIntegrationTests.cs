using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Firebase.Tests.Integration;

public class TurnoLavadoCierreIntegrationTests
{
    private static async Task<(FirestoreDb db, LavadoService service, MemoryCache cache)> Setup(string[] states)
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-close-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var servicios = new ServicioService(db);
        var service = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache), new PersonalService(db), new ConfiguracionService(db, cache));
        await service.ActualizarLavado(new Lavado { Id = "wash", Estado = "EnProceso", ClienteId = "client", VehiculoId = "vehicle", FechaCreacion = DateTime.UtcNow, Servicios = states.Select((s, i) => new ServicioEnLavado { ServicioId = "s" + i, Estado = s, MotivoCancelacion = s == "Cancelado" ? "detalle" : null }).ToList() });
        await db.Collection("lavados").Document("wash").UpdateAsync("CampoAjeno", "conservar");
        return (db, service, cache);
    }

    [LocalFirestoreFact]
    public async Task CierreDiferencialSinTurnoYVinculadoPreservaDatosYCapacidad()
    {
        foreach (var states in new[] { new[] { "Realizado" }, new[] { "Realizado", "Cancelado" }, new[] { "Pendiente", "Cancelado" } })
        foreach (var linked in new[] { false, true })
        {
            var (db, service, cache) = await Setup(states);
            using (cache)
            {
                var wash = db.Collection("lavados").Document("wash");
                var turno = db.Collection("turnos").Document("turno");
                var coordinator = db.Collection("coordinacion").Document("turnos");
                await turno.SetAsync(new { Estado = "EnProceso", LavadoId = "wash", Extra = "conservar" });
                if (linked) await wash.UpdateAsync("TurnoId", "turno");
                var beforeWash = await wash.GetSnapshotAsync();
                var beforeTurno = await turno.GetSnapshotAsync();
                var beforeCoordinator = await coordinator.GetSnapshotAsync();
                var rejects = linked && states[0] == "Pendiente";
                if (rejects)
                {
                    var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.FinalizarLavado("wash"));
                    Assert.Equal("No se puede finalizar el lavado porque hay servicios pendientes o en proceso.", error.Message);
                }
                else await service.FinalizarLavado("wash");

                var afterWash = await wash.GetSnapshotAsync();
                var afterTurno = await turno.GetSnapshotAsync();
                var afterCoordinator = await coordinator.GetSnapshotAsync();
                var original = beforeWash.ToDictionary();
                var result = afterWash.ToDictionary();
                if (!rejects)
                {
                    Assert.Equal(states.Length == 1 ? "Realizado" : "RealizadoParcialmente", afterWash.GetValue<string>("Estado"));
                    Assert.NotNull(afterWash.GetValue<Timestamp?>("TiempoFinalizacion"));
                    original.Remove("Estado"); original.Remove("TiempoFinalizacion");
                    result.Remove("Estado"); result.Remove("TiempoFinalizacion");
                }
                else Assert.Equal(beforeWash.UpdateTime, afterWash.UpdateTime);
                Assert.Equal(System.Text.Json.JsonSerializer.Serialize(original), System.Text.Json.JsonSerializer.Serialize(result));
                if (linked && !rejects)
                {
                    Assert.Equal("Completado", afterTurno.GetValue<string>("Estado"));
                    Assert.Equal("wash", afterTurno.GetValue<string>("LavadoId"));
                    Assert.Equal("conservar", afterTurno.GetValue<string>("Extra"));
                    Assert.Equal(beforeCoordinator.GetValue<long>("Version") + 1, afterCoordinator.GetValue<long>("Version"));
                    await service.FinalizarLavado("wash");
                    Assert.Equal(afterWash.UpdateTime, (await wash.GetSnapshotAsync()).UpdateTime);
                    Assert.Equal(afterTurno.UpdateTime, (await turno.GetSnapshotAsync()).UpdateTime);
                    Assert.Equal(afterCoordinator.UpdateTime, (await coordinator.GetSnapshotAsync()).UpdateTime);
                }
                else
                {
                    Assert.Equal(beforeTurno.UpdateTime, afterTurno.UpdateTime);
                    Assert.Equal(beforeCoordinator.UpdateTime, afterCoordinator.UpdateTime);
                }
            }
        }
    }

    [LocalFirestoreFact]
    public async Task CierreCompletoYParcialActualizaAmbosYPreservaDetallesYReplayNoEscribe()
    {
        foreach (var states in new[] { new[] { "Realizado" }, new[] { "Realizado", "Cancelado" } })
        {
            var (db, service, cache) = await Setup(states);
            using (cache)
            {
                var wash = db.Collection("lavados").Document("wash");
                var turno = db.Collection("turnos").Document("turno");
                await wash.UpdateAsync("TurnoId", "turno");
                await turno.SetAsync(new { Estado = "EnProceso", LavadoId = "wash", Extra = "conservar" });
                var before = (await wash.GetSnapshotAsync()).ToDictionary();
                await service.FinalizarLavado("wash");
                var closed = await wash.GetSnapshotAsync();
                var appointment = await turno.GetSnapshotAsync();
                Assert.Equal(states.Length == 1 ? "Realizado" : "RealizadoParcialmente", closed.GetValue<string>("Estado"));
                Assert.True(closed.ContainsField("TiempoFinalizacion"));
                var after = closed.ToDictionary();
                before.Remove("Estado"); before.Remove("TiempoFinalizacion");
                after.Remove("Estado"); after.Remove("TiempoFinalizacion");
                Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before), System.Text.Json.JsonSerializer.Serialize(after));
                Assert.Equal("Completado", appointment.GetValue<string>("Estado"));
                Assert.Equal("wash", appointment.GetValue<string>("LavadoId"));
                Assert.Equal("conservar", appointment.GetValue<string>("Extra"));
                await service.FinalizarLavado("wash");
                Assert.Equal(closed.UpdateTime, (await wash.GetSnapshotAsync()).UpdateTime);
                Assert.Equal(appointment.UpdateTime, (await turno.GetSnapshotAsync()).UpdateTime);
            }
        }
    }

    [LocalFirestoreFact]
    public async Task VinculadoConServicioActivoYCanceladoRechazaSinCambios()
    {
        foreach (var estado in new[] { "Pendiente", "EnProceso" })
        {
            var (db, service, cache) = await Setup(new[] { "Realizado", "Cancelado", estado });
            using (cache)
            {
                var wash = db.Collection("lavados").Document("wash");
                var turno = db.Collection("turnos").Document("turno");
                await wash.UpdateAsync("TurnoId", "turno");
                await turno.SetAsync(new { Estado = "EnProceso", LavadoId = "wash", Extra = "conservar" });
                var beforeWash = await wash.GetSnapshotAsync();
                var beforeTurno = await turno.GetSnapshotAsync();
                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.FinalizarLavado("wash"));
                Assert.Equal("No se puede finalizar el lavado porque hay servicios pendientes o en proceso.", error.Message);
                var afterWash = await wash.GetSnapshotAsync();
                var afterTurno = await turno.GetSnapshotAsync();
                Assert.Equal(beforeWash.UpdateTime, afterWash.UpdateTime);
                Assert.Equal(beforeTurno.UpdateTime, afterTurno.UpdateTime);
                Assert.Equal(System.Text.Json.JsonSerializer.Serialize(beforeWash.ToDictionary()), System.Text.Json.JsonSerializer.Serialize(afterWash.ToDictionary()));
                Assert.Equal(System.Text.Json.JsonSerializer.Serialize(beforeTurno.ToDictionary()), System.Text.Json.JsonSerializer.Serialize(afterTurno.ToDictionary()));
            }
        }
    }

    [LocalFirestoreFact]
    public async Task UltimoServicioActivoCompletaTurnoParcial()
    {
        var (db, service, cache) = await Setup(new[] { "Realizado", "Cancelado", "EnProceso" });
        using (cache)
        {
            var wash = db.Collection("lavados").Document("wash");
            var turno = db.Collection("turnos").Document("turno");
            await wash.UpdateAsync("TurnoId", "turno");
            await turno.SetAsync(new { Estado = "EnProceso", LavadoId = "wash" });
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.FinalizarLavado("wash"));
            await service.FinalizarServicio("wash", "s2");
            Assert.Equal("RealizadoParcialmente", (await wash.GetSnapshotAsync()).GetValue<string>("Estado"));
            Assert.Equal("Completado", (await turno.GetSnapshotAsync()).GetValue<string>("Estado"));
        }
    }

    [LocalFirestoreFact]
    public async Task SinVinculoConPendienteYCanceladoConservaCierreParcial()
    {
        var (db, service, cache) = await Setup(new[] { "Pendiente", "Cancelado" });
        using (cache)
        {
            await service.FinalizarLavado("wash");
            var wash = await db.Collection("lavados").Document("wash").GetSnapshotAsync();
            Assert.Equal("RealizadoParcialmente", wash.GetValue<string>("Estado"));
        }
    }

    [LocalFirestoreFact]
    public async Task LavadoNoEnProcesoConTurnoActivoRechazaSinCambiarDocumentos()
    {
        foreach (var estado in new[] { "Cancelado", "Pendiente", "Realizado", "RealizadoParcialmente" })
        {
            var (db, service, cache) = await Setup(new[] { "Realizado" });
            using (cache)
            {
                var wash = db.Collection("lavados").Document("wash");
                var turno = db.Collection("turnos").Document("turno");
                await wash.UpdateAsync(new Dictionary<string, object> { ["TurnoId"] = "turno", ["Estado"] = estado });
                await turno.SetAsync(new { Estado = "EnProceso", LavadoId = "wash", Extra = "conservar" });
                var beforeWash = await wash.GetSnapshotAsync();
                var beforeTurno = await turno.GetSnapshotAsync();
                await Assert.ThrowsAsync<InvalidOperationException>(() => service.FinalizarLavado("wash"));
                var afterWash = await wash.GetSnapshotAsync();
                var afterTurno = await turno.GetSnapshotAsync();
                Assert.Equal(beforeWash.UpdateTime, afterWash.UpdateTime);
                Assert.Equal(beforeTurno.UpdateTime, afterTurno.UpdateTime);
                Assert.Equal(System.Text.Json.JsonSerializer.Serialize(beforeWash.ToDictionary()), System.Text.Json.JsonSerializer.Serialize(afterWash.ToDictionary()));
                Assert.Equal(System.Text.Json.JsonSerializer.Serialize(beforeTurno.ToDictionary()), System.Text.Json.JsonSerializer.Serialize(afterTurno.ToDictionary()));
            }
        }
    }

    [LocalFirestoreFact]
    public async Task VinculosInvalidosYTodosCanceladosRechazanSinEscrituras()
    {
        foreach (var scenario in new[] { "null", "empty", "slash", "number", "missing", "reciprocalMissing", "reciprocalMalformed", "mismatch", "stateMissing", "stateWrong", "allCancelled" })
        {
            var (db, service, cache) = await Setup(scenario == "allCancelled" ? new[] { "Cancelado" } : new[] { "Realizado" });
            using (cache)
            {
                var wash = db.Collection("lavados").Document("wash");
                var turno = db.Collection("turnos").Document("turno");
                object? link = scenario switch { "null" => null, "empty" => " ", "slash" => "bad/id", "number" => 42, _ => "turno" };
                await wash.UpdateAsync("TurnoId", link);
                if (scenario != "missing")
                {
                    var fields = new Dictionary<string, object> { ["Estado"] = scenario == "stateWrong" ? "Pendiente" : "EnProceso", ["LavadoId"] = scenario == "mismatch" ? "other" : "wash" };
                    if (scenario == "reciprocalMissing") fields.Remove("LavadoId");
                    if (scenario == "reciprocalMalformed") fields["LavadoId"] = 42;
                    if (scenario == "stateMissing") fields.Remove("Estado");
                    await turno.SetAsync(fields);
                }
                var beforeWash = await wash.GetSnapshotAsync();
                var beforeTurno = await turno.GetSnapshotAsync();
                await Assert.ThrowsAsync<InvalidOperationException>(() => service.FinalizarLavado("wash"));
                Assert.Equal(beforeWash.UpdateTime, (await wash.GetSnapshotAsync()).UpdateTime);
                Assert.Equal(beforeTurno.UpdateTime, (await turno.GetSnapshotAsync()).UpdateTime);
            }
        }
    }
}

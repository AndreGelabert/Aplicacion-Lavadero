using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Firebase.Tests.Integration;

public class TurnoLavadoCierreServicioIntegrationTests
{
    private static async Task<(FirestoreDb db, LavadoService service, InsumoService insumos, StockService stock, string item, MemoryCache cache)> Setup(bool stages, bool linked = true, bool partial = false, bool intermediate = false)
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-close-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var servicios = new ServicioService(db);
        var stock = new StockService(db);
        var service = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache), new PersonalService(db), new ConfiguracionService(db, cache), stock);
        var insumos = new InsumoService(db);
        var item = new Insumo { Nombre = "Jabon", Tipo = "Quimico", UnidadMedida = "ml" };
        Assert.True((await insumos.Guardar(item, true)).EsExitoso);
        Assert.True((await stock.Ajustar(item.Id, 5, true, "Ingreso", "actor")).EsExitoso);
        List<RecetaInsumo> Recipe() => new() { new RecetaInsumo { InsumoId = item.Id, DosisTeorica = 1 } };
        var s = new ServicioEnLavado { ServicioId = "s", Estado = "EnProceso", Orden = 1 };
        if (stages) {
            s.Etapas.Add(new EtapaEnLavado { EtapaId = "e", Estado = "EnProceso", Receta = Recipe() });
            if (intermediate) s.Etapas.Add(new EtapaEnLavado { EtapaId = "next", Estado = "Pendiente", Receta = Recipe() });
        } else s.Receta = Recipe();
        var wash = new Lavado { Id = "wash", Estado = "EnProceso", ClienteId = "c", VehiculoId = "v", FechaCreacion = DateTime.UtcNow, Servicios = new() { s } };
        if (partial) wash.Servicios.Add(new ServicioEnLavado { ServicioId = "cancelled", Estado = "Cancelado", MotivoCancelacion = "detalle", Orden = 2 });
        if (intermediate && !stages) wash.Servicios.Add(new ServicioEnLavado { ServicioId = "next", Estado = "Pendiente", Orden = 2, Receta = Recipe() });
        await service.ActualizarLavado(wash);
        if (linked) {
            await db.Collection("lavados").Document("wash").UpdateAsync("TurnoId", "turno");
            await db.Collection("turnos").Document("turno").SetAsync(new { Estado = "EnProceso", LavadoId = "wash" });
        }
        return (db, service, insumos, stock, item.Id, cache);
    }

    [LocalFirestoreFact]
    public async Task CierreEIntermedioServicioYEtapaConStockYReplay()
    {
        foreach (var stages in new[] { false, true })
        foreach (var partial in new[] { false, true })
        foreach (var intermediate in new[] { false, true })
        {
            var (db, service, insumos, stock, item, cache) = await Setup(stages, partial: partial, intermediate: intermediate);
            using (cache) {
                async Task Finish() { if (stages) await service.FinalizarEtapa("wash", "s", "e"); else await service.FinalizarServicio("wash", "s"); }
                await Finish();
                var wash = (await service.ObtenerLavado("wash"))!;
                var turno = await db.Collection("turnos").Document("turno").GetSnapshotAsync();
                Assert.Equal(intermediate ? "EnProceso" : partial ? "RealizadoParcialmente" : "Realizado", wash.Estado);
                Assert.Equal(intermediate ? "EnProceso" : "Completado", turno.GetValue<string>("Estado"));
                Assert.Equal(4, (await insumos.Obtener(item))!.StockActual);
                Assert.Single((await stock.ObtenerMovimientos(item)).Where(x => x.Tipo == nameof(TipoMovimientoStock.ConsumoEtapa)));
                var before = await db.Collection("lavados").Document("wash").GetSnapshotAsync();
                await Finish();
                Assert.Equal(before.UpdateTime, (await db.Collection("lavados").Document("wash").GetSnapshotAsync()).UpdateTime);
                Assert.Equal(turno.UpdateTime, (await db.Collection("turnos").Document("turno").GetSnapshotAsync()).UpdateTime);
                await db.Collection("turnos").Document("turno").UpdateAsync("LavadoId", "other");
                var invalidTurno = await db.Collection("turnos").Document("turno").GetSnapshotAsync();
                var error = await Assert.ThrowsAsync<InvalidOperationException>(Finish);
                Assert.Equal("El vínculo con el turno es inválido.", error.Message);
                Assert.Equal(before.UpdateTime, (await db.Collection("lavados").Document("wash").GetSnapshotAsync()).UpdateTime);
                Assert.Equal(invalidTurno.UpdateTime, (await db.Collection("turnos").Document("turno").GetSnapshotAsync()).UpdateTime);
            }
        }
    }

    [LocalFirestoreFact]
    public async Task VinculoInvalidoYCancelacionesRechazanSinCambiosNiConsumo()
    {
        foreach (var operation in new[] { "service", "stage", "cancelWash", "cancelService", "cancelStage" })
        {
            var (db, service, insumos, stock, item, cache) = await Setup(operation != "service");
            using (cache) {
                var wash = db.Collection("lavados").Document("wash");
                var turno = db.Collection("turnos").Document("turno");
                if (operation is "service" or "stage") await turno.UpdateAsync("LavadoId", "other");
                var before = await wash.GetSnapshotAsync(); var beforeTurno = await turno.GetSnapshotAsync();
                Func<Task> call = operation switch {
                    "service" => () => service.FinalizarServicio("wash", "s"),
                    "stage" => async () => { await service.FinalizarEtapa("wash", "s", "e"); },
                    "cancelWash" => () => service.CancelarLavado("wash", "motivo", new Dictionary<string, decimal> { [item] = 1 }),
                    "cancelService" => () => service.CancelarServicio("wash", "s", "motivo", new Dictionary<string, decimal> { [item] = 1 }),
                    _ => () => service.CancelarEtapa("wash", "s", "e", "motivo")
                };
                await Assert.ThrowsAsync<InvalidOperationException>(call);
                Assert.Equal(before.UpdateTime, (await wash.GetSnapshotAsync()).UpdateTime);
                Assert.Equal(beforeTurno.UpdateTime, (await turno.GetSnapshotAsync()).UpdateTime);
                Assert.Equal(5, (await insumos.Obtener(item))!.StockActual);
                Assert.Empty((await stock.ObtenerMovimientos(item)).Where(x => x.Tipo == nameof(TipoMovimientoStock.ConsumoEtapa)));
            }
        }
    }

    [LocalFirestoreFact]
    public async Task ServicioConVinculoInvalidoConservaStockYReservaReal()
    {
        var (db, service, insumos, stock, item, cache) = await Setup(false, linked: false);
        using (cache)
        {
            await new TipoInsumoService(db).AsegurarTipoHerramienta("actor", "actor@example.invalid");
            var herramienta = new Insumo { Nombre = "Hidro", Tipo = "Herramienta" };
            Assert.True((await insumos.Guardar(herramienta, true)).EsExitoso);
            Assert.True((await stock.Ajustar(herramienta.Id, 1, true, "Ingreso", "actor")).EsExitoso);
            var lavado = (await service.ObtenerLavado("wash"))!;
            lavado.Servicios[0].Estado = "Pendiente";
            lavado.Servicios[0].Receta.Add(new RecetaInsumo { InsumoId = herramienta.Id, DosisTeorica = 1 });
            await service.ActualizarLavado(lavado);
            await service.IniciarServicio("wash", "s");
            Assert.False((await stock.ReservarHerramienta("competidor-antes", herramienta.Id, 1)).EsExitoso);
            var wash = db.Collection("lavados").Document("wash");
            var turno = db.Collection("turnos").Document("turno");
            await wash.UpdateAsync("TurnoId", "turno");
            await turno.SetAsync(new { Estado = "EnProceso", LavadoId = "other" });
            var beforeWash = await wash.GetSnapshotAsync();
            var beforeTurno = await turno.GetSnapshotAsync();
            var beforeItem = await db.Collection("insumos").Document(item).GetSnapshotAsync();
            var beforeTool = await db.Collection("insumos").Document(herramienta.Id).GetSnapshotAsync();
            var beforeReservations = await db.Collection("reservasHerramientas").GetSnapshotAsync();
            var counter = db.Collection("reservasHerramientasTotales").Document(herramienta.Id);
            var beforeCounter = await counter.GetSnapshotAsync();
            var beforeMovements = await db.Collection("movimientosStock").GetSnapshotAsync();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.FinalizarServicio("wash", "s"));
            Assert.Equal("El vínculo con el turno es inválido.", error.Message);
            Assert.Equal(beforeWash.UpdateTime, (await wash.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(beforeTurno.UpdateTime, (await turno.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(beforeItem.UpdateTime, (await db.Collection("insumos").Document(item).GetSnapshotAsync()).UpdateTime);
            Assert.Equal(beforeTool.UpdateTime, (await db.Collection("insumos").Document(herramienta.Id).GetSnapshotAsync()).UpdateTime);
            Assert.Equal(beforeCounter.UpdateTime, (await counter.GetSnapshotAsync()).UpdateTime);
            var afterReservations = await db.Collection("reservasHerramientas").GetSnapshotAsync();
            Assert.Equal(beforeReservations.Documents.Select(x => (x.Id, x.UpdateTime)), afterReservations.Documents.Select(x => (x.Id, x.UpdateTime)));
            Assert.Equal(beforeMovements.Count, (await db.Collection("movimientosStock").GetSnapshotAsync()).Count);
            Assert.False((await stock.ReservarHerramienta("competidor-despues", herramienta.Id, 1)).EsExitoso);
            Assert.Equal(5, (await insumos.Obtener(item))!.StockActual);
        }
    }

    [LocalFirestoreFact]
    public async Task ServicioCanceladoConEtapasNoDebeFinalizarNiIniciarSiguiente()
    {
        var (db, service, _, _, _, cache) = await Setup(true, intermediate: false);
        using (cache) {
            var wash = db.Collection("lavados").Document("wash");
            var existing = (await service.ObtenerLavado("wash"))!;
            existing.Servicios[0].Estado = "Cancelado";
            existing.Servicios[0].MotivoCancelacion = "cancelado";
            existing.Servicios[0].Etapas[0].Estado = "Cancelado";
            existing.Servicios.Add(new ServicioEnLavado { ServicioId = "next", Estado = "Pendiente", Orden = 2 });
            // Seed the staged target without bypassing the linked overwrite guard.
            await wash.UpdateAsync("TurnoId", FieldValue.Delete);
            await service.ActualizarLavado(existing);
            await wash.UpdateAsync("TurnoId", "turno");
            var before = await wash.GetSnapshotAsync();
            var turn = await db.Collection("turnos").Document("turno").GetSnapshotAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.FinalizarServicio("wash", "s"));
            Assert.Equal(before.UpdateTime, (await wash.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(turn.UpdateTime, (await turn.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Equal("Pendiente", (await service.ObtenerLavado("wash"))!.Servicios[1].Estado);
        }
    }

    [LocalFirestoreFact]
    public async Task SinTurnoConservaFinalizacionYConsumo()
    {
        foreach (var stages in new[] { false, true }) {
            var (_, service, insumos, stock, item, cache) = await Setup(stages, linked: false);
            using (cache) {
                if (stages) await service.FinalizarEtapa("wash", "s", "e"); else await service.FinalizarServicio("wash", "s");
                Assert.Equal("Realizado", (await service.ObtenerLavado("wash"))!.Estado);
                Assert.Equal(4, (await insumos.Obtener(item))!.StockActual);
                Assert.Single((await stock.ObtenerMovimientos(item)).Where(x => x.Tipo == nameof(TipoMovimientoStock.ConsumoEtapa)));
            }
        }
    }
}

using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Firebase.Tests.Integration;

public class TurnoLavadoInicioIntegrationTests
{
    [LocalFirestoreFact]
    public async Task InicioValidaVinculoAntesDeReservarYConservaInicioSinTurno()
    {
        foreach (var stage in new[] { false, true })
        foreach (var scenario in new[] { "invalid", "cancelledWash", "cancelledTurn", "valid", "unlinked" })
        {
            var db = new FirestoreDbBuilder { ProjectId = "demo-start-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var services = new ServicioService(db);
            var stock = new StockService(db);
            var washes = new LavadoService(db, services, new PaqueteServicioService(db, services, cache), new PersonalService(db), new ConfiguracionService(db, cache), stock);
            var items = new InsumoService(db);
            await new TipoInsumoService(db).AsegurarTipoHerramienta("actor", "actor@example.invalid");
            var tool = new Insumo { Nombre = "Herramienta", Tipo = "Herramienta" };
            Assert.True((await items.Guardar(tool, true)).EsExitoso);
            Assert.True((await stock.Ajustar(tool.Id, 2, true, "Ingreso", "actor")).EsExitoso);
            Assert.True((await stock.ReservarHerramienta("otra", tool.Id, 1)).EsExitoso);
            var recipe = new List<RecetaInsumo> { new() { InsumoId = tool.Id, DosisTeorica = 1 } };
            var s = new ServicioEnLavado { ServicioId = "s", Estado = stage ? "EnProceso" : "Pendiente", Orden = 1 };
            if (stage) s.Etapas.Add(new EtapaEnLavado { EtapaId = "e", Estado = "Pendiente", Receta = recipe }); else s.Receta = recipe;
            await washes.ActualizarLavado(new Lavado { Id = "wash", Estado = "EnProceso", ClienteId = "c", VehiculoId = "v", FechaCreacion = DateTime.UtcNow, Servicios = new() { s } });
            var wash = db.Collection("lavados").Document("wash");
            var turn = db.Collection("turnos").Document("turno");
            if (scenario != "unlinked") {
                await wash.UpdateAsync("TurnoId", "turno");
                await turn.SetAsync(new { Estado = scenario == "cancelledTurn" ? "Cancelado" : "EnProceso", LavadoId = scenario == "invalid" ? "other" : "wash" });
                if (scenario == "cancelledWash") await wash.UpdateAsync("Estado", "Cancelado");
            }
            var collections = new[] { "lavados", "turnos", "insumos", "reservasHerramientas", "reservasHerramientasTotales", "movimientosStock" };
            async Task<string[]> Snapshot() {
                var result = new List<string>();
                foreach (var name in collections) {
                    var docs = await db.Collection(name).GetSnapshotAsync();
                    result.AddRange(docs.Documents.Select(d => $"{name}/{d.Id}:{d.UpdateTime}").OrderBy(x => x));
                }
                return result.ToArray();
            }
            var before = await Snapshot();
            Func<Task> start = stage ? () => washes.IniciarEtapa("wash", "s", "e") : () => washes.IniciarServicio("wash", "s");
            if (scenario is "invalid" or "cancelledWash" or "cancelledTurn") {
                var error = await Assert.ThrowsAsync<InvalidOperationException>(start);
                Assert.Equal("El vínculo con el turno es inválido.", error.Message);
                Assert.Equal(before, await Snapshot());
            } else {
                await start();
                var current = (await washes.ObtenerLavado("wash"))!;
                Assert.Equal("EnProceso", stage ? current.Servicios[0].Etapas[0].Estado : current.Servicios[0].Estado);
                Assert.False((await stock.ReservarHerramienta("competidor", tool.Id, 1)).EsExitoso);
                Assert.Equal(2, (await items.Obtener(tool.Id))!.StockActual);
            }
        }
    }
}

using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Firebase.Tests.Integration;

public class LavadoCancelacionConsumosIntegrationTests
{
    [LocalFirestoreFact]
    public async Task CancelarServicioDescuentaSoloNoRegistradoLiberaYEsIdempotente()
    {
        var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions()); var washes = Servicio(db, cache); var stock = new StockService(db);
        await Producto(db, 2m); await washes.ActualizarLavado(Lavado());
        await washes.IniciarEtapa("l", "s", "e", new Dictionary<int,string> { [0] = "i" });
        await washes.FinalizarEtapa("l", "s", "e");
        await washes.IniciarEtapa("l", "s", "e2", new Dictionary<int,string> { [0] = "i" });
        await washes.CancelarServicio("l", "s", "cancelado", new Dictionary<string,decimal> { ["i"] = 0.2m });
        await washes.CancelarServicio("l", "s", "cancelado", new Dictionary<string,decimal> { ["i"] = 0.2m });
        Assert.Equal(1.55m, (await db.Collection("insumos").Document("i").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
        Assert.Equal(0m, (await stock.ObtenerReservasInventario(new[] { "i" }))["i"]);
        var movimientos = await stock.ObtenerMovimientos("i");
        Assert.Equal(-0.25m, Assert.Single(movimientos.Where(x => x.Tipo == "ConsumoEtapa")).Cantidad);
        Assert.Equal(new[] { -0.2m, 0m }, movimientos.Where(x => x.Tipo == "MermaCancelacion").Select(x => x.Cantidad).OrderBy(x => x).ToArray());
        Assert.Equal("Cancelado", (await washes.ObtenerLavado("l"))!.Servicios[0].Estado);
    }

    [LocalFirestoreFact]
    public async Task CancelarLavadoRechazaInvalidoSinMutarYPermiteCero()
    {
        var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions()); var washes = Servicio(db, cache); var stock = new StockService(db);
        await Producto(db, 1m); await washes.ActualizarLavado(Lavado());
        await washes.IniciarEtapa("l", "s", "e", new Dictionary<int,string> { [0] = "i" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => washes.CancelarLavado("l", "cancelado", new Dictionary<string,decimal> { ["i"] = 2m }));
        Assert.Equal(1m, (await db.Collection("insumos").Document("i").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
        Assert.Equal(0.25m, (await stock.ObtenerReservasInventario(new[] { "i" }))["i"]);
        Assert.Equal("EnProceso", (await washes.ObtenerLavado("l"))!.Estado); Assert.Empty(await stock.ObtenerMovimientos("i"));
        await washes.CancelarLavado("l", "cancelado", new Dictionary<string,decimal>());
        Assert.Equal("Cancelado", (await washes.ObtenerLavado("l"))!.Estado);
        Assert.Equal(0m, (await stock.ObtenerReservasInventario(new[] { "i" }))["i"]);
    }

    [LocalFirestoreFact]
    public async Task CancelacionesConcurrentesNoDuplicanMerma()
    {
        var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions()); var washes = Servicio(db, cache); var stock = new StockService(db);
        await Producto(db, 1m); await washes.ActualizarLavado(Lavado());
        await washes.IniciarEtapa("l", "s", "e", new Dictionary<int,string> { [0] = "i" });
        var cantidades = new Dictionary<string,decimal> { ["i"] = 0.2m };
        await Task.WhenAll(washes.CancelarServicio("l", "s", "cancelado", cantidades), washes.CancelarServicio("l", "s", "cancelado", cantidades));
        Assert.Equal(0.8m, (await db.Collection("insumos").Document("i").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
        Assert.Equal(0m, (await stock.ObtenerReservasInventario(new[] { "i" }))["i"]);
        Assert.Equal(-0.2m, Assert.Single((await stock.ObtenerMovimientos("i")).Where(m => m.Tipo == "MermaCancelacion")).Cantidad);
    }

    [LocalFirestoreFact]
    public async Task RechazaProductosAjenosOPendientesSinMutar()
    {
        var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions()); var washes = Servicio(db, cache); var stock = new StockService(db);
        await Producto(db, 1m); await db.Collection("insumos").Document("otro").SetAsync(new Insumo { Id = "otro", Nombre = "otro", TipoId = "tipo", Tipo = "Jabon", UnidadMedida = "l", StockActual = 1m });
        await washes.ActualizarLavado(Lavado());
        await washes.IniciarEtapa("l", "s", "e", new Dictionary<int,string> { [0] = "i" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => washes.CancelarServicio("l", "s", "cancelado", new Dictionary<string,decimal> { ["otro"] = 0.1m }));
        Assert.Equal(1m, (await db.Collection("insumos").Document("otro").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
        Assert.Equal(0.25m, (await stock.ObtenerReservasInventario(new[] { "i" }))["i"]);
        Assert.Equal("EnProceso", (await washes.ObtenerLavado("l"))!.Servicios[0].Estado);
        Assert.Empty(await stock.ObtenerMovimientos("otro"));
    }

    [LocalFirestoreFact]
    public async Task ConsultaRegistradosAislaOtroLavadoYExpresaUnidadPropia()
    {
        var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions()); var washes = Servicio(db, cache); var stock = new StockService(db);
        var servicios = new ServicioService(db); var paquetes = new PaqueteServicioService(db, servicios, cache); var personal = new PersonalService(db); var config = new ConfiguracionService(db, cache);
        var clientes = new ClienteService(db); var vehiculos = new VehiculoService(db); var tipos = new TipoVehiculoService(db); var documentos = new TipoDocumentoService(db);
        var controller = new FirebaseLoginCustom.Controllers.LavadosController(Microsoft.Extensions.Logging.Abstractions.NullLogger<FirebaseLoginCustom.Controllers.LavadosController>.Instance, new AuditService(db), washes, servicios, paquetes, clientes, vehiculos, personal, config, tipos, documentos, new LavadoCreationService(clientes, vehiculos, servicios, paquetes, tipos, washes), new LavadoAssociatedClientService(clientes, vehiculos, documentos)) { ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() } };
        await Producto(db, 3m); var primero = Lavado(); var otro = Lavado(); otro.Id = "otro";
        await washes.ActualizarLavado(primero); await washes.ActualizarLavado(otro);
        foreach (var id in new[] { "l", "otro" }) { await washes.IniciarEtapa(id, "s", "e", new Dictionary<int,string> { [0] = "i" }); await washes.FinalizarEtapa(id, "s", "e"); }
        await washes.IniciarEtapa("l", "s", "e2", new Dictionary<int,string> { [0] = "i" });
        var result = Assert.IsType<Microsoft.AspNetCore.Mvc.JsonResult>(await controller.RecursosCancelacion("l", "s", new InsumoService(db), stock));
        Assert.Equal("{\"success\":true,\"productos\":[{\"id\":\"i\",\"nombre\":\"i\",\"unidad\":\"l\"}],\"registrados\":[{\"nombre\":\"i\",\"unidad\":\"l\",\"cantidad\":0.25}]}", System.Text.Json.JsonSerializer.SerializeToElement(result.Value).GetRawText());
    }

    [LocalFirestoreFact]
    public async Task ReintentoLavadoLegacyCanceladoConsolidaConsumoFaltante()
    {
        var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions()); var washes = Servicio(db, cache); var stock = new StockService(db);
        await Producto(db, 2m); await washes.ActualizarLavado(Lavado());
        await washes.IniciarEtapa("l", "s", "e", new Dictionary<int,string> { [0] = "i" }); await washes.FinalizarEtapa("l", "s", "e");
        var lavado = (await washes.ObtenerLavado("l"))!; lavado.Estado = "Cancelado"; lavado.Servicios[0].Estado = "Cancelado"; await washes.ActualizarLavado(lavado);
        await washes.CancelarLavado("l", "cancelado"); await washes.CancelarLavado("l", "cancelado");
        var movimientos = await stock.ObtenerMovimientos("i");
        Assert.Equal(-0.25m, Assert.Single(movimientos.Where(x => x.Tipo == "ConsumoEtapa")).Cantidad);
        Assert.Equal(0m, Assert.Single(movimientos.Where(x => x.Tipo == "MermaCancelacion")).Cantidad);
        Assert.Equal(1.75m, (await db.Collection("insumos").Document("i").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
    }

    [LocalFirestoreFact]
    public async Task CancelacionMultiservicioAislaReferenciasConPrefijoSimilar()
    {
        var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions()); var washes = Servicio(db, cache); var stock = new StockService(db);
        await Producto(db, 3m); var lavado = Lavado(); lavado.Servicios.Add(new ServicioEnLavado { ServicioId = "s2", Estado = "EnProceso", Etapas = new() { new EtapaEnLavado { EtapaId = "e", Receta = new() { new RecetaInsumo { Modalidad = "Tipo", TipoId = "tipo", UnidadMedida = "ml", DosisTeorica = 250m } } } } });
        await washes.ActualizarLavado(lavado);
        foreach (var s in new[] { "s", "s2" }) { await washes.IniciarEtapa("l", s, "e", new Dictionary<int,string> { [0] = "i" }); await washes.FinalizarEtapa("l", s, "e"); }
        var otro = Lavado(); otro.Id = "l_extra"; await washes.ActualizarLavado(otro); await washes.IniciarEtapa("l_extra", "s", "e", new Dictionary<int,string> { [0] = "i" }); await washes.FinalizarEtapa("l_extra", "s", "e");
        await washes.CancelarLavado("l", "motivo", new Dictionary<string,decimal>()); await washes.CancelarLavado("l", "motivo", new Dictionary<string,decimal>());
        var movimientos = await stock.ObtenerMovimientos("i");
        Assert.Equal(3, movimientos.Count(m => m.Tipo == "ConsumoEtapa"));
        Assert.Equal(new[] { "l_s2_e", "l_s_e" }, movimientos.Where(m => m.Tipo == "MermaCancelacion").Select(m => m.ReferenciaId).OrderBy(x => x, StringComparer.Ordinal).ToArray());
        Assert.Equal(2.25m, (await db.Collection("insumos").Document("i").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
    }

    [LocalFirestoreFact]
    public async Task ReintentoServicioLegacyConsolidaUnaVez()
    {
        var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions()); var washes = Servicio(db, cache); var stock = new StockService(db);
        await Producto(db, 2m); await washes.ActualizarLavado(Lavado()); await washes.IniciarEtapa("l", "s", "e", new Dictionary<int,string> { [0] = "i" }); await washes.FinalizarEtapa("l", "s", "e");
        var lavado = (await washes.ObtenerLavado("l"))!; lavado.Servicios[0].Estado = "Cancelado"; await washes.ActualizarLavado(lavado);
        await washes.CancelarServicio("l", "s", "motivo"); await washes.CancelarServicio("l", "s", "motivo");
        var movimientos = await stock.ObtenerMovimientos("i"); Assert.Single(movimientos.Where(m => m.Tipo == "ConsumoEtapa")); Assert.Equal(0m, Assert.Single(movimientos.Where(m => m.Tipo == "MermaCancelacion")).Cantidad);
        Assert.Equal(1.75m, (await db.Collection("insumos").Document("i").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
    }

    [LocalFirestoreFact]
    public async Task CancelacionServicioYLavadoConcurrenteNoDuplicaConsumos()
    {
        var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions()); var washes = Servicio(db, cache); var stock = new StockService(db);
        await Producto(db, 2m); await washes.ActualizarLavado(Lavado()); await washes.IniciarEtapa("l", "s", "e", new Dictionary<int,string> { [0] = "i" }); await washes.FinalizarEtapa("l", "s", "e"); await washes.IniciarEtapa("l", "s", "e2", new Dictionary<int,string> { [0] = "i" });
        var adicional = new Dictionary<string,decimal> { ["i"] = 0.2m };
        await Task.WhenAll(washes.CancelarServicio("l", "s", "motivo", adicional), washes.CancelarLavado("l", "motivo", adicional));
        var movimientos = await stock.ObtenerMovimientos("i"); Assert.Single(movimientos.Where(m => m.Tipo == "ConsumoEtapa")); Assert.Equal(new[] { -0.2m, 0m }, movimientos.Where(m => m.Tipo == "MermaCancelacion").Select(m => m.Cantidad).OrderBy(x => x).ToArray());
        Assert.Equal(1.55m, (await db.Collection("insumos").Document("i").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual); Assert.Equal(0m, (await stock.ObtenerReservasInventario(new[] { "i" }))["i"]);
    }

    private static FirestoreDb Db() => new FirestoreDbBuilder { ProjectId = "demo-cancel-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
    private static LavadoService Servicio(FirestoreDb db, MemoryCache cache) { var s = new ServicioService(db); return new LavadoService(db, s, new PaqueteServicioService(db, s, cache), new PersonalService(db), new ConfiguracionService(db, cache), new StockService(db)); }
    private static Task Producto(FirestoreDb db, decimal stock) => db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "i", TipoId = "tipo", Tipo = "Jabon", UnidadMedida = "l", StockActual = stock });
    private static Lavado Lavado() { RecetaInsumo Item() => new() { Modalidad = "Tipo", TipoId = "tipo", UnidadMedida = "ml", DosisTeorica = 250m }; return new Lavado { Id = "l", Estado = "EnProceso", ClienteId = "c", VehiculoId = "v", FechaCreacion = DateTime.UtcNow, Servicios = new() { new ServicioEnLavado { ServicioId = "s", Estado = "EnProceso", Etapas = new() { new EtapaEnLavado { EtapaId = "e", Receta = new() { Item() } }, new EtapaEnLavado { EtapaId = "e2", Receta = new() { Item() } } } } } }; }
}

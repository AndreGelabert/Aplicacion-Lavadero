using System.Security.Claims;
using Firebase.Controllers;
using Firebase.Models;
using Firebase.Security;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;

namespace Firebase.Tests.Integration;

public class InventarioReservasIntegrationTests
{
    private static FirestoreDb CrearDb() => new FirestoreDbBuilder
    {
        ProjectId = "demo-inventario-" + Guid.NewGuid().ToString("N")[..16],
        EmulatorDetection = EmulatorDetection.EmulatorOnly
    }.Build();

    [LocalFirestoreFact]
    public async Task ConsultaReservasDistingueAusenciaCantidadYContadorIlegibleSinMutar()
    {
        var db = CrearDb();
        var contadores = db.Collection("reservasHerramientasTotales");
        await contadores.Document("valido").SetAsync(new Dictionary<string, object> { ["Cantidad"] = 2.5, ["Otro"] = "conservar" });
        await contadores.Document("ilegible").SetAsync(new Dictionary<string, object> { ["Cantidad"] = "desconocida" });
        await contadores.Document("negativo").SetAsync(new Dictionary<string, object> { ["Cantidad"] = -1.0 });
        var antes = await contadores.GetSnapshotAsync();

        var resultado = await new StockService(db).ObtenerReservasInventario(new[] { "ausente", "valido", "ilegible", "negativo", "valido", "" });

        Assert.Equal(4, resultado.Count);
        Assert.Equal(0m, resultado["ausente"]);
        Assert.Equal(2.5m, resultado["valido"]);
        Assert.Null(resultado["ilegible"]);
        Assert.Null(resultado["negativo"]);
        var despues = await contadores.GetSnapshotAsync();
        Assert.Equal(antes.Count, despues.Count);
        foreach (var anterior in antes.Documents)
        {
            var actual = Assert.Single(despues.Documents, d => d.Id == anterior.Id);
            Assert.Equal(anterior.UpdateTime, actual.UpdateTime);
            Assert.Equal(anterior.ToDictionary(), actual.ToDictionary());
        }
        Assert.False((await contadores.Document("ausente").GetSnapshotAsync()).Exists);
    }

    // Caracterización preexistente; la escritura en GET queda pendiente de evaluación y no acredita pureza de consulta.
    [LocalFirestoreFact]
    public async Task IndexSinConfiguracionCreaConfiguracionPredeterminada_ComportamientoPreexistente()
    {
        var db = CrearDb();
        var config = db.Collection("configuracion").Document("system_config");
        Assert.False((await config.GetSnapshotAsync()).Exists);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var controlador = new InsumosController(new InsumoService(db), new ProveedorService(db), new StockService(db),
            new ComprasService(db), Mock.Of<IAuditWriter>(), Mock.Of<IEmailSender>(), new ConfiguracionService(db, cache), new TipoInsumoService(db))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(PermisoClaim.Tipo, "Insumos.Consultar") }, "test")) } }
        };
        Assert.IsType<ViewResult>(await controlador.Index(null, null));
        var creada = await config.GetSnapshotAsync();
        Assert.True(creada.Exists);
        Assert.Equal(20m, creada.ConvertTo<Configuracion>().PorcentajeAlertaStock);
    }

    [LocalFirestoreFact]
    public async Task IndexDirectoProyectaReservasYFiltrosYExponeMetadatosDePermisos()
    {
        var metodo = typeof(InsumosController).GetMethod(nameof(InsumosController.Index))!;
        var permiso = Assert.Single(metodo.GetCustomAttributes(typeof(PermisoAttribute), true).Cast<PermisoAttribute>());
        Assert.Equal(new[] { "Insumos.Consultar", "Insumos.Crear" }, permiso.Permisos);
        Assert.NotEmpty(typeof(InsumosController).GetCustomAttributes(typeof(AuthorizeAttribute), true));

        var db = CrearDb();
        var tipo = new TipoInsumo { Id = "volumen", Nombre = "Limpieza", Categoria = "Volumen", UnidadResumen = "l" };
        await db.Collection("tiposInsumo").Document(tipo.Id).SetAsync(tipo);
        var productos = new[] {
            new Insumo { Id = "a", Nombre = "Visible", TipoId = tipo.Id, Tipo = tipo.Nombre, UnidadMedida = "ml", StockActual = 1500, StockMinimo = 1000, Estado = "Activo" },
            new Insumo { Id = "b", Nombre = "Oculto", TipoId = tipo.Id, Tipo = tipo.Nombre, UnidadMedida = "l", StockActual = 2, StockMinimo = 1, Estado = "Activo" }
        };
        foreach (var producto in productos) await db.Collection("insumos").Document(producto.Id).SetAsync(producto);
        await db.Collection("reservasHerramientasTotales").Document("a").SetAsync(new Dictionary<string, object> { ["Cantidad"] = 500.0 });
        using var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set("system_configuration", new Configuracion { PorcentajeAlertaStock = 20 });
        var controlador = new InsumosController(new InsumoService(db), new ProveedorService(db), new StockService(db),
            new ComprasService(db), Mock.Of<IAuditWriter>(), Mock.Of<IEmailSender>(), new ConfiguracionService(db, cache), new TipoInsumoService(db))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(PermisoClaim.Tipo, "Insumos.Consultar") }, "test")) } }
        };

        var vista = Assert.IsType<ViewResult>(await controlador.Index("Visible", tipo.Id));
        var pagina = Assert.IsType<List<Insumo>>(vista.Model);
        Assert.Equal("a", Assert.Single(pagina).Id);
        Assert.False((bool)controlador.ViewBag.PuedeAdministrar);
        Assert.Equal(1, (int)controlador.ViewBag.Page);
        Assert.Equal(1, (int)controlador.ViewBag.TotalPages);
        var grupos = Assert.IsType<List<InventarioAgrupadoViewModel>>(controlador.ViewBag.Grupos);
        var grupo = Assert.Single(grupos);
        Assert.Equal("Limpieza", grupo.Nombre);
        Assert.Equal("l", grupo.UnidadResumen);
        Assert.Equal  (3.5m, grupo.TotalFisico);
        var detalle = Assert.Single(grupo.Productos);
        Assert.Equal("ml", detalle.Insumo.UnidadMedida);
        Assert.Equal(500m, detalle.Reservado);
        Assert.Equal(1000m, detalle.Disponible);
    }
}

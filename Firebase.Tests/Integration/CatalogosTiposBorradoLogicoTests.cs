using System.Security.Claims;
using Firebase.Controllers;
using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Firebase.Tests.Integration;

public class CatalogosTiposBorradoLogicoTests
{
    private static FirestoreDb Db() => new FirestoreDbBuilder
    {
        ProjectId = "demo-catalogos-tipos-" + Guid.NewGuid().ToString("N")[..16],
        EmulatorDetection = EmulatorDetection.EmulatorOnly
    }.Build();

    private static ActorAuditoria Actor => new("admin-1", "admin@example.invalid");

    [LocalFirestoreFact]
    public async Task NormalizacionSinTildes_ImpideDuplicadosEnTodosLosCatalogosInclusoContraInactivos()
    {
        var db = Db();
        var tiposServicio = new TipoServicioService(db);
        var tiposVehiculo = new TipoVehiculoService(db);
        var tiposDocumento = new TipoDocumentoService(db);
        var tiposInsumo = new TipoInsumoService(db);

        // TipoServicio
        var s1 = await tiposServicio.GuardarConAuditoria(new TipoServicio { Id = "", Nombre = "Lavado Básico" }, true, Actor);
        Assert.True(s1.Resultado.EsExitoso);
        Assert.False((await tiposServicio.GuardarConAuditoria(new TipoServicio { Id = "", Nombre = "  lavado basico " }, true, Actor)).Resultado.EsExitoso);
        Assert.True((await tiposServicio.CambiarEstadoConAuditoria(s1.TipoServicioId, "Inactivo", Actor)).Resultado.EsExitoso);
        // Sin reactivación silenciosa: sigue rechazando duplicado aunque esté Inactivo
        Assert.False((await tiposServicio.GuardarConAuditoria(new TipoServicio { Id = "", Nombre = "LAVADO BÁSICO" }, true, Actor)).Resultado.EsExitoso);

        // TipoVehiculo
        var v1 = await tiposVehiculo.GuardarConAuditoria(new TipoVehiculo { Id = "", Nombre = "Automóvil", FormatoPatente = "llnnnll", CantidadEmpleadosRequeridos = 1 }, true, Actor);
        Assert.True(v1.Resultado.EsExitoso);
        Assert.False((await tiposVehiculo.GuardarConAuditoria(new TipoVehiculo { Id = "", Nombre = "automovil", FormatoPatente = "lllnnn", CantidadEmpleadosRequeridos = 1 }, true, Actor)).Resultado.EsExitoso);

        // TipoDocumento
        var d1 = await tiposDocumento.GuardarConAuditoria(new TipoDocumento { Id = "", Nombre = "Cédula", Formato = "nnnnnnn" }, true, Actor);
        Assert.True(d1.Resultado.EsExitoso);
        Assert.False((await tiposDocumento.GuardarConAuditoria(new TipoDocumento { Id = "", Nombre = "cedula", Formato = "nnnnnnn" }, true, Actor)).Resultado.EsExitoso);

        // TipoInsumo
        var i1 = await tiposInsumo.GuardarConAuditoria(new TipoInsumo { Id = "", Nombre = "Cerás Líquidas", Categoria = "Volumen", UnidadResumen = "ml" }, true, Actor);
        Assert.True(i1.Resultado.EsExitoso);
        Assert.False((await tiposInsumo.GuardarConAuditoria(new TipoInsumo { Id = "", Nombre = "ceras liquidas", Categoria = "Volumen", UnidadResumen = "ml" }, true, Actor)).Resultado.EsExitoso);
    }

    [LocalFirestoreFact]
    public async Task Desactivacion_ProhibidaSiEnUsoActivo_PermitidaSiSoloEnInactivos()
    {
        var db = Db();
        var tiposServicio = new TipoServicioService(db);
        var tiposVehiculo = new TipoVehiculoService(db);
        var tiposDocumento = new TipoDocumentoService(db);
        var tiposInsumo = new TipoInsumoService(db);

        var ts = await tiposServicio.GuardarConAuditoria(new TipoServicio { Id = "", Nombre = "Pulido" }, true, Actor);
        var tv = await tiposVehiculo.GuardarConAuditoria(new TipoVehiculo { Id = "", Nombre = "Camioneta", FormatoPatente = "llnnnll", CantidadEmpleadosRequeridos = 2 }, true, Actor);
        var td = await tiposDocumento.GuardarConAuditoria(new TipoDocumento { Id = "", Nombre = "Pasaporte", Formato = "lllnnnnnn" }, true, Actor);
        var ti = await tiposInsumo.GuardarConAuditoria(new TipoInsumo { Id = "", Nombre = "Shampoo", Categoria = "Volumen", UnidadResumen = "ml" }, true, Actor);

        var srvRef = db.Collection("servicios").Document("srv1");
        await srvRef.SetAsync(new Dictionary<string, object> { ["Nombre"] = "S1", ["Tipo"] = "Pulido", ["TipoVehiculo"] = "Camioneta", ["Estado"] = "Activo" });

        var cliRef = db.Collection("clientes").Document("cli1");
        await cliRef.SetAsync(new Dictionary<string, object> { ["Nombre"] = "Juan", ["TipoDocumento"] = "Pasaporte", ["Estado"] = "Activo" });

        var insRef = db.Collection("insumos").Document("ins1");
        await insRef.SetAsync(new Dictionary<string, object> { ["Nombre"] = "Shampoo Neutro", ["TipoId"] = ti.TipoInsumoId, ["Tipo"] = "Shampoo", ["Estado"] = "Activo" });

        Assert.False((await tiposServicio.CambiarEstadoConAuditoria(ts.TipoServicioId, "Inactivo", Actor)).Resultado.EsExitoso);
        Assert.False((await tiposVehiculo.CambiarEstadoConAuditoria(tv.TipoVehiculoId, "Inactivo", Actor)).Resultado.EsExitoso);
        Assert.False((await tiposDocumento.CambiarEstadoConAuditoria(td.TipoDocumentoId, "Inactivo", Actor)).Resultado.EsExitoso);
        Assert.False((await tiposInsumo.CambiarEstadoConAuditoria(ti.TipoInsumoId, "Inactivo", Actor)).Resultado.EsExitoso);

        // Al pasar las entidades consumidoras a Inactivo, ahora sí permite desactivar los tipos
        await srvRef.UpdateAsync("Estado", "Inactivo");
        await cliRef.UpdateAsync("Estado", "Inactivo");
        await insRef.UpdateAsync("Estado", "Inactivo");

        Assert.True((await tiposServicio.CambiarEstadoConAuditoria(ts.TipoServicioId, "Inactivo", Actor)).Resultado.EsExitoso);
        Assert.True((await tiposVehiculo.CambiarEstadoConAuditoria(tv.TipoVehiculoId, "Inactivo", Actor)).Resultado.EsExitoso);
        Assert.True((await tiposDocumento.CambiarEstadoConAuditoria(td.TipoDocumentoId, "Inactivo", Actor)).Resultado.EsExitoso);
        Assert.True((await tiposInsumo.CambiarEstadoConAuditoria(ti.TipoInsumoId, "Inactivo", Actor)).Resultado.EsExitoso);

        // Dropdowns activos excluyen desactivados, pero lookups históricos/runtime los preservan
        Assert.Empty(await tiposServicio.ObtenerTiposServicio());
        Assert.Empty(await tiposVehiculo.ObtenerTiposVehiculos());
        Assert.Empty(await tiposDocumento.ObtenerTiposDocumento());
        Assert.NotNull(await tiposVehiculo.ObtenerTipoVehiculoPorNombre("Camioneta"));
        Assert.NotNull(await tiposDocumento.ObtenerTipoDocumentoPorNombre("Pasaporte"));
    }

    [LocalFirestoreFact]
    public async Task LegacySinEstado_SeLeeComoActivoYPermiteDesactivarYReactivar()
    {
        var db = Db();
        await db.Collection("tiposServicio").Document("legacy-ts").SetAsync(new Dictionary<string, object> { ["Nombre"] = "Encerado" });

        var service = new TipoServicioService(db);
        Assert.Equal(new[] { "Encerado" }, await service.ObtenerTiposServicio());

        Assert.True((await service.CambiarEstadoConAuditoria("legacy-ts", "Inactivo", Actor)).Resultado.EsExitoso);
        Assert.Empty(await service.ObtenerTiposServicio());

        Assert.True((await service.CambiarEstadoConAuditoria("legacy-ts", "Activo", Actor)).Resultado.EsExitoso);
        Assert.Equal(new[] { "Encerado" }, await service.ObtenerTiposServicio());
    }

    [LocalFirestoreFact]
    public async Task ConcurrenciaAltaSimultanea_SoloCreaUnDocumento()
    {
        var db = Db();
        var service = new TipoServicioService(db);

        var resultados = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(i =>
                service.GuardarConAuditoria(new TipoServicio { Id = "", Nombre = i % 2 == 0 ? "Lavado Rápido" : "lavado rapido" }, true, Actor)));

        Assert.Equal(1, resultados.Count(r => r.Resultado.EsExitoso));
        Assert.Equal(3, resultados.Count(r => !r.Resultado.EsExitoso));
        Assert.Single((await db.Collection("tiposServicio").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task ControllersModalABM_CreanEditanDesactivanYReactivan()
    {
        var db = Db();
        var tiposVehiculo = new TipoVehiculoService(db);
        var controller = new TipoVehiculoController(tiposVehiculo, new AuditService(db))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "u1"),
                        new Claim(ClaimTypes.Email, "u1@example.invalid"),
                        new Claim("permiso", "Vehiculos.Crear"),
                        new Claim("permiso", "Vehiculos.Modificar")
                    }, "test"))
                }
            }
        };

        var crearRes = Assert.IsType<JsonResult>(await controller.ModalGuardar(
            new TipoVehiculoController.TipoVehiculoModalInput { Nombre = "Utilitario", FormatoPatente = "llnnnll", CantidadEmpleadosRequeridos = 2 }, true));
        var lista = Assert.IsType<List<TipoVehiculo>>(Assert.IsType<JsonResult>(await controller.ModalDatos()).Value);
        var creado = Assert.Single(lista);

        Assert.IsType<JsonResult>(await controller.ModalEstado(creado.Id, "Inactivo"));
        Assert.Empty(await tiposVehiculo.ObtenerTiposVehiculos());
        Assert.Single(Assert.IsType<List<TipoVehiculo>>(Assert.IsType<JsonResult>(await controller.ModalDatos()).Value));

        Assert.IsType<JsonResult>(await controller.ModalEstado(creado.Id, "Activo"));
        Assert.Equal(new[] { "Utilitario" }, await tiposVehiculo.ObtenerTiposVehiculos());
    }
}

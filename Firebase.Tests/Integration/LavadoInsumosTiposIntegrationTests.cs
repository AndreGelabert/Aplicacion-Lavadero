using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Firebase.Tests.Integration;

public class LavadoInsumosTiposIntegrationTests
{
    [LocalFirestoreFact]
    public async Task InicioExigeSeleccionValidaReservaYFinalizaUnaSolaVez()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-lavado-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var servicios = new ServicioService(db);
        var stock = new StockService(db);
        var lavados = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache), new PersonalService(db), new ConfiguracionService(db, cache), stock);
        await db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "Jabon", TipoId = "tipo", Tipo = "Jabon", UnidadMedida = "l", StockActual = 1m });
        await db.Collection("insumos").Document("otro").SetAsync(new Insumo { Id = "otro", Nombre = "Otro", TipoId = "otro", Tipo = "Otro", UnidadMedida = "l", StockActual = 1m });
        await db.Collection("insumos").Document("inactivo").SetAsync(new Insumo { Id = "inactivo", Nombre = "Inactivo", TipoId = "tipo", Tipo = "Jabon", Estado = "Inactivo", UnidadMedida = "l", StockActual = 1m });
        await db.Collection("insumos").Document("insuficiente").SetAsync(new Insumo { Id = "insuficiente", Nombre = "Insuficiente", TipoId = "tipo", Tipo = "Jabon", UnidadMedida = "l", StockActual = 0.1m });
        await db.Collection("insumos").Document("incompatible").SetAsync(new Insumo { Id = "incompatible", Nombre = "Incompatible", TipoId = "tipo", Tipo = "Jabon", UnidadMedida = "kg", StockActual = 1m });
        var lavado = new Lavado { Id = "l", Estado = "EnProceso", ClienteId = "c", VehiculoId = "v", FechaCreacion = DateTime.UtcNow, Servicios = new() { new ServicioEnLavado { ServicioId = "s", Estado = "EnProceso", Etapas = new() { new EtapaEnLavado { EtapaId = "e", Receta = new() { new RecetaInsumo { Modalidad = "Tipo", TipoId = "tipo", NombreTipo = "Jabon", Categoria = "Volumen", UnidadMedida = "ml", DosisTeorica = 250m } } } } } } };
        await lavados.ActualizarLavado(lavado);
        await Assert.ThrowsAsync<InvalidOperationException>(() => lavados.IniciarEtapa("l", "s", "e"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => lavados.IniciarEtapa("l", "s", "e", new Dictionary<int, string> { [0] = "otro" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => lavados.IniciarEtapa("l", "s", "e", new Dictionary<int, string> { [0] = "inactivo" }));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => lavados.IniciarEtapa("l", "s", "e", new Dictionary<int, string> { [0] = "insuficiente" }));
        await Assert.ThrowsAsync<ArgumentException>(() => lavados.IniciarEtapa("l", "s", "e", new Dictionary<int, string> { [0] = "incompatible" }));
        Assert.Equal("Pendiente", (await lavados.ObtenerLavado("l"))!.Servicios[0].Etapas[0].Estado);
        Assert.Equal(0m, (await stock.ObtenerReservasInventario(new[] { "i" }))["i"]);
        await lavados.IniciarEtapa("l", "s", "e", new Dictionary<int, string> { [0] = "i" });
        await lavados.IniciarEtapa("l", "s", "e", new Dictionary<int, string> { [0] = "i" });
        Assert.Equal(0.25m, (await stock.ObtenerReservasInventario(new[] { "i" }))["i"]);
        Assert.Equal("i", (await lavados.ObtenerLavado("l"))!.Servicios[0].Etapas[0].Receta[0].InsumoId);
        await lavados.FinalizarEtapa("l", "s", "e");
        await lavados.FinalizarEtapa("l", "s", "e");
        Assert.Equal(0m, (await stock.ObtenerReservasInventario(new[] { "i" }))["i"]);
        Assert.Equal(0.75m, (await db.Collection("insumos").Document("i").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
        Assert.Single((await stock.ObtenerMovimientos("i")).Where(x => x.Tipo == "ConsumoEtapa"));
    }

    [LocalFirestoreFact]
    public async Task ConsumoRealSeConvierteYReintentoDiferenteNoMuta()
    {
        var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions()); var washes = Servicio(db, cache); var stock = new StockService(db);
        await Producto(db, "i", 1m); await Producto(db, "j", 1m);
        await washes.ActualizarLavado(Nuevo("l", true));
        await washes.IniciarEtapa("l", "s", "e", Elegir("i"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => washes.IniciarEtapa("l", "s", "e", Elegir("j")));
        Assert.Equal(0.25m, (await stock.ObtenerReservasInventario(new[] { "i" }))["i"]);
        Assert.Equal("i", (await washes.ObtenerLavado("l"))!.Servicios[0].Etapas[0].Receta[0].InsumoId);
        await washes.FinalizarEtapa("l", "s", "e", "{\"i\":500}", "Mayor consumo");
        Assert.Equal(0.5m, (await db.Collection("insumos").Document("i").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
        Assert.Equal(-0.5m, Assert.Single(await stock.ObtenerMovimientos("i")).Cantidad);
    }

    [LocalFirestoreFact]
    public async Task CancelacionEtapaPendienteNoSeleccionadaNoTocaStock()
    {
        var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions()); var washes = Servicio(db, cache); var stock = new StockService(db);
        await Producto(db, "i", 1m); await washes.ActualizarLavado(Nuevo("l", true));
        await washes.CancelarEtapa("l", "s", "e", "Cancelada");
        Assert.Equal("Cancelado", (await washes.ObtenerLavado("l"))!.Servicios[0].Etapas[0].Estado);
        Assert.Equal(1m, (await db.Collection("insumos").Document("i").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
        Assert.Equal(0m, (await stock.ObtenerReservasInventario(new[] { "i" }))["i"]);
        Assert.Empty(await stock.ObtenerMovimientos("i"));
    }

    [LocalFirestoreFact]
    public async Task ServicioSinEtapasReservaYConsume()
    {
        var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions()); var washes = Servicio(db, cache); var stock = new StockService(db);
        await Producto(db, "i", 1m); await washes.ActualizarLavado(Nuevo("l", false));
        await washes.IniciarServicio("l", "s", Elegir("i"));
        await washes.IniciarServicio("l", "s", Elegir("i"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => washes.IniciarServicio("l", "s", Elegir("otro")));
        Assert.Equal(0.25m, (await stock.ObtenerReservasInventario(new[] { "i" }))["i"]);
        await washes.FinalizarServicio("l", "s"); await washes.FinalizarServicio("l", "s");
        Assert.Equal(0.75m, (await db.Collection("insumos").Document("i").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
        Assert.Equal(0m, (await stock.ObtenerReservasInventario(new[] { "i" }))["i"]);
        Assert.Single(await stock.ObtenerMovimientos("i"));
    }

    [LocalFirestoreFact]
    public async Task IniciosConcurrentesNoSobreasignan()
    {
        var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions()); var washes = Servicio(db, cache); var stock = new StockService(db);
        await Producto(db, "i", 0.25m); await washes.ActualizarLavado(Nuevo("l1", true)); await washes.ActualizarLavado(Nuevo("l2", true));
        async Task<bool> Inicio(string id) { try { await washes.IniciarEtapa(id, "s", "e", Elegir("i")); return true; } catch (InvalidOperationException) { return false; } }
        var resultados = await Task.WhenAll(Inicio("l1"), Inicio("l2"));
        Assert.Single(resultados.Where(x => x)); Assert.Equal(0.25m, (await stock.ObtenerReservasInventario(new[] { "i" }))["i"]);
        Assert.Equal(0.25m, (await db.Collection("insumos").Document("i").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
        Assert.Empty(await stock.ObtenerMovimientos("i"));
    }

    [LocalFirestoreFact]
    public async Task CrearPrimeraEtapaRechazaSinSeleccionYReservaConSeleccion()
    {
        var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set("system_configuration", new Configuracion { HorariosOperacion = new(), CapacidadMaximaConcurrente = 10 });
        var washes = Servicio(db, cache); var stock = new StockService(db);
        await Producto(db, "i", 1m);
        await Assert.ThrowsAsync<InvalidOperationException>(() => washes.CrearLavadoConInicio(Nuevo("", true)));
        Assert.Empty((await db.Collection("lavados").GetSnapshotAsync()).Documents);
        var creado = await washes.CrearLavadoConInicio(Nuevo("", true), Elegir("i"));
        Assert.Equal("EnProceso", (await washes.ObtenerLavado(creado.Id))!.Servicios[0].Etapas[0].Estado);
        Assert.Equal(0.25m, (await stock.ObtenerReservasInventario(new[] { "i" }))["i"]);
        Assert.Equal(1m, (await db.Collection("insumos").Document("i").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
    }

    [LocalFirestoreFact]
    public async Task ProductoLegacySinUnidadConservaReservaYConsumo()
    {
        var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions()); var washes = Servicio(db, cache); var stock = new StockService(db);
        await Producto(db, "i", 1m); var lavado = Nuevo("l", true);
        lavado.Servicios[0].Etapas[0].Receta = new() { new RecetaInsumo { InsumoId = "i", DosisTeorica = 0.25m } };
        await washes.ActualizarLavado(lavado); await washes.IniciarEtapa("l", "s", "e");
        await washes.FinalizarEtapa("l", "s", "e");
        Assert.Equal(0.75m, (await db.Collection("insumos").Document("i").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
        Assert.Equal(0m, (await stock.ObtenerReservasInventario(new[] { "i" }))["i"]);
        Assert.Single(await stock.ObtenerMovimientos("i"));
    }

    [LocalFirestoreFact]
    public async Task ControladorCreacionTransportaSeleccionDeReceta()
    {
        var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set("system_configuration", new Configuracion { HorariosOperacion = new(), CapacidadMaximaConcurrente = 10 });
        var servicios = new ServicioService(db); var paquetes = new PaqueteServicioService(db, servicios, cache);
        var personal = new PersonalService(db); var config = new ConfiguracionService(db, cache); var washes = Servicio(db, cache);
        var clientes = new ClienteService(db); var vehiculos = new VehiculoService(db); var tipos = new TipoVehiculoService(db); var documentos = new TipoDocumentoService(db);
        var controller = new FirebaseLoginCustom.Controllers.LavadosController(Microsoft.Extensions.Logging.Abstractions.NullLogger<FirebaseLoginCustom.Controllers.LavadosController>.Instance, new AuditService(db), washes, servicios, paquetes, clientes, vehiculos, personal, config, tipos, documentos, new LavadoCreationService(clientes, vehiculos, servicios, paquetes, tipos, washes), new LavadoAssociatedClientService(clientes, vehiculos, documentos)) { ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() } };
        await db.Collection("clientes").Document("c").SetAsync(new Cliente { Id = "c", Nombre = "Ana", Apellido = "Perez", TipoDocumento = "DNI", NumeroDocumento = "123", Telefono = "1234567890", Email = "ana@example.com" });
        await db.Collection("vehiculos").Document("v").SetAsync(new Vehiculo { Id = "v", TipoVehiculo = "Auto", Patente = "ABC123", Marca = "Marca", Modelo = "Modelo", Color = "Azul" });
        await db.Collection("empleados").Document("p").SetAsync(new Dictionary<string, object> { ["Nombre"] = "Empleado", ["Email"] = "x@example.com", ["Rol"] = "Empleado", ["Estado"] = "Activo" });
        await Producto(db, "i", 1m);
        await db.Collection("servicios").Document("s").SetAsync(new Dictionary<string, object> { ["Nombre"] = "Servicio", ["Tipo"] = "Lavado", ["TipoVehiculo"] = "Auto", ["Estado"] = "Activo", ["Precio"] = 10.0, ["TiempoEstimado"] = 10, ["Descripcion"] = "Servicio", ["Receta"] = new[] { new RecetaInsumo { Modalidad = "Tipo", TipoId = "tipo", UnidadMedida = "ml", DosisTeorica = 250m } } });
        var request = new FirebaseLoginCustom.Controllers.LavadosController.CrearLavadoConSeleccionRequest { ClienteId = "c", VehiculosServicios = new() { new FirebaseLoginCustom.Controllers.VehiculoServiciosRequest { VehiculoId = "v", Servicios = new() { new FirebaseLoginCustom.Controllers.ServicioItemRequest { ServicioId = "s" } } } }, Selecciones = new() { ["v"] = Elegir("i") } };
        var result = Assert.IsType<Microsoft.AspNetCore.Mvc.JsonResult>(await controller.CrearLavadoAjax(request));
        var json = System.Text.Json.JsonSerializer.SerializeToElement(result.Value);
        Assert.True(json.GetProperty("success").GetBoolean(), json.GetRawText());
        var id = json.GetProperty("lavadosIds")[0].GetString()!;
        Assert.Equal("i", (await washes.ObtenerLavado(id))!.Servicios[0].Receta[0].InsumoId);
        Assert.Equal(0.25m, (await new StockService(db).ObtenerReservasInventario(new[] { "i" }))["i"]);
    }

    [LocalFirestoreFact]
    public async Task ControladorInicioYCancelacionTransportanJson()
    {
        var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions());
        var servicios = new ServicioService(db); var paquetes = new PaqueteServicioService(db, servicios, cache);
        var personal = new PersonalService(db); var config = new ConfiguracionService(db, cache); var washes = Servicio(db, cache);
        var clientes = new ClienteService(db); var vehiculos = new VehiculoService(db); var tipos = new TipoVehiculoService(db); var documentos = new TipoDocumentoService(db);
        var controller = new FirebaseLoginCustom.Controllers.LavadosController(Microsoft.Extensions.Logging.Abstractions.NullLogger<FirebaseLoginCustom.Controllers.LavadosController>.Instance, new AuditService(db), washes, servicios, paquetes, clientes, vehiculos, personal, config, tipos, documentos, new LavadoCreationService(clientes, vehiculos, servicios, paquetes, tipos, washes), new LavadoAssociatedClientService(clientes, vehiculos, documentos)) { ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() } };
        await Producto(db, "i", 1m); await washes.ActualizarLavado(Nuevo("l", true));
        foreach (var jsonInvalido in new[] { "[]", "{\"0\":\"i\",\"0\":\"i\"}", "{\"-1\":\"i\"}" })
        {
            var rechazo = Assert.IsType<Microsoft.AspNetCore.Mvc.JsonResult>(await controller.IniciarEtapa("l", "s", "e", jsonInvalido));
            Assert.False(System.Text.Json.JsonSerializer.SerializeToElement(rechazo.Value).GetProperty("success").GetBoolean());
            Assert.Equal("Pendiente", (await washes.ObtenerLavado("l"))!.Servicios[0].Etapas[0].Estado);
        }
        var consulta = Assert.IsType<Microsoft.AspNetCore.Mvc.JsonResult>(await controller.RecursosCancelacion("l", "s", new InsumoService(db), new StockService(db)));
        Assert.True(System.Text.Json.JsonSerializer.SerializeToElement(consulta.Value).GetProperty("success").GetBoolean());
        var inicio = Assert.IsType<Microsoft.AspNetCore.Mvc.JsonResult>(await controller.IniciarEtapa("l", "s", "e", "{\"0\":\"i\"}"));
        Assert.True(System.Text.Json.JsonSerializer.SerializeToElement(inicio.Value).GetProperty("success").GetBoolean());
        foreach (var jsonInvalido in new[] { "[]", "{\"i\":-1}", "{\"i\":0.1,\"i\":0.2}" })
        {
            var rechazo = Assert.IsType<Microsoft.AspNetCore.Mvc.JsonResult>(await controller.CancelarServicio("l", "s", "motivo", jsonInvalido));
            Assert.False(System.Text.Json.JsonSerializer.SerializeToElement(rechazo.Value).GetProperty("success").GetBoolean());
            Assert.Equal(1m, (await db.Collection("insumos").Document("i").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
            Assert.Equal("EnProceso", (await washes.ObtenerLavado("l"))!.Servicios[0].Estado);
        }
        var recursos = Assert.IsType<Microsoft.AspNetCore.Mvc.JsonResult>(await controller.RecursosCancelacion("l", "s", new InsumoService(db), new StockService(db)));
        var dto = System.Text.Json.JsonSerializer.SerializeToElement(recursos.Value);
        Assert.Equal("{\"success\":true,\"productos\":[{\"id\":\"i\",\"nombre\":\"i\",\"unidad\":\"l\"}],\"registrados\":[]}", dto.GetRawText());
        Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(await controller.RecursosCancelacion("l", "ajeno", new InsumoService(db), new StockService(db)));
        var cancelacion = Assert.IsType<Microsoft.AspNetCore.Mvc.JsonResult>(await controller.CancelarServicio("l", "s", "motivo", "{\"i\":0.2}"));
        Assert.True(System.Text.Json.JsonSerializer.SerializeToElement(cancelacion.Value).GetProperty("success").GetBoolean());
        Assert.Equal(0.8m, (await db.Collection("insumos").Document("i").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
    }

    [LocalFirestoreFact]
    public async Task RecursosInicioRespetaPermisosSnapshotPrimeraEtapaYOpciones()
    {
        var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions());
        var servicios = new ServicioService(db); var paquetes = new PaqueteServicioService(db, servicios, cache); var personal = new PersonalService(db); var config = new ConfiguracionService(db, cache); var washes = Servicio(db, cache);
        var clientes = new ClienteService(db); var vehiculos = new VehiculoService(db); var tipos = new TipoVehiculoService(db); var documentos = new TipoDocumentoService(db);
        var ctx = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        var controller = new FirebaseLoginCustom.Controllers.LavadosController(Microsoft.Extensions.Logging.Abstractions.NullLogger<FirebaseLoginCustom.Controllers.LavadosController>.Instance, new AuditService(db), washes, servicios, paquetes, clientes, vehiculos, personal, config, tipos, documentos, new LavadoCreationService(clientes, vehiculos, servicios, paquetes, tipos, washes), new LavadoAssociatedClientService(clientes, vehiculos, documentos)) { ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = ctx } };
        var insumos = new InsumoService(db); await Producto(db, "i", 1m);
        await db.Collection("insumos").Document("inactivo").SetAsync(new Insumo { Id = "inactivo", Nombre = "inactivo", TipoId = "tipo", Estado = "Inactivo", UnidadMedida = "l" });
        await db.Collection("insumos").Document("masa").SetAsync(new Insumo { Id = "masa", Nombre = "masa", TipoId = "tipo", UnidadMedida = "kg" });
        await db.Collection("insumos").Document("ajeno").SetAsync(new Insumo { Id = "ajeno", Nombre = "ajeno", TipoId = "otro", UnidadMedida = "l" });
        var receta = new RecetaInsumo { Modalidad = "Tipo", TipoId = "tipo", NombreTipo = "Jabon", UnidadMedida = "ml", DosisTeorica = 250m };
        await db.Collection("servicios").Document("s").SetAsync(new Dictionary<string, object> { ["Nombre"] = "Servicio", ["Tipo"] = "Lavado", ["TipoVehiculo"] = "Auto", ["Estado"] = "Activo", ["Precio"] = 10.0, ["TiempoEstimado"] = 10, ["Descripcion"] = "Servicio", ["Etapas"] = new[] { new Etapa { Id = "e", Nombre = "Primera", Receta = new() { receta } }, new Etapa { Id = "e2", Nombre = "Segunda", Receta = new() } } });
        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(await controller.RecursosInicio(null, "s", null, insumos));
        ctx.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[] { new System.Security.Claims.Claim("permiso", "Lavados.Crear") }, "test"));
        var alta = Assert.IsType<Microsoft.AspNetCore.Mvc.JsonResult>(await controller.RecursosInicio(null, "s", null, insumos));
        Assert.Equal("{\"success\":true,\"items\":[{\"indice\":0,\"nombre\":\"Jabon\",\"unidad\":\"ml\",\"dosis\":250,\"opciones\":[{\"id\":\"i\",\"nombre\":\"i\",\"unidad\":\"l\"}]}]}", System.Text.Json.JsonSerializer.SerializeToElement(alta.Value).GetRawText());
        var lavado = Nuevo("l", true); lavado.Servicios[0].Etapas[0].Receta[0].NombreTipo = "Snapshot"; await washes.ActualizarLavado(lavado);
        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(await controller.RecursosInicio("l", "s", "e", insumos));
        ctx.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[] { new System.Security.Claims.Claim("permiso", "Lavados.Modificar") }, "test"));
        var detalle = Assert.IsType<Microsoft.AspNetCore.Mvc.JsonResult>(await controller.RecursosInicio("l", "s", "e", insumos));
        Assert.Equal("Snapshot", System.Text.Json.JsonSerializer.SerializeToElement(detalle.Value).GetProperty("items")[0].GetProperty("nombre").GetString());
        Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(await controller.RecursosInicio("l", "s", "ajena", insumos));
    }

    [LocalFirestoreFact]
    public async Task HerramientaVinculadaPorTipoSeReservaSinConsumirse()
    {
        var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions()); var washes = Servicio(db, cache); var stock = new StockService(db);
        await db.Collection("tiposInsumo").Document("herramienta").SetAsync(new TipoInsumo { Id = "herramienta", Nombre = "Herramienta", Categoria = "Unidades", UnidadResumen = "unidad" });
        await db.Collection("insumos").Document("h").SetAsync(new Insumo { Id = "h", Nombre = "Aspiradora", TipoId = "herramienta", Tipo = "", UnidadMedida = "unidad", StockActual = 1m });
        var lavado = Nuevo("l", true); lavado.Servicios[0].Etapas[0].Receta = new() { new RecetaInsumo { InsumoId = "h", UnidadMedida = "unidad", DosisTeorica = 1m } };
        await washes.ActualizarLavado(lavado); await washes.IniciarEtapa("l", "s", "e"); await washes.FinalizarEtapa("l", "s", "e");
        Assert.Equal(1m, (await db.Collection("insumos").Document("h").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
        Assert.Empty(await stock.ObtenerMovimientos("h"));
    }

    [LocalFirestoreFact]
    public async Task AltaMultivehiculoFalloSegundoNoPersisteLavadosNiReservas()
    {
        var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions()); cache.Set("system_configuration", new Configuracion { HorariosOperacion = new(), CapacidadMaximaConcurrente = 10 });
        var servicios = new ServicioService(db); var paquetes = new PaqueteServicioService(db, servicios, cache); var personal = new PersonalService(db); var config = new ConfiguracionService(db, cache); var washes = Servicio(db, cache);
        var clientes = new ClienteService(db); var vehiculos = new VehiculoService(db); var tipos = new TipoVehiculoService(db); var documentos = new TipoDocumentoService(db);
        var controller = new FirebaseLoginCustom.Controllers.LavadosController(Microsoft.Extensions.Logging.Abstractions.NullLogger<FirebaseLoginCustom.Controllers.LavadosController>.Instance, new AuditService(db), washes, servicios, paquetes, clientes, vehiculos, personal, config, tipos, documentos, new LavadoCreationService(clientes, vehiculos, servicios, paquetes, tipos, washes), new LavadoAssociatedClientService(clientes, vehiculos, documentos)) { ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() } };
        await db.Collection("clientes").Document("c").SetAsync(new Cliente { Id = "c", Nombre = "Ana", Apellido = "Perez", TipoDocumento = "DNI", NumeroDocumento = "123", Telefono = "1234567890", Email = "ana@example.com" });
        await db.Collection("vehiculos").Document("v").SetAsync(new Vehiculo { Id = "v", TipoVehiculo = "Auto", Patente = "ABC123", Marca = "Marca", Modelo = "Modelo", Color = "Azul" });
        await db.Collection("empleados").Document("p").SetAsync(new Dictionary<string, object> { ["Nombre"] = "Empleado", ["Email"] = "x@example.com", ["Rol"] = "Empleado", ["Estado"] = "Activo" });
        await Producto(db, "i", 1m);
        await db.Collection("servicios").Document("s").SetAsync(new Dictionary<string, object> { ["Nombre"] = "Servicio", ["Tipo"] = "Lavado", ["TipoVehiculo"] = "Auto", ["Estado"] = "Activo", ["Precio"] = 10.0, ["TiempoEstimado"] = 10, ["Descripcion"] = "Servicio", ["Receta"] = new[] { new RecetaInsumo { Modalidad = "Tipo", TipoId = "tipo", UnidadMedida = "ml", DosisTeorica = 250m } } });
        FirebaseLoginCustom.Controllers.VehiculoServiciosRequest V(string id) => new() { VehiculoId = id, Servicios = new() { new FirebaseLoginCustom.Controllers.ServicioItemRequest { ServicioId = "s" } } };
        var request = new FirebaseLoginCustom.Controllers.LavadosController.CrearLavadoConSeleccionRequest { ClienteId = "c", VehiculosServicios = new() { V("v"), V("ausente") }, Selecciones = new() { ["v"] = Elegir("i") } };
        var result = Assert.IsType<Microsoft.AspNetCore.Mvc.JsonResult>(await controller.CrearLavadoAjax(request)); Assert.False(System.Text.Json.JsonSerializer.SerializeToElement(result.Value).GetProperty("success").GetBoolean());
        Assert.Empty((await db.Collection("lavados").GetSnapshotAsync()).Documents);
        Assert.Equal(0m, (await new StockService(db).ObtenerReservasInventario(new[] { "i" }))["i"]);
        Assert.Equal(1m, (await new InsumoService(db).Obtener("i"))!.StockActual);
        Assert.Empty(await new StockService(db).ObtenerMovimientos("i"));
    }

    [LocalFirestoreFact]
    public async Task AltaMultivehiculoAcumulaDemandaYRechazaSinEfectos()
    {
        var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set("system_configuration", new Configuracion { HorariosOperacion = new(), CapacidadMaximaConcurrente = 10 });
        var servicios = new ServicioService(db); var paquetes = new PaqueteServicioService(db, servicios, cache);
        var personal = new PersonalService(db); var config = new ConfiguracionService(db, cache); var washes = Servicio(db, cache);
        var clientes = new ClienteService(db); var vehiculos = new VehiculoService(db); var tipos = new TipoVehiculoService(db); var documentos = new TipoDocumentoService(db);
        var controller = new FirebaseLoginCustom.Controllers.LavadosController(Microsoft.Extensions.Logging.Abstractions.NullLogger<FirebaseLoginCustom.Controllers.LavadosController>.Instance, new AuditService(db), washes, servicios, paquetes, clientes, vehiculos, personal, config, tipos, documentos, new LavadoCreationService(clientes, vehiculos, servicios, paquetes, tipos, washes), new LavadoAssociatedClientService(clientes, vehiculos, documentos)) { ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() } };
        await db.Collection("clientes").Document("c").SetAsync(new Cliente { Id = "c", Nombre = "Ana", Apellido = "Perez", TipoDocumento = "DNI", NumeroDocumento = "123", Telefono = "1234567890", Email = "ana@example.com" });
        foreach (var id in new[] { "v1", "v2" })
            await db.Collection("vehiculos").Document(id).SetAsync(new Vehiculo { Id = id, TipoVehiculo = "Auto", Patente = id, Marca = "Marca", Modelo = "Modelo", Color = "Azul" });
        await db.Collection("empleados").Document("p").SetAsync(new Dictionary<string, object> { ["Nombre"] = "Empleado", ["Email"] = "x@example.com", ["Rol"] = "Empleado", ["Estado"] = "Activo" });
        await db.Collection("empleados").Document("p2").SetAsync(new Dictionary<string, object> { ["Nombre"] = "Segundo", ["Email"] = "y@example.com", ["Rol"] = "Empleado", ["Estado"] = "Activo" });
        await Producto(db, "i", 0.4m);
        await db.Collection("servicios").Document("s").SetAsync(new Dictionary<string, object> { ["Nombre"] = "Servicio", ["Tipo"] = "Lavado", ["TipoVehiculo"] = "Auto", ["Estado"] = "Activo", ["Precio"] = 10.0, ["TiempoEstimado"] = 10, ["Descripcion"] = "Servicio", ["Receta"] = new[] { new RecetaInsumo { Modalidad = "Tipo", TipoId = "tipo", UnidadMedida = "ml", DosisTeorica = 250m } } });
        FirebaseLoginCustom.Controllers.VehiculoServiciosRequest V(string id) => new() { VehiculoId = id, Servicios = new() { new FirebaseLoginCustom.Controllers.ServicioItemRequest { ServicioId = "s" } } };
        var request = new FirebaseLoginCustom.Controllers.LavadosController.CrearLavadoConSeleccionRequest { ClienteId = "c", VehiculosServicios = new() { V("v1"), V("v2") }, Selecciones = new() { ["v1"] = Elegir("i"), ["v2"] = Elegir("i") } };
        var rechazado = Assert.IsType<Microsoft.AspNetCore.Mvc.JsonResult>(await controller.CrearLavadoAjax(request));
        Assert.False(System.Text.Json.JsonSerializer.SerializeToElement(rechazado.Value).GetProperty("success").GetBoolean());
        Assert.Empty((await db.Collection("lavados").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
        Assert.Equal(0m, (await new StockService(db).ObtenerReservasInventario(new[] { "i" }))["i"]);
        Assert.Equal(0.4m, (await new InsumoService(db).Obtener("i"))!.StockActual);
        Assert.Empty(await new StockService(db).ObtenerMovimientos("i"));
        await Producto(db, "i", 1m);
        var aceptado = Assert.IsType<Microsoft.AspNetCore.Mvc.JsonResult>(await controller.CrearLavadoAjax(request));
        Assert.True(System.Text.Json.JsonSerializer.SerializeToElement(aceptado.Value).GetProperty("success").GetBoolean());
        Assert.Equal(2, (await db.Collection("lavados").GetSnapshotAsync()).Count);
        Assert.Equal(0.5m, (await new StockService(db).ObtenerReservasInventario(new[] { "i" }))["i"]);
        Assert.Equal(1m, (await new InsumoService(db).Obtener("i"))!.StockActual);
        Assert.Empty(await new StockService(db).ObtenerMovimientos("i"));
    }

    [LocalFirestoreFact]
    public async Task ReservaHerramientaRespetaTipoVinculadoYFallbackLegacy()
    {
        var db = Db(); var stock = new StockService(db);
        await db.Collection("tiposInsumo").Document("h").SetAsync(new TipoInsumo { Id = "h", Nombre = "Herramienta", Categoria = "Unidades", UnidadResumen = "unidad" });
        await db.Collection("tiposInsumo").Document("j").SetAsync(new TipoInsumo { Id = "j", Nombre = "Jabon", Categoria = "Unidades", UnidadResumen = "unidad" });
        await db.Collection("insumos").Document("vinculada").SetAsync(new Insumo { Id = "vinculada", TipoId = "h", Tipo = "", StockActual = 1m });
        await db.Collection("insumos").Document("legacy").SetAsync(new Insumo { Id = "legacy", Tipo = "Herramienta", StockActual = 1m });
        await db.Collection("insumos").Document("conflicto").SetAsync(new Insumo { Id = "conflicto", TipoId = "j", Tipo = "Herramienta", StockActual = 1m });
        Assert.True((await stock.ReservarHerramienta("a", "vinculada", 1m)).EsExitoso);
        Assert.False((await stock.ReservarHerramienta("b", "vinculada", 1m)).EsExitoso);
        Assert.True((await stock.LiberarHerramienta("a", "vinculada")).EsExitoso);
        Assert.True((await stock.ReservarHerramienta("c", "legacy", 1m)).EsExitoso);
        Assert.False((await stock.ReservarHerramienta("d", "conflicto", 1m)).EsExitoso);
        Assert.Equal(1m, (await db.Collection("insumos").Document("vinculada").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
        Assert.Empty(await stock.ObtenerMovimientos("vinculada"));
    }

    private static FirestoreDb Db() => new FirestoreDbBuilder { ProjectId = "demo-lavado-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
    private static LavadoService Servicio(FirestoreDb db, MemoryCache cache) { var s = new ServicioService(db); return new LavadoService(db, s, new PaqueteServicioService(db, s, cache), new PersonalService(db), new ConfiguracionService(db, cache), new StockService(db)); }
    private static Task Producto(FirestoreDb db, string id, decimal stock) => db.Collection("insumos").Document(id).SetAsync(new Insumo { Id = id, Nombre = id, TipoId = "tipo", Tipo = "Jabon", UnidadMedida = "l", StockActual = stock });
    private static Dictionary<int, string> Elegir(string id) => new() { [0] = id };
    private static Lavado Nuevo(string id, bool etapa) { var receta = new List<RecetaInsumo> { new() { Modalidad = "Tipo", TipoId = "tipo", UnidadMedida = "ml", DosisTeorica = 250m } }; return new Lavado { Id = id, Estado = "EnProceso", ClienteId = "c", VehiculoId = "v", FechaCreacion = DateTime.UtcNow, Servicios = new() { new ServicioEnLavado { ServicioId = "s", Estado = etapa ? "EnProceso" : "Pendiente", Receta = etapa ? new() : receta, Etapas = etapa ? new() { new EtapaEnLavado { EtapaId = "e", Receta = receta } } : new() } } }; }

    [LocalFirestoreFact]
    public async Task SnapshotTipoYProductoSobreviveLecturaYActualizacion()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-lavado-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var servicios = new ServicioService(db);
        var lavados = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache), new PersonalService(db), new ConfiguracionService(db, cache));
        var tipo = new RecetaInsumo { Modalidad = "Tipo", TipoId = "tipo", NombreTipo = "Jabon", Categoria = "Volumen", UnidadMedida = "ml", DosisTeorica = 250m };
        var producto = new RecetaInsumo { InsumoId = "i", NombreInsumo = "Producto", UnidadMedida = "l", DosisTeorica = 1m };
        var lavado = new Lavado { Id = "l", Estado = "EnProceso", ClienteId = "c", VehiculoId = "v", FechaCreacion = DateTime.UtcNow, Servicios = new() { new ServicioEnLavado { ServicioId = "s", Receta = new() { producto }, Etapas = new() { new EtapaEnLavado { EtapaId = "e", Receta = new() { tipo } } } } } };
        await lavados.ActualizarLavado(lavado);
        var leido = (await lavados.ObtenerLavado("l"))!;
        Assert.Equal("Producto", leido.Servicios[0].Receta[0].Modalidad);
        Assert.Equal("i", leido.Servicios[0].Receta[0].InsumoId);
        var leida = leido.Servicios[0].Etapas[0].Receta[0];
        Assert.Equal("Tipo", leida.Modalidad);
        Assert.Equal("tipo", leida.TipoId);
        Assert.Equal("Jabon", leida.NombreTipo);
        Assert.Equal("Volumen", leida.Categoria);
        Assert.Equal("ml", leida.UnidadMedida);
        Assert.Equal(250m, leida.DosisTeorica);
        leido.Notas = "actualizado";
        await lavados.ActualizarLavado(leido);
        Assert.Equal("tipo", (await lavados.ObtenerLavado("l"))!.Servicios[0].Etapas[0].Receta[0].TipoId);
    }
}

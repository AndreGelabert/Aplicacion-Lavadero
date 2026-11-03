using System.Security.Claims;
using System.Text.Json;
using Firebase.Models;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Firebase.Tests.Integration;

public class AuditCatalogueCaptureTests
{
    private static FirestoreDb Db() => new FirestoreDbBuilder { ProjectId = "demo-lava-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
    private static DefaultHttpContext Context() => new() { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "actor"), new Claim(ClaimTypes.Email, "actor@example.invalid") }, "test")) };
    private sealed class TempProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
    private static void Setup(Controller controller, bool ajax = false)
    {
        var context = Context();
        if (ajax) context.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        controller.TempData = new TempDataDictionary(context, new TempProvider());
    }
    private static bool Success(IActionResult result) => JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(result).Value).GetProperty("success").GetBoolean();
    private static async Task Events(FirestoreDb db, params (string action, string id, string type, string name, Dictionary<string,string> before, Dictionary<string,string> after)[] expected)
    {
        var docs = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents;
        Assert.Equal(expected.Length, docs.Count);
        foreach (var e in expected)
        {
            var doc = Assert.Single(docs, d => d.GetValue<string>("Action") == e.action && d.GetValue<string>("TargetId") == e.id);
            Assert.Equal(e.type, doc.GetValue<string>("TargetType"));
            Assert.Equal(e.name, doc.GetValue<string>("NombreHistorico"));
            Assert.Equal(e.before.OrderBy(x => x.Key), doc.GetValue<Dictionary<string,string>>("Antes").OrderBy(x => x.Key));
            Assert.Equal(e.after.OrderBy(x => x.Key), doc.GetValue<Dictionary<string,string>>("Despues").OrderBy(x => x.Key));
        }
    }
    private static Dictionary<string,string> Package(string name, string state, string discount, string price) => new() {
        ["Nombre"] = name, ["Estado"] = state, ["TipoVehiculo"] = "Auto", ["PorcentajeDescuento"] = discount,
        ["ServiciosIds"] = "[\"s1\",\"s2\"]", ["Precio"] = price, ["TiempoEstimado"] = "30"
    };

    [LocalFirestoreFact]
    public async Task PackageCreateUpdateStateAndRejectedUpdatePreserveAudit()
    {
        var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions());
        foreach (var (id, type, price, time) in new[] { ("s1", "Exterior", 100, 10), ("s2", "Interior", 200, 20) })
            await db.Collection("servicios").Document(id).SetAsync(new Dictionary<string,object> { ["Nombre"] = id, ["Tipo"] = type, ["TipoVehiculo"] = "Auto", ["Precio"] = price, ["TiempoEstimado"] = time, ["Descripcion"] = "", ["Estado"] = "Activo" });
        var service = new ServicioService(db); var packages = new PaqueteServicioService(db, service, cache);
        var controller = new PaqueteServicioController(packages, service, new TipoVehiculoService(db), new AuditService(db), new ConfiguracionService(db, cache)); Setup(controller);
        var p = new PaqueteServicio { Id = "input", Nombre = "Completo", Estado = "Activo", TipoVehiculo = "Auto", PorcentajeDescuento = 10, ServiciosIds = new() { "s1", "s2" } };
        Assert.IsType<PartialViewResult>(await controller.CrearPaqueteAjax(p, null)); Assert.Equal("true", controller.Response.Headers["X-Form-Valid"].ToString());
        var created = Package("Completo", "Activo", "10", (300m - 300m * (10m / 100m)).ToString());
        Assert.Equal("Completo", (await packages.ObtenerPaquete(p.Id)).Nombre);
        await Events(db, ("Creacion de paquete de servicios", p.Id, "PaqueteServicio", "Completo", new(), created));
        p.Nombre = "Especial"; p.PorcentajeDescuento = 20;
        Assert.IsType<PartialViewResult>(await controller.ActualizarPaqueteAjax(p, null)); Assert.Equal("true", controller.Response.Headers["X-Form-Valid"].ToString());
        var updated = Package("Especial", "Activo", "20", (300m - 300m * (20m / 100m)).ToString());
        Assert.Equal("Especial", (await packages.ObtenerPaquete(p.Id)).Nombre);
        await Events(db, ("Creacion de paquete de servicios", p.Id, "PaqueteServicio", "Completo", new(), created), ("Actualizacion de paquete de servicios", p.Id, "PaqueteServicio", "Especial", created, updated));
        p.PorcentajeDescuento = 0;
        Assert.IsType<PartialViewResult>(await controller.ActualizarPaqueteAjax(p, null)); Assert.Equal("false", controller.Response.Headers["X-Form-Valid"].ToString());
        Assert.Equal(20, (await packages.ObtenerPaquete(p.Id)).PorcentajeDescuento);
        controller.ModelState.Clear();
        var inactive = Package("Especial", "Inactivo", "20", (300m - 300m * (20m / 100m)).ToString());
        Assert.IsType<RedirectToActionResult>(await controller.DeactivatePaquete(p.Id));
        Assert.IsType<RedirectToActionResult>(await controller.ReactivatePaquete(p.Id));
        Assert.Equal("Activo", (await packages.ObtenerPaquete(p.Id)).Estado);
        await Events(db, ("Creacion de paquete de servicios", p.Id, "PaqueteServicio", "Completo", new(), created), ("Actualizacion de paquete de servicios", p.Id, "PaqueteServicio", "Especial", created, updated), ("Desactivacion de paquete de servicios", p.Id, "PaqueteServicio", "Especial", updated, inactive), ("Reactivacion de paquete de servicios", p.Id, "PaqueteServicio", "Especial", inactive, updated));
    }

    [LocalFirestoreFact]
    public async Task ConfigurationUpdateAndRejectedUpdateCapturePersistedValues()
    {
        var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions()); var service = new ConfiguracionService(db, cache);
        var initial = await service.ObtenerConfiguracion(); var before = AuditCampos.CapturarConfiguracion(initial);
        var controller = new ConfiguracionController(service, new AuditService(db)); Setup(controller);
        var updated = new Configuracion { NombreLavadero = "Nuevo Lavadero", Ubicacion = "Centro", PaquetesDescuentoStep = 10 };
        Assert.IsType<RedirectToActionResult>(await controller.Actualizar(updated, null));
        var persisted = await service.ObtenerConfiguracion(); Assert.Equal("Nuevo Lavadero", persisted.NombreLavadero); Assert.Equal("Centro", persisted.Ubicacion);
        var after = AuditCampos.CapturarConfiguracion(persisted);
        await Events(db, ("Actualización de configuración del sistema", "system_config", "Configuracion", "Nuevo Lavadero", before, after));
        updated.PaquetesDescuentoStep = 4; Assert.IsType<ViewResult>(await controller.Actualizar(updated, null));
        Assert.Equal(10, (await service.ObtenerConfiguracion()).PaquetesDescuentoStep);
        await Events(db, ("Actualización de configuración del sistema", "system_config", "Configuracion", "Nuevo Lavadero", before, after));
    }

    [LocalFirestoreFact]
    public async Task DocumentCreateDeleteAndRejectedInputCaptureEachId()
    {
        var db = Db(); var service = new TipoDocumentoService(db); var controller = new TipoDocumentoController(service, new AuditService(db), new ClienteService(db)); Setup(controller, true);
        Assert.False(Success(await controller.CrearTipoDocumento("DNI", "bad")));
        Assert.Empty((await db.Collection("tiposDocumento").GetSnapshotAsync()).Documents); await Events(db);
        Assert.True(Success(await controller.CrearTipoDocumento("DNI", "nnn")));
        var first = Assert.Single((await db.Collection("tiposDocumento").GetSnapshotAsync()).Documents);
        await db.Collection("tiposDocumento").Document("duplicate").SetAsync(new Dictionary<string,object> { ["Nombre"] = "DNI", ["Formato"] = "lll" });
        var one = new Dictionary<string,string> { ["Nombre"] = "DNI", ["Formato"] = "nnn" };
        var two = new Dictionary<string,string> { ["Nombre"] = "DNI", ["Formato"] = "lll" };
        Assert.False(Success(await controller.CrearTipoDocumento("DNI", "nnn")));
        await Events(db, ("Creacion de tipo de documento", first.Id, "TipoDocumento", "DNI", new(), one));
        Assert.True(Success(await controller.EliminarTipoDocumento("DNI")));
        Assert.Empty((await db.Collection("tiposDocumento").GetSnapshotAsync()).Documents);
        await Events(db, ("Creacion de tipo de documento", first.Id, "TipoDocumento", "DNI", new(), one), ("Eliminacion de tipo de documento", first.Id, "TipoDocumento", "DNI", one, new()), ("Eliminacion de tipo de documento", "duplicate", "TipoDocumento", "DNI", two, new()));
    }
}

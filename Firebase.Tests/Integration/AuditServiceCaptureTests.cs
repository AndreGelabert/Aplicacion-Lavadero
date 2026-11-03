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

public class AuditServiceCaptureTests
{
    private sealed class TempProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
    private static FirestoreDb Db() => new FirestoreDbBuilder { ProjectId = "demo-lava-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
    private static ServicioController Controller(FirestoreDb db, IMemoryCache cache, bool ajax)
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "actor"), new Claim(ClaimTypes.Email, "actor@example.invalid") }, "test")) };
        if (ajax) context.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        var service = new ServicioService(db);
        var controller = new ServicioController(service, new AuditService(db), new TipoServicioService(db), new TipoVehiculoService(db), new PaqueteServicioService(db, service, cache), new VehiculoService(db)) { ControllerContext = new ControllerContext { HttpContext = context } };
        controller.TempData = new TempDataDictionary(context, new TempProvider());
        return controller;
    }
    private static bool Success(IActionResult result) => JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(result).Value).GetProperty("success").GetBoolean();
    private static async Task Events(FirestoreDb db, params (string action, string id, string type, string name, Dictionary<string,string> before, Dictionary<string,string> after)[] expected)
    {
        var docs = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents;
        Assert.Equal(expected.Length, docs.Count);
        foreach (var e in expected)
        {
            var doc = Assert.Single(docs, d => d.GetValue<string>("Action") == e.action && d.GetValue<string>("TargetId") == e.id);
            Assert.Equal(e.type, doc.GetValue<string>("TargetType")); Assert.Equal(e.name, doc.GetValue<string>("NombreHistorico"));
            Assert.Equal(e.before.OrderBy(x => x.Key), doc.GetValue<Dictionary<string,string>>("Antes").OrderBy(x => x.Key));
            Assert.Equal(e.after.OrderBy(x => x.Key), doc.GetValue<Dictionary<string,string>>("Despues").OrderBy(x => x.Key));
        }
    }
    [LocalFirestoreFact]
    public async Task ServiceCreateUpdateStatesAndRejectedTypeChange()
    {
        var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions()); var controller = Controller(db, cache, true); var service = new ServicioService(db);
        var input = new Servicio { Id = "input", Nombre = "Lavado", Tipo = "Exterior", TipoVehiculo = "Auto", Descripcion = "Detalle", Estado = "Activo", Precio = 10, TiempoEstimado = 5, Etapas = new() { new Etapa { Nombre = " Enjuague " } } };
        Assert.IsType<PartialViewResult>(await controller.CrearServicioAjax(input, null)); Assert.Equal("true", controller.Response.Headers["X-Form-Valid"].ToString());
        var created = await service.ObtenerServicio(input.Id); var before = AuditCampos.CapturarServicio(created);
        Assert.Equal("Enjuague", Assert.Single(created.Etapas).Nombre); Assert.False(string.IsNullOrWhiteSpace(created.Etapas[0].Id));
        await Events(db, ("Creacion de servicio", input.Id, "Servicio", "Lavado", new(), before));
        input.Tipo = "Interior"; Assert.IsType<PartialViewResult>(await controller.ActualizarServicioAjax(input, null)); Assert.Equal("false", controller.Response.Headers["X-Form-Valid"].ToString());
        Assert.Equal(before.OrderBy(x => x.Key), AuditCampos.CapturarServicio(await service.ObtenerServicio(input.Id)).OrderBy(x => x.Key));
        await Events(db, ("Creacion de servicio", input.Id, "Servicio", "Lavado", new(), before));
        controller.ModelState.Clear(); input.Tipo = "Exterior"; input.Nombre = "Lavado nuevo"; input.Etapas = new() { new Etapa { Id = created.Etapas[0].Id, Nombre = "Secado" }, new Etapa { Nombre = "Pulido" } };
        Assert.IsType<PartialViewResult>(await controller.ActualizarServicioAjax(input, null)); Assert.Equal("true", controller.Response.Headers["X-Form-Valid"].ToString());
        var updated = AuditCampos.CapturarServicio(await service.ObtenerServicio(input.Id)); Assert.Contains("Secado", updated["Etapas"]); Assert.Contains("Pulido", updated["Etapas"]);
        Assert.True(Success(await controller.DeactivateServicio(input.Id))); var inactive = AuditCampos.CapturarServicio(await service.ObtenerServicio(input.Id)); Assert.Equal("Inactivo", inactive["Estado"]);
        Assert.True(Success(await controller.ReactivateServicio(input.Id))); Assert.Equal("Activo", (await service.ObtenerServicio(input.Id)).Estado);
        await Events(db, ("Creacion de servicio", input.Id, "Servicio", "Lavado", new(), before), ("Actualizacion de servicio", input.Id, "Servicio", "Lavado", before, updated), ("Desactivacion de servicio", input.Id, "Servicio", "Lavado nuevo", updated, inactive), ("Reactivacion de servicio", input.Id, "Servicio", "Lavado nuevo", inactive, updated));
    }
    [LocalFirestoreFact]
    public async Task VehicleTypeDeleteRejectsSnapshotDriftWithoutChangingDocumentsOrAudit()
    {
        var db = Db(); var service = new TipoVehiculoService(db);
        var firstId = await service.CrearTipoVehiculo("Auto", "lllnnn", 2);
        var captured = (await service.ObtenerParaAuditoriaPorNombre("Auto")).Select(t => t.Id).ToHashSet();
        Assert.Equal(firstId, Assert.Single(captured));
        var secondId = await service.CrearTipoVehiculo("Auto", "nnn", 3);
        var before = (await db.Collection("tiposVehiculos").GetSnapshotAsync()).Documents.ToDictionary(d => d.Id, d => d.ToDictionary());
        await Events(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EliminarTipoVehiculo("Auto", captured));

        var after = (await db.Collection("tiposVehiculos").GetSnapshotAsync()).Documents.ToDictionary(d => d.Id, d => d.ToDictionary());
        Assert.Equal(new[] { firstId, secondId }.OrderBy(id => id), after.Keys.OrderBy(id => id));
        foreach (var id in before.Keys)
            Assert.Equal(before[id].OrderBy(p => p.Key), after[id].OrderBy(p => p.Key));
        await Events(db);
    }

    [LocalFirestoreFact]
    public async Task TypesAjaxAndFallbackCaptureDuplicatesAndRejectInvalid()
    {
        var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions()); var ajax = Controller(db, cache, true); var fallback = Controller(db, cache, false);
        Assert.False(Success(await ajax.CrearTipoVehiculo("Auto", "bad", 2))); await Events(db);
        Assert.True(Success(await ajax.CrearTipoVehiculo("Auto", "lllnnn", 2)));
        var first = Assert.Single((await db.Collection("tiposVehiculos").GetSnapshotAsync()).Documents);
        await db.Collection("tiposVehiculos").Document("duplicate").SetAsync(new Dictionary<string,object> { ["Nombre"] = "Auto", ["FormatoPatente"] = "nnn", ["CantidadEmpleadosRequeridos"] = 3 });
        var one = new Dictionary<string,string> { ["Nombre"] = "Auto", ["FormatoPatente"] = "lllnnn", ["CantidadEmpleadosRequeridos"] = "2" };
        var two = new Dictionary<string,string> { ["Nombre"] = "Auto", ["FormatoPatente"] = "nnn", ["CantidadEmpleadosRequeridos"] = "3" };
        Assert.False(Success(await ajax.CrearTipoVehiculo("Auto", "nnn", 1)));
        await Events(db, ("Creacion de tipo de vehiculo", first.Id, "TipoVehiculo", "Auto", new(), one));
        Assert.IsType<RedirectToActionResult>(await fallback.EliminarTipoVehiculo("Auto")); Assert.Empty((await db.Collection("tiposVehiculos").GetSnapshotAsync()).Documents);
        Assert.True(Success(await ajax.CrearTipoServicio("Exterior"))); var type = Assert.Single((await db.Collection("tiposServicio").GetSnapshotAsync()).Documents);
        Assert.True(Success(await ajax.EliminarTipoServicio("Exterior"))); Assert.Empty((await db.Collection("tiposServicio").GetSnapshotAsync()).Documents);
        var name = new Dictionary<string,string> { ["Nombre"] = "Exterior" };
        await Events(db, ("Creacion de tipo de vehiculo", first.Id, "TipoVehiculo", "Auto", new(), one), ("Eliminacion de tipo de vehiculo", first.Id, "TipoVehiculo", "Auto", one, new()), ("Eliminacion de tipo de vehiculo", "duplicate", "TipoVehiculo", "Auto", two, new()), ("Creacion de tipo de servicio", type.Id, "TipoServicio", "Exterior", new(), name), ("Eliminacion de tipo de servicio", type.Id, "TipoServicio", "Exterior", name, new()));
    }
}

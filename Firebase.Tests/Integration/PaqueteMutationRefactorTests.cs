using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Firebase.Tests.Integration;

public class PaqueteMutationRefactorTests
{
    private sealed class AuditCounter : IAuditWriter
    {
        public int Count { get; private set; }
        public Task LogEvent(string userId, string userEmail, string action, string targetId, string targetType) { Count++; return Task.CompletedTask; }
        public Task LogEvent(string userId, string userEmail, string action, string targetId, string targetType,
            Dictionary<string, string> antes, Dictionary<string, string> despues, string nombreHistorico = null!) { Count++; return Task.CompletedTask; }
        public Task LogSessionEvent(string userId, string userEmail, string action, string targetId, string targetType) { Count++; return Task.CompletedTask; }
    }

    private static PaqueteServicio Package(string name, string id = "input") => new()
    {
        Id = id, Nombre = name, Estado = "Activo", TipoVehiculo = "Auto", PorcentajeDescuento = 10,
        ServiciosIds = new() { "s1", "s2" }
    };

    [LocalFirestoreFact]
    public async Task RejectedFormsPreserveStoredPackagesAndDoNotAudit()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-pmutation-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        foreach (var (id, type) in new[] { ("s1", "Exterior"), ("s2", "Interior") })
            await db.Collection("servicios").Document(id).SetAsync(new Dictionary<string, object>
            {
                ["Nombre"] = id, ["Tipo"] = type, ["TipoVehiculo"] = "Auto", ["Precio"] = 100,
                ["TiempoEstimado"] = 10, ["Descripcion"] = "", ["Estado"] = "Activo"
            });
        var services = new ServicioService(db);
        var packages = new PaqueteServicioService(db, services, cache);
        var first = Package("Alfa"); var second = Package("Beta");
        await packages.CrearPaquete(first); await packages.CrearPaquete(second);
        await packages.CambiarEstadoPaquete(first.Id, "Inactivo");
        var audit = new AuditCounter();
        PaqueteServicioController Controller() => new(packages, services, new TipoVehiculoService(db), audit, new ConfiguracionService(db, cache), new PaqueteServicioFormService(packages, new ConfiguracionService(db, cache)))
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        void Failure(PaqueteServicioController controller, IActionResult result, PaqueteServicio input, string key, string message)
        {
            var view = Assert.IsType<PartialViewResult>(result);
            Assert.Equal("_PaqueteServicioForm", view.ViewName); Assert.Same(input, view.Model);
            Assert.Equal("false", controller.Response.Headers["X-Form-Valid"].ToString());
            Assert.Equal("Editando un Paquete de Servicios", controller.Response.Headers["X-Form-Title"].ToString());
            Assert.False(controller.Response.Headers.ContainsKey("X-Form-Message"));
            Assert.Equal(message, Assert.Single(controller.ModelState[key]!.Errors).ErrorMessage);
        }
        var invalid = Package("Invalido1"); invalid.PorcentajeDescuento = 0; invalid.ServiciosIds = new() { "s1" };
        var controller = Controller();
        Failure(controller, await controller.CrearPaqueteAjax(invalid, null), invalid, "Nombre", "El nombre solo puede contener letras y espacios.");
        Assert.Equal("El porcentaje de descuento debe estar entre 5 y 95.", Assert.Single(controller.ModelState["PorcentajeDescuento"]!.Errors).ErrorMessage);
        Assert.Equal("Debe seleccionar al menos 2 servicios.", Assert.Single(controller.ModelState["ServiciosIds"]!.Errors).ErrorMessage);
        var duplicate = Package("alfa"); controller = Controller();
        Failure(controller, await controller.CrearPaqueteAjax(duplicate, null), duplicate, "Nombre", "Ya existe un paquete con este nombre.");
        var update = Package("Beta", first.Id); controller = Controller();
        Failure(controller, await controller.ActualizarPaqueteAjax(update, null), update, "Nombre", "Ya existe un paquete con este nombre.");
        update = Package("Alfa", first.Id); update.TipoVehiculo = "Moto"; controller = Controller();
        Failure(controller, await controller.ActualizarPaqueteAjax(update, null), update, "TipoVehiculo", "No se puede cambiar el tipo de vehículo de un paquete existente.");
        var saved = await packages.ObtenerPaquete(first.Id);
        Assert.Equal("Alfa", saved.Nombre); Assert.Equal("Inactivo", saved.Estado); Assert.Equal("Auto", saved.TipoVehiculo);
        Assert.Equal(10m, saved.PorcentajeDescuento); Assert.Equal(new[] { "s1", "s2" }, saved.ServiciosIds);
        saved = await packages.ObtenerPaquete(second.Id);
        Assert.Equal("Beta", saved.Nombre); Assert.Equal("Activo", saved.Estado); Assert.Equal("Auto", saved.TipoVehiculo);
        Assert.Equal(10m, saved.PorcentajeDescuento); Assert.Equal(new[] { "s1", "s2" }, saved.ServiciosIds);
        Assert.Equal(2, (await packages.ObtenerPaquetes(new() { "Activo", "Inactivo" })).Count);
        Assert.Equal(0, audit.Count);
    }
}

using System.Security.Claims;
using System.Text.Json;
using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Firebase.Tests.Integration;

public class ServicioMutationRefactorTests
{
    private sealed class TempProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class AuditCounter : IAuditWriter
    {
        public int Count { get; private set; }
        public Task LogEvent(string userId, string userEmail, string action, string targetId, string targetType) { Count++; return Task.CompletedTask; }
        public Task LogEvent(string userId, string userEmail, string action, string targetId, string targetType,
            Dictionary<string, string> antes, Dictionary<string, string> despues, string nombreHistorico = null!) { Count++; return Task.CompletedTask; }
        public Task LogSessionEvent(string userId, string userEmail, string action, string targetId, string targetType) { Count++; return Task.CompletedTask; }
    }

    private static FirestoreDb Db() => new FirestoreDbBuilder
    {
        ProjectId = "demo-mutation-" + Guid.NewGuid().ToString("N")[..16],
        EmulatorDetection = EmulatorDetection.EmulatorOnly
    }.Build();

    private static ServicioController Controller(FirestoreDb db, IMemoryCache cache, AuditCounter audit, bool ajax = true)
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "actor"), new Claim(ClaimTypes.Email, "actor@example.invalid")
        }, "test")) };
        if (ajax) context.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        var services = new ServicioService(db);
        var controller = new ServicioController(services, audit, new TipoServicioService(db), new TipoVehiculoService(db),
            new VehiculoService(db), new ServicioQueryService(services),
            new ServicioLifecycleService(services, new PaqueteServicioService(db, services, cache)), new ServicioFormService(services),
            new ServicioTypeService(services, new TipoServicioService(db), new TipoVehiculoService(db), new VehiculoService(db)))
        { ControllerContext = new ControllerContext { HttpContext = context } };
        controller.TempData = new TempDataDictionary(context, new TempProvider());
        return controller;
    }

    private static Servicio Service(string name = "Lavado") => new()
    {
        Id = "input", Nombre = name, Tipo = "Exterior", TipoVehiculo = "Auto", Descripcion = "Detalle",
        Estado = "Activo", Precio = 10, TiempoEstimado = 5
    };

    private static void JsonFailure(IActionResult result, string message)
    {
        var json = JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(result).Value);
        Assert.Equal(JsonValueKind.Object, json.ValueKind);
        Assert.Equal(2, json.EnumerateObject().Count());
        Assert.False(json.GetProperty("success").GetBoolean());
        Assert.Equal(message, json.GetProperty("message").GetString());
    }

    private static void FormFailure(ServicioController controller, IActionResult result, Servicio input, string key, string message)
    {
        var partial = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("_ServicioForm", partial.ViewName);
        Assert.Same(input, partial.Model);
        Assert.Equal("false", controller.Response.Headers["X-Form-Valid"].ToString());
        Assert.False(controller.Response.Headers.ContainsKey("X-Form-Message"));
        Assert.Equal(message, Assert.Single(controller.ModelState[key]!.Errors).ErrorMessage);
        Assert.Empty(controller.TempData);
    }

    [LocalFirestoreFact]
    public async Task DeactivationWithActivePackagesLeavesServiceAuditAndTempDataUntouched()
    {
        var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions());
        var audit = new AuditCounter(); var controller = Controller(db, cache, audit);
        var services = new ServicioService(db); var input = Service(); await services.CrearServicio(input);
        foreach (var (id, name, state) in new[] { ("a", "Alfa", "Activo"), ("b", "Beta", "Activo"), ("c", "Ignorado", "Inactivo") })
            await db.Collection("paquetes_servicios").Document(id).SetAsync(new Dictionary<string, object>
            {
                ["Nombre"] = name, ["Estado"] = state, ["TipoVehiculo"] = "Auto",
                ["PorcentajeDescuento"] = 10.0, ["ServiciosIds"] = new[] { input.Id }
            });
        var before = AuditCampos.CapturarServicio(await services.ObtenerServicio(input.Id));

        JsonFailure(await controller.DeactivateServicio(input.Id),
            "No se puede desactivar el servicio porque está siendo usado en los siguientes paquetes activos: Alfa, Beta");

        Assert.Equal(before.OrderBy(x => x.Key), AuditCampos.CapturarServicio(await services.ObtenerServicio(input.Id)).OrderBy(x => x.Key));
        Assert.Equal(0, audit.Count);
        Assert.Empty(controller.TempData);
    }

    [LocalFirestoreFact]
    public async Task CreateAndUpdateRejectValidationDuplicatesAndTypeChangesWithoutWrites()
    {
        var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions());
        var audit = new AuditCounter(); var services = new ServicioService(db);
        var existing = Service(); await services.CrearServicio(existing);
        var other = Service("Pulido"); await services.CrearServicio(other);
        var beforeExisting = AuditCampos.CapturarServicio(await services.ObtenerServicio(existing.Id));
        var beforeOther = AuditCampos.CapturarServicio(await services.ObtenerServicio(other.Id));

        var invalid = Service("Lavado1");
        var controller = Controller(db, cache, audit);
        FormFailure(controller, await controller.CrearServicioAjax(invalid, null), invalid, "Nombre", "El nombre solo puede contener letras y espacios.");

        var duplicate = Service(); controller = Controller(db, cache, audit);
        FormFailure(controller, await controller.CrearServicioAjax(duplicate, null), duplicate, "Nombre", "Ya existe un servicio con este nombre para vehículos tipo 'Auto'.");

        var update = Service("Pulido"); update.Id = existing.Id; controller = Controller(db, cache, audit);
        FormFailure(controller, await controller.ActualizarServicioAjax(update, null), update, "Nombre", "Ya existe un servicio con este nombre para vehículos tipo 'Auto'.");

        update = Service(); update.Id = existing.Id; update.TipoVehiculo = "Moto"; controller = Controller(db, cache, audit);
        FormFailure(controller, await controller.ActualizarServicioAjax(update, null), update, "TipoVehiculo", "No se puede cambiar el tipo de vehículo de un servicio existente.");

        update = Service(); update.Id = existing.Id; update.Tipo = "Interior"; controller = Controller(db, cache, audit);
        FormFailure(controller, await controller.ActualizarServicioAjax(update, null), update, "Tipo", "No se puede cambiar el tipo de servicio de un servicio existente.");

        update = Service(); update.Id = existing.Id; update.Precio = -1; update.TiempoEstimado = 0; controller = Controller(db, cache, audit);
        FormFailure(controller, await controller.ActualizarServicioAjax(update, null), update, "Precio", "El precio debe ser igual o mayor a 0.");
        Assert.Equal("El tiempo estimado debe ser mayor a 0.", Assert.Single(controller.ModelState["TiempoEstimado"]!.Errors).ErrorMessage);
        Assert.Equal(beforeExisting.OrderBy(x => x.Key), AuditCampos.CapturarServicio(await services.ObtenerServicio(existing.Id)).OrderBy(x => x.Key));
        Assert.Equal(beforeOther.OrderBy(x => x.Key), AuditCampos.CapturarServicio(await services.ObtenerServicio(other.Id)).OrderBy(x => x.Key));
        Assert.Equal(2, (await services.ObtenerServicios(new List<string> { "Activo" }, null!, null!, null, null, 1, 10, null!, null!)).Count);
        Assert.Equal(0, audit.Count);
    }

    [LocalFirestoreFact]
    public async Task TypeRejectionsPreserveAjaxFallbackDifferencesAndStoredTypes()
    {
        var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions()); var audit = new AuditCounter();
        var types = new TipoServicioService(db); var vehicles = new TipoVehiculoService(db);
        await types.CrearTipoServicio("Exterior"); await vehicles.CrearTipoVehiculo("Auto", "lllnnn", 2);
        var services = new ServicioService(db); var input = Service(); await services.CrearServicio(input);
        var ajax = Controller(db, cache, audit);

        JsonFailure(await ajax.CrearTipoServicio("Exterior"), "Ya existe un tipo de servicio con el mismo nombre.");
        JsonFailure(await ajax.CrearTipoVehiculo("Moto", "bad", 1), "El formato solo puede contener 'n' (números), 'l' (letras), '.' '-' y '|'. Mínimo 3 caracteres.");
        JsonFailure(await ajax.CrearTipoVehiculo("Moto", "nnn", 11), "La cantidad de empleados debe estar entre 1 y 10.");
        JsonFailure(await ajax.EliminarTipoServicio("Exterior"), "No se puede eliminar el tipo de servicio porque hay servicios que lo utilizan.");
        JsonFailure(await ajax.EliminarTipoVehiculo("Auto"), "No se puede eliminar el tipo 'Auto' porque hay servicios que lo utilizan.");
        Assert.Empty(ajax.TempData);

        var fallback = Controller(db, cache, audit, false);
        Assert.Equal("Index", Assert.IsType<RedirectToActionResult>(await fallback.CrearTipoServicio("Exterior")).ActionName);
        Assert.Equal("Ya existe un TipoServicio con el mismo nombre.", fallback.TempData["Error"]);
        fallback.TempData.Clear();
        Assert.Equal("Index", Assert.IsType<RedirectToActionResult>(await fallback.EliminarTipoVehiculo("Auto")).ActionName);
        Assert.Equal("No se puede eliminar TipoVehiculo porque hay servicios que lo utilizan.", fallback.TempData["Error"]);
        Assert.Equal(new[] { "Exterior" }, await types.ObtenerTiposServicio());
        var vehicle = Assert.Single(await vehicles.ObtenerTiposVehiculosCompletos());
        Assert.Equal("Auto", vehicle.Nombre); Assert.Equal("lllnnn", vehicle.FormatoPatente); Assert.Equal(2, vehicle.CantidadEmpleadosRequeridos);
        Assert.Equal(0, audit.Count);
    }
}

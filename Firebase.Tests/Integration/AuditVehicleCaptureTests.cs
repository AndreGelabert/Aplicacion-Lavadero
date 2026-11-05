using System.Security.Claims;
using System.Text.Json;
using Firebase.Models;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Firebase.Tests.Integration;

public class AuditVehicleCaptureTests
{
    private static Dictionary<string, string> Snapshot(string model, string color, string state) => new() {
        ["Patente"] = "ABC123", ["TipoVehiculo"] = "Auto", ["Marca"] = "Marca", ["Modelo"] = model,
        ["Color"] = color, ["Estado"] = state, ["ClienteId"] = "", ["ClientesIds"] = "[]"
    };

    [LocalFirestoreFact]
    public async Task UpdateDeactivateDeleteCapturePersistedValuesAndRejectedUpdateChangesNothing()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-lava-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var service = new VehiculoService(db);
        var vehicle = new Vehiculo { Id = "vehicle", Patente = "ABC123", TipoVehiculo = "Auto", Marca = "Marca", Modelo = "Original", Color = "Azul", ClientesIds = new(), ClaveAsociacionHash = "secret-hash" };
        await service.ActualizarVehiculo(vehicle);
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "actor"), new Claim(ClaimTypes.Email, "actor@example.invalid") }, "test")) };
        var controller = new VehiculoController(service, new TipoVehiculoService(db), new ClienteService(db), new AuditService(db), null!, NullLogger<VehiculoController>.Instance) { ControllerContext = new ControllerContext { HttpContext = context } };
        vehicle.Modelo = "Rechazado";
        controller.ModelState.AddModelError("Modelo", "invalid");
        Assert.IsType<PartialViewResult>(await controller.ActualizarVehiculoAjax(vehicle));
        Assert.Equal("false", controller.Response.Headers["X-Form-Valid"].ToString());
        Assert.Equal("Original", (await service.ObtenerVehiculo(vehicle.Id))!.Modelo);
        await Events(db, vehicle.Id);
        controller.ModelState.Clear();
        vehicle.Modelo = "Nuevo"; vehicle.Color = "Rojo";
        Assert.IsType<PartialViewResult>(await controller.ActualizarVehiculoAjax(vehicle));
        Assert.Equal("true", controller.Response.Headers["X-Form-Valid"].ToString());
        var original = Snapshot("Original", "Azul", "Activo"); var updated = Snapshot("Nuevo", "Rojo", "Activo"); var inactive = Snapshot("Nuevo", "Rojo", "Inactivo");
        Assert.Equal("Nuevo", (await service.ObtenerVehiculo(vehicle.Id))!.Modelo);
        await Events(db, vehicle.Id, ("Actualizacion de vehiculo", original, updated));
        Assert.True(Success(await controller.DeactivateVehiculo(vehicle.Id)));
        Assert.Equal("Inactivo", (await service.ObtenerVehiculo(vehicle.Id))!.Estado);
        await Events(db, vehicle.Id, ("Actualizacion de vehiculo", original, updated), ("Desactivacion de vehiculo", updated, inactive));
        Assert.True(Success(await controller.EliminarVehiculo(vehicle.Id)));
        Assert.Null(await service.ObtenerVehiculo(vehicle.Id));
        await Events(db, vehicle.Id, ("Actualizacion de vehiculo", original, updated), ("Desactivacion de vehiculo", updated, inactive), ("Eliminacion fisica de vehiculo", inactive, new()));
    }

    private static bool Success(IActionResult result) => JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(result).Value).GetProperty("success").GetBoolean();
    private static async Task Events(FirestoreDb db, string id, params (string action, Dictionary<string, string> before, Dictionary<string, string> after)[] expected)
    {
        var docs = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents;
        Assert.Equal(expected.Length, docs.Count);
        foreach (var e in expected)
        {
            var doc = Assert.Single(docs, d => d.GetValue<string>("Action") == e.action);
            Assert.Equal(id, doc.GetValue<string>("TargetId")); Assert.Equal("Vehiculo", doc.GetValue<string>("TargetType")); Assert.Equal("ABC123", doc.GetValue<string>("NombreHistorico"));
            var before = doc.GetValue<Dictionary<string, string>>("Antes"); var after = doc.GetValue<Dictionary<string, string>>("Despues");
            Assert.Equal(e.before.OrderBy(x => x.Key), before.OrderBy(x => x.Key)); Assert.Equal(e.after.OrderBy(x => x.Key), after.OrderBy(x => x.Key));
            Assert.DoesNotContain("secret-hash", JsonSerializer.Serialize(before)); Assert.DoesNotContain("secret-hash", JsonSerializer.Serialize(after));
        }
    }
}

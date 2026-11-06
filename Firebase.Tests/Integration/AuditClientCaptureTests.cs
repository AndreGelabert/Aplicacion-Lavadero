using System.Security.Claims;
using System.Text.Json;
using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Firebase.Tests.Integration;

public class AuditClientCaptureTests
{
    [LocalFirestoreFact]
    public async Task CreationUpdateStatesAndDeleteCaptureClientAndVehicle()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-lava-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var clients = new ClienteService(db); var vehicles = new VehiculoService(db);
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "actor"), new Claim(ClaimTypes.Email, "actor@example.invalid"), new Claim("permiso", "Clientes.Crear"), new Claim("permiso", "Clientes.Modificar"), new Claim("permiso", "Vehiculos.Crear") }, "test");
        var controller = new ClienteController(clients, new TipoDocumentoService(db), vehicles, new AuditService(db), NullLogger<ClienteController>.Instance, new ClienteLifecycleService(clients, vehicles, new AuditService(db))) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } } };
        var client = new Cliente { Id = "", Nombre = "Juan", Apellido = "Perez", TipoDocumento = "DNI", NumeroDocumento = "123", Telefono = "1234567890", Email = "juan@example.invalid" };
        const string input = "[{\"EsNuevo\":true,\"Patente\":\"abc123\",\"Marca\":\"Marca\",\"Modelo\":\"Modelo\",\"Color\":\"Azul\",\"TipoVehiculo\":\"Auto\"}]";
        Assert.IsType<PartialViewResult>(await controller.CrearClienteAjax(client, input));
        Assert.Equal("true", controller.Response.Headers["X-Form-Valid"].ToString());
        var vehicle = Assert.Single(await vehicles.ObtenerVehiculosPorCliente(client.Id));
        var createdClient = AuditCampos.CapturarCliente((await clients.ObtenerCliente(client.Id))!);
        var createdVehicle = AuditCampos.CapturarVehiculo(vehicle);
        await Event(db, "Creacion de cliente", client.Id, "Cliente", new(), createdClient);
        await Event(db, "Creacion de vehiculo", vehicle.Id, "Vehiculo", new(), createdVehicle);
        controller.ModelState.Clear(); client.Nombre = "Pedro";
        var existing = JsonSerializer.Serialize(new[] { new { Id = vehicle.Id, Color = "Rojo", TipoVehiculo = "Auto" } });
        Assert.IsType<PartialViewResult>(await controller.ActualizarClienteAjax(client, existing));
        Assert.Equal("true", controller.Response.Headers["X-Form-Valid"].ToString());
        var updated = AuditCampos.CapturarCliente((await clients.ObtenerCliente(client.Id))!);
        await Event(db, "Actualizacion de cliente", client.Id, "Cliente", createdClient, updated);
        Assert.True(Success(await controller.DeactivateCliente(client.Id)));
        var inactive = AuditCampos.CapturarCliente((await clients.ObtenerCliente(client.Id))!);
        var inactiveVehicle = AuditCampos.CapturarVehiculo((await vehicles.ObtenerVehiculo(vehicle.Id))!);
        await Event(db, "Desactivacion de cliente", client.Id, "Cliente", updated, inactive);
        var updatedVehicle = new Dictionary<string, string>(createdVehicle) { ["Color"] = "Rojo" };
        await Event(db, "Desactivacion de vehiculo (por desactivacion de cliente)", vehicle.Id, "Vehiculo", updatedVehicle, inactiveVehicle);
        Assert.True(Success(await controller.ReactivateCliente(client.Id)));
        await Event(db, "Reactivacion de cliente", client.Id, "Cliente", inactive, updated);
        await Event(db, "Reactivacion de vehiculo (por reactivacion de cliente)", vehicle.Id, "Vehiculo", inactiveVehicle, updatedVehicle);
        Assert.False(Success(await controller.EliminarCliente(client.Id)));
        Assert.Equal(7, (await db.Collection("registros_auditoria").GetSnapshotAsync()).Count);
    }
    private static bool Success(IActionResult result) => JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(result).Value).GetProperty("success").GetBoolean();
    private static async Task Event(FirestoreDb db, string action, string id, string type, Dictionary<string,string> before, Dictionary<string,string> after)
    {
        var docs = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents;
        var doc = Assert.Single(docs, d => d.GetValue<string>("Action") == action && d.GetValue<string>("TargetId") == id);
        Assert.Equal(type, doc.GetValue<string>("TargetType"));
        Assert.Equal(before.OrderBy(x => x.Key), doc.GetValue<Dictionary<string,string>>("Antes").OrderBy(x => x.Key));
        Assert.Equal(after.OrderBy(x => x.Key), doc.GetValue<Dictionary<string,string>>("Despues").OrderBy(x => x.Key));
        Assert.DoesNotContain("ClaveAsociacion", JsonSerializer.Serialize(doc.ToDictionary()));
    }
}

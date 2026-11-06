using System.Text.Json;
using Firebase.Models;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Firebase.Tests.Integration;

public class VehiculoMutationRefactorTests
{
    [LocalFirestoreFact]
    public async Task UpdateRestoresImmutableFieldsAndDeactivationRejectsBothOwnershipForms()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-vehmutation-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var service = new VehiculoService(db);
        var audit = new AuditService(db);
        VehiculoController Controller() => new(service, new TipoVehiculoService(db), new ClienteService(db), audit, null!, NullLogger<VehiculoController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        Vehiculo Input(string id) => new() { Id = id, Patente = "FAKE999", TipoVehiculo = "Camion", Marca = "Otra", Modelo = "Nuevo", Color = "Negro", ClienteId = "intruder", ClientesIds = new() { "intruder" }, ClaveAsociacionHash = "forged", Estado = "Inactivo" };
        Assert.IsType<NotFoundResult>(await Controller().ActualizarVehiculoAjax(Input("missing")));
        var missing = Assert.IsType<JsonResult>(await Controller().DeactivateVehiculo("missing"));
        Assert.Equal("{\"success\":false,\"message\":\"Vehículo no encontrado.\"}", JsonSerializer.Serialize(missing.Value, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        foreach (var primary in new[] { true, false })
        {
            var id = primary ? "primary" : "secondary";
            await service.ActualizarVehiculo(new Vehiculo { Id = id, Patente = "ABC123", TipoVehiculo = "Auto", Marca = "Ford", Modelo = "Original", Color = "Azul", ClienteId = primary ? "owner" : "", ClientesIds = primary ? new() : new() { "owner" }, ClaveAsociacionHash = "secret", Estado = "Activo" });
            var rejected = Controller();
            rejected.ModelState.AddModelError("Modelo", "invalid");
            var rejectedInput = Input(id);
            Assert.Same(rejectedInput, Assert.IsType<PartialViewResult>(await rejected.ActualizarVehiculoAjax(rejectedInput)).Model);
            Assert.Equal("false", rejected.Response.Headers["X-Form-Valid"].ToString());
            Assert.Equal("Original", (await service.ObtenerVehiculo(id)).Modelo);
            Assert.Equal(primary ? 0 : 1, (await audit.ObtenerRegistros()).Count);
            var response = Assert.IsType<JsonResult>(await Controller().DeactivateVehiculo(id));
            var json = JsonSerializer.SerializeToElement(response.Value);
            Assert.False(json.GetProperty("success").GetBoolean());
            Assert.Equal("No se puede desactivar el vehículo porque está asignado al cliente: . Para desactivarlo, primero remuévalo del cliente desde la edición del cliente.", json.GetProperty("message").GetString());
            Assert.Equal(primary ? 0 : 1, (await audit.ObtenerRegistros()).Count);
            var controller = Controller();
            var input = Input(id);
            Assert.Null(Assert.IsType<PartialViewResult>(await controller.ActualizarVehiculoAjax(input)).Model);
            Assert.Equal("true", controller.Response.Headers["X-Form-Valid"].ToString());
            Assert.Equal("Vehiculo actualizado correctamente.", controller.Response.Headers["X-Form-Message"].ToString());
            var stored = await service.ObtenerVehiculo(id);
            foreach (var actual in new[] { input, stored })
            {
                Assert.Equal("ABC123", actual.Patente); Assert.Equal("Auto", actual.TipoVehiculo); Assert.Equal("Ford", actual.Marca);
                Assert.Equal(primary ? "owner" : "", actual.ClienteId); Assert.Equal(primary ? Array.Empty<string>() : new[] { "owner" }, actual.ClientesIds);
                Assert.Equal("secret", actual.ClaveAsociacionHash); Assert.Equal("Activo", actual.Estado);
                Assert.Equal("Nuevo", actual.Modelo); Assert.Equal("Negro", actual.Color);
            }
            var entries = await audit.ObtenerRegistros();
            Assert.Equal(primary ? 1 : 2, entries.Count);
            Assert.All(entries, entry => Assert.Equal("Actualizacion de vehiculo", entry.Action));
        }
    }
}

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
    public async Task AssociationValidationPreservesNullMissingAndBlankKeyOrphanResponses()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-vehkey-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var service = new VehiculoService(db);
        var controller = new VehiculoController(service, new TipoVehiculoService(db), new ClienteService(db), new AuditService(db), null!, NullLogger<VehiculoController>.Instance);
        async Task<JsonElement> Check(VehiculoController.ValidarClaveRequest request) => JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(await controller.ValidarClaveAsociacion(request)).Value);
        var blank = await Check(null!);
        Assert.False(blank.GetProperty("valida").GetBoolean());
        Assert.Equal("La patente y la clave de asociación son obligatorias.", blank.GetProperty("error").GetString());
        var missing = await Check(new() { Patente = "UNKNOWN" });
        Assert.False(missing.GetProperty("valida").GetBoolean());
        Assert.Equal("La clave de asociación no es válida para este vehículo.", missing.GetProperty("error").GetString());
        await service.ActualizarVehiculo(new Vehiculo { Id = "orphan", Patente = "ABC123", Marca = "Ford", Modelo = "Fiesta", Color = "Azul", TipoVehiculo = "Auto", Estado = "Inactivo" });
        var result = await Check(new() { Patente = "abc123", ClaveAsociacion = null! });
        Assert.True(result.GetProperty("valida").GetBoolean());
        Assert.True(result.GetProperty("esHuerfano").GetBoolean());
        var expected = JsonSerializer.SerializeToElement(new { id = "orphan", patente = "ABC123", marca = "Ford", modelo = "Fiesta", color = "Azul", tipoVehiculo = "Auto", clienteNombreCompleto = (string?)null });
        Assert.Equal(expected.GetRawText(), result.GetProperty("vehiculo").GetRawText());
        Assert.Equal("Inactivo", (await service.ObtenerVehiculo("orphan")).Estado);
        Assert.Empty(await new AuditService(db).ObtenerRegistros());
    }

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

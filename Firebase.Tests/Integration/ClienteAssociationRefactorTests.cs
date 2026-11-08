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

public class ClienteAssociationRefactorTests
{
    private static FirestoreDb Database() => new FirestoreDbBuilder { ProjectId = "demo-lava-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
    private static Cliente Client(string document, string state = "Activo") => new() { Id = "", TipoDocumento = "DNI", NumeroDocumento = document, Nombre = "Ana", Apellido = "Perez", Telefono = "1234567890", Email = "test@example.invalid", Estado = state };
    private static Vehiculo Vehicle(string plate, string owner, params string[] associated) => new() { Id = "", Patente = plate, Marca = "Toyota", Modelo = "Corolla", Color = "Blanco", TipoVehiculo = "Auto", Estado = "Activo", ClienteId = owner, ClientesIds = associated.ToList(), ClaveAsociacionHash = "key" };
    private static DefaultHttpContext Context(params string[] permissions) => new() { User = new ClaimsPrincipal(new ClaimsIdentity(permissions.Select(p => new Claim("permiso", p)).Append(new Claim(ClaimTypes.NameIdentifier, "test")), "test")) };
    private static ClienteController Controller(FirestoreDb db, DefaultHttpContext context)
    {
        var clients = new ClienteService(db); var vehicles = new VehiculoService(db); var audit = new AuditService(db);
        return new ClienteController(clients, new TipoDocumentoService(db), vehicles, audit, NullLogger<ClienteController>.Instance, new ClienteLifecycleService(clients, vehicles, audit), new ClienteVehicleService(clients, vehicles, audit)) { ControllerContext = new ControllerContext { HttpContext = context } };
    }

    [Fact]
    public async Task VehicleQueryRequiresBothPermissionsBeforeAccessingServices()
    {
        foreach (var permissions in new[] { Array.Empty<string>(), new[] { "Clientes.Consultar" }, new[] { "Vehiculos.Consultar" } })
        {
            var controller = new ClienteController(null!, null!, null!, null!, NullLogger<ClienteController>.Instance, null!, null!) { ControllerContext = new ControllerContext { HttpContext = Context(permissions) } };
            Assert.IsType<ForbidResult>(await controller.GetVehiculosCliente("any"));
        }
    }

    [LocalFirestoreFact]
    public async Task VehicleQueryReportsCurrentListAndPrincipalOwnerRules()
    {
        var db = Database(); var clients = new ClienteService(db); var vehicles = new VehiculoService(db);
        var target = Client("11111111"); var active = Client("22222222"); var active2 = Client("33333333"); var inactive = Client("44444444", "Inactivo");
        foreach (var client in new[] { target, active, active2, inactive }) await clients.CrearCliente(client);
        var principal = Vehicle("AB101CD", active.Id, target.Id, inactive.Id);
        var list = Vehicle("AB102CD", active.Id, target.Id, active2.Id, inactive.Id);
        var titular = Vehicle("AB103CD", target.Id, target.Id, inactive.Id, "missing");
        var exclusive = Vehicle("AB104CD", target.Id);
        foreach (var vehicle in new[] { principal, list, titular, exclusive }) await vehicles.CrearVehiculo(vehicle);
        var controller = Controller(db, Context("Clientes.Consultar", "Vehiculos.Consultar"));
        var payload = JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(await controller.GetVehiculosCliente(target.Id)).Value);
        Assert.True(payload.GetProperty("success").GetBoolean());
        var rows = payload.GetProperty("vehiculos").EnumerateArray().ToDictionary(x => x.GetProperty("id").GetString()!);
        Assert.Equal(4, rows.Count);
        void Check(Vehiculo vehicle, bool shared, bool owner, bool others, int count)
        {
            var row = rows[vehicle.Id];
            Assert.Equal(vehicle.Patente, row.GetProperty("patente").GetString()); Assert.Equal(vehicle.Marca, row.GetProperty("marca").GetString());
            Assert.Equal(vehicle.Modelo, row.GetProperty("modelo").GetString()); Assert.Equal(vehicle.Color, row.GetProperty("color").GetString());
            Assert.Equal(vehicle.TipoVehiculo, row.GetProperty("tipoVehiculo").GetString()); Assert.Equal(vehicle.Estado, row.GetProperty("estado").GetString());
            Assert.Equal(shared, row.GetProperty("esCompartido").GetBoolean()); Assert.Equal(owner, row.GetProperty("esTitular").GetBoolean());
            Assert.Equal(others, row.GetProperty("tieneOtrosAsociados").GetBoolean()); Assert.Equal(count, row.GetProperty("cantidadOtrosClientesActivos").GetInt32());
            Assert.Equal(11, row.EnumerateObject().Count());
        }
        Check(principal, true, false, true, 1); // Active principal is fallback when list has no active other.
        Check(list, true, false, true, 1); // Active list member suppresses the distinct active principal.
        Check(titular, false, true, true, 0);
        Check(exclusive, false, true, false, 0);
        var missing = JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(await controller.GetVehiculosCliente("missing")).Value);
        Assert.False(missing.GetProperty("success").GetBoolean()); Assert.Equal("Cliente no encontrado", missing.GetProperty("message").GetString());
    }

    [LocalFirestoreFact]
    public async Task UpdateRejectsRemovingSharedPrimaryWithoutChangingPublicState()
    {
        var db = Database(); var clients = new ClienteService(db); var vehicles = new VehiculoService(db); var audit = new AuditService(db);
        var owner = Client("11111111"); var other = Client("22222222");
        await clients.CrearCliente(owner); await clients.CrearCliente(other);
        var shared = Vehicle("AB123CD", owner.Id, owner.Id, other.Id); var kept = Vehicle("AB124CD", owner.Id, owner.Id);
        await vehicles.CrearVehiculo(shared); await vehicles.CrearVehiculo(kept);
        owner.VehiculosIds = new() { shared.Id, kept.Id }; other.VehiculosIds = new() { shared.Id };
        await clients.ActualizarCliente(owner); await clients.ActualizarCliente(other);
        var beforeClients = await clients.ObtenerTotalClientes("");
        var beforeVehicles = await vehicles.ObtenerTotalVehiculos("", new(), new(), new());
        var beforeAudit = (await audit.ObtenerRegistros()).Count;
        var edited = Client(owner.NumeroDocumento); edited.Id = owner.Id; edited.Nombre = "Cambiado";
        var context = Context("Clientes.Modificar", "Vehiculos.Crear"); var controller = Controller(db, context);
        await controller.ActualizarClienteAjax(edited, JsonSerializer.Serialize(new[] { new { id = kept.Id, patente = kept.Patente, color = kept.Color, tipoVehiculo = kept.TipoVehiculo } }));
        Assert.Equal("false", context.Response.Headers["X-Form-Valid"].ToString());
        Assert.Equal("No se pudo guardar. Verifique la patente y la clave; para quitar un vehículo compartido del titular, primero los demás deben desvincularse.", Assert.Single(controller.ModelState.Values.SelectMany(v => v.Errors)).ErrorMessage);
        var actualOwner = (await clients.ObtenerCliente(owner.Id))!; var actualOther = (await clients.ObtenerCliente(other.Id))!;
        Assert.Equal(owner.Nombre, actualOwner.Nombre); Assert.Equal(owner.VehiculosIds, actualOwner.VehiculosIds); Assert.Equal(other.VehiculosIds, actualOther.VehiculosIds);
        foreach (var vehicle in new[] { shared, kept })
        {
            var actual = (await vehicles.ObtenerVehiculo(vehicle.Id))!;
            Assert.Equal(vehicle.ClienteId, actual.ClienteId); Assert.Equal(vehicle.ClientesIds, actual.ClientesIds);
            Assert.Equal(vehicle.Estado, actual.Estado); Assert.Equal(vehicle.ClaveAsociacionHash, actual.ClaveAsociacionHash);
        }
        Assert.Equal(beforeClients, await clients.ObtenerTotalClientes("")); Assert.Equal(beforeVehicles, await vehicles.ObtenerTotalVehiculos("", new(), new(), new()));
        Assert.Equal(beforeAudit, (await audit.ObtenerRegistros()).Count);
    }
}

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

public class ClienteLifecycleRefactorTests
{
    [LocalFirestoreFact]
    public async Task LifecyclePreservesSharedAndInactiveOwnerBranchesAndAuditOrder()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-lava-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var clients = new ClienteService(db);
        var vehicles = new VehiculoService(db);
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "actor"), new Claim(ClaimTypes.Email, "actor@example.invalid") }, "test");
        var controller = new ClienteController(clients, new TipoDocumentoService(db), vehicles, new AuditService(db), NullLogger<ClienteController>.Instance, new ClienteLifecycleService(clients, vehicles, new AuditService(db)), new ClienteVehicleService(clients, vehicles, new AuditService(db)))
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } } };
        var target = Client("Activo"); var active = Client("Activo"); var inactive = Client("Inactivo");
        await clients.CrearCliente(target); await clients.CrearCliente(active); await clients.CrearCliente(inactive);
        var exclusive = Vehicle("EXCLUSIVE", target.Id, new[] { target.Id }, "Activo");
        var sharedList = Vehicle("SHAREDLIST", target.Id, new[] { target.Id, active.Id }, "Activo");
        var sharedPrincipal = Vehicle("SHAREDMAIN", active.Id, new[] { target.Id }, "Activo");
        var inactiveOwner = Vehicle("INACTIVEOWNER", target.Id, new[] { target.Id, inactive.Id }, "Activo");
        var alreadyInactive = Vehicle("ALREADYINACTIVE", target.Id, new[] { target.Id, active.Id }, "Inactivo");
        var all = new[] { exclusive, sharedList, sharedPrincipal, inactiveOwner, alreadyInactive };
        foreach (var vehicle in all) await vehicles.CrearVehiculo(vehicle);
        var beforeClient = AuditCampos.CapturarCliente((await clients.ObtenerCliente(target.Id))!);
        var beforeVehicles = new Dictionary<string, Dictionary<string, string>>();
        foreach (var vehicle in all) beforeVehicles[vehicle.Id] = AuditCampos.CapturarVehiculo((await vehicles.ObtenerVehiculo(vehicle.Id))!);

        AssertResponse(await controller.DeactivateCliente(target.Id), true, "Cliente desactivado correctamente. Se desactivaron 2 vehículo(s) exclusivo(s). 2 vehículo(s) compartido(s) permanecen activos (usados por otros clientes).");
        Assert.Equal("Inactivo", (await clients.ObtenerCliente(target.Id))!.Estado);
        Assert.Equal("Activo", (await clients.ObtenerCliente(active.Id))!.Estado);
        Assert.Equal("Inactivo", (await clients.ObtenerCliente(inactive.Id))!.Estado);
        foreach (var vehicle in all)
        {
            var stored = (await vehicles.ObtenerVehiculo(vehicle.Id))!;
            Assert.Equal(vehicle == sharedList || vehicle == sharedPrincipal ? "Activo" : "Inactivo", stored.Estado);
            Assert.Equal(vehicle.ClienteId, stored.ClienteId);
            Assert.Equal(vehicle.ClientesIds, stored.ClientesIds);
            Assert.Equal(vehicle.ClaveAsociacionHash, stored.ClaveAsociacionHash);
        }
        var inactiveClient = AuditCampos.CapturarCliente((await clients.ObtenerCliente(target.Id))!);
        var inactiveVehicles = new Dictionary<string, Dictionary<string, string>>();
        foreach (var vehicle in all) inactiveVehicles[vehicle.Id] = AuditCampos.CapturarVehiculo((await vehicles.ObtenerVehiculo(vehicle.Id))!);
        await AssertEvents(db, new[] {
            ("Desactivacion de vehiculo (por desactivacion de cliente)", exclusive.Id, "Vehiculo", beforeVehicles[exclusive.Id], inactiveVehicles[exclusive.Id]),
            ("Desactivacion de vehiculo (por desactivacion de cliente)", inactiveOwner.Id, "Vehiculo", beforeVehicles[inactiveOwner.Id], inactiveVehicles[inactiveOwner.Id]),
            ("Desactivacion de cliente", target.Id, "Cliente", beforeClient, inactiveClient)
        });

        AssertResponse(await controller.ReactivateCliente(target.Id), true, "Cliente reactivado correctamente. Se reactivaron 3 vehículo(s). 2 vehículo(s) ya estaban activos (compartidos con otros clientes).");
        Assert.Equal("Activo", (await clients.ObtenerCliente(target.Id))!.Estado);
        Assert.Equal("Activo", (await clients.ObtenerCliente(active.Id))!.Estado);
        Assert.Equal("Inactivo", (await clients.ObtenerCliente(inactive.Id))!.Estado);
        var reactivated = new Dictionary<string, Dictionary<string, string>>();
        foreach (var vehicle in all)
        {
            var stored = (await vehicles.ObtenerVehiculo(vehicle.Id))!;
            Assert.Equal("Activo", stored.Estado);
            Assert.Equal(vehicle.ClienteId, stored.ClienteId);
            Assert.Equal(vehicle.ClientesIds, stored.ClientesIds);
            Assert.Equal(vehicle.ClaveAsociacionHash, stored.ClaveAsociacionHash);
            reactivated[vehicle.Id] = AuditCampos.CapturarVehiculo(stored);
        }
        await AssertEvents(db, new[] {
            ("Desactivacion de vehiculo (por desactivacion de cliente)", exclusive.Id, "Vehiculo", beforeVehicles[exclusive.Id], inactiveVehicles[exclusive.Id]),
            ("Desactivacion de vehiculo (por desactivacion de cliente)", inactiveOwner.Id, "Vehiculo", beforeVehicles[inactiveOwner.Id], inactiveVehicles[inactiveOwner.Id]),
            ("Desactivacion de cliente", target.Id, "Cliente", beforeClient, inactiveClient),
            ("Reactivacion de vehiculo (por reactivacion de cliente)", exclusive.Id, "Vehiculo", inactiveVehicles[exclusive.Id], reactivated[exclusive.Id]),
            ("Reactivacion de vehiculo (por reactivacion de cliente)", inactiveOwner.Id, "Vehiculo", inactiveVehicles[inactiveOwner.Id], reactivated[inactiveOwner.Id]),
            ("Reactivacion de vehiculo (por reactivacion de cliente)", alreadyInactive.Id, "Vehiculo", inactiveVehicles[alreadyInactive.Id], reactivated[alreadyInactive.Id]),
            ("Reactivacion de cliente", target.Id, "Cliente", inactiveClient, AuditCampos.CapturarCliente((await clients.ObtenerCliente(target.Id))!))
        });
        AssertResponse(await controller.DeactivateCliente("missing"), false, "No se pudo desactivar el cliente. Intente nuevamente.");
        AssertResponse(await controller.ReactivateCliente("missing"), false, "No se pudo reactivar el cliente. Intente nuevamente.");
        Assert.Equal(7, (await db.Collection("registros_auditoria").GetSnapshotAsync()).Count);
    }

    private static Cliente Client(string state) => new() { Id = "", Nombre = "Juan", Apellido = "Perez", TipoDocumento = "DNI", NumeroDocumento = "123", Telefono = "1234567890", Email = "juan@example.invalid", Estado = state };
    private static Vehiculo Vehicle(string plate, string owner, string[] owners, string state) => new() { Id = "", Patente = plate, TipoVehiculo = "Auto", Marca = "Marca", Modelo = "Modelo", Color = "Azul", ClienteId = owner, ClientesIds = owners.ToList(), Estado = state, ClaveAsociacionHash = "hash" };
    private static void AssertResponse(IActionResult result, bool success, string message)
    {
        var json = JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(result).Value);
        Assert.Equal(success, json.GetProperty("success").GetBoolean());
        Assert.Equal(message, json.GetProperty("message").GetString());
        Assert.Equal(2, json.EnumerateObject().Count());
    }
    private static async Task AssertEvents(FirestoreDb db, (string action, string id, string type, Dictionary<string, string> before, Dictionary<string, string> after)[] expected)
    {
        var docs = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents;
        Assert.Equal(expected.Length, docs.Count);
        var matches = expected.Select(e => (expected: e, doc: Assert.Single(docs, d => d.GetValue<string>("Action") == e.action && d.GetValue<string>("TargetId") == e.id))).ToArray();
        foreach (var (item, doc) in matches)
        {
            Assert.Equal(item.type, doc.GetValue<string>("TargetType"));
            Assert.Equal(item.before.OrderBy(x => x.Key), doc.GetValue<Dictionary<string, string>>("Antes").OrderBy(x => x.Key));
            Assert.Equal(item.after.OrderBy(x => x.Key), doc.GetValue<Dictionary<string, string>>("Despues").OrderBy(x => x.Key));
            Assert.DoesNotContain("ClaveAsociacion", JsonSerializer.Serialize(doc.ToDictionary()));
        }
        // Firestore document IDs do not encode write order; compare timestamps across the client boundary.
        foreach (var client in matches.Where(x => x.expected.type == "Cliente"))
        {
            var prefix = client.expected.action.StartsWith("Desactivacion") ? "Desactivacion de vehiculo" : "Reactivacion de vehiculo";
            foreach (var vehicle in matches.Where(x => x.expected.action.StartsWith(prefix)))
                Assert.True(vehicle.doc.GetValue<Timestamp>("Timestamp").CompareTo(client.doc.GetValue<Timestamp>("Timestamp")) <= 0);
        }
    }
}

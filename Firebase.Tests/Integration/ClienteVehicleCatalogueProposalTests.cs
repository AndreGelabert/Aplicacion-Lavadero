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

public class ClienteVehicleCatalogueProposalTests
{
    [LocalFirestoreFact]
    public async Task ClientRegistrationAtomicallyCreatesVehicleAndExplicitManualProposal()
    {
        var db = new FirestoreDbBuilder
        {
            ProjectId = "demo-lava-" + Guid.NewGuid().ToString("N")[..16],
            EmulatorDetection = EmulatorDetection.EmulatorOnly
        }.Build();
        var controller = new ClienteController(
            new ClienteService(db), new TipoDocumentoService(db), new VehiculoService(db), new AuditService(db),
            NullLogger<ClienteController>.Instance);
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "employee-test"),
            new Claim(ClaimTypes.Email, "employee@example.invalid")
        }, "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        var client = new Cliente
        {
            Id = "", TipoDocumento = "DNI", NumeroDocumento = Guid.NewGuid().ToString("N")[..8],
            Nombre = "Luis", Apellido = "Perez", Telefono = "3751123456", Email = "client@example.invalid"
        };
        var vehicles = JsonSerializer.Serialize(new[]
        {
            new
            {
                patente = "AB123CD", marca = "Toyota", marcaId = "1", modelo = "Corolla X",
                color = "Blanco", tipoVehiculo = "Automóvil", esNuevo = true,
                marcaManual = false, modeloManual = true
            }
        });

        await controller.CrearClienteAjax(client, vehicles);

        var storedVehicles = await db.Collection("vehiculos").GetSnapshotAsync();
        var vehicle = Assert.Single(storedVehicles.Documents).ConvertTo<Vehiculo>();
        var proposal = (await db.Collection("vehicleCatalogueProposals").Document(vehicle.Id).GetSnapshotAsync())
            .ConvertTo<VehicleCatalogueProposal>();
        Assert.Equal("Toyota", vehicle.Marca);
        Assert.Equal("Corolla X", vehicle.Modelo);
        Assert.Equal("Pendiente", proposal.Estado);
        Assert.False(proposal.MarcaManual);
        Assert.True(proposal.ModeloManual);
        Assert.Equal("1", proposal.MarcaId);
        Assert.Equal("true", context.Response.Headers["X-Form-Valid"]);
    }

    [LocalFirestoreFact]
    public async Task ClientUpdateAlsoCreatesProposalForAnExplicitManualVehicleValue()
    {
        var db = new FirestoreDbBuilder
        {
            ProjectId = "demo-lava-" + Guid.NewGuid().ToString("N")[..16],
            EmulatorDetection = EmulatorDetection.EmulatorOnly
        }.Build();
        var clients = new ClienteService(db);
        var client = new Cliente
        {
            Id = "", TipoDocumento = "DNI", NumeroDocumento = Guid.NewGuid().ToString("N")[..8],
            Nombre = "Luis", Apellido = "Perez", Telefono = "3751123456", Email = "client@example.invalid"
        };
        await clients.CrearCliente(client);
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "employee-test"),
            new Claim(ClaimTypes.Email, "employee@example.invalid")
        }, "test"));
        var controller = new ClienteController(clients, new TipoDocumentoService(db), new VehiculoService(db), new AuditService(db),
            NullLogger<ClienteController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
        var vehicles = JsonSerializer.Serialize(new[]
        {
            new
            {
                patente = "AB123CD", marca = "Toyota", marcaId = "1", modelo = "Corolla X",
                color = "Blanco", tipoVehiculo = "Automóvil", esNuevo = true,
                marcaManual = false, modeloManual = true
            }
        });

        await controller.ActualizarClienteAjax(client, vehicles);

        var vehicle = Assert.Single((await db.Collection("vehiculos").GetSnapshotAsync()).Documents).ConvertTo<Vehiculo>();
        var proposal = (await db.Collection("vehicleCatalogueProposals").Document(vehicle.Id).GetSnapshotAsync())
            .ConvertTo<VehicleCatalogueProposal>();
        Assert.Equal("Pendiente", proposal.Estado);
        Assert.True(proposal.ModeloManual);
        Assert.Equal("Corolla X", vehicle.Modelo);
    }

    [LocalFirestoreFact]
    public async Task ClientRegistrationRejectsVehicleAssociationWithoutValidKeyBeforeCreatingClient()
    {
        var db = new FirestoreDbBuilder
        {
            ProjectId = "demo-lava-" + Guid.NewGuid().ToString("N")[..16],
            EmulatorDetection = EmulatorDetection.EmulatorOnly
        }.Build();
        var vehicles = new VehiculoService(db);
        var existingVehicle = new Vehiculo
        {
            Id = "", Patente = "AB123CD", Marca = "Toyota", Modelo = "Corolla", Color = "Blanco",
            TipoVehiculo = "Automóvil", ClienteId = "owner", ClientesIds = new List<string> { "owner" },
            ClaveAsociacionHash = VehiculoService.HashClaveAsociacion("ABCD-EFGH"), Estado = "Activo"
        };
        await vehicles.CrearVehiculo(existingVehicle);
        var controller = new ClienteController(new ClienteService(db), new TipoDocumentoService(db), vehicles,
            new AuditService(db), NullLogger<ClienteController>.Instance);
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "employee-test"),
            new Claim(ClaimTypes.Email, "employee@example.invalid")
        }, "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        var client = new Cliente
        {
            Id = "", TipoDocumento = "DNI", NumeroDocumento = Guid.NewGuid().ToString("N")[..8],
            Nombre = "Ana", Apellido = "Perez", Telefono = "3751123456", Email = "client@example.invalid"
        };
        var request = JsonSerializer.Serialize(new[]
        {
            new { patente = existingVehicle.Patente, esAsociacion = true, claveAsociacion = "WRONG-KEY" }
        });

        await controller.CrearClienteAjax(client, request);

        Assert.Empty((await db.Collection("clientes").GetSnapshotAsync()).Documents);
        var unchanged = await vehicles.ObtenerVehiculo(existingVehicle.Id);
        Assert.Equal(new[] { "owner" }, unchanged!.ClientesIds);
        Assert.Equal("false", context.Response.Headers["X-Form-Valid"]);
        Assert.DoesNotContain("WRONG-KEY", string.Join(" ", controller.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
    }

    [LocalFirestoreFact]
    public async Task InvalidManualCatalogueDataReturnsGenericErrorWithoutPersistingExceptionDetails()
    {
        var db = new FirestoreDbBuilder
        {
            ProjectId = "demo-lava-" + Guid.NewGuid().ToString("N")[..16],
            EmulatorDetection = EmulatorDetection.EmulatorOnly
        }.Build();
        var controller = new ClienteController(new ClienteService(db), new TipoDocumentoService(db),
            new VehiculoService(db), new AuditService(db), NullLogger<ClienteController>.Instance);
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "employee-test"),
            new Claim(ClaimTypes.Email, "employee@example.invalid")
        }, "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        var client = new Cliente
        {
            Id = "", TipoDocumento = "DNI", NumeroDocumento = Guid.NewGuid().ToString("N")[..8],
            Nombre = "Ana", Apellido = "Perez", Telefono = "3751123456", Email = "client@example.invalid"
        };
        var request = JsonSerializer.Serialize(new[]
        {
            new
            {
                patente = "AB123CD", marca = "Toyota", modelo = new string('M', 101), color = "Blanco",
                tipoVehiculo = "Automóvil", esNuevo = true, modeloManual = true
            }
        });

        await controller.CrearClienteAjax(client, request);

        var errors = string.Join(" ", controller.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
        Assert.Contains("No se pudo guardar el cliente", errors);
        Assert.DoesNotContain("deben tener entre", errors);
        Assert.Empty((await db.Collection("clientes").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task ClientRegistrationRejectsIdOfVehicleOwnedByAnotherClient()
    {
        var db = new FirestoreDbBuilder
        {
            ProjectId = "demo-lava-" + Guid.NewGuid().ToString("N")[..16],
            EmulatorDetection = EmulatorDetection.EmulatorOnly
        }.Build();
        var vehicles = new VehiculoService(db);
        var existingVehicle = new Vehiculo
        {
            Id = "", Patente = "AB123CD", Marca = "Toyota", Modelo = "Corolla", Color = "Blanco",
            TipoVehiculo = "Automóvil", ClienteId = "owner", ClientesIds = new List<string> { "owner" }, Estado = "Activo"
        };
        await vehicles.CrearVehiculo(existingVehicle);
        var controller = new ClienteController(new ClienteService(db), new TipoDocumentoService(db), vehicles,
            new AuditService(db), NullLogger<ClienteController>.Instance);
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "employee-test"),
            new Claim(ClaimTypes.Email, "employee@example.invalid")
        }, "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        var client = new Cliente
        {
            Id = "", TipoDocumento = "DNI", NumeroDocumento = Guid.NewGuid().ToString("N")[..8],
            Nombre = "Ana", Apellido = "Perez", Telefono = "3751123456", Email = "client@example.invalid"
        };
        var request = JsonSerializer.Serialize(new[] { new { id = existingVehicle.Id, esNuevo = false } });

        await controller.CrearClienteAjax(client, request);

        Assert.Empty((await db.Collection("clientes").GetSnapshotAsync()).Documents);
        Assert.Equal(new[] { "owner" }, (await vehicles.ObtenerVehiculo(existingVehicle.Id))!.ClientesIds);
        Assert.Equal("false", context.Response.Headers["X-Form-Valid"]);
    }
}

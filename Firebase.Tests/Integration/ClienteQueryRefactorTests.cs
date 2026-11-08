using Firebase.Models;
using Google.Api.Gax;
using Google.Cloud.Firestore;

namespace Firebase.Tests.Integration;

public class ClienteQueryRefactorTests
{
    [LocalFirestoreFact]
    public async Task ListingAndCountPreserveFiltersSortingAndPaging()
    {
        var db = new FirestoreDbBuilder
        {
            ProjectId = "demo-clientes-" + Guid.NewGuid().ToString("N")[..16],
            EmulatorDetection = EmulatorDetection.EmulatorOnly
        }.Build();
        var service = new ClienteService(db);
        var clients = new[]
        {
            Client("a", "Ana", "Perez", "12345678", "ana@example.invalid", "1111111111", "Activo"),
            Client("b", "Bruno", "Lopez", "87654321", "bruno@example.invalid", "2222222222", "Activo"),
            Client("c", "Carla", "Garcia", "55555555", "carla@example.invalid", "3333333333", "Inactivo")
        };
        foreach (var client in clients)
            await db.Collection("clientes").Document(client.Id).SetAsync(client);

        async Task Check(string? term, List<string>? states, params string[] expected)
        {
            var rows = await service.ObtenerClientes(term!, 1, 10, "Nombre", "asc", states!);
            Assert.Equal(expected, rows.Select(c => c.Id));
            Assert.Equal(expected.Length, await service.ObtenerTotalClientes(term!, states!));
        }

        await Check(null, null, "a", "b");
        await Check("   ", new(), "a", "b");
        await Check(null, new() { "Inactivo" }, "c");
        await Check(null, new() { "activo" });
        await Check(null, new() { "Activo", "Inactivo" }, "a", "b", "c");
        await Check("ANA", null, "a");
        await Check("PEREZ", null, "a");
        await Check("3456", null, "a");
        await Check("ANA@EXAMPLE", null, "a");
        await Check("1111", null, "a");
        await Check(" Ana ", null);
        await Check("CARLA", null);
        await Check("CARLA", new() { "Inactivo" }, "c");
        Assert.Equal("b", Assert.Single(await service.ObtenerClientes(null!, 1, 1, "Nombre", "DESC")).Id);
        Assert.Equal("a", Assert.Single(await service.ObtenerClientes(null!, 2, 1, "Nombre", "DESC")).Id);
        Assert.Empty(await service.ObtenerClientes(null!, 3, 1, "Nombre", "asc"));
        Assert.Equal(2, await service.ObtenerTotalClientes(null!));
        foreach (var client in clients)
        {
            var stored = await service.ObtenerCliente(client.Id);
            Assert.NotNull(stored);
            Assert.Equal(client.Nombre, stored.Nombre);
            Assert.Equal(client.Apellido, stored.Apellido);
            Assert.Equal(client.NumeroDocumento, stored.NumeroDocumento);
            Assert.Equal(client.Email, stored.Email);
            Assert.Equal(client.Telefono, stored.Telefono);
            Assert.Equal(client.Estado, stored.Estado);
            Assert.Empty(stored.VehiculosIds);
        }
    }

    [LocalFirestoreFact]
    public async Task ControllerListingsPreserveMetadataAndCreateOnlyEmptyRows()
    {
        var db = new FirestoreDbBuilder
        {
            ProjectId = "demo-clientmvc-" + Guid.NewGuid().ToString("N")[..16],
            EmulatorDetection = EmulatorDetection.EmulatorOnly
        }.Build();
        await db.Collection("clientes").Document("a").SetAsync(
            Client("a", "Ana", "Perez", "123", "ana@example.invalid", "1111111111", "Activo"));
        ClienteController Controller(bool canRead) => new(new ClienteService(db),
            new TipoDocumentoService(db), new VehiculoService(db), new AuditService(db),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ClienteController>.Instance,
            new Firebase.Services.ClienteLifecycleService(new ClienteService(db), new VehiculoService(db), new AuditService(db)),
            new Firebase.Services.ClienteVehicleService(new ClienteService(db), new VehiculoService(db), new AuditService(db)))
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                        new[] { new System.Security.Claims.Claim("permiso", canRead ? "Clientes.Consultar" : "Clientes.Crear") }, "test"))
                }
            }
        };
        void Metadata(Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary data)
        {
            Assert.Equal(2, data["CurrentPage"]);
            Assert.Equal(1, data["TotalPages"]);
            Assert.Equal(new[] { 1 }, Assert.IsType<List<int>>(data["VisiblePages"]));
            Assert.Equal("Nombre", data["SortBy"]);
            Assert.Equal("desc", data["SortOrder"]);
            Assert.Equal(new[] { "Activo" }, Assert.IsType<List<string>>(data["Estados"]));
        }
        var index = Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(
            await Controller(true).Index("ANA", null!, 2, 1, "Nombre", "desc"));
        Assert.Empty(Assert.IsType<List<Cliente>>(index.Model));
        Metadata(index.ViewData);
        Assert.Equal("ANA", index.ViewData["SearchTerm"]);
        Assert.Equal(1, index.ViewData["PageSize"]);
        var search = Assert.IsType<Microsoft.AspNetCore.Mvc.PartialViewResult>(
            await Controller(true).SearchPartial("ANA", new(), 2, 1, "Nombre", "desc"));
        Assert.Equal("_ClienteTable", search.ViewName);
        Assert.Empty(Assert.IsType<List<Cliente>>(search.Model));
        Metadata(search.ViewData);
        Assert.Equal("ANA", search.ViewData["SearchTerm"]);
        Assert.False(search.ViewData.ContainsKey("PageSize"));
        var table = Assert.IsType<Microsoft.AspNetCore.Mvc.PartialViewResult>(
            await Controller(true).TablePartial(null!, 2, 1, "Nombre", "desc"));
        Assert.Empty(Assert.IsType<List<Cliente>>(table.Model));
        Metadata(table.ViewData);
        Assert.Null(table.ViewData["SearchTerm"]);
        var createOnly = Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(
            await Controller(false).Index(null!, null!));
        Assert.Empty(Assert.IsType<List<Cliente>>(createOnly.Model));
        Assert.Equal(1, createOnly.ViewData["TotalPages"]);
        Assert.Equal("CrearClienteAjax", createOnly.ViewData["FormAction"]);
        Assert.Single(await new ClienteService(db).ObtenerClientes(null!, 1, 10, "Nombre", "asc"));
    }

    private static Cliente Client(string id, string name, string surname, string document,
        string email, string phone, string state) => new()
    {
        Id = id, Nombre = name, Apellido = surname, TipoDocumento = "DNI",
        NumeroDocumento = document, Email = email, Telefono = phone, Estado = state
    };
}

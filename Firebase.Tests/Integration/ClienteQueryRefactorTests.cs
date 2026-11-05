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

    private static Cliente Client(string id, string name, string surname, string document,
        string email, string phone, string state) => new()
    {
        Id = id, Nombre = name, Apellido = surname, TipoDocumento = "DNI",
        NumeroDocumento = document, Email = email, Telefono = phone, Estado = state
    };
}

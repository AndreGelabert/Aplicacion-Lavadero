using Firebase.Models;
using Google.Api.Gax;
using Google.Cloud.Firestore;

namespace Firebase.Tests.Integration;

public class VehiculoQueryRefactorTests
{
    [LocalFirestoreFact]
    public async Task ListingAndCountPreserveFiltersHydrationSortingAndPaging()
    {
        var db = new FirestoreDbBuilder
        {
            ProjectId = "demo-vehquery-" + Guid.NewGuid().ToString("N")[..16],
            EmulatorDetection = EmulatorDetection.EmulatorOnly
        }.Build();
        await db.Collection("clientes").Document("owner").SetAsync(new Cliente
        {
            Id = "owner", Nombre = "Ana", Apellido = "Perez", TipoDocumento = "DNI",
            NumeroDocumento = "123", Telefono = "1111111111", Email = "ana@example.invalid"
        });
        var vehicles = new[]
        {
            Vehicle("a", "AAA123", "Ford", "Fiesta", "Rojo", "Auto", "Activo"),
            Vehicle("b", "BBB456", "Toyota", "Hilux", "Azul", "Camioneta", "Activo"),
            Vehicle("c", "CCC789", "Ford", "Focus", "Rojo", "Auto", "Inactivo")
        };
        foreach (var vehicle in vehicles)
            await db.Collection("vehiculos").Document(vehicle.Id).SetAsync(vehicle);
        var service = new VehiculoService(db);
        async Task Check(string? term, List<string>? types, List<string>? brands,
            List<string>? colors, List<string>? states, params string[] expected)
        {
            var rows = await service.ObtenerVehiculos(term!, types!, brands!, colors!, 1, 10, "Patente", "asc", states!);
            Assert.Equal(expected, rows.Select(v => v.Id));
            Assert.All(rows, v => Assert.Equal("Ana Perez", v.ClienteNombreCompleto));
            Assert.Equal(expected.Length, await service.ObtenerTotalVehiculos(term!, types!, brands!, colors!, states!));
        }
        await Check(null, null, null, null, null, "a", "b");
        await Check("   ", new(), new(), new(), new(), "a", "b");
        await Check("aaa", null, null, null, null, "a");
        await Check("TOYOTA", null, null, null, null, "b");
        await Check("FIESTA", null, null, null, null, "a");
        await Check("ANA PEREZ", null, null, null, null, "a", "b");
        await Check(null, new() { "Auto" }, null, null, null, "a");
        await Check(null, null, new() { "Ford" }, null, null, "a");
        await Check(null, null, null, new() { "Azul" }, null, "b");
        await Check("FOCUS", new() { "Auto" }, new() { "Ford" }, new() { "Rojo" }, new() { "Inactivo" }, "c");
        await Check(null, null, null, null, new() { "Activo", "Inactivo" }, "a", "b", "c");
        await Check(null, new() { "auto" }, null, null, null);
        await Check(null, null, new() { "ford" }, null, null);
        await Check(null, null, null, new() { "rojo" }, null);
        await Check(null, null, null, null, new() { "activo" });
        await Check(" AAA ", null, null, null, null);
        Assert.Equal("b", Assert.Single(await service.ObtenerVehiculos(null!, null!, null!, null!, 1, 1, "Patente", "DESC")).Id);
        Assert.Equal("a", Assert.Single(await service.ObtenerVehiculos(null!, null!, null!, null!, 2, 1, "Patente", "DESC")).Id);
        Assert.Empty(await service.ObtenerVehiculos(null!, null!, null!, null!, 3, 1, "Patente", "asc"));
        foreach (var vehicle in vehicles)
        {
            var stored = await service.ObtenerVehiculo(vehicle.Id);
            Assert.NotNull(stored);
            Assert.Equal(vehicle.Patente, stored.Patente);
            Assert.Equal(vehicle.Marca, stored.Marca);
            Assert.Equal(vehicle.Modelo, stored.Modelo);
            Assert.Equal(vehicle.Color, stored.Color);
            Assert.Equal(vehicle.TipoVehiculo, stored.TipoVehiculo);
            Assert.Equal(vehicle.Estado, stored.Estado);
            Assert.Equal(vehicle.ClienteId, stored.ClienteId);
            Assert.Equal(vehicle.ClientesIds, stored.ClientesIds);
            Assert.Equal(vehicle.ClaveAsociacionHash, stored.ClaveAsociacionHash);
        }
    }

    private static Vehiculo Vehicle(string id, string plate, string brand, string model,
        string color, string type, string state) => new()
    {
        Id = id, Patente = plate, Marca = brand, Modelo = model, Color = color,
        TipoVehiculo = type, Estado = state, ClienteId = "owner", ClientesIds = new() { "owner" }
    };
}

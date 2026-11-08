using Firebase.Models;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;

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

    [LocalFirestoreFact]
    public async Task ListingActionsPreserveModelsMetadataAndPermissionEarlyReturn()
    {
        var db = new FirestoreDbBuilder
        {
            ProjectId = "demo-vehmvc-" + Guid.NewGuid().ToString("N")[..16],
            EmulatorDetection = EmulatorDetection.EmulatorOnly
        }.Build();
        await db.Collection("clientes").Document("owner").SetAsync(new Cliente
        {
            Id = "owner", Nombre = "Ana", Apellido = "Perez", TipoDocumento = "DNI",
            NumeroDocumento = "123", Telefono = "1111111111", Email = "ana@example.invalid"
        });
        foreach (var vehicle in new[]
        {
            Vehicle("a", "AAA123", "Ford", "Fiesta", "Rojo", "Auto", "Activo"),
            Vehicle("b", "BBB456", "Toyota", "Hilux", "Azul", "Camioneta", "Inactivo")
        })
            await db.Collection("vehiculos").Document(vehicle.Id).SetAsync(vehicle);
        await db.Collection("tiposVehiculos").Document("auto").SetAsync(new Dictionary<string, object> { ["Nombre"] = "Auto" });

        VehiculoController Controller(string permission, bool services = true) => new(
            services ? new VehiculoService(db) : null!, services ? new TipoVehiculoService(db) : null!,
            services ? new ClienteService(db) : null!, services ? new AuditService(db) : null!,
            null!, NullLogger<VehiculoController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("permiso", permission) }, "test"))
            } }
        };

        foreach (var action in new[] { "Index", "SearchPartial", "TablePartial" })
        foreach (var page in new[] { 1, 2 })
        {
            var controller = Controller("Vehiculos.Consultar");
            var types = new List<string> { "Auto" };
            var brands = new List<string> { "Ford" };
            var colors = new List<string> { "Rojo" };
            var term = action == "TablePartial" ? null : "AAA";
            var result = action switch
            {
                "Index" => await controller.Index(term!, types, brands, colors, new(), page, 1, "Patente", "desc"),
                "SearchPartial" => await controller.SearchPartial(term!, types, brands, colors, new(), page, 1, "Patente", "desc"),
                _ => await controller.TablePartial(types, brands, colors, new(), page, 1, "Patente", "desc")
            };
            object? model;
            if (action == "Index") model = Assert.IsType<ViewResult>(result).Model;
            else
            {
                var partial = Assert.IsType<PartialViewResult>(result);
                Assert.Equal("_VehiculoTable", partial.ViewName);
                model = partial.Model;
            }
            var rows = Assert.IsType<List<Vehiculo>>(model);
            Assert.Equal(page == 1 ? new[] { "a" } : Array.Empty<string>(), rows.Select(v => v.Id));
            Assert.All(rows, v => Assert.Equal("Ana Perez", v.ClienteNombreCompleto));
            var data = controller.ViewData;
            Assert.Equal(page, data["CurrentPage"]);
            Assert.Equal(1, data["TotalPages"]);
            Assert.Equal(new[] { 1 }, Assert.IsType<List<int>>(data["VisiblePages"]));
            Assert.Equal("Patente", data["SortBy"]);
            Assert.Equal("desc", data["SortOrder"]);
            Assert.Equal(term, data["SearchTerm"]);
            Assert.Equal(types, Assert.IsType<List<string>>(data["TiposVehiculoFiltrados"]));
            Assert.Equal(brands, Assert.IsType<List<string>>(data["MarcasFiltradas"]));
            Assert.Equal(colors, Assert.IsType<List<string>>(data["ColoresFiltrados"]));
            Assert.Equal(new[] { "Activo" }, Assert.IsType<List<string>>(data["Estados"]));
            if (action == "Index")
            {
                Assert.Equal(1, data["PageSize"]);
                Assert.Equal(new[] { "Ford", "Toyota" }, Assert.IsType<List<string>>(data["MarcasDisponibles"]));
                Assert.Equal(new[] { "Azul", "Rojo" }, Assert.IsType<List<string>>(data["ColoresDisponibles"]));
                Assert.Equal(new[] { "Auto" }, Assert.IsType<List<string>>(data["TiposVehiculo"]));
                Assert.Equal("Editar Vehículo", data["FormTitle"]);
                Assert.True(data.ContainsKey("EditVehiculo"));
                Assert.Null(data["EditVehiculo"]);
            }
            else
                foreach (var key in new[] { "PageSize", "MarcasDisponibles", "ColoresDisponibles", "TiposVehiculo", "FormTitle", "EditVehiculo" })
                    Assert.False(data.ContainsKey(key));
        }

        foreach (var permission in new[] { "Vehiculos.Modificar", "Vehiculos.Eliminar" })
        {
            var controller = Controller(permission, services: false);
            var result = Assert.IsType<ViewResult>(await controller.Index(null!, new(), new(), new(), new()));
            Assert.Empty(Assert.IsType<List<Vehiculo>>(result.Model));
            Assert.True(controller.ViewData.ContainsKey("EditVehiculo"));
            Assert.Null(controller.ViewData["EditVehiculo"]);
            Assert.Single(controller.ViewData);
        }
    }

    private static Vehiculo Vehicle(string id, string plate, string brand, string model,
        string color, string type, string state) => new()
    {
        Id = id, Patente = plate, Marca = brand, Modelo = model, Color = color,
        TipoVehiculo = type, Estado = state, ClienteId = "owner", ClientesIds = new() { "owner" }
    };
}

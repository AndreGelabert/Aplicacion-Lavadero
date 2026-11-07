using System.Security.Claims;
using System.Text.Json;
using Firebase.Models;
using Firebase.Services;
using FirebaseLoginCustom.Controllers;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Firebase.Tests.Integration;

public class LavadoCreationPricingRefactorTests
{
    [LocalFirestoreFact]
    public async Task CreationPreservesIndividualAndSelectedPackagePrices()
    {
        foreach (var (items, discount, original, final) in new[]
        {
            (new[] { Item("one"), Item("two") }, 0m, 150.25m, 150.25m),
            (new[] { Item("one", "pack"), Item("two") }, 12.5m, 130.20m, 113.925m),
            (new[] { Item("one", "pack"), Item("two", "pack"), Item("three") }, 10m, 145.20m, 130.68m)
        })
        {
            using var fixture = await Fixture.Create();
            var response = Json(await fixture.Controller.CrearLavadoAjax(Request(items, discount)));
            Assert.True(response.GetProperty("success").GetBoolean(), response.GetRawText());
            Assert.Equal("Lavado creado correctamente.", response.GetProperty("message").GetString());
            var id = Assert.Single(response.GetProperty("lavadosIds").EnumerateArray()).GetString();
            var lavado = await fixture.Washes.ObtenerLavado(id!);
            Assert.NotNull(lavado);
            Assert.Equal(original, lavado.PrecioOriginal);
            Assert.Equal(final, lavado.Precio);
            Assert.Equal(discount, lavado.Descuento);
            Assert.Equal(items.Select(x => x.ServicioId), lavado.Servicios.Select(x => x.ServicioId));
            Assert.Equal(items.Select(x => x.PaqueteId ?? string.Empty), lavado.Servicios.Select(x => x.PaqueteId));
            Assert.Equal("EnProceso", lavado.Servicios[0].Estado);
            Assert.Equal(1, (await fixture.Db.Collection("lavados").GetSnapshotAsync()).Count);
        }
    }

    [LocalFirestoreFact]
    public async Task InvalidPackageSelectionsKeepExistingErrorAndDoNotPersist()
    {
        using var fixture = await Fixture.Create();
        foreach (var items in new[]
        {
            new[] { Item("one", "missing") },
            new[] { Item("three", "pack") },
            new[] { Item("one", "pack"), Item("one", "pack") }
        })
        {
            var response = Json(await fixture.Controller.CrearLavadoAjax(Request(items, 10m)));
            Assert.False(response.GetProperty("success").GetBoolean());
            Assert.Equal(items[0].PaqueteId == "missing"
                ? "Uno de los paquetes seleccionados ya no está disponible."
                : "Los servicios seleccionados no corresponden al paquete indicado.", response.GetProperty("message").GetString());
            Assert.Empty((await fixture.Db.Collection("lavados").GetSnapshotAsync()).Documents);
        }
    }

    [LocalFirestoreFact]
    public async Task CreationPersistsDefaultCappedAndWithinLimitStaffAssignments()
    {
        foreach (var (requirement, maximum, expected) in new (int?, int, int)[]
        {
            (null, 3, 1),
            (4, 2, 2),
            (2, 3, 2),
            (2, 2, 2)
        })
        {
            using var fixture = await Fixture.Create();
            fixture.SetStaffMaximum(maximum);
            if (requirement.HasValue)
                await fixture.Db.Collection("tiposVehiculos").Document("auto").SetAsync(new Dictionary<string, object>
                {
                    ["Nombre"] = "Auto", ["CantidadEmpleadosRequeridos"] = requirement.Value
                });
            var staff = new Dictionary<string, string> { ["staff"] = "Empleado", ["staff2"] = "Segundo", ["staff3"] = "Tercero" };
            foreach (var id in new[] { "staff2", "staff3" })
                await fixture.Db.Collection("empleados").Document(id).SetAsync(new Dictionary<string, object>
                {
                    ["Nombre"] = staff[id], ["Email"] = id + "@example.com", ["Rol"] = "Empleado", ["Estado"] = "Activo"
                });

            var response = Json(await fixture.Controller.CrearLavadoAjax(Request(new[] { Item("one") }, 0m)));
            Assert.True(response.GetProperty("success").GetBoolean(), response.GetRawText());
            Assert.Equal("Lavado creado correctamente.", response.GetProperty("message").GetString());
            var idCreated = Assert.Single(response.GetProperty("lavadosIds").EnumerateArray()).GetString();
            var lavado = await fixture.Washes.ObtenerLavado(idCreated!);
            Assert.NotNull(lavado);
            Assert.Equal(expected, lavado.CantidadEmpleadosRequeridos);
            Assert.Equal(expected, lavado.EmpleadosAsignadosIds.Count);
            Assert.Equal(expected, lavado.EmpleadosAsignadosNombres.Count);
            Assert.Equal(expected, lavado.EmpleadosAsignadosIds.Distinct().Count());
            for (var i = 0; i < expected; i++)
            {
                Assert.True(staff.TryGetValue(lavado.EmpleadosAsignadosIds[i], out var name));
                Assert.Equal(name, lavado.EmpleadosAsignadosNombres[i]);
            }
            Assert.Equal(1, (await fixture.Db.Collection("lavados").GetSnapshotAsync()).Count);
        }
    }

    private static ServicioItemRequest Item(string id, string? package = null) => new() { ServicioId = id, PaqueteId = package, PaqueteNombre = package == null ? null : "Paquete" };
    private static CrearLavadoRequest Request(ServicioItemRequest[] items, decimal discount) => new()
    {
        ClienteId = "client", Descuento = discount,
        VehiculosServicios = new() { new VehiculoServiciosRequest { VehiculoId = "vehicle", Servicios = items.ToList() } }
    };
    private static JsonElement Json(IActionResult result) => JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(result).Value);

    private sealed class Fixture : IDisposable
    {
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());
        public FirestoreDb Db { get; }
        public LavadoService Washes { get; }
        public LavadosController Controller { get; }

        private Fixture(FirestoreDb db)
        {
            Db = db;
            var services = new ServicioService(db);
            var packages = new PaqueteServicioService(db, services, _cache);
            var staff = new PersonalService(db);
            var config = new ConfiguracionService(db, _cache);
            _cache.Set("system_configuration", new Configuracion { HorariosOperacion = new(), CapacidadMaximaConcurrente = 10 });
            Washes = new LavadoService(db, services, packages, staff, config);
            Controller = new LavadosController(NullLogger<LavadosController>.Instance, new AuditService(db), Washes,
                services, packages, new ClienteService(db), new VehiculoService(db), staff, config,
                new TipoVehiculoService(db), new TipoDocumentoService(db))
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "actor") }, "test"))
                } }
            };
        }

        public void SetStaffMaximum(int maximum) => _cache.Set("system_configuration", new Configuracion
        {
            HorariosOperacion = new(), CapacidadMaximaConcurrente = 10, EmpleadosMaximosPorLavado = maximum
        });

        public static async Task<Fixture> Create()
        {
            var fixture = new Fixture(new FirestoreDbBuilder { ProjectId = "demo-washprice-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build());
            var db = fixture.Db;
            await db.Collection("clientes").Document("client").SetAsync(new Cliente { Id = "client", TipoDocumento = "DNI", NumeroDocumento = "123", Nombre = "Ana", Apellido = "Perez", Telefono = "1234567890", Email = "ana@example.com" });
            await db.Collection("vehiculos").Document("vehicle").SetAsync(new Vehiculo { Id = "vehicle", Patente = "ABC123", TipoVehiculo = "Auto", Marca = "Marca", Modelo = "Modelo", Color = "Azul", ClienteId = "client" });
            await db.Collection("empleados").Document("staff").SetAsync(new Dictionary<string, object> { ["Nombre"] = "Empleado", ["Email"] = "staff@example.com", ["Rol"] = "Empleado", ["Estado"] = "Activo" });
            foreach (var (id, price) in new[] { ("one", 100.25), ("two", 50.0), ("three", 25.0) })
                await db.Collection("servicios").Document(id).SetAsync(new Dictionary<string, object> { ["Nombre"] = id, ["Tipo"] = id, ["TipoVehiculo"] = "auto", ["Precio"] = price, ["TiempoEstimado"] = 10, ["Descripcion"] = "Servicio", ["Estado"] = "Activo" });
            await db.Collection("paquetes_servicios").Document("pack").SetAsync(new Dictionary<string, object> { ["Nombre"] = "Paquete", ["Estado"] = "Activo", ["TipoVehiculo"] = "Auto", ["Precio"] = 999.0, ["PorcentajeDescuento"] = 20.0, ["ServiciosIds"] = new[] { "one", "two" } });
            return fixture;
        }

        public void Dispose() => _cache.Dispose();
    }
}

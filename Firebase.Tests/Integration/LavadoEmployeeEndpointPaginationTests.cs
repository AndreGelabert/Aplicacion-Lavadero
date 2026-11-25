using System.Security.Claims;
using System.Text.Json;
using Firebase.Models;
using Firebase.Security;
using Firebase.Services;
using FirebaseLoginCustom.Controllers;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace Firebase.Tests.Integration;

public class LavadoEmployeeEndpointPaginationTests
{
    [LocalFirestoreFact]
    public async Task EndpointIncludesAllActiveEmployeesAndExcludesBusyEmployees()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-endpoint-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set("system_configuration", new Configuracion());
        var batch = db.StartBatch();
        for (var i = 0; i < 106; i++)
            batch.Set(db.Collection("empleados").Document($"emp{i:D3}"), new Dictionary<string, object>
            {
                ["Nombre"] = $"Empleado {i:D3}", ["Email"] = $"employee{i}@example.invalid", ["Estado"] = "Activo"
            });
        batch.Set(db.Collection("empleados").Document("inactive"), new Dictionary<string, object>
        {
            ["Nombre"] = "Empleado 999", ["Email"] = "inactive@example.invalid", ["Estado"] = "Inactivo"
        });
        batch.Set(db.Collection("lavados").Document("busy"), new Dictionary<string, object>
        {
            ["Estado"] = "EnProceso", ["ClienteId"] = "client", ["VehiculoId"] = "vehicle", ["EmpleadosAsignadosIds"] = new[] { "emp000", "emp100" }
        });
        await batch.CommitAsync();

        var controller = Controller(db, cache, "Lavados.Consultar");
        var json = JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(await controller.ObtenerEmpleadosDisponibles()).Value);
        Assert.Equal(new[] { "totalActivos", "totalDisponibles", "empleados" }, json.EnumerateObject().Select(p => p.Name));
        Assert.Equal(106, json.GetProperty("totalActivos").GetInt32());
        Assert.Equal(104, json.GetProperty("totalDisponibles").GetInt32());
        var employees = json.GetProperty("empleados").EnumerateArray().ToArray();
        Assert.All(employees, e => Assert.Equal(new[] { "id", "nombre" }, e.EnumerateObject().Select(p => p.Name)));
        Assert.Equal(Enumerable.Range(0, 106).Where(i => i != 0 && i != 100).Select(i => $"emp{i:D3}"), employees.Select(e => e.GetProperty("id").GetString()).OrderBy(id => id));
        Assert.Equal("Empleado 105", employees.Single(e => e.GetProperty("id").GetString() == "emp105").GetProperty("nombre").GetString());

        controller.ControllerContext.HttpContext.User = UserWith("Lavados.Crear");
        json = JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(await controller.ObtenerEmpleadosDisponibles()).Value);
        Assert.Equal(new[] { "totalActivos", "totalDisponibles" }, json.EnumerateObject().Select(p => p.Name));
        Assert.Equal(106, json.GetProperty("totalActivos").GetInt32());
        Assert.Equal(104, json.GetProperty("totalDisponibles").GetInt32());
    }

    [LocalFirestoreFact]
    public async Task EndpointPreservesSmallRosterBehavior()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-endpoint-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set("system_configuration", new Configuracion());
        await db.Collection("empleados").Document("one").SetAsync(new Dictionary<string, object> { ["Nombre"] = "Uno", ["Email"] = "one@example.invalid", ["Estado"] = "Activo" });
        var json = JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(await Controller(db, cache, "Lavados.Consultar").ObtenerEmpleadosDisponibles()).Value);
        Assert.Equal(new[] { "totalActivos", "totalDisponibles", "empleados" }, json.EnumerateObject().Select(p => p.Name));
        Assert.Equal(1, json.GetProperty("totalActivos").GetInt32());
        Assert.Equal(1, json.GetProperty("totalDisponibles").GetInt32());
        var employee = Assert.Single(json.GetProperty("empleados").EnumerateArray());
        Assert.Equal("one", employee.GetProperty("id").GetString());
        Assert.Equal("Uno", employee.GetProperty("nombre").GetString());
    }

    [Fact]
    public async Task EndpointPreservesFailureFallback()
    {
        var controller = new LavadosController(NullLogger<LavadosController>.Instance, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);
        var json = JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(await controller.ObtenerEmpleadosDisponibles()).Value);
        Assert.Equal(new[] { "totalActivos", "totalDisponibles", "empleados" }, json.EnumerateObject().Select(p => p.Name));
        Assert.Equal(0, json.GetProperty("totalActivos").GetInt32());
        Assert.Equal(0, json.GetProperty("totalDisponibles").GetInt32());
        Assert.Empty(json.GetProperty("empleados").EnumerateArray());
    }

    private static ClaimsPrincipal UserWith(string permission) => new(new ClaimsIdentity(new[] { new Claim(PermisoClaim.Tipo, permission) }, "test"));

    private static LavadosController Controller(FirestoreDb db, IMemoryCache cache, string permission)
    {
        var staff = new PersonalService(db);
        var config = new ConfiguracionService(db, cache);
        var services = new ServicioService(db);
        var washes = new LavadoService(db, services, new PaqueteServicioService(db, services, cache), staff, config);
        return new LavadosController(NullLogger<LavadosController>.Instance, null!, washes, services, null!, null!, null!, staff, config, null!, null!, null!, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = UserWith(permission) } }
        };
    }
}

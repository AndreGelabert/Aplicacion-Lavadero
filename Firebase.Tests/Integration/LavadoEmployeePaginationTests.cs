using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Caching.Memory;

namespace Firebase.Tests.Integration;

public class LavadoEmployeePaginationTests
{
    [LocalFirestoreFact]
    public async Task AssignmentIncludesAvailableEmployeesBeyondFirstHundredAndPreservesFilters()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-employees-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var services = new ServicioService(db);
        var service = new LavadoService(db, services, new PaqueteServicioService(db, services, cache), new PersonalService(db), new ConfiguracionService(db, cache));
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
            ["Estado"] = "EnProceso", ["ClienteId"] = "client", ["VehiculoId"] = "vehicle", ["EmpleadosAsignadosIds"] = new[] { "emp100" }
        });
        await batch.CommitAsync();
        var excluded = Enumerable.Range(0, 100).Select(i => $"emp{i:D3}").Append("emp101").ToHashSet();

        var result = await service.PrepararAsignacionCreacion(4, excluded);

        Assert.Equal(4, result.CantidadEmpleadosRequeridos);
        Assert.Equal(new[] { "emp102", "emp103", "emp104", "emp105" }, result.EmpleadosAsignados.Select(e => e.Id).OrderBy(x => x));
        Assert.Equal(107, (await db.Collection("empleados").GetSnapshotAsync()).Count);
        Assert.Single((await db.Collection("lavados").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task AssignmentRetainsDefaultAndInsufficientAvailabilityBehavior()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-employees-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var services = new ServicioService(db);
        var service = new LavadoService(db, services, new PaqueteServicioService(db, services, cache), new PersonalService(db), new ConfiguracionService(db, cache));
        await db.Collection("empleados").Document("one").SetAsync(new Dictionary<string, object> { ["Nombre"] = "Uno", ["Email"] = "one@example.invalid", ["Estado"] = "Activo" });
        var result = await service.PrepararAsignacionCreacion(null);
        Assert.Equal(1, result.CantidadEmpleadosRequeridos);
        Assert.Equal("one", Assert.Single(result.EmpleadosAsignados).Id);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AsignarEmpleadosAleatorios(2));
        Assert.Equal("No hay suficientes empleados disponibles. Se requieren 2 pero solo hay 1 disponibles.", error.Message);
        Assert.Empty((await db.Collection("lavados").GetSnapshotAsync()).Documents);
    }
}

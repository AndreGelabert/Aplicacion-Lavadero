using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Firebase.Tests.Integration;

public class LavadoPaymentWithdrawalConcurrencyTests
{
    [LocalFirestoreFact]
    public async Task ServiceAllowsPaymentsBeyondPriceWithoutChangingOtherFields()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-wash-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var services = new ServicioService(db);
        var washes = new LavadoService(db, services, new PaqueteServicioService(db, services, cache), new PersonalService(db), new ConfiguracionService(db, cache));
        var wash = new Lavado { Id = "wash", Estado = "EnProceso", ClienteId = "client", VehiculoId = "vehicle", Precio = 100, FechaCreacion = DateTime.UtcNow, EmpleadosAsignadosIds = new() { "staff" }, Servicios = new() { new ServicioEnLavado { ServicioId = "service", Estado = "Pendiente" } } };
        await db.Collection("empleados").Document("staff").SetAsync(new { Estado = "Activo" });
        await washes.ActualizarLavado(wash);
        var reference = db.Collection("lavados").Document(wash.Id);
        await reference.UpdateAsync("CampoAjeno", "conservar");
        var before = (await reference.GetSnapshotAsync()).ToDictionary();
        before.Remove("Pago");
        await washes.RegistrarPago(wash.Id, 70, "Efectivo");
        await washes.RegistrarPago(wash.Id, 45, "Transferencia");
        var persisted = (await washes.ObtenerLavado(wash.Id))!;
        Assert.Equal(115m, persisted.Pago!.MontoPagado);
        Assert.Equal("Pagado", persisted.Pago.Estado);
        Assert.Equal(new[] { 70m, 45m }, persisted.Pago.Pagos.Select(p => p.Monto));
        var after = (await reference.GetSnapshotAsync()).ToDictionary();
        after.Remove("Pago");
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before), System.Text.Json.JsonSerializer.Serialize(after));
    }

    [LocalFirestoreFact]
    public async Task UpdatingInProgressWashRejectsMissingAssignedStaffWithoutWrites()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-wash-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var services = new ServicioService(db);
        var washes = new LavadoService(db, services, new PaqueteServicioService(db, services, cache), new PersonalService(db), new ConfiguracionService(db, cache));
        var wash = new Lavado { Id = "wash", Estado = "EnProceso", ClienteId = "client", VehiculoId = "vehicle", Precio = 100, FechaCreacion = DateTime.UtcNow, EmpleadosAsignadosIds = new() { "staff" }, Servicios = new() { new ServicioEnLavado { ServicioId = "service", Estado = "Pendiente" } } };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => washes.ActualizarLavado(wash));
        Assert.Equal("El empleado asignado no está activo o ya está ocupado.", error.Message);
        Assert.False((await db.Collection("lavados").Document(wash.Id).GetSnapshotAsync()).Exists);
        Assert.False((await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).Exists);
    }

    [LocalFirestoreFact]
    public async Task PickupIndependentlyPreservesUnrelatedFields()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-wash-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var services = new ServicioService(db);
        var washes = new LavadoService(db, services, new PaqueteServicioService(db, services, cache), new PersonalService(db), new ConfiguracionService(db, cache));
        var wash = new Lavado { Id = "wash", Estado = "Cancelado", ClienteId = "client", VehiculoId = "vehicle", Precio = 100, FechaCreacion = DateTime.UtcNow, EmpleadosAsignadosIds = new() { "staff" }, Servicios = new() { new ServicioEnLavado { ServicioId = "service", Estado = "Pendiente" } } };
        await washes.ActualizarLavado(wash);
        var reference = db.Collection("lavados").Document(wash.Id);
        await reference.UpdateAsync("CampoAjeno", "conservar");
        var before = (await reference.GetSnapshotAsync()).ToDictionary();
        before.Remove("EstadoRetiro"); before.Remove("FechaRetiro");
        var result = await washes.RegistrarRetiro(wash.Id);
        Assert.Null(result.Error);
        var persisted = (await washes.ObtenerLavado(wash.Id))!;
        Assert.Equal(Lavado.EstadosRetiro.Retirado, persisted.EstadoRetiro);
        Assert.NotNull(persisted.FechaRetiro);
        var after = (await reference.GetSnapshotAsync()).ToDictionary();
        after.Remove("EstadoRetiro"); after.Remove("FechaRetiro");
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before), System.Text.Json.JsonSerializer.Serialize(after));
    }

    [LocalFirestoreFact]
    public async Task PaymentAndPickupPreserveUnrelatedPersistedFieldsAndStaff()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-wash-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var services = new ServicioService(db);
        var washes = new LavadoService(db, services, new PaqueteServicioService(db, services, cache), new PersonalService(db), new ConfiguracionService(db, cache));
        var wash = new Lavado { Id = "wash", Estado = "EnProceso", ClienteId = "client", VehiculoId = "vehicle", Precio = 100, FechaCreacion = DateTime.UtcNow, EmpleadosAsignadosIds = new() { "staff" }, EmpleadosAsignadosNombres = new() { "Staff" }, Servicios = new() { new ServicioEnLavado { ServicioId = "service", Estado = "Pendiente" } } };
        await db.Collection("empleados").Document("staff").SetAsync(new { Estado = "Activo" });
        await washes.ActualizarLavado(wash);
        var reference = db.Collection("lavados").Document(wash.Id);
        await reference.UpdateAsync("CampoAjeno", "conservar");
        await washes.RegistrarPago(wash.Id, 100, "Efectivo");
        var afterPayment = await reference.GetSnapshotAsync();
        Assert.Equal("conservar", afterPayment.GetValue<string>("CampoAjeno"));
        Assert.Equal(new[] { "staff" }, (await washes.ObtenerLavado(wash.Id))!.EmpleadosAsignadosIds);
        await washes.RegistrarRetiro(wash.Id);
        var afterPickup = await reference.GetSnapshotAsync();
        Assert.Equal("conservar", afterPickup.GetValue<string>("CampoAjeno"));
        var persisted = (await washes.ObtenerLavado(wash.Id))!;
        Assert.Equal(new[] { "staff" }, persisted.EmpleadosAsignadosIds);
        Assert.Equal("Pendiente", persisted.Servicios.Single().Estado);
        Assert.Equal("Pagado", persisted.Pago!.Estado);
        Assert.Equal(Lavado.EstadosRetiro.Retirado, persisted.EstadoRetiro);
    }

    [LocalFirestoreFact]
    public async Task Concurrencia_PagosSimultaneosActualizanSaldoExactoYPermitenRetiro()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-wash-concur-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var services = new ServicioService(db);
        var washes = new LavadoService(db, services, new PaqueteServicioService(db, services, cache), new PersonalService(db), new ConfiguracionService(db, cache));
        var wash = new Lavado
        {
            Id = "wash-concur-race",
            Estado = "EnProceso",
            ClienteId = "client-concur",
            VehiculoId = "vehicle-concur",
            Precio = 300m,
            FechaCreacion = DateTime.UtcNow,
            EmpleadosAsignadosIds = new() { "staff-concur" },
            EmpleadosAsignadosNombres = new() { "Staff Concur" },
            Servicios = new() { new ServicioEnLavado { ServicioId = "service-concur", Estado = "Pendiente" } }
        };
        await db.Collection("empleados").Document("staff-concur").SetAsync(new { Estado = "Activo" });
        await washes.ActualizarLavado(wash);

        // Tres pagos simultáneos de $100 que compiten en transacción de Firestore
        var task1 = washes.RegistrarPago(wash.Id, 100m, "Efectivo", "pago-1");
        var task2 = washes.RegistrarPago(wash.Id, 100m, "Transferencia", "pago-2");
        var task3 = washes.RegistrarPago(wash.Id, 100m, "Debito", "pago-3");

        await Task.WhenAll(task1, task2, task3);

        var persisted = (await washes.ObtenerLavado(wash.Id))!;
        Assert.NotNull(persisted.Pago);
        Assert.Equal(300m, persisted.Pago.MontoPagado);
        Assert.Equal("Pagado", persisted.Pago.Estado);
        Assert.Equal(3, persisted.Pago.Pagos.Count);
        Assert.Equal(300m, persisted.Pago.Pagos.Sum(p => p.Monto));

        // Con saldo cancelado exactamente en concurrencia, el retiro se habilita y registra
        var resultadoRetiro = await washes.RegistrarRetiro(wash.Id);
        Assert.Null(resultadoRetiro.Error);

        var finalWash = (await washes.ObtenerLavado(wash.Id))!;
        Assert.Equal(Lavado.EstadosRetiro.Retirado, finalWash.EstadoRetiro);
        Assert.NotNull(finalWash.FechaRetiro);
    }
}

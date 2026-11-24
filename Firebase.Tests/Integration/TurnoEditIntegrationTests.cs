using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;

namespace Firebase.Tests.Integration;

public class TurnoEditIntegrationTests
{
    [LocalFirestoreFact]
    public async Task NonCouponServiceEditRebuildsFromCurrentCatalog()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-edit-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("empleados").Document("e").SetAsync(new { Estado = "Activo" });
        await db.Collection("clientes").Document("c").SetAsync(new { Id = "c" });
        await db.Collection("vehiculos").Document("v").SetAsync(new Vehiculo { Id = "v", Patente = "ABC123", TipoVehiculo = "Auto", Marca = "a", Modelo = "b", Color = "c", ClienteId = "c" });
        await db.Collection("tiposVehiculos").Document("t").SetAsync(new TipoVehiculo { Id = "t", Nombre = "Auto", FormatoPatente = "lllnnn", CantidadEmpleadosRequeridos = 1 });
        await db.Collection("servicios").Document("s").SetAsync(new Servicio { Id = "s", Nombre = "Lavado", Tipo = "Lavado", TipoVehiculo = "Auto", Descripcion = "x", Estado = "Activo", Precio = 100, TiempoEstimado = 30 });
        await db.Collection("servicios").Document("s2").SetAsync(new Servicio { Id = "s2", Nombre = "Otro", Tipo = "Lavado", TipoVehiculo = "Auto", Descripcion = "x", Estado = "Activo", Precio = 200, TiempoEstimado = 45 });
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion { CapacidadMaximaConcurrente = 1, HorariosOperacion = new() { ["Martes"] = "09:00-18:00" } });
        var start = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var service = new TurnoService(db);
        var request = new TurnoService.SnapshotRequest("id", "c", "v", "Web", "create", start, false, [new("s", null)]);
        Assert.True(await service.Reservar(request));
        Assert.True(await service.Editar(new(request with { Servicios = [new("s2", null)] }, "rebuild")));
        var edited = (await db.Collection("turnos").Document("id").GetSnapshotAsync()).ConvertTo<Turno>();
        Assert.Equal(200m, edited.PrecioAcordado);
        Assert.Equal("s2", Assert.Single(edited.Vehiculos[0].Servicios).ServicioId);
        Assert.Equal(start.AddMinutes(45), edited.FinUtc);
    }

    [LocalFirestoreFact]
    public async Task EditPreservesFrozenSnapshotAndRejectsConflictsWithoutWrites()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-edit-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("empleados").Document("e").SetAsync(new { Estado = "Activo" });
        await db.Collection("clientes").Document("c").SetAsync(new { Id = "c" });
        await db.Collection("vehiculos").Document("v").SetAsync(new Vehiculo { Id = "v", Patente = "ABC123", TipoVehiculo = "Auto", Marca = "a", Modelo = "b", Color = "c", ClienteId = "c" });
        await db.Collection("tiposVehiculos").Document("t").SetAsync(new TipoVehiculo { Id = "t", Nombre = "Auto", FormatoPatente = "lllnnn", CantidadEmpleadosRequeridos = 1 });
        await db.Collection("servicios").Document("s").SetAsync(new Servicio { Id = "s", Nombre = "Lavado", Tipo = "Lavado", TipoVehiculo = "Auto", Descripcion = "x", Estado = "Activo", Precio = 100, TiempoEstimado = 30 });
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion { CapacidadMaximaConcurrente = 1, HorariosOperacion = new() { ["Martes"] = "09:00-18:00" } });
        var start = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var service = new TurnoService(db);
        var request = new TurnoService.SnapshotRequest("id", "c", "v", "Web", "create", start, false, [new("s", null)]);
        Assert.True(await service.Reservar(request));
        var unchanged = await db.Collection("turnos").Document("id").GetSnapshotAsync();
        Assert.True(await service.Editar(new TurnoService.EditRequest(request, "noop")));
        Assert.Equal(unchanged.UpdateTime, (await unchanged.Reference.GetSnapshotAsync()).UpdateTime);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Editar(new TurnoService.EditRequest(request with { AceptaAdelanto = true }, "noop")));
        await db.Collection("servicios").Document("s").UpdateAsync(new Dictionary<string, object> { ["Precio"] = 999d, ["Estado"] = "Inactivo" });
        var edit = new TurnoService.EditRequest(request with { InicioUtc = start.AddMinutes(15), AceptaAdelanto = true }, "edit");
        Assert.True(await service.Editar(edit));
        var reference = db.Collection("turnos").Document("id");
        var before = await reference.GetSnapshotAsync();
        Assert.Equal(100m, before.ConvertTo<Turno>().PrecioAcordado);
        Assert.Equal(start.AddMinutes(15), before.ConvertTo<Turno>().InicioUtc);
        Assert.True(await service.Editar(edit));
        Assert.Equal(before.UpdateTime, (await reference.GetSnapshotAsync()).UpdateTime);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Editar(edit with { Solicitud = edit.Solicitud with { InicioUtc = start.AddMinutes(30) } }));
        Assert.Equal(before.UpdateTime, (await reference.GetSnapshotAsync()).UpdateTime);
    }
}

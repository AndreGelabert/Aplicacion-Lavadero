using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Caching.Memory;

namespace Firebase.Tests.Integration;

public class TurnoEnProcesoCapacityIntegrationTests
{
    [LocalFirestoreFact]
    public async Task BookingAndEditRespectStartedIntervalAndIgnoreCompleted()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-cap-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("empleados").Document("e").SetAsync(new { Estado = "Activo" });
        await db.Collection("clientes").Document("c").SetAsync(new { Id = "c" });
        await db.Collection("vehiculos").Document("v").SetAsync(new Vehiculo { Id = "v", Patente = "ABC123", TipoVehiculo = "Auto", Marca = "a", Modelo = "b", Color = "c", ClienteId = "c" });
        await db.Collection("tiposVehiculos").Document("t").SetAsync(new TipoVehiculo { Id = "t", Nombre = "Auto", FormatoPatente = "lllnnn", CantidadEmpleadosRequeridos = 1 });
        await db.Collection("servicios").Document("s").SetAsync(new Servicio { Id = "s", Nombre = "Lavado", Tipo = "Lavado", TipoVehiculo = "Auto", Descripcion = "x", Estado = "Activo", Precio = 100, TiempoEstimado = 30 });
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion { CapacidadMaximaConcurrente = 1, ToleranciaLlegadaTurnosMinutos = 15, HorariosOperacion = new() { ["Martes"] = "09:00-18:00" } });
        var start = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var service = new TurnoService(db);
        var first = new TurnoService.SnapshotRequest("first", "c", "v", "Web", "first", start, false, [new("s", null)]);
        Assert.True(await service.Reservar(first));
        var other = first with { Id = "other", ClaveIdempotencia = "other", InicioUtc = start.AddHours(1) };
        Assert.True(await service.Reservar(other));
        await db.Collection("turnos").Document("first").UpdateAsync(new Dictionary<string, object> { ["Estado"] = "EnProceso", ["LavadoId"] = "wash" });
        await db.Collection("lavados").Document("wash").SetAsync(new { Id = "wash", Estado = "EnProceso", TurnoId = "first" });
        var before = await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
        Assert.False(await service.Reservar(first with { Id = "blocked", ClaveIdempotencia = "blocked" }));
        Assert.False(await service.Editar(new(other with { InicioUtc = start }, "edit-blocked")));
        Assert.False((await db.Collection("turnos").Document("blocked").GetSnapshotAsync()).Exists);
        Assert.False((await db.Collection("turno_ediciones").GetSnapshotAsync()).Documents.Any());
        Assert.Equal(before.UpdateTime, (await before.Reference.GetSnapshotAsync()).UpdateTime);
        await db.Collection("turnos").Document("first").UpdateAsync("Estado", "Completado");
        Assert.True(await service.Editar(new(other with { InicioUtc = start }, "edit-allowed")));
    }

    [LocalFirestoreFact]
    public async Task LinkedWashMustExistAndBeReciprocalAndActiveForBookingAndEdit()
    {
        foreach (var scenario in new[] { "missing", "mismatch", "completed" })
        {
            var (db, service, first, other) = await SetupTwo();
            var wash = db.Collection("lavados").Document("wash");
            await db.Collection("turnos").Document("first").UpdateAsync(new Dictionary<string, object> { ["Estado"] = "EnProceso", ["LavadoId"] = "wash" });
            if (scenario != "missing") await wash.SetAsync(new { TurnoId = scenario == "mismatch" ? "other" : "first", Estado = scenario == "completed" ? "Realizado" : "EnProceso" });
            var coordinator = await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
            var original = await db.Collection("turnos").Document("other").GetSnapshotAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.Reservar(first with { Id = "new", ClaveIdempotencia = "new" }));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.Editar(new(other with { InicioUtc = first.InicioUtc }, "bad-edit")));
            Assert.False((await db.Collection("turnos").Document("new").GetSnapshotAsync()).Exists);
            Assert.Empty((await db.Collection("turno_ediciones").GetSnapshotAsync()).Documents);
            Assert.Equal(original.UpdateTime, (await original.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(coordinator.UpdateTime, (await coordinator.Reference.GetSnapshotAsync()).UpdateTime);
        }
    }

    [LocalFirestoreFact]
    public async Task ValidStartedTurnCountsOnceAtCapacityTwo()
    {
        var (db, service, first, _) = await SetupTwo();
        await db.Collection("empleados").Document("e2").SetAsync(new { Estado = "Activo" });
        await db.Collection("configuracion").Document("system_config").UpdateAsync("CapacidadMaximaConcurrente", 2);
        await db.Collection("turnos").Document("first").UpdateAsync(new Dictionary<string, object> { ["Estado"] = "EnProceso", ["LavadoId"] = "wash" });
        await db.Collection("lavados").Document("wash").SetAsync(new { TurnoId = "first", Estado = "EnProceso" });
        Assert.True(await service.Reservar(first with { Id = "new", ClaveIdempotencia = "new" }));
        Assert.False(await service.Reservar(first with { Id = "overflow", ClaveIdempotencia = "overflow" }));
    }

    [LocalFirestoreFact]
    public async Task EachCompletionPathAdvancesCoordinatorOnceAndReplayPreservesIt()
    {
        foreach (var path in new[] { "wash", "service", "stage" })
        {
            var db = new FirestoreDbBuilder { ProjectId = "demo-close-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var services = new ServicioService(db);
            var washes = new LavadoService(db, services, new PaqueteServicioService(db, services, cache), new PersonalService(db), new ConfiguracionService(db, cache));
            var s = new ServicioEnLavado { ServicioId = "s", Estado = path == "wash" ? "Realizado" : "EnProceso" };
            if (path == "stage") s.Etapas.Add(new EtapaEnLavado { EtapaId = "e", Estado = "EnProceso" });
            await washes.ActualizarLavado(new Lavado { Id = "wash", Estado = "EnProceso", ClienteId = "c", VehiculoId = "v", FechaCreacion = DateTime.UtcNow, Servicios = new() { s } });
            await db.Collection("lavados").Document("wash").UpdateAsync("TurnoId", "turno");
            await db.Collection("turnos").Document("turno").SetAsync(new { Estado = "EnProceso", LavadoId = "wash" });
            var coordinator = db.Collection("coordinacion").Document("turnos");
            var version = (await coordinator.GetSnapshotAsync()).GetValue<long>("Version");
            async Task Finish() { if (path == "wash") await washes.FinalizarLavado("wash"); else if (path == "service") await washes.FinalizarServicio("wash", "s"); else await washes.FinalizarEtapa("wash", "s", "e"); }
            await Finish();
            var after = await coordinator.GetSnapshotAsync();
            Assert.Equal(version + 1, after.GetValue<long>("Version"));
            Assert.Equal("Completado", (await db.Collection("turnos").Document("turno").GetSnapshotAsync()).GetValue<string>("Estado"));
            await Finish();
            Assert.Equal(after.UpdateTime, (await coordinator.GetSnapshotAsync()).UpdateTime);
        }
    }

    [LocalFirestoreFact]
    public async Task CompletionRacingBookingHasConsistentOutcome()
    {
        var (db, service, first, _) = await SetupTwo();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var services = new ServicioService(db);
        var washes = new LavadoService(db, services, new PaqueteServicioService(db, services, cache), new PersonalService(db), new ConfiguracionService(db, cache));
        await washes.ActualizarLavado(new Lavado { Id = "wash", Estado = "EnProceso", ClienteId = "c", VehiculoId = "v", FechaCreacion = DateTime.UtcNow, Servicios = new() { new ServicioEnLavado { ServicioId = "s", Estado = "Realizado" } } });
        await db.Collection("lavados").Document("wash").UpdateAsync("TurnoId", "first");
        await db.Collection("turnos").Document("first").UpdateAsync(new Dictionary<string, object> { ["Estado"] = "EnProceso", ["LavadoId"] = "wash" });
        var coordinator = db.Collection("coordinacion").Document("turnos");
        var version = (await coordinator.GetSnapshotAsync()).GetValue<long>("Version");
        var booking = service.Reservar(first with { Id = "race", ClaveIdempotencia = "race" });
        var completion = washes.FinalizarLavado("wash");
        await Task.WhenAll(booking, completion);
        Assert.Equal("Completado", (await db.Collection("turnos").Document("first").GetSnapshotAsync()).GetValue<string>("Estado"));
        Assert.Equal(booking.Result, (await db.Collection("turnos").Document("race").GetSnapshotAsync()).Exists);
        Assert.Equal(version + 1 + (booking.Result ? 1 : 0), (await coordinator.GetSnapshotAsync()).GetValue<long>("Version"));
    }

    private static async Task<(FirestoreDb db, TurnoService service, TurnoService.SnapshotRequest first, TurnoService.SnapshotRequest other)> SetupTwo()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-cap-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("empleados").Document("e").SetAsync(new { Estado = "Activo" });
        await db.Collection("clientes").Document("c").SetAsync(new { Id = "c" });
        await db.Collection("vehiculos").Document("v").SetAsync(new Vehiculo { Id = "v", Patente = "ABC123", TipoVehiculo = "Auto", Marca = "a", Modelo = "b", Color = "c", ClienteId = "c" });
        await db.Collection("tiposVehiculos").Document("t").SetAsync(new TipoVehiculo { Id = "t", Nombre = "Auto", FormatoPatente = "lllnnn", CantidadEmpleadosRequeridos = 1 });
        await db.Collection("servicios").Document("s").SetAsync(new Servicio { Id = "s", Nombre = "Lavado", Tipo = "Lavado", TipoVehiculo = "Auto", Descripcion = "x", Estado = "Activo", Precio = 100, TiempoEstimado = 30 });
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion { CapacidadMaximaConcurrente = 1, ToleranciaLlegadaTurnosMinutos = 15, HorariosOperacion = new() { ["Martes"] = "09:00-18:00" } });
        var first = new TurnoService.SnapshotRequest("first", "c", "v", "Web", "first", new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc), false, [new("s", null)]);
        var other = first with { Id = "other", ClaveIdempotencia = "other", InicioUtc = first.InicioUtc.AddHours(1) };
        var service = new TurnoService(db);
        Assert.True(await service.Reservar(first));
        Assert.True(await service.Reservar(other));
        return (db, service, first, other);
    }

    [LocalFirestoreFact]
    public async Task MalformedStartedAppointmentFailsClosed()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-cap-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("empleados").Document("e").SetAsync(new { Estado = "Activo" });
        await db.Collection("clientes").Document("c").SetAsync(new { Id = "c" });
        await db.Collection("vehiculos").Document("v").SetAsync(new Vehiculo { Id = "v", Patente = "ABC123", TipoVehiculo = "Auto", Marca = "a", Modelo = "b", Color = "c", ClienteId = "c" });
        await db.Collection("tiposVehiculos").Document("t").SetAsync(new TipoVehiculo { Id = "t", Nombre = "Auto", FormatoPatente = "lllnnn", CantidadEmpleadosRequeridos = 1 });
        await db.Collection("servicios").Document("s").SetAsync(new Servicio { Id = "s", Nombre = "Lavado", Tipo = "Lavado", TipoVehiculo = "Auto", Descripcion = "x", Estado = "Activo", Precio = 100, TiempoEstimado = 30 });
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion { CapacidadMaximaConcurrente = 1, ToleranciaLlegadaTurnosMinutos = 15, HorariosOperacion = new() { ["Martes"] = "09:00-18:00" } });
        await db.Collection("turnos").Document("broken").SetAsync(new { Estado = "EnProceso", LavadoId = "wash" });
        var request = new TurnoService.SnapshotRequest("new", "c", "v", "Web", "new", new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc), false, [new("s", null)]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new TurnoService(db).Reservar(request));
        Assert.False((await db.Collection("turnos").Document("new").GetSnapshotAsync()).Exists);
        Assert.False((await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).Exists);
    }
}

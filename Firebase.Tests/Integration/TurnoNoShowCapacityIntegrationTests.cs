using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Firebase.Tests.Integration;

public class TurnoNoShowCapacityIntegrationTests
{
    [LocalFirestoreFact]
    public async Task SweepReleasesCapacityOnlyAfterConfiguredDeadline()
    {
        var (db, service, first) = await Setup();
        Assert.True(await service.Reservar(first));
        var turno = db.Collection("turnos").Document(first.Id);
        var coordinator = db.Collection("coordinacion").Document("turnos");
        var before = await turno.GetSnapshotAsync();
        var version = await coordinator.GetSnapshotAsync();
        var sweep = new TurnoNoShowSweep(db, service, NullLogger<TurnoNoShowSweep>.Instance);
        var replacement = first with { Id = "replacement", ClaveIdempotencia = "replacement" };

        Assert.Null(await sweep.RunBatchAsync(() => first.InicioUtc.AddMinutes(15), 10, null, CancellationToken.None));
        Assert.Equal(before.UpdateTime, (await turno.GetSnapshotAsync()).UpdateTime);
        Assert.False(await service.Reservar(replacement));
        Assert.False((await db.Collection("turnos").Document(replacement.Id).GetSnapshotAsync()).Exists);
        Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        Assert.Equal(version.UpdateTime, (await coordinator.GetSnapshotAsync()).UpdateTime);

        Assert.Null(await sweep.RunBatchAsync(() => first.InicioUtc.AddMinutes(15).AddSeconds(1), 10, null, CancellationToken.None));
        Assert.Equal("Ausente", (await turno.GetSnapshotAsync()).GetValue<string>("Estado"));
        Assert.True(await service.Reservar(replacement));
        Assert.Equal("Pendiente", (await db.Collection("turnos").Document(replacement.Id).GetSnapshotAsync()).GetValue<string>("Estado"));
        Assert.Equal(2, (await db.Collection("turno_solicitudes").GetSnapshotAsync()).Count);
        Assert.Empty((await db.Collection("turno_cupones").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task SweepDoesNotCancelStartedWashOrReleaseItsCapacity()
    {
        var (db, service, first) = await Setup();
        Assert.True(await service.Reservar(first));
        var turno = db.Collection("turnos").Document(first.Id);
        var wash = db.Collection("lavados").Document("wash");
        await turno.UpdateAsync(new Dictionary<string, object> { ["Estado"] = "EnProceso", ["LavadoId"] = "wash" });
        await wash.SetAsync(new { TurnoId = first.Id, Estado = "EnProceso" });
        var beforeTurno = await turno.GetSnapshotAsync();
        var beforeWash = await wash.GetSnapshotAsync();
        var coordinator = db.Collection("coordinacion").Document("turnos");
        var beforeCoordinator = await coordinator.GetSnapshotAsync();
        var sweep = new TurnoNoShowSweep(db, service, NullLogger<TurnoNoShowSweep>.Instance);

        Assert.Null(await sweep.RunBatchAsync(() => first.InicioUtc.AddHours(1), 10, null, CancellationToken.None));
        Assert.False(await service.Reservar(first with { Id = "blocked", ClaveIdempotencia = "blocked" }));
        Assert.Equal(beforeTurno.UpdateTime, (await turno.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(beforeWash.UpdateTime, (await wash.GetSnapshotAsync()).UpdateTime);
        Assert.Equal("EnProceso", (await wash.GetSnapshotAsync()).GetValue<string>("Estado"));
        Assert.Equal(beforeCoordinator.UpdateTime, (await coordinator.GetSnapshotAsync()).UpdateTime);
        Assert.False((await db.Collection("turnos").Document("blocked").GetSnapshotAsync()).Exists);
        Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
    }

    private static async Task<(FirestoreDb db, TurnoService service, TurnoService.SnapshotRequest first)> Setup()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-noshow-cap-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("empleados").Document("e").SetAsync(new { Estado = "Activo" });
        await db.Collection("clientes").Document("c").SetAsync(new { Id = "c" });
        await db.Collection("vehiculos").Document("v").SetAsync(new Vehiculo { Id = "v", Patente = "ABC123", TipoVehiculo = "Auto", Marca = "a", Modelo = "b", Color = "c", ClienteId = "c" });
        await db.Collection("tiposVehiculos").Document("t").SetAsync(new TipoVehiculo { Id = "t", Nombre = "Auto", FormatoPatente = "lllnnn", CantidadEmpleadosRequeridos = 1 });
        await db.Collection("servicios").Document("s").SetAsync(new Servicio { Id = "s", Nombre = "Lavado", Tipo = "Lavado", TipoVehiculo = "Auto", Descripcion = "x", Estado = "Activo", Precio = 100, TiempoEstimado = 30 });
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion { CapacidadMaximaConcurrente = 1, ToleranciaLlegadaTurnosMinutos = 15, HorariosOperacion = new() { ["Martes"] = "09:00-18:00" } });
        var first = new TurnoService.SnapshotRequest("first", "c", "v", "Web", "first", new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc), false, [new("s", null)]);
        return (db, new TurnoService(db), first);
    }
}

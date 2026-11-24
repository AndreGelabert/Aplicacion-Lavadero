using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Firebase.Tests.Integration;

public class NoShowRepeatedSweepTests
{
    [LocalFirestoreFact]
    public async Task RepeatedSweepLeavesReleasedAppointmentAndCoordinatorUnchanged()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-repeat-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("empleados").Document("e").SetAsync(new { Estado = "Activo" });
        await db.Collection("clientes").Document("c").SetAsync(new { Id = "c" });
        await db.Collection("vehiculos").Document("v").SetAsync(new Vehiculo { Id = "v", Patente = "ABC123", TipoVehiculo = "Auto", Marca = "a", Modelo = "b", Color = "c", ClienteId = "c" });
        await db.Collection("tiposVehiculos").Document("t").SetAsync(new TipoVehiculo { Id = "t", Nombre = "Auto", FormatoPatente = "lllnnn", CantidadEmpleadosRequeridos = 1 });
        await db.Collection("servicios").Document("s").SetAsync(new Servicio { Id = "s", Nombre = "Lavado", Tipo = "Lavado", TipoVehiculo = "Auto", Descripcion = "x", Estado = "Activo", Precio = 100, TiempoEstimado = 30 });
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion { CapacidadMaximaConcurrente = 1, ToleranciaLlegadaTurnosMinutos = 15, HorariosOperacion = new() { ["Martes"] = "09:00-18:00" } });
        var first = new TurnoService.SnapshotRequest("first", "c", "v", "Web", "first", new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc), false, [new("s", null)]);
        var service = new TurnoService(db);
        Assert.True(await service.Reservar(first));
        var sweep = new TurnoNoShowSweep(db, service, NullLogger<TurnoNoShowSweep>.Instance);
        DateTime Clock() => first.InicioUtc.AddMinutes(16);
        Assert.Null(await sweep.RunBatchAsync(Clock, 10, null, CancellationToken.None));
        var appointment = db.Collection("turnos").Document(first.Id);
        var coordinator = db.Collection("coordinacion").Document("turnos");
        var released = await appointment.GetSnapshotAsync();
        var coordinated = await coordinator.GetSnapshotAsync();
        Assert.Equal("Ausente", released.GetValue<string>("Estado"));
        Assert.True(coordinated.Exists);

        Assert.Null(await sweep.RunBatchAsync(Clock, 10, null, CancellationToken.None));
        var repeated = await appointment.GetSnapshotAsync();
        var repeatedCoordinator = await coordinator.GetSnapshotAsync();
        Assert.Equal(released.UpdateTime, repeated.UpdateTime);
        Assert.Equal(coordinated.UpdateTime, repeatedCoordinator.UpdateTime);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(released.ToDictionary()), System.Text.Json.JsonSerializer.Serialize(repeated.ToDictionary()));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(coordinated.ToDictionary()), System.Text.Json.JsonSerializer.Serialize(repeatedCoordinator.ToDictionary()));
        Assert.Empty((await db.Collection("turno_cupones").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);

        var replacement = first with { Id = "replacement", ClaveIdempotencia = "replacement" };
        Assert.True(await service.Reservar(replacement));
        Assert.Equal("Pendiente", (await db.Collection("turnos").Document(replacement.Id).GetSnapshotAsync()).GetValue<string>("Estado"));
        Assert.Empty((await db.Collection("turno_cupones").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);
    }
}

using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;

namespace Firebase.Tests.Integration;

public class TurnoBookingToleranceIntegrationTests
{
    [LocalFirestoreFact]
    public async Task FreshBookingsRequireValidToleranceButReplayDoesNot()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-tolerance-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("empleados").Document("e").SetAsync(new { Estado = "Activo" });
        await db.Collection("clientes").Document("c").SetAsync(new { Id = "c" });
        await db.Collection("vehiculos").Document("v").SetAsync(new Vehiculo { Id = "v", ClienteId = "c", TipoVehiculo = "Auto", Patente = "ABC123", Marca = "a", Modelo = "b", Color = "c" });
        await db.Collection("tiposVehiculos").Document("t").SetAsync(new TipoVehiculo { Id = "t", Nombre = "Auto", FormatoPatente = "lllnnn", CantidadEmpleadosRequeridos = 1 });
        await db.Collection("servicios").Document("s").SetAsync(new Servicio { Id = "s", Nombre = "Lavado", Tipo = "Lavado", TipoVehiculo = "Auto", Descripcion = "x", Estado = "Activo", Precio = 123, TiempoEstimado = 30 });
        var config = db.Collection("configuracion").Document("system_config");
        await config.SetAsync(new Configuracion { CapacidadMaximaConcurrente = 1, HorariosOperacion = new() { ["Martes"] = "09:00-18:00" } });
        var service = new TurnoService(db);
        var request = new TurnoService.SnapshotRequest("id", "c", "v", "Web", "key", new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc), false, [new("s", null)]);
        async Task<string[]> State()
        {
            var result = new List<string>();
            foreach (var name in new[] { "turnos", "turno_solicitudes", "coordinacion" })
                result.AddRange((await db.Collection(name).GetSnapshotAsync()).Documents.Select(d => $"{name}/{d.Id}:{d.UpdateTime}:{System.Text.Json.JsonSerializer.Serialize(d.ToDictionary())}"));
            return result.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        }
        foreach (var invalid in new object?[] { FieldValue.Delete, null, -1L, 525601L, "15", 1.5d })
        {
            await config.UpdateAsync("ToleranciaLlegadaTurnosMinutos", invalid);
            var before = await State();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.Reservar(request));
            Assert.Equal("Tolerancia de llegada no disponible.", error.Message);
            Assert.Equal(before, await State());
        }
        await config.UpdateAsync("ToleranciaLlegadaTurnosMinutos", 0);
        Assert.True(await service.Reservar(request));
        Assert.Single((await db.Collection("turnos").GetSnapshotAsync()).Documents);
        Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        var saved = await State();
        foreach (var invalid in new object?[] { FieldValue.Delete, null, -1L, 525601L, "15", 1.5d })
        {
            await config.UpdateAsync("ToleranciaLlegadaTurnosMinutos", invalid);
            Assert.True(await service.Reservar(request));
            Assert.Equal(saved, await State());
        }
    }
}

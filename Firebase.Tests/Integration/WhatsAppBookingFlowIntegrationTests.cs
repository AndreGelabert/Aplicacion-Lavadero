using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using System.Text.Json;

namespace Firebase.Tests.Integration;

public class WhatsAppBookingFlowIntegrationTests
{
    private static async Task<(FirestoreDb db, TurnoService service, TurnoService.SnapshotRequest request, TurnoService.BookingAuthorization auth)> Setup()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-wa-book-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("empleados").Document("e").SetAsync(new { Estado = "Activo" });
        await db.Collection("clientes").Document("c").SetAsync(new { Id = "c" });
        await db.Collection("vehiculos").Document("v").SetAsync(new Vehiculo { Id = "v", Patente = "ABC123", TipoVehiculo = "Auto", Marca = "a", Modelo = "b", Color = "c", ClienteId = "c" });
        await db.Collection("tiposVehiculos").Document("t").SetAsync(new TipoVehiculo { Id = "t", Nombre = "Auto", FormatoPatente = "lllnnn", CantidadEmpleadosRequeridos = 1 });
        await db.Collection("servicios").Document("s").SetAsync(new Servicio { Id = "s", Nombre = "Lavado", Tipo = "Lavado", TipoVehiculo = "Auto", Descripcion = "x", Estado = "Activo", Precio = 100, TiempoEstimado = 30 });
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion { CapacidadMaximaConcurrente = 1, ToleranciaLlegadaTurnosMinutos = 10, HorariosOperacion = new() { ["Martes"] = "09:00-18:00" } });
        var request = new TurnoService.SnapshotRequest("id", "c", "v", "WhatsApp", "key", new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc), false, [new("s", null)]);
        var auth = new TurnoService.BookingAuthorization("5491112345678", "inc", 4, "token", 10);
        await db.Collection("whatsapp_sessions").Document(auth.Phone).SetAsync(new { Id = auth.Phone, ClienteId = "c", IncarnationId = auth.Incarnation, Revision = auth.Revision, CurrentState = "TURNO_RESERVA_CONFIRMAR", SelectionId = auth.Selection, BookingRequest = JsonSerializer.Serialize(request), BookingTolerance = 10 });
        return (db, new TurnoService(db), request, auth);
    }

    [LocalFirestoreFact]
    public async Task CapabilityAndToleranceAreAtomicAndReplayIsMarkerFirst()
    {
        var (db, service, request, auth) = await Setup();
        Assert.True(await service.ReservarWhatsApp(request, auth));
        var appointment = await db.Collection("turnos").Document("id").GetSnapshotAsync();
        var marker = Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        var capacity = await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
        var session = await db.Collection("whatsapp_sessions").Document(auth.Phone).GetSnapshotAsync();
        Assert.Equal("MENU_CLIENTE_AUTENTICADO", session.GetValue<string>("CurrentState"));
        Assert.Equal(5L, session.GetValue<long>("Revision"));
        await db.Collection("configuracion").Document("system_config").UpdateAsync("ToleranciaLlegadaTurnosMinutos", 20);
        Assert.True(await service.ReservarWhatsApp(request, auth));
        Assert.Equal(appointment.UpdateTime, (await appointment.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(marker.UpdateTime, (await marker.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(capacity.UpdateTime, (await capacity.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(session.UpdateTime, (await session.Reference.GetSnapshotAsync()).UpdateTime);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReservarWhatsApp(request with { VehiculoId = "other" }, auth));
    }

    [LocalFirestoreFact]
    public async Task StaleCapabilityOrToleranceRejectsWithoutWrites()
    {
        foreach (var field in new[] { "ClienteId", "IncarnationId", "Revision", "CurrentState", "SelectionId", "BookingRequest", "BookingTolerance", "config" })
        {
            var (db, service, request, auth) = await Setup();
            var session = db.Collection("whatsapp_sessions").Document(auth.Phone);
            var config = db.Collection("configuracion").Document("system_config");
            if (field == "config") await config.UpdateAsync("ToleranciaLlegadaTurnosMinutos", 11);
            else await session.UpdateAsync(field, field is "Revision" or "BookingTolerance" ? (object)11 : "wrong");
            var before = await session.GetSnapshotAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReservarWhatsApp(request, auth));
            Assert.Equal(before.UpdateTime, (await session.GetSnapshotAsync()).UpdateTime);
            Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
            Assert.Empty((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
            Assert.False((await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).Exists);
            Assert.False((await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync()).Exists);
        }
    }

    [LocalFirestoreFact]
    public async Task ExistingWebReservationRemainsSupported()
    {
        var (db, service, request, _) = await Setup();
        Assert.True(await service.Reservar(request with { Origen = "Web" }));
        Assert.True((await db.Collection("turnos").Document("id").GetSnapshotAsync()).Exists);
    }
}

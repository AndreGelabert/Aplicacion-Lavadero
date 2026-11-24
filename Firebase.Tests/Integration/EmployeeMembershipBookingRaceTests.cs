using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using System.Security.Cryptography;
using System.Text;

namespace Firebase.Tests.Integration;

public class EmployeeMembershipBookingRaceTests
{
    private static async Task<(FirestoreDb db, TurnoService booking, PersonalService staff, TurnoService.SnapshotRequest request)> Setup()
    {
        Assert.Equal("127.0.0.1:8190", Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST"));
        var db = new FirestoreDbBuilder { ProjectId = "demo-race-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("empleados").Document("0").SetAsync(new { Estado = "Activo" });
        await db.Collection("clientes").Document("c").SetAsync(new { Id = "c" });
        await db.Collection("vehiculos").Document("v").SetAsync(new Vehiculo { Id = "v", Patente = "ABC123", TipoVehiculo = "Auto", Marca = "a", Modelo = "b", Color = "c", ClienteId = "c" });
        await db.Collection("tiposVehiculos").Document("t").SetAsync(new TipoVehiculo { Id = "t", Nombre = "Auto", FormatoPatente = "lllnnn", CantidadEmpleadosRequeridos = 1 });
        await db.Collection("servicios").Document("s").SetAsync(new Servicio { Id = "s", Nombre = "Lavado", Tipo = "Lavado", TipoVehiculo = "Auto", Descripcion = "x", Estado = "Activo", Precio = 100, TiempoEstimado = 30 });
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion { CapacidadMaximaConcurrente = 1, ToleranciaLlegadaTurnosMinutos = 15, HorariosOperacion = new() { ["Martes"] = "09:00-18:00" } });
        var request = new TurnoService.SnapshotRequest("id", "c", "v", "Web", "key", new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc), false, [new("s", null)]);
        return (db, new TurnoService(db), new PersonalService(db), request);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MembershipAndBookingSerializeAtVersionBoundary(bool deactivateFirst)
    {
        var (db, booking, staff, request) = await Setup();
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Pause() { reached.TrySetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(20)); }
        Task<bool>? reservation = null;
        Task? deactivation = null;
        try
        {
            if (deactivateFirst)
            {
                booking.BeforeMembershipRead = Pause;
                reservation = booking.Reservar(request);
                await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
                deactivation = staff.CambiarEstadoEmpleado("0", "Inactivo");
                await deactivation.WaitAsync(TimeSpan.FromSeconds(20));
                release.TrySetResult();
            }
            else
            {
                staff.BeforeMembershipTransaction = Pause;
                deactivation = staff.CambiarEstadoEmpleado("0", "Inactivo");
                await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
                reservation = booking.Reservar(request);
                Assert.True(await reservation.WaitAsync(TimeSpan.FromSeconds(20)));
                release.TrySetResult();
            }
            var accepted = await reservation.WaitAsync(TimeSpan.FromSeconds(20));
            await deactivation.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal(!deactivateFirst, accepted);
            Assert.Equal("Inactivo", (await db.Collection("empleados").Document("0").GetSnapshotAsync()).GetValue<string>("Estado"));
            var appointments = await db.Collection("turnos").GetSnapshotAsync();
            var markers = await db.Collection("turno_solicitudes").GetSnapshotAsync();
            var capacity = await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
            var membership = await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync();
            Assert.Equal(accepted ? 1 : 0, appointments.Count);
            Assert.Equal(accepted ? 1 : 0, markers.Count);
            Assert.Equal(accepted, capacity.Exists);
            Assert.Equal(accepted ? 2L : 1L, membership.GetValue<long>("Version"));
            if (accepted)
            {
                var appointment = Assert.Single(appointments.Documents).ConvertTo<Turno>();
                Assert.Equal(request.Id, appointment.Id);
                Assert.Equal("Pendiente", appointment.Estado);
                Assert.Equal(1, appointment.Vehiculos.Single().CantidadEmpleadosRequeridos);
                Assert.Equal(1L, capacity.GetValue<long>("Version"));
                var marker = Assert.Single(markers.Documents);
                Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.ClaveIdempotencia))), marker.Id);
                Assert.Equal("ids-v2", marker.GetValue<string>("Esquema"));
                Assert.Equal(System.Text.Json.JsonSerializer.Serialize(request), marker.GetValue<string>("Solicitud"));
            }
        }
        finally
        {
            release.TrySetResult();
            if (reservation != null) try { await reservation.WaitAsync(TimeSpan.FromSeconds(20)); } catch { }
            if (deactivation != null) try { await deactivation.WaitAsync(TimeSpan.FromSeconds(20)); } catch { }
        }
    }
}

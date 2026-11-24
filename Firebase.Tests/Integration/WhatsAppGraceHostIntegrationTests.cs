using System.Net;
using System.Text.Json;
using Firebase.BackgroundServices;
using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Firebase.Tests.Integration;

public class WhatsAppGraceHostIntegrationTests
{
    private const string Phone = "5491112345678";

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public List<string> Messages { get; } = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Messages.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    [LocalFirestoreFact]
    public async Task RunningRegisteredSchedulerDisclosesGraceBeforeRealReservation()
    {
        var (db, host, handler) = await Setup();
        using (host)
        {
            await host.StartAsync();
            try
            {
                using var scope = host.Services.CreateScope();
                var flow = scope.ServiceProvider.GetRequiredService<WhatsAppFlowService>();
                var sessions = scope.ServiceProvider.GetRequiredService<WhatsAppSessionService>();
                Assert.True(flow.BookingSchedulerEnabled());
                await Draft(flow, sessions);
                var confirmation = await sessions.GetOrCreateSession(Phone);
                Assert.Equal("TURNO_RESERVA_CONFIRMAR", confirmation.CurrentState);
                Assert.Equal(23, confirmation.BookingTolerance);
                var disclosure = JsonDocument.Parse(handler.Messages.Last()).RootElement.GetProperty("text").GetProperty("body").GetString()!;
                Assert.Contains("23 minutos", disclosure);
                Assert.Contains("libera automáticamente", disclosure);
                Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
                Assert.Empty((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
                await flow.ProcessMessage(Phone, "si");
                Assert.Contains("Tu turno fue reservado", handler.Messages.Last());
                var appointment = Assert.Single((await db.Collection("turnos").GetSnapshotAsync()).Documents);
                Assert.Equal("owner", appointment.GetValue<string>("ClienteId"));
                Assert.Equal("owned", Assert.Single(appointment.ConvertTo<Turno>().Vehiculos).VehiculoId);
                Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
            }
            finally { await host.StopAsync(); }
        }
    }

    [LocalFirestoreFact]
    public async Task RegisteredButStoppedSchedulerFailsClosed()
    {
        var (db, host, handler) = await Setup();
        using (host)
        using (var scope = host.Services.CreateScope())
        {
            var flow = scope.ServiceProvider.GetRequiredService<WhatsAppFlowService>();
            var sessions = scope.ServiceProvider.GetRequiredService<WhatsAppSessionService>();
            Assert.False(flow.BookingSchedulerEnabled());
            await Draft(flow, sessions);
            var session = await sessions.GetOrCreateSession(Phone);
            Assert.Equal("TURNO_RESERVA_BORRADOR", session.CurrentState);
            Assert.Equal(23, session.BookingTolerance);
            var message = JsonDocument.Parse(handler.Messages.Last()).RootElement.GetProperty("text").GetProperty("body").GetString()!;
            Assert.Contains("23 minutos", message);
            Assert.Contains("no está habilitada", message);
            Assert.Contains("no se reservó", message);
            var before = await db.Collection("whatsapp_sessions").Document(Phone).GetSnapshotAsync();
            await flow.ProcessMessage(Phone, "si");
            Assert.Equal(before.UpdateTime, (await before.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
            Assert.Empty((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
            Assert.False((await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).Exists);
        }
    }

    [LocalFirestoreFact]
    public async Task RegisteredFlowPreservesUpcomingAppointmentsMenu()
    {
        var (_, host, handler) = await Setup();
        using (host)
        using (var scope = host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<WhatsAppFlowService>().ProcessMessage(Phone, "mis_turnos");
            var message = JsonDocument.Parse(Assert.Single(handler.Messages)).RootElement.GetProperty("text").GetProperty("body").GetString();
            Assert.Contains("No tienes próximos turnos", message);
            Assert.Equal("MENU_CLIENTE_AUTENTICADO", (await scope.ServiceProvider.GetRequiredService<WhatsAppSessionService>().GetOrCreateSession(Phone)).CurrentState);
        }
    }

    private static async Task Draft(WhatsAppFlowService flow, WhatsAppSessionService sessions)
    {
        await flow.ProcessMessage(Phone, "reservar_turno");
        await flow.ProcessMessage(Phone, Assert.Single((await sessions.GetOrCreateSession(Phone)).BookingChoices!).Key);
        await flow.ProcessMessage(Phone, Assert.Single((await sessions.GetOrCreateSession(Phone)).BookingChoices!).Key);
        await flow.ProcessMessage(Phone, "01/01/2030 10:00");
        await flow.ProcessMessage(Phone, "si");
    }

    private static async Task<(FirestoreDb, IHost, CaptureHandler)> Setup()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-wa-host-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("clientes").Document("owner").SetAsync(new { Id = "owner" });
        await db.Collection("vehiculos").Document("owned").SetAsync(new { ClienteId = "owner", Patente = "ABC123", Estado = "Activo", TipoVehiculo = "Auto" });
        await db.Collection("empleados").Document("e").SetAsync(new { Estado = "Activo" });
        await db.Collection("tiposVehiculos").Document("t").SetAsync(new TipoVehiculo { Id = "t", Nombre = "Auto", FormatoPatente = "lllnnn", CantidadEmpleadosRequeridos = 1 });
        await db.Collection("servicios").Document("s").SetAsync(new Servicio { Id = "s", Nombre = "Lavado", Tipo = "Lavado", TipoVehiculo = "Auto", Estado = "Activo", Descripcion = "x", Precio = 100, TiempoEstimado = 30 });
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion { CapacidadMaximaConcurrente = 1, ToleranciaLlegadaTurnosMinutos = 23, HorariosOperacion = new() { ["Martes"] = "09:00-18:00" } });
        var sessions = new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance);
        await sessions.GetOrCreateSession(Phone);
        await sessions.AssociateClienteToSession(Phone, "owner");
        var handler = new CaptureHandler();
        var services = new ServicioService(db);
        var packages = new PaqueteServicioService(db, services, new MemoryCache(new MemoryCacheOptions()));
        var host = new HostBuilder().ConfigureServices(registration =>
        {
            registration.AddSingleton(db);
            registration.AddSingleton(sessions);
            registration.AddSingleton(new ClienteService(db));
            registration.AddSingleton(new VehiculoService(db));
            registration.AddSingleton(new TipoDocumentoService(db));
            registration.AddSingleton(new TipoVehiculoService(db));
            registration.AddSingleton(new LavaderoInfoService(null!, null!, NullLogger<LavaderoInfoService>.Instance));
            registration.AddSingleton(Mock.Of<ICarQueryService>());
            registration.AddSingleton(new MetaWhatsAppService(new HttpClient(handler), Options.Create(new WhatsAppSettings { VerifyToken = "synthetic", PhoneNumberId = "synthetic", AccessToken = "synthetic" }), NullLogger<MetaWhatsAppService>.Instance));
            registration.AddSingleton<ITurnoConsultaService>(new TurnoConsultaService(db));
            registration.AddSingleton<ITurnoSelectorService>(new TurnoSelectorService(null!, null!, services, packages));
            registration.AddScoped<TurnoService>();
            registration.AddScoped<TurnoNoShowSweep>();
            registration.AddScoped(provider => new TurnoWhatsAppBookingAdapter(provider.GetRequiredService<TurnoService>()));
            TurnoHostRegistrations.AddWhatsAppFlow(registration);
            TurnoHostRegistrations.AddBackgroundServices(registration);
        }).Build();
        return (db, host, handler);
    }
}

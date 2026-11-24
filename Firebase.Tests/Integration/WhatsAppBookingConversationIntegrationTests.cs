using System.Net;
using System.Text.Json;
using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Firebase.Tests.Integration;

public class WhatsAppBookingConversationIntegrationTests
{
    private sealed class Handler : HttpMessageHandler
    {
        public List<string> Messages { get; } = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Messages.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    [LocalFirestoreFact]
    public async Task AuthenticatedCustomerCanStartBookingWithOwnedVehiclesOnly()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-wa-conv-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        const string phone = "5491112345678";
        var sessions = new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance);
        await sessions.GetOrCreateSession(phone);
        await sessions.AssociateClienteToSession(phone, "owner");
        await db.Collection("vehiculos").Document("owned").SetAsync(new { ClienteId = "owner", Patente = "ABC123", Estado = "Activo", TipoVehiculo = "Auto" });
        await db.Collection("vehiculos").Document("other").SetAsync(new { ClienteId = "other", Patente = "XYZ999", Estado = "Activo", TipoVehiculo = "Auto" });
        var handler = new Handler();
        var meta = new MetaWhatsAppService(new HttpClient(handler), Options.Create(new WhatsAppSettings { AccessToken = "test", VerifyToken = "test", PhoneNumberId = "123" }), NullLogger<MetaWhatsAppService>.Instance);
        var flow = new WhatsAppFlowService(sessions, meta, null!, null!, null!, null!, null!, null!, NullLogger<WhatsAppFlowService>.Instance, new TurnoConsultaService(db), db);
        await flow.ProcessMessage(phone, "reservar_turno");
        using var message = JsonDocument.Parse(Assert.Single(handler.Messages));
        Assert.Contains("ABC123", message.RootElement.ToString());
        Assert.DoesNotContain("XYZ999", message.RootElement.ToString());
        var session = await sessions.GetOrCreateSession(phone);
        Assert.Equal("TURNO_RESERVA_VEHICULO", session.CurrentState);
        var choices = Assert.IsType<Dictionary<string, string>>(session.BookingChoices);
        var choice = Assert.Single(choices);
        Assert.Equal("owned", choice.Value);
        Assert.NotEqual("owned", choice.Key);
        var row = message.RootElement.GetProperty("interactive").GetProperty("action").GetProperty("sections")[0].GetProperty("rows")[0];
        Assert.Equal(choice.Key, row.GetProperty("id").GetString());
        Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task SelectingOwnedVehicleShowsCompatibleServicesAndPersistsDraft()
    {
        var (db, sessions, flow, handler) = await SetupSelection();
        await flow.ProcessMessage("5491112345678", "reservar_turno");
        var before = await sessions.GetOrCreateSession("5491112345678");
        var choice = Assert.Single(before.BookingChoices!).Key;
        await flow.ProcessMessage("5491112345678", choice);
        using var message = JsonDocument.Parse(handler.Messages.Last());
        Assert.Contains("Lavado", message.RootElement.ToString());
        Assert.DoesNotContain("Moto", message.RootElement.ToString());
        var after = await sessions.GetOrCreateSession("5491112345678");
        Assert.Equal("TURNO_RESERVA_SERVICIO", after.CurrentState);
        Assert.Contains("owned", after.BookingRequest!);
        Assert.Single(after.BookingChoices!);
        Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task ForgedStaleAndReassignedVehicleChoicesNeverWrite()
    {
        foreach (var mode in new[] { "forged", "stale", "owner", "inactive" })
        {
            var (db, sessions, flow, handler) = await SetupSelection();
            const string phone = "5491112345678";
            await flow.ProcessMessage(phone, "reservar_turno");
            var choice = Assert.Single((await sessions.GetOrCreateSession(phone)).BookingChoices!).Key;
            if (mode == "owner") await db.Collection("vehiculos").Document("owned").UpdateAsync("ClienteId", "other");
            if (mode == "inactive") await db.Collection("vehiculos").Document("owned").UpdateAsync("Estado", "Inactivo");
            if (mode == "stale") await sessions.UpdateSessionState(phone, "MENU_CLIENTE_AUTENTICADO");
            var before = await db.Collection("whatsapp_sessions").Document(phone).GetSnapshotAsync();
            await flow.ProcessMessage(phone, mode == "forged" ? "forged" : choice);
            Assert.Equal(before.UpdateTime, (await before.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
            Assert.DoesNotContain("Lavado", handler.Messages.Last());
        }
    }

    [LocalFirestoreFact]
    public async Task CatalogPagesExposeMixedChoicesAndRejectStaleNavigation()
    {
        var (db, sessions, flow, handler) = await SetupSelection();
        const string phone = "5491112345678";
        for (var i = 0; i < 15; i++)
            await db.Collection("servicios").Document($"extra{i:D2}").SetAsync(new Servicio { Id = $"extra{i:D2}", Nombre = $"Servicio {i:D2}", Tipo = "Lavado", TipoVehiculo = "Auto", Estado = "Activo", Descripcion = "x", Precio = 100, TiempoEstimado = 30 });
        await db.Collection("paquetes_servicios").Document("valid").SetAsync(new PaqueteServicio { Id = "valid", Nombre = "Paquete valido", Estado = "Activo", TipoVehiculo = "Auto", ServiciosIds = new() { "s" }, PorcentajeDescuento = 10 });
        await db.Collection("paquetes_servicios").Document("invalid").SetAsync(new PaqueteServicio { Id = "invalid", Nombre = "Paquete invalido", Estado = "Activo", TipoVehiculo = "Auto", ServiciosIds = new() { "missing" }, PorcentajeDescuento = 10 });
        await flow.ProcessMessage(phone, "reservar_turno");
        await flow.ProcessMessage(phone, Assert.Single((await sessions.GetOrCreateSession(phone)).BookingChoices!).Key);
        var seen = new HashSet<string>();
        string? previous = null;
        for (var page = 0; page < 10; page++)
        {
            var session = await sessions.GetOrCreateSession(phone);
            using var message = JsonDocument.Parse(handler.Messages.Last());
            var rows = message.RootElement.GetProperty("interactive").GetProperty("action").GetProperty("sections")[0].GetProperty("rows");
            Assert.InRange(rows.GetArrayLength(), 1, 10);
            Assert.Equal(rows.GetArrayLength(), session.BookingChoices!.Count);
            foreach (var row in rows.EnumerateArray())
                if (row.GetProperty("title").GetString() != "Siguiente") Assert.True(seen.Add(row.GetProperty("title").GetString()!));
            var next = rows.EnumerateArray().FirstOrDefault(r => r.GetProperty("title").GetString() == "Siguiente");
            if (next.ValueKind == JsonValueKind.Undefined) break;
            var token = next.GetProperty("id").GetString()!;
            var before = await db.Collection("whatsapp_sessions").Document(phone).GetSnapshotAsync();
            await flow.ProcessMessage(phone, previous ?? "forged");
            Assert.Equal(before.UpdateTime, (await before.Reference.GetSnapshotAsync()).UpdateTime);
            previous = token;
            await flow.ProcessMessage(phone, token);
        }
        Assert.Equal(17, seen.Count);
        Assert.Contains("Paquete valido", seen);
        Assert.DoesNotContain("Paquete invalido", seen);
        Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
    }

    private static async Task<(FirestoreDb, WhatsAppSessionService, WhatsAppFlowService, Handler)> SetupSelection()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-wa-sel-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var sessions = new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance);
        await sessions.GetOrCreateSession("5491112345678");
        await sessions.AssociateClienteToSession("5491112345678", "owner");
        await db.Collection("vehiculos").Document("owned").SetAsync(new { ClienteId = "owner", Patente = "ABC123", Estado = "Activo", TipoVehiculo = "Auto" });
        foreach (var (id, name, type) in new[] { ("s", "Lavado", "Auto"), ("m", "Moto", "Moto") })
            await db.Collection("servicios").Document(id).SetAsync(new Servicio { Id = id, Nombre = name, Tipo = "Lavado", TipoVehiculo = type, Estado = "Activo", Descripcion = "x", Precio = 100, TiempoEstimado = 30 });
        var handler = new Handler();
        var meta = new MetaWhatsAppService(new HttpClient(handler), Options.Create(new WhatsAppSettings { AccessToken = "test", VerifyToken = "test", PhoneNumberId = "123" }), NullLogger<MetaWhatsAppService>.Instance);
        var services = new ServicioService(db);
        var packages = new PaqueteServicioService(db, services, new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));
        var selector = new TurnoSelectorService(null!, null!, services, packages);
        var flow = new WhatsAppFlowService(sessions, meta, null!, null!, null!, null!, null!, null!, NullLogger<WhatsAppFlowService>.Instance, new TurnoConsultaService(db), db, selector);
        return (db, sessions, flow, handler);
    }

    [LocalFirestoreFact]
    public async Task VehiclePagesReachEligibleRecordsAfterInactiveAndDeduplicateSharedOwners()
    {
        var (db, sessions, flow, handler) = await SetupSelection();
        const string phone = "5491112345678";
        await db.Collection("vehiculos").Document("owned").UpdateAsync("Estado", "Inactivo");
        for (var i = 0; i < 23; i++)
            await db.Collection("vehiculos").Document($"a{i:D2}").SetAsync(new { ClienteId = "owner", ClientesIds = new[] { "owner" }, Estado = i < 11 ? "Inactivo" : "Activo", Patente = $"ABC{i:D3}", TipoVehiculo = "Auto" });
        await flow.ProcessMessage(phone, "reservar_turno");
        var found = new HashSet<string>();
        string? previous = null;
        for (var page = 0; page < 5; page++)
        {
            var session = await sessions.GetOrCreateSession(phone);
            using var message = JsonDocument.Parse(handler.Messages.Last());
            var rows = message.RootElement.GetProperty("interactive").GetProperty("action").GetProperty("sections")[0].GetProperty("rows");
            foreach (var row in rows.EnumerateArray())
                if (row.GetProperty("title").GetString()!.StartsWith("ABC")) Assert.True(found.Add(row.GetProperty("title").GetString()!));
            var next = rows.EnumerateArray().FirstOrDefault(r => r.GetProperty("title").GetString() == "Siguiente");
            if (next.ValueKind == JsonValueKind.Undefined) break;
            var token = next.GetProperty("id").GetString()!;
            if (previous != null)
            {
                var before = await db.Collection("whatsapp_sessions").Document(phone).GetSnapshotAsync();
                await flow.ProcessMessage(phone, previous);
                Assert.Equal(before.UpdateTime, (await before.Reference.GetSnapshotAsync()).UpdateTime);
            }
            previous = token;
            await flow.ProcessMessage(phone, token);
        }
        Assert.Equal(12, found.Count);
    }

    [LocalFirestoreFact]
    public async Task ServiceChoicePromptsForLocalDateAndRejectsForgedChoiceWithoutWrites()
    {
        var (db, sessions, flow, handler) = await SetupSelection();
        const string phone = "5491112345678";
        await flow.ProcessMessage(phone, "reservar_turno");
        await flow.ProcessMessage(phone, Assert.Single((await sessions.GetOrCreateSession(phone)).BookingChoices!).Key);
        var session = await sessions.GetOrCreateSession(phone);
        var token = Assert.Single(session.BookingChoices!).Key;
        var before = await db.Collection("whatsapp_sessions").Document(phone).GetSnapshotAsync();
        await flow.ProcessMessage(phone, "forged");
        Assert.Equal(before.UpdateTime, (await before.Reference.GetSnapshotAsync()).UpdateTime);
        await flow.ProcessMessage(phone, token);
        var after = await sessions.GetOrCreateSession(phone);
        Assert.Equal("TURNO_RESERVA_FECHA", after.CurrentState);
        Assert.Contains("owned", after.BookingRequest!);
        Assert.Contains("\"s\"", after.BookingRequest!);
        using var message = JsonDocument.Parse(handler.Messages.Last());
        Assert.Contains("fecha", message.RootElement.GetProperty("text").GetProperty("body").GetString(), StringComparison.OrdinalIgnoreCase);
        var snapshot = await before.Reference.GetSnapshotAsync();
        await flow.ProcessMessage(phone, token);
        Assert.Equal(snapshot.UpdateTime, (await before.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task DateInputFailsClosedWhenConfirmationCannotBeOffered()
    {
        var (db, sessions, flow, handler) = await SetupSelection();
        const string phone = "5491112345678";
        await flow.ProcessMessage(phone, "reservar_turno");
        await flow.ProcessMessage(phone, Assert.Single((await sessions.GetOrCreateSession(phone)).BookingChoices!).Key);
        await flow.ProcessMessage(phone, Assert.Single((await sessions.GetOrCreateSession(phone)).BookingChoices!).Key);
        var reference = db.Collection("whatsapp_sessions").Document(phone);
        var before = await reference.GetSnapshotAsync();
        foreach (var input in new[] { "1/01/2030 10:00", "31/02/2030 10:00", "01/01/2030 10:00 UTC" })
        {
            await flow.ProcessMessage(phone, input);
            Assert.Equal(before.UpdateTime, (await reference.GetSnapshotAsync()).UpdateTime);
            Assert.Contains("DD/MM/AAAA HH:MM", handler.Messages.Last());
        }
        await flow.ProcessMessage(phone, "01/01/2030 10:00");
        var dated = await sessions.GetOrCreateSession(phone);
        Assert.Equal("TURNO_RESERVA_ADELANTO", dated.CurrentState);
        Assert.Contains("2030-01-01T10:00:00", dated.BookingRequest!);
        var datedSnapshot = await reference.GetSnapshotAsync();
        await flow.ProcessMessage(phone, "forged");
        Assert.Equal(datedSnapshot.UpdateTime, (await reference.GetSnapshotAsync()).UpdateTime);
        await flow.ProcessMessage(phone, "si");
        Assert.Equal(datedSnapshot.UpdateTime, (await reference.GetSnapshotAsync()).UpdateTime);
        using var unavailable = JsonDocument.Parse(handler.Messages.Last());
        Assert.Contains("no está disponible", unavailable.RootElement.GetProperty("text").GetProperty("body").GetString());
        Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task PreferenceSavesSafeDraftAndDisclosesConfiguredToleranceWithoutClaimingRelease()
    {
        foreach (var preference in new[] { "si", "no" })
        {
            var (db, sessions, flow, handler) = await SetupSelection();
            const string phone = "5491112345678";
            await db.Collection("configuracion").Document("system_config").SetAsync(new { ToleranciaLlegadaTurnosMinutos = 23 });
            await flow.ProcessMessage(phone, "reservar_turno");
            await flow.ProcessMessage(phone, Assert.Single((await sessions.GetOrCreateSession(phone)).BookingChoices!).Key);
            await flow.ProcessMessage(phone, Assert.Single((await sessions.GetOrCreateSession(phone)).BookingChoices!).Key);
            await flow.ProcessMessage(phone, "01/01/2030 10:00");
            await flow.ProcessMessage(phone, preference);
            var session = await sessions.GetOrCreateSession(phone);
            Assert.Equal("TURNO_RESERVA_BORRADOR", session.CurrentState);
            Assert.Equal(23, session.BookingTolerance);
            Assert.Contains(preference == "si" ? "\"AceptaAdelanto\":true" : "\"AceptaAdelanto\":false", session.BookingRequest!);
            var message = JsonDocument.Parse(handler.Messages.Last()).RootElement.GetProperty("text").GetProperty("body").GetString()!;
            Assert.Contains("23 minutos", message);
            Assert.Contains("no está habilitada", message);
            Assert.Contains("no se reservó", message);
            var before = await db.Collection("whatsapp_sessions").Document(phone).GetSnapshotAsync();
            await flow.ProcessMessage(phone, preference);
            Assert.Equal(before.UpdateTime, (await before.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
        }
    }

    [LocalFirestoreFact]
    public async Task EnabledSchedulerOffersConfirmationAndDelegatesOnlyAfterConsent()
    {
        var (db, sessions, flow, handler) = await SetupSelection();
        const string phone = "5491112345678";
        await db.Collection("configuracion").Document("system_config").SetAsync(new { ToleranciaLlegadaTurnosMinutos = 23 });
        var calls = 0;
        flow.BookingSchedulerEnabled = () => true;
        flow.BookingAdapter = new TurnoWhatsAppBookingAdapter((_, _) => { calls++; return Task.FromResult(false); });
        await flow.ProcessMessage(phone, "reservar_turno");
        await flow.ProcessMessage(phone, Assert.Single((await sessions.GetOrCreateSession(phone)).BookingChoices!).Key);
        await flow.ProcessMessage(phone, Assert.Single((await sessions.GetOrCreateSession(phone)).BookingChoices!).Key);
        await flow.ProcessMessage(phone, "01/01/2030 10:00");
        await flow.ProcessMessage(phone, "si");
        Assert.Equal("TURNO_RESERVA_CONFIRMAR", (await sessions.GetOrCreateSession(phone)).CurrentState);
        Assert.Contains("23 minutos", handler.Messages.Last());
        Assert.Equal(0, calls);
        await flow.ProcessMessage(phone, "si");
        Assert.Equal(1, calls);
        Assert.Contains("disponibilidad", handler.Messages.Last());
        Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task ConversationBooksForSessionOwnerAndRealAdapterReplayDoesNotDoubleBook()
    {
        var (db, sessions, flow, handler) = await SetupSelection();
        const string phone = "5491112345678";
        await db.Collection("clientes").Document("owner").SetAsync(new { Id = "owner" });
        await db.Collection("empleados").Document("e").SetAsync(new { Estado = "Activo" });
        await db.Collection("tiposVehiculos").Document("t").SetAsync(new TipoVehiculo { Id = "t", Nombre = "Auto", FormatoPatente = "lllnnn", CantidadEmpleadosRequeridos = 1 });
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion { CapacidadMaximaConcurrente = 1, ToleranciaLlegadaTurnosMinutos = 23, HorariosOperacion = new() { ["Martes"] = "09:00-18:00" } });
        var adapter = new TurnoWhatsAppBookingAdapter(new TurnoService(db));
        flow.BookingAdapter = adapter;
        flow.BookingSchedulerEnabled = () => true;
        await flow.ProcessMessage(phone, "reservar_turno");
        await flow.ProcessMessage(phone, Assert.Single((await sessions.GetOrCreateSession(phone)).BookingChoices!).Key);
        await flow.ProcessMessage(phone, Assert.Single((await sessions.GetOrCreateSession(phone)).BookingChoices!).Key);
        await flow.ProcessMessage(phone, "01/01/2030 10:00");
        await flow.ProcessMessage(phone, "si");
        var confirmation = await sessions.GetOrCreateSession(phone);
        Assert.Equal("owner", confirmation.ClienteId);
        Assert.Equal(23, confirmation.BookingTolerance);
        var disclosure = JsonDocument.Parse(handler.Messages.Last()).RootElement.GetProperty("text").GetProperty("body").GetString()!;
        Assert.Contains("23 minutos", disclosure);
        Assert.Contains("libera automáticamente", disclosure);
        Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
        var command = JsonSerializer.Deserialize<TurnoService.SnapshotRequest>(confirmation.BookingRequest!)!;
        await flow.ProcessMessage(phone, "si");
        Assert.Contains("Tu turno fue reservado", handler.Messages.Last());
        var appointment = Assert.Single((await db.Collection("turnos").GetSnapshotAsync()).Documents);
        Assert.Equal(command.Id, appointment.Id);
        Assert.Equal("owner", appointment.GetValue<string>("ClienteId"));
        Assert.Equal("owned", Assert.Single(appointment.ConvertTo<Turno>().Vehiculos).VehiculoId);
        var marker = Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        var capacity = await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
        var sessionAfter = await db.Collection("whatsapp_sessions").Document(phone).GetSnapshotAsync();
        Assert.Equal("owner", sessionAfter.GetValue<string>("ClienteId"));
        Assert.Equal("MENU_CLIENTE_AUTENTICADO", sessionAfter.GetValue<string>("CurrentState"));
        var replay = new TurnoWhatsAppBookingRequest(command.Id, command.ClaveIdempotencia, command.VehiculoId,
            new DateTime(2030, 1, 1, 10, 0, 0), command.AceptaAdelanto,
            command.Servicios.Select(s => new TurnoWhatsAppBookingSelection(s.ServicioId, s.PaqueteId)).ToArray());
        Assert.True(await adapter.Reservar(confirmation, replay));
        Assert.Single((await db.Collection("turnos").GetSnapshotAsync()).Documents);
        Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        Assert.Equal(appointment.UpdateTime, (await appointment.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(marker.UpdateTime, (await marker.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(capacity.UpdateTime, (await capacity.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(sessionAfter.UpdateTime, (await sessionAfter.Reference.GetSnapshotAsync()).UpdateTime);
    }

    [LocalFirestoreFact]
    public async Task ExistingUpcomingAppointmentsMenuStillWorks()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-wa-pres-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        const string phone = "5491112345678";
        var sessions = new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance);
        await sessions.GetOrCreateSession(phone);
        await sessions.AssociateClienteToSession(phone, "owner");
        var handler = new Handler();
        var meta = new MetaWhatsAppService(new HttpClient(handler), Options.Create(new WhatsAppSettings { AccessToken = "test", VerifyToken = "test", PhoneNumberId = "123" }), NullLogger<MetaWhatsAppService>.Instance);
        var flow = new WhatsAppFlowService(sessions, meta, null!, null!, null!, null!, null!, null!, NullLogger<WhatsAppFlowService>.Instance, new TurnoConsultaService(db), db);
        await flow.ProcessMessage(phone, "mis_turnos");
        using var message = JsonDocument.Parse(Assert.Single(handler.Messages));
        Assert.Contains("No tienes próximos turnos", message.RootElement.GetProperty("text").GetProperty("body").GetString());
        Assert.Equal("MENU_CLIENTE_AUTENTICADO", (await sessions.GetOrCreateSession(phone)).CurrentState);
    }
}

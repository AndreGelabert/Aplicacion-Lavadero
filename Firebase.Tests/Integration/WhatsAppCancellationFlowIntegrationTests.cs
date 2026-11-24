using System.Net;
using System.Text.Json;
using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Firebase.Tests.Integration;

public class WhatsAppCancellationFlowIntegrationTests
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

    private static (WhatsAppFlowService Flow, Handler Handler) CreateFlow(FirestoreDb db, WhatsAppSessionService sessions)
    {
        var handler = new Handler();
        var meta = new MetaWhatsAppService(new HttpClient(handler), Options.Create(new WhatsAppSettings { AccessToken = "test", VerifyToken = "test", PhoneNumberId = "123" }), NullLogger<MetaWhatsAppService>.Instance);
        return (new WhatsAppFlowService(sessions, meta, null!, null!, null!, null!, null!, null!, NullLogger<WhatsAppFlowService>.Instance, new TurnoConsultaService(db), db), handler);
    }

    private static async Task<WhatsAppSessionService> AuthenticatedSession(FirestoreDb db, string phone)
    {
        var sessions = new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance);
        await sessions.GetOrCreateSession(phone);
        await sessions.AssociateClienteToSession(phone, "owner");
        return sessions;
    }

    [LocalFirestoreFact]
    public async Task ConfirmSelectedAppointmentCancelsOnlyItAndCreatesPolicyEffects()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-wa-success-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        const string phone = "5491112345678";
        var sessions = await AuthenticatedSession(db, phone);
        var start = DateTime.UtcNow.AddDays(2);
        foreach (var (id, plate) in new[] { ("one", "ABC123"), ("two", "DEF456") })
        {
            await db.Collection("vehiculos").Document(id).SetAsync(new { ClienteId = "owner", Patente = plate });
            await db.Collection("turnos").Document(id).SetAsync(new Turno { Id = id, ClienteId = "owner", Estado = "Pendiente", InicioUtc = start, FinUtc = start.AddMinutes(30), Vehiculos = new() { new() { VehiculoId = id } } });
        }
        await db.Collection("coordinacion").Document("turnos").SetAsync(new { Version = 7L });
        await db.Collection("configuracion").Document("system_config").SetAsync(new { CancelacionAnticipadaHorasMinimas = 24, CancelacionAnticipadaDescuento = 10d, CancelacionAnticipadaValidezDias = 30 });
        var other = await db.Collection("turnos").Document("two").GetSnapshotAsync();
        var (flow, handler) = CreateFlow(db, sessions);
        await flow.ProcessMessage(phone, "cancelar_turno");
        using var list = JsonDocument.Parse(Assert.Single(handler.Messages));
        var rows = list.RootElement.GetProperty("interactive").GetProperty("action").GetProperty("sections")[0].GetProperty("rows");
        var choice = rows.EnumerateArray().Single(r => r.ToString().Contains("ABC123")).GetProperty("id").GetString()!;
        await flow.ProcessMessage(phone, choice);
        using var confirmation = JsonDocument.Parse(handler.Messages.Last());
        Assert.Contains("ABC123", confirmation.RootElement.GetProperty("interactive").GetProperty("body").GetProperty("text").GetString());
        var token = confirmation.RootElement.GetProperty("interactive").GetProperty("action").GetProperty("buttons")[0].GetProperty("reply").GetProperty("id").GetString()!;
        var beforeSession = await db.Collection("whatsapp_sessions").Document(phone).GetSnapshotAsync();
        await flow.ProcessMessage(phone, token);
        Assert.Contains("Tu turno fue cancelado", handler.Messages.Last());
        var selected = await db.Collection("turnos").Document("one").GetSnapshotAsync();
        Assert.Equal("owner", selected.GetValue<string>("ClienteId"));
        Assert.Equal("Cancelado", selected.GetValue<string>("Estado"));
        Assert.Equal(other.UpdateTime, (await db.Collection("turnos").Document("two").GetSnapshotAsync()).UpdateTime);
        var session = await db.Collection("whatsapp_sessions").Document(phone).GetSnapshotAsync();
        Assert.Equal("owner", session.GetValue<string>("ClienteId"));
        Assert.Equal("MENU_CLIENTE_AUTENTICADO", session.GetValue<string>("CurrentState"));
        Assert.Equal(beforeSession.GetValue<long>("Revision") + 1, session.GetValue<long>("Revision"));
        Assert.False(session.ContainsField("SelectionId"));
        Assert.False(session.ContainsField("CancellationAppointmentId"));
        Assert.False(session.ContainsField("CancellationAppointmentVersion"));
        Assert.Equal(8L, (await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).GetValue<long>("Version"));
        var coupon = Assert.Single((await db.Collection("turno_cupones").GetSnapshotAsync()).Documents);
        Assert.Equal("one", coupon.GetValue<string>("AppointmentId"));
        Assert.Equal("owner", coupon.GetValue<string>("Owner"));
        Assert.Equal  (10d, coupon.GetValue<double>("Percentage"));
        Assert.False(coupon.GetValue<bool>("Consumed"));
        var cascade = Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);
        Assert.Equal("one", cascade.GetValue<string>("CancelledAppointmentId"));
        Assert.Equal("Pendiente", cascade.GetValue<string>("ContinuationState"));
        Assert.Equal(selected.GetValue<DateTime>("InicioUtc"), cascade.GetValue<DateTime>("VacancyStartUtc"));
        Assert.Equal(selected.GetValue<DateTime>("FinUtc"), cascade.GetValue<DateTime>("VacancyEndUtc"));
    }

    [LocalFirestoreFact]
    public async Task ReassignedAndMissingVehiclesDoNotLeakPlateAndLongFallbacksRemainDistinct()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-wa-private-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        const string phone = "5491112345678";
        var sessions = await AuthenticatedSession(db, phone);
        var start = DateTime.UtcNow.AddDays(2);
        var first = new string('x', 59) + "1";
        var second = new string('x', 59) + "2";
        await db.Collection("vehiculos").Document("reassigned").SetAsync(new { ClienteId = "other", ClientesIds = new[] { "other" }, Patente = "SECRET123" });
        foreach (var (id, vehicle) in new[] { (first, "reassigned"), (second, "missing") })
            await db.Collection("turnos").Document(id).SetAsync(new Turno { Id = id, ClienteId = "owner", Estado = "Pendiente", InicioUtc = start, FinUtc = start.AddMinutes(30), Vehiculos = new() { new() { VehiculoId = vehicle } } });
        var (flow, handler) = CreateFlow(db, sessions);
        await flow.ProcessMessage(phone, "cancelar_turno");
        using var list = JsonDocument.Parse(Assert.Single(handler.Messages));
        var rows = list.RootElement.GetProperty("interactive").GetProperty("action").GetProperty("sections")[0].GetProperty("rows");
        Assert.Equal(2, rows.GetArrayLength());
        Assert.DoesNotContain("SECRET123", handler.Messages.Last());
        var descriptions = rows.EnumerateArray().Select(r => r.GetProperty("description").GetString()!).ToArray();
        Assert.Equal(2, descriptions.Distinct().Count());
        Assert.All(descriptions, d => Assert.True(d.Length <= 72));
        Assert.Contains("Turno: " + first, descriptions);
        Assert.Contains("Turno: " + second, descriptions);
        await flow.ProcessMessage(phone, rows[0].GetProperty("id").GetString()!);
        Assert.DoesNotContain("SECRET123", handler.Messages.Last());
        Assert.Contains("Turno: ", handler.Messages.Last());
    }

    [LocalFirestoreFact]
    public async Task SameTimeChoicesIdentifyVehicleAndStaleConfirmationDoesNotCancel()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-wa-flow-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        const string phone = "5491112345678";
        var sessions = new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance);
        await sessions.GetOrCreateSession(phone);
        await sessions.AssociateClienteToSession(phone, "owner");
        var start = DateTime.UtcNow.AddDays(2);
        foreach (var (id, plate) in new[] { ("one", "ABC123"), ("two", "DEF456") })
        {
            await db.Collection("vehiculos").Document(id).SetAsync(new { ClienteId = "owner", ClientesIds = new[] { "owner" }, Patente = plate });
            await db.Collection("turnos").Document(id).SetAsync(new Turno { Id = id, ClienteId = "owner", Estado = "Pendiente", InicioUtc = start, FinUtc = start.AddMinutes(30), Vehiculos = new() { new() { VehiculoId = id } } });
        }
        await db.Collection("coordinacion").Document("turnos").SetAsync(new { Version = 7L });
        var handler = new Handler();
        var meta = new MetaWhatsAppService(new HttpClient(handler), Options.Create(new WhatsAppSettings { AccessToken = "test", VerifyToken = "test", PhoneNumberId = "123" }), NullLogger<MetaWhatsAppService>.Instance);
        var flow = new WhatsAppFlowService(sessions, meta, null!, null!, null!, null!, null!, null!, NullLogger<WhatsAppFlowService>.Instance, new TurnoConsultaService(db), db);
        await flow.ProcessMessage(phone, "cancelar_turno");
        using var list = JsonDocument.Parse(Assert.Single(handler.Messages));
        var rows = list.RootElement.GetProperty("interactive").GetProperty("action").GetProperty("sections")[0].GetProperty("rows");
        Assert.Equal(2, rows.GetArrayLength());
        var first = rows.EnumerateArray().Single(r => r.ToString().Contains("ABC123"));
        Assert.Contains("DEF456", rows.ToString());
        await flow.ProcessMessage(phone, first.GetProperty("id").GetString()!);
        using var confirmation = JsonDocument.Parse(handler.Messages.Last());
        Assert.Contains("ABC123", confirmation.RootElement.GetProperty("interactive").GetProperty("body").GetProperty("text").GetString());
        var token = confirmation.RootElement.GetProperty("interactive").GetProperty("action").GetProperty("buttons")[0].GetProperty("reply").GetProperty("id").GetString()!;
        var selected = db.Collection("turnos").Document("one");
        await selected.UpdateAsync("Notas", "changed");
        var before = await selected.GetSnapshotAsync();
        var other = await db.Collection("turnos").Document("two").GetSnapshotAsync();
        var session = await db.Collection("whatsapp_sessions").Document(phone).GetSnapshotAsync();
        await flow.ProcessMessage(phone, token);
        Assert.Equal(before.UpdateTime, (await selected.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(other.UpdateTime, (await db.Collection("turnos").Document("two").GetSnapshotAsync()).UpdateTime);
        Assert.Equal(session.UpdateTime, (await db.Collection("whatsapp_sessions").Document(phone).GetSnapshotAsync()).UpdateTime);
        Assert.Equal(7L, (await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).GetValue<long>("Version"));
        Assert.Empty((await db.Collection("turno_cupones").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);
        Assert.Contains("No se pudo cancelar", handler.Messages.Last());
    }
}

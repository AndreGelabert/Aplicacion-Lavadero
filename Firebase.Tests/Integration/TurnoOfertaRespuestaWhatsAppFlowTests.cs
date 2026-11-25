using System.Net;
using System.Text.Json;
using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Firebase.Tests.Integration;

public class TurnoOfertaRespuestaWhatsAppFlowTests
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
    public async Task ResponsesAndStaleSessionAreTransactional()
    {
        foreach (var answer in new[] { "SI", "NO", "stale" })
        {
            var db = new FirestoreDbBuilder { ProjectId = "demo-wa-flow-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
            const string phone = "5491112345678";
            var sessions = new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance);
            await sessions.GetOrCreateSession(phone);
            await sessions.AssociateClienteToSession(phone, "owner");
            var now = DateTime.UtcNow;
            now = now.AddTicks(-(now.Ticks % 10));
            var original = now.AddDays(2);
            var proposed = new DateTime(now.Year, now.Month, now.Day, 15, 0, 0, DateTimeKind.Utc).AddDays(1);
            await db.Collection("turnos").Document("appointment").SetAsync(new Turno { Id = "appointment", ClienteId = "owner", Estado = "Pendiente", Origen = "test", ClaveIdempotencia = "appointment", AceptaAdelanto = true, InicioUtc = original, FinUtc = original.AddMinutes(30), Vehiculos = [new() { VehiculoId = "vehicle", CantidadEmpleadosRequeridos = 1, Servicios = [new() { ServicioId = "service", TiempoEstimado = 30 }] }] });
            await db.Collection("empleados").Document("one").SetAsync(new { Estado = "Activo" });
            await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion { CapacidadMaximaConcurrente = 1, HorariosOperacion = new() { ["Miércoles"] = "00:00-23:59", ["Jueves"] = "00:00-23:59", ["Viernes"] = "00:00-23:59", ["Sábado"] = "00:00-23:59", ["Domingo"] = "00:00-23:59", ["Lunes"] = "00:00-23:59", ["Martes"] = "00:00-23:59" } });
            await new TurnoOfertaPersistence(db).Create(new("cascade", "offer", "appointment", now, now.AddMinutes(10), original, proposed));
            var handler = new Handler();
            var meta = new MetaWhatsAppService(new HttpClient(handler), Options.Create(new WhatsAppSettings { AccessToken = "test", VerifyToken = "test", PhoneNumberId = "123" }), NullLogger<MetaWhatsAppService>.Instance);
            var flow = new WhatsAppFlowService(sessions, meta, null!, null!, null!, null!, null!, null!, NullLogger<WhatsAppFlowService>.Instance, new TurnoConsultaService(db), db);
            await flow.ProcessMessage(phone, "ofertas_turno");
            using var list = JsonDocument.Parse(handler.Messages.Last());
            var choice = list.RootElement.GetProperty("interactive").GetProperty("action").GetProperty("sections")[0].GetProperty("rows")[0].GetProperty("id").GetString()!;
            await flow.ProcessMessage(phone, choice);
            var refs = new[] { db.Collection("turnos").Document("appointment"), db.Collection("turno_ofertas").Document("offer"), db.Collection("turno_cascadas").Document("cascade"), db.Collection("turno_ofertas_pendientes").Document("appointment"), db.Collection("coordinacion").Document("turnos"), db.Collection("coordinacion").Document("empleados") };
            if (answer == "stale") flow.AfterOfferSessionRead = async () => await db.Collection("whatsapp_sessions").Document(phone).UpdateAsync("Revision", FieldValue.Increment(1));
            var before = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
            await flow.ProcessMessage(phone, answer == "stale" ? "SI" : answer);
            var after = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
            if (answer == "stale")
            {
                for (var i = 0; i < refs.Length; i++) Assert.Equal(before[i].UpdateTime, after[i].UpdateTime);
                Assert.False((await db.Collection("turno_aceptaciones").Document("offer").GetSnapshotAsync()).Exists);
            }
            else
            {
                Assert.Equal(answer == "SI" ? "Aceptada" : "Rechazada", after[1].GetValue<string>("Estado"));
                Assert.Equal(answer == "SI" ? proposed : original, after[0].GetValue<DateTime>("InicioUtc"));
                Assert.Equal("MENU_CLIENTE_AUTENTICADO", (await db.Collection("whatsapp_sessions").Document(phone).GetSnapshotAsync()).GetValue<string>("CurrentState"));
            }
        }
    }

    [LocalFirestoreFact]
    public async Task OwnedPendingOfferCanBeSelectedButForgedSelectionCannot()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-wa-flow-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        const string phone = "5491112345678";
        var sessions = new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance);
        await sessions.GetOrCreateSession(phone);
        await sessions.AssociateClienteToSession(phone, "owner");
        var now = DateTime.UtcNow;
        now = now.AddTicks(-(now.Ticks % 10));
        var original = now.AddDays(2);
        await db.Collection("turnos").Document("appointment").SetAsync(new Turno { Id = "appointment", ClienteId = "owner", Estado = "Pendiente", Origen = "test", ClaveIdempotencia = "appointment", AceptaAdelanto = true, InicioUtc = original, FinUtc = original.AddMinutes(30), Vehiculos = [new() { VehiculoId = "vehicle", CantidadEmpleadosRequeridos = 1, Servicios = [new() { ServicioId = "service", TiempoEstimado = 30 }] }] });
        await new TurnoOfertaPersistence(db).Create(new("cascade", "offer", "appointment", now, now.AddMinutes(10), original, now.AddDays(1)));
        var handler = new Handler();
        var meta = new MetaWhatsAppService(new HttpClient(handler), Options.Create(new WhatsAppSettings { AccessToken = "test", VerifyToken = "test", PhoneNumberId = "123" }), NullLogger<MetaWhatsAppService>.Instance);
        var flow = new WhatsAppFlowService(sessions, meta, null!, null!, null!, null!, null!, null!, NullLogger<WhatsAppFlowService>.Instance, new TurnoConsultaService(db), db);
        await flow.ProcessMessage(phone, "ofertas_turno");
        using var list = JsonDocument.Parse(handler.Messages.Last());
        var choice = list.RootElement.GetProperty("interactive").GetProperty("action").GetProperty("sections")[0].GetProperty("rows")[0].GetProperty("id").GetString()!;
        var before = await db.Collection("whatsapp_sessions").Document(phone).GetSnapshotAsync();
        await flow.ProcessMessage(phone, "offer");
        Assert.Equal(before.UpdateTime, (await before.Reference.GetSnapshotAsync()).UpdateTime);
        await flow.ProcessMessage(phone, choice);
        var after = await before.Reference.GetSnapshotAsync();
        Assert.Equal("TURNO_OFERTA_RESPUESTA", after.GetValue<string>("CurrentState"));
        Assert.Equal("offer", after.GetValue<string>("SelectionId"));
        Assert.Equal(before.GetValue<long>("Revision") + 1, after.GetValue<long>("Revision"));
        Assert.Equal("Pendiente", (await db.Collection("turno_ofertas").Document("offer").GetSnapshotAsync()).GetValue<string>("Estado"));
    }
}

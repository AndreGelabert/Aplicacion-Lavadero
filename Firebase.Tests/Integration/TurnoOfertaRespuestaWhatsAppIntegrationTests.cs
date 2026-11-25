using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;

namespace Firebase.Tests.Integration;

public class TurnoOfertaRespuestaWhatsAppIntegrationTests
{
    private static readonly DateTime T = new(2030, 1, 8, 12, 0, 0, DateTimeKind.Utc);
    private const string Phone = "5491112345678";
    private static async Task<(FirestoreDb db, TurnoOfertaPersistence offers)> Setup()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-wa-offer-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var booking = new Turno { Id = "appointment", ClienteId = "owner", Estado = "Pendiente", Origen = "test", ClaveIdempotencia = "appointment", AceptaAdelanto = true, InicioUtc = T.AddDays(2), FinUtc = T.AddDays(2).AddMinutes(30), Vehiculos = [new() { VehiculoId = "vehicle", CantidadEmpleadosRequeridos = 1, Servicios = [new() { ServicioId = "service", TiempoEstimado = 30 }] }] };
        await db.Collection("turnos").Document("appointment").SetAsync(booking);
        await db.Collection("empleados").Document("one").SetAsync(new { Estado = "Activo" });
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion { CapacidadMaximaConcurrente = 1, HorariosOperacion = new() { ["Miércoles"] = "00:00-23:59", ["Jueves"] = "00:00-23:59", ["Viernes"] = "00:00-23:59", ["Sábado"] = "00:00-23:59", ["Domingo"] = "00:00-23:59", ["Lunes"] = "00:00-23:59", ["Martes"] = "00:00-23:59" } });
        var offers = new TurnoOfertaPersistence(db);
        await offers.Create(new("cascade", "offer", "appointment", T, T.AddMinutes(10), T.AddDays(2), T.AddDays(1)));
        await db.Collection("whatsapp_sessions").Document(Phone).SetAsync(new { Id = Phone, ClienteId = "owner", IncarnationId = "inc", Revision = 4L, CurrentState = "TURNO_OFERTA_RESPUESTA", SelectionId = "offer" });
        return (db, offers);
    }
    private static TurnoOfertaPersistence.WhatsAppCapability Capability(string incarnation = "inc", string owner = "owner") => new(Phone, owner, incarnation, 4, "offer");
    private static Task Respond(TurnoOfertaPersistence offers, bool accept, TurnoOfertaPersistence.WhatsAppCapability cap) => accept ? offers.AcceptWhatsApp("cascade", "offer", T.AddMinutes(1), cap) : offers.RejectWhatsApp("cascade", "offer", T.AddMinutes(1), cap);
    private static DocumentReference[] Refs(FirestoreDb db) => [db.Collection("whatsapp_sessions").Document(Phone), db.Collection("turnos").Document("appointment"), db.Collection("turno_ofertas").Document("offer"), db.Collection("turno_cascadas").Document("cascade"), db.Collection("turno_ofertas_pendientes").Document("appointment"), db.Collection("turno_aceptaciones").Document("offer"), db.Collection("coordinacion").Document("turnos"), db.Collection("coordinacion").Document("empleados")];
    private static async Task Unchanged(FirestoreDb db, Func<Task> action)
    {
        var refs = Refs(db); var before = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
        var ledger = await db.Collection("turno_ofertas_terminales").GetSnapshotAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(action);
        var after = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
        for (int i = 0; i < refs.Length; i++) { Assert.Equal(before[i].Exists, after[i].Exists); Assert.Equal(before[i].UpdateTime, after[i].UpdateTime); }
        Assert.Equal(ledger.Count, (await db.Collection("turno_ofertas_terminales").GetSnapshotAsync()).Count);
    }
    [LocalFirestoreFact]
    public async Task AuthorizedResponsesConsumeCapabilityAndReplayFailsClosed()
    {
        foreach (var accept in new[] { true, false })
        {
            var (db, offers) = await Setup();
            await Respond(offers, accept, Capability());
            Assert.Equal(accept ? "Aceptada" : "Rechazada", (await db.Collection("turno_ofertas").Document("offer").GetSnapshotAsync()).GetValue<string>("Estado"));
            Assert.Equal(5L, (await db.Collection("whatsapp_sessions").Document(Phone).GetSnapshotAsync()).GetValue<long>("Revision"));
            await Unchanged(db, () => Respond(offers, accept, Capability()));
        }
    }
    [LocalFirestoreFact]
    public async Task ForgeryRecreationAndOwnerMismatchDoNotWrite()
    {
        foreach (var accept in new[] { true, false })
        foreach (var scenario in new[] { "forged", "recreated", "owner", "offer-owner" })
        {
            var (db, offers) = await Setup();
            if (scenario == "recreated") await db.Collection("whatsapp_sessions").Document(Phone).UpdateAsync("IncarnationId", "new");
            if (scenario == "owner") await db.Collection("turnos").Document("appointment").UpdateAsync("ClienteId", "other");
            if (scenario == "offer-owner") await db.Collection("turno_ofertas").Document("offer").UpdateAsync("ClienteId", "other");
            await Unchanged(db, () => Respond(offers, accept, scenario == "forged" ? Capability(owner: "other") : Capability()));
        }
    }
    [LocalFirestoreFact]
    public async Task ExistingNonWhatsAppRejectAndReplayStillWork()
    {
        var (db, offers) = await Setup();
        await offers.Reject("cascade", "offer", T.AddMinutes(1));
        var before = await db.Collection("turno_ofertas").Document("offer").GetSnapshotAsync();
        await offers.Reject("cascade", "offer", T.AddMinutes(1));
        Assert.Equal(before.UpdateTime, (await before.Reference.GetSnapshotAsync()).UpdateTime);
    }
}

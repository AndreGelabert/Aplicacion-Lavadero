using System.Security.Cryptography;
using System.Text;
using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;

namespace Firebase.Tests.Integration;

public class AtomicEmailOutboxIssuanceTests
{
    private static readonly DateTime Now = new(2030, 1, 8, 12, 0, 0, DateTimeKind.Utc);
    private sealed class Sender : IEmailSender
    {
        public Task<ResultadoEnvioEmail> EnviarAsync(EmailMessage message, CancellationToken cancellationToken = default) => throw new Exception("must not send");
    }
    private static async Task<(FirestoreDb db, string id, TurnoOutboxPersistence.BoundClaim claim)> Setup()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-atomic-outbox-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("turnos").Document("appointment").SetAsync(new Turno { Id = "appointment", ClienteId = "client", Estado = "Pendiente", Origen = "test", ClaveIdempotencia = "appointment", AceptaAdelanto = true, InicioUtc = Now.AddDays(2), FinUtc = Now.AddDays(2).AddMinutes(30), Vehiculos = [new() { VehiculoId = "vehicle", CantidadEmpleadosRequeridos = 1, Servicios = [new() { ServicioId = "service", TiempoEstimado = 30 }] }] });
        await db.Collection("clientes").Document("client").SetAsync(new { Id = "client", Estado = "Activo", Email = "client@example.com" });
        await new TurnoOfertaPersistence(db).Create(new("cascade", "offer", "appointment", Now, Now.AddMinutes(10), Now.AddDays(2), Now.AddDays(1)));
        var id = TurnoOutboxPersistence.OfferIntentId("offer", TurnoOutboxPersistence.Channel.Email);
        var claim = (await TurnoOutboxPersistence.ClaimBound(db, id, "worker", Now, TimeSpan.FromMinutes(1)))!;
        return (db, id, claim);
    }
    [LocalFirestoreFact]
    public async Task IssuesHashAndTransitionsAtomicallyAndReplayDoesNotWrite()
    {
        var (db, id, claim) = await Setup();
        var resolver = new TurnoOfertaEmail(new Sender(), "https://example.com");
        var message = await TurnoOutboxPersistence.BeginEmailOffer(db, id, "worker", claim, Now, resolver);
        Assert.NotNull(message);
        var match = System.Text.RegularExpressions.Regex.Match(message!.Html, @"Token=([A-F0-9]{64})");
        Assert.True(match.Success);
        var token = match.Groups[1].Value;
        Assert.Contains("https://example.com/turno-oferta-email/responder#", message.Html);
        var cap = await db.Collection("turno_ofertas_email_capabilities").Document("offer").GetSnapshotAsync();
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))), cap.GetValue<string>("Hash"));
        Assert.DoesNotContain(token, cap.ToDictionary().Values.OfType<string>());
        var doc = db.Collection("turno_outbox").Document(id); var snapshot = await doc.GetSnapshotAsync();
        Assert.Equal("Indeterminate", snapshot.GetValue<string>("State"));
        Assert.Null(await TurnoOutboxPersistence.BeginEmailOffer(db, id, "worker", claim, Now, resolver));
        Assert.Equal(snapshot.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(cap.UpdateTime, (await cap.Reference.GetSnapshotAsync()).UpdateTime);
    }
    [LocalFirestoreFact]
    public async Task MissingOriginAndDuplicateCapabilityFailWithoutWrites()
    {
        var (db, id, claim) = await Setup();
        var doc = db.Collection("turno_outbox").Document(id); var before = await doc.GetSnapshotAsync();
        Assert.Null(await TurnoOutboxPersistence.BeginEmailOffer(db, id, "worker", claim, Now, new TurnoOfertaEmail(new Sender())));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        var cap = db.Collection("turno_ofertas_email_capabilities").Document("offer");
        Assert.False((await cap.GetSnapshotAsync()).Exists);
        await new TurnoOfertaEmailCapability(db).Issue("cascade", "offer", "client", Now, Now.AddMinutes(5));
        var prior = await cap.GetSnapshotAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => TurnoOutboxPersistence.BeginEmailOffer(db, id, "worker", claim, Now, new TurnoOfertaEmail(new Sender(), "https://example.com")));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(prior.UpdateTime, (await cap.GetSnapshotAsync()).UpdateTime);
    }
}

using Firebase.Services;
using Firebase.Models;
using Firebase.Tests.Integration;
using Google.Api.Gax;
using Google.Cloud.Firestore;

namespace Firebase.Tests.Services;

public class TurnoOfertaEmailTests
{
    private static readonly DateTime Now = new(2030, 1, 1, 1, 0, 0, DateTimeKind.Utc);
    private static readonly TurnoOutboxPersistence.BoundClaim Binding = new(1, TurnoOutboxPersistence.Event.Offer, TurnoOutboxPersistence.Channel.Email, "offer");

    [LocalFirestoreFact]
    public async Task PersistedPreferencesAndEscapedTemplate()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-email-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("turno_ofertas").Document("offer").SetAsync(new { Estado = "Pendiente", CascadeId = "cascade", AppointmentId = "appointment", ClienteId = "client", CreatedUtc = Now.AddMinutes(-1), ExpiresUtc = Now.AddHours(1), OriginalUtc = Now.AddDays(1), AnchorUtc = Now.AddDays(1), ProposedUtc = Now.AddHours(2), ProposedEndUtc = Now.AddHours(3) });
        await db.Collection("turno_ofertas_pendientes").Document("appointment").SetAsync(new { OfferId = "offer", CascadeId = "cascade" });
        await db.Collection("turno_cascadas").Document("cascade").SetAsync(new { PendingId = "offer" });
        await db.Collection("turnos").Document("appointment").SetAsync(new { Id = "appointment", ClienteId = "client", Estado = "Pendiente", AceptaAdelanto = true, InicioUtc = Now.AddDays(1), FinUtc = Now.AddDays(1).AddHours(1) });
        var client = db.Collection("clientes").Document("client");
        var sender = new Sender(); var service = new TurnoOfertaEmail(sender);
        Assert.Null(typeof(TurnoOfertaEmail).GetMethod("Send", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic));
        foreach (var (email, whatsapp, expected) in new (bool?, bool?, bool)[] {
            (false, false, false), (false, null, false), (false, true, false),
            (null, true, false), (null, null, true), (null, false, true),
            (true, false, true), (true, true, true) })
        {
            await client.SetAsync(new { Id = "client", Estado = "Activo", Email = "ana@example.com", Nombre = "<Ana & Co>", NotificarPorEmail = email, NotificarPorWhatsApp = whatsapp });
            var before = await client.GetSnapshotAsync();
            var result = await service.Resolve(db, Binding, Now);
            var inTransaction = await db.RunTransactionAsync(tx => service.ResolveInTransaction(db, tx, Binding, Now));
            Assert.Equal(result, inTransaction);
            Assert.Equal(expected, result != null);
            Assert.Equal(0, sender.Count);
            Assert.Equal(before.UpdateTime, (await client.GetSnapshotAsync()).UpdateTime);
            if (expected)
            {
                Assert.Equal("ana@example.com", result!.To);
                Assert.Contains("&lt;Ana &amp; Co&gt;", result.Html);
                Assert.DoesNotContain("<Ana", result.Html);
                Assert.Contains("al 2030-01-01 00:00 (hora de Buenos Aires)", result.Html);
                Assert.Contains("antes de 2029-12-31 23:00 (hora de Buenos Aires)", result.Html);
                Assert.DoesNotContain("UTC", result.Html);
                Assert.DoesNotContain("<a ", result.Html);
            }
        }
        Assert.Null(await service.Resolve(db, Binding with { ReferenceId = "missing" }, Now));
        Assert.Null(await service.Resolve(db, Binding with { DeliveryChannel = TurnoOutboxPersistence.Channel.WhatsApp }, Now));
        Assert.Null(await service.Resolve(db, Binding, Now.AddHours(1)));
        foreach (var (binding, now) in new[] { (Binding with { ReferenceId = "missing" }, Now), (Binding with { DeliveryChannel = TurnoOutboxPersistence.Channel.WhatsApp }, Now), (Binding, Now.AddHours(1)) })
            Assert.Null(await db.RunTransactionAsync(tx => service.ResolveInTransaction(db, tx, binding, now)));
        Assert.Equal(0, sender.Count);
    }

    private sealed class Sender : IEmailSender
    {
        public EmailMessage? Message;
        public int Count;
        public Task<ResultadoEnvioEmail> EnviarAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            Message = message; Count++;
            return Task.FromResult(new ResultadoEnvioEmail(true, "accepted"));
        }
    }
}

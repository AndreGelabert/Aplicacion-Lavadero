using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;

namespace Firebase.Tests.Integration;

public class TurnoOfertaOutboxIntegrationTests
{
    private static readonly DateTime T = new(2030, 1, 8, 12, 0, 0, DateTimeKind.Utc);
    private static FirestoreDb Setup() => new FirestoreDbBuilder { ProjectId = "demo-offer-outbox-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
    private static TurnoOfertaPersistence.Request Request() => new("cascade", "offer", "appointment", T, T.AddMinutes(10), T.AddDays(2), T.AddDays(1));
    private static async Task Seed(FirestoreDb db, Dictionary<string, object> client)
    {
        await db.Collection("turnos").Document("appointment").SetAsync(new Turno { Id = "appointment", ClienteId = "client", Estado = "Pendiente", Origen = "test", ClaveIdempotencia = "appointment", AceptaAdelanto = true, InicioUtc = T.AddDays(2), FinUtc = T.AddDays(2).AddMinutes(30), Vehiculos = [new() { VehiculoId = "vehicle", CantidadEmpleadosRequeridos = 1, Servicios = [new() { ServicioId = "service", TiempoEstimado = 30 }] }] });
        await db.Collection("clientes").Document("client").SetAsync(client);
    }
    private static Dictionary<string, object> Client() => new() { ["Id"] = "client", ["Estado"] = "Activo", ["Telefono"] = "3751590586", ["Email"] = "client@example.com" };

    [LocalFirestoreFact]
    public async Task SelectedChannelsAndLegacyEmailProduceDistinctAtomicIntentsAndReplayDoesNotWrite()
    {
        foreach (var (wa, email, expected) in new (bool?, bool?, string[])[] {
            (true, true, ["Email", "WhatsApp"]), (true, false, ["WhatsApp"]),
            (false, true, ["Email"]), (null, true, ["Email"]), (null, null, ["Email"]),
            (false, null, ["Email"]), (true, null, ["WhatsApp"]) })
        {
            var db = Setup(); var client = Client();
            if (wa.HasValue) client["NotificarPorWhatsApp"] = wa.Value;
            if (email.HasValue) client["NotificarPorEmail"] = email.Value;
            await Seed(db, client); var persistence = new TurnoOfertaPersistence(db);
            await persistence.Create(Request());
            var entries = (await db.Collection("turno_outbox").GetSnapshotAsync()).Documents;
            Assert.Equal(expected, entries.Select(x => x.GetValue<string>("Channel")).OrderBy(x => x).ToArray());
            Assert.Equal(expected.Length, entries.Select(x => x.Id).Distinct().Count());
            Assert.All(entries, x => { Assert.Equal("Offer", x.GetValue<string>("Event")); Assert.Equal("offer", x.GetValue<string>("ReferenceId")); Assert.Equal("Pending", x.GetValue<string>("State")); Assert.Equal(0L, x.GetValue<long>("Generation")); });
            var offer = db.Collection("turno_ofertas").Document("offer");
            var before = await offer.GetSnapshotAsync();
            await persistence.Create(Request());
            Assert.Equal(before.UpdateTime, (await offer.GetSnapshotAsync()).UpdateTime);
            var after = (await db.Collection("turno_outbox").GetSnapshotAsync()).Documents;
            Assert.Equal(entries.OrderBy(x => x.Id).Select(x => x.UpdateTime), after.OrderBy(x => x.Id).Select(x => x.UpdateTime));
        }
    }

    [LocalFirestoreFact]
    public async Task PendingReplayRejectsMissingExpectedIntentButIgnoresChangedPreferences()
    {
        var db = Setup(); var client = Client(); client["NotificarPorWhatsApp"] = true; client["NotificarPorEmail"] = true;
        await Seed(db, client); var persistence = new TurnoOfertaPersistence(db); await persistence.Create(Request());
        var offer = db.Collection("turno_ofertas").Document("offer");
        var intents = (await db.Collection("turno_outbox").GetSnapshotAsync()).Documents;
        await db.Collection("clientes").Document("client").UpdateAsync("NotificarPorEmail", false);
        await persistence.Create(Request());
        await intents[0].Reference.DeleteAsync();
        var before = await offer.GetSnapshotAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => persistence.Create(Request()));
        Assert.Equal(before.UpdateTime, (await offer.GetSnapshotAsync()).UpdateTime);
        Assert.Single((await db.Collection("turno_outbox").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task ReplayChecksIntentMetadataAndAllowsValidLifecycle()
    {
        var db = Setup(); await Seed(db, Client()); var persistence = new TurnoOfertaPersistence(db);
        await persistence.Create(Request());
        var intent = Assert.Single((await db.Collection("turno_outbox").GetSnapshotAsync()).Documents).Reference;
        var claim = await TurnoOutboxPersistence.ClaimBound(db, intent.Id, "worker", T, TimeSpan.FromMinutes(1));
        Assert.NotNull(claim);
        await persistence.Create(Request());
        Assert.True(await TurnoOutboxPersistence.BeginSendBound(db, intent.Id, "worker", claim!, T));
        await persistence.Create(Request());
        Assert.True(await TurnoOutboxPersistence.Complete(db, intent.Id, "worker", claim!.Generation));
        await persistence.Create(Request());
        foreach (var (field, bad, good) in new (string, object, object)[] {
            ("Event", "Booking", "Offer"), ("Channel", "WhatsApp", "Email"), ("ReferenceId", "other", "offer"), ("Generation", 0L, claim.Generation) })
        {
            await intent.UpdateAsync(field, bad); var before = await intent.GetSnapshotAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => persistence.Create(Request()));
            Assert.Equal(before.UpdateTime, (await intent.GetSnapshotAsync()).UpdateTime);
            await intent.UpdateAsync(field, good);
        }
        var offer = db.Collection("turno_ofertas").Document("offer");
        await offer.UpdateAsync("OutboxChannelsV1", 0L);
        var offerBefore = await offer.GetSnapshotAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => persistence.Create(Request()));
        Assert.Equal(offerBefore.UpdateTime, (await offer.GetSnapshotAsync()).UpdateTime);
    }

    [LocalFirestoreFact]
    public async Task EmailBeginFencesAndResolvesCommittedSnapshot()
    {
        var db = Setup(); await Seed(db, Client()); await new TurnoOfertaPersistence(db).Create(Request());
        var id = TurnoOutboxPersistence.OfferIntentId("offer", TurnoOutboxPersistence.Channel.Email);
        var claim = (await TurnoOutboxPersistence.ClaimBound(db, id, "worker", T, TimeSpan.FromMinutes(1)))!;
        var sender = new NullSender(); var resolver = new TurnoOfertaEmail(sender);
        var doc = db.Collection("turno_outbox").Document(id); var before = await doc.GetSnapshotAsync();
        Assert.Null(await TurnoOutboxPersistence.BeginEmailOffer(db, id, "worker", claim with { Generation = 2 }, T, resolver));
        Assert.Null(await TurnoOutboxPersistence.BeginEmailOffer(db, id, "worker", claim, T.AddMinutes(1), resolver));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        var offer = db.Collection("turno_ofertas").Document("offer");
        await offer.UpdateAsync("OutboxChannelsV1", 1L);
        Assert.Null(await TurnoOutboxPersistence.BeginEmailOffer(db, id, "worker", claim, T, resolver));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        await offer.UpdateAsync("OutboxChannelsV1", 2L);
        var message = await TurnoOutboxPersistence.BeginEmailOffer(db, id, "worker", claim, T, resolver);
        Assert.NotNull(message); Assert.Equal("client@example.com", message.To);
        Assert.Equal("Indeterminate", (await doc.GetSnapshotAsync()).GetValue<string>("State"));
        await db.Collection("clientes").Document("client").UpdateAsync("Email", "changed@example.com");
        Assert.Equal("client@example.com", message.To);
        Assert.Null(await TurnoOutboxPersistence.BeginEmailOffer(db, id, "worker", claim, T, resolver));
        Assert.Equal(0, sender.Count);
    }

    [LocalFirestoreFact]
    public async Task DispatcherSendsCommittedOfferSnapshotAndCompletes()
    {
        var db = Setup(); await Seed(db, Client()); await new TurnoOfertaPersistence(db).Create(Request());
        var id = TurnoOutboxPersistence.OfferIntentId("offer", TurnoOutboxPersistence.Channel.Email);
        var doc = db.Collection("turno_outbox").Document(id); var resolver = new TurnoOfertaEmail(new NullSender());
        int sends = 0;
        var dispatcher = new TurnoEmailOutboxDispatcher(
            (i, token, now, lease) => TurnoOutboxPersistence.ClaimBound(db, i, token, now, lease),
            (i, token, binding, now) => TurnoOutboxPersistence.BeginEmailOffer(db, i, token, binding, now, resolver),
            (i, token, binding, now) => TurnoOutboxPersistence.ReleaseBound(db, i, token, binding, now),
            (i, token, generation) => TurnoOutboxPersistence.Complete(db, i, token, generation),
            async (message, token) => {
                Assert.Equal("Indeterminate", (await doc.GetSnapshotAsync()).GetValue<string>("State"));
                await db.Collection("clientes").Document("client").UpdateAsync("Email", "changed@example.com");
                Assert.Equal("client@example.com", message.To);
                Assert.Equal("Oferta para adelantar su turno", message.Subject);
                Assert.Contains("2030-01-09 12:00 UTC", message.Html);
                sends++; return new ResultadoEnvioEmail(true, "accepted");
            }, () => T, TimeSpan.FromMinutes(1));
        Assert.Equal(TurnoEmailOutboxDispatcher.Result.Completed, await dispatcher.Dispatch(id, "worker"));
        Assert.Equal("Completed", (await doc.GetSnapshotAsync()).GetValue<string>("State"));
        Assert.Equal(TurnoEmailOutboxDispatcher.Result.ClaimUnavailable, await dispatcher.Dispatch(id, "worker"));
        Assert.Equal(1, sends);
    }

    [LocalFirestoreFact]
    public async Task InvalidProvenanceAndExpiredOfferLeaveClaimUnchanged()
    {
        var db = Setup(); await Seed(db, Client()); await new TurnoOfertaPersistence(db).Create(Request());
        var id = TurnoOutboxPersistence.OfferIntentId("offer", TurnoOutboxPersistence.Channel.Email);
        var claim = (await TurnoOutboxPersistence.ClaimBound(db, id, "worker", T, TimeSpan.FromHours(1)))!;
        var doc = db.Collection("turno_outbox").Document(id); var offer = db.Collection("turno_ofertas").Document("offer");
        var resolver = new TurnoOfertaEmail(new NullSender());
        foreach (var invalid in new object[] { FieldValue.Delete, 0L, 1L, 4L, "2" })
        {
            await offer.UpdateAsync("OutboxChannelsV1", invalid); var before = await doc.GetSnapshotAsync();
            Assert.Null(await TurnoOutboxPersistence.BeginEmailOffer(db, id, "worker", claim, T, resolver));
            Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        }
        await offer.UpdateAsync("OutboxChannelsV1", 2L);
        var unchanged = await doc.GetSnapshotAsync();
        Assert.Null(await TurnoOutboxPersistence.BeginEmailOffer(db, id, "worker", claim, T.AddMinutes(10), resolver));
        Assert.Equal(unchanged.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
    }

    private sealed class NullSender : IEmailSender
    {
        public int Count;
        public Task<ResultadoEnvioEmail> EnviarAsync(EmailMessage message, CancellationToken cancellationToken = default)
        { Count++; return Task.FromResult(new ResultadoEnvioEmail(true, "accepted")); }
    }

    [LocalFirestoreFact]
    public async Task LegacyOfferWithoutProvenanceReplaysWithoutBackfill()
    {
        var db = Setup(); await Seed(db, Client()); var persistence = new TurnoOfertaPersistence(db);
        await persistence.Create(Request());
        var offer = db.Collection("turno_ofertas").Document("offer");
        await offer.UpdateAsync("OutboxChannelsV1", FieldValue.Delete);
        foreach (var intent in (await db.Collection("turno_outbox").GetSnapshotAsync()).Documents) await intent.Reference.DeleteAsync();
        var before = await offer.GetSnapshotAsync();
        await persistence.Create(Request());
        Assert.Equal(before.UpdateTime, (await offer.GetSnapshotAsync()).UpdateTime);
        Assert.Empty((await db.Collection("turno_outbox").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task InvalidOptInOrCorruptPreferenceRejectsWithoutDomainOrOutboxWrites()
    {
        foreach (var mutation in new Action<Dictionary<string, object>>[] {
            c => { c["NotificarPorWhatsApp"] = true; c.Remove("Telefono"); },
            c => c["NotificarPorWhatsApp"] = "true", c => c["NotificarPorEmail"] = 42L,
            c => c["Id"] = "other", c => c["Estado"] = "Inactivo", c => c.Remove("Email"),
            c => c["Email"] = "invalid", c => c["Email"] = "client@example.com\r\nInjected: x",
            c => c["NotificarPorEmail"] = false,
            c => { c["NotificarPorEmail"] = false; c["NotificarPorWhatsApp"] = false; } })
        {
            var db = Setup(); var client = Client(); mutation(client); await Seed(db, client);
            await Assert.ThrowsAsync<InvalidOperationException>(() => new TurnoOfertaPersistence(db).Create(Request()));
            Assert.Empty((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
            Assert.Empty((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);
            Assert.Empty((await db.Collection("turno_ofertas_pendientes").GetSnapshotAsync()).Documents);
            Assert.Empty((await db.Collection("turno_adelanto_anclas").GetSnapshotAsync()).Documents);
            Assert.Empty((await db.Collection("turno_outbox").GetSnapshotAsync()).Documents);
        }
    }
}

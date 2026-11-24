using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;

namespace Firebase.Tests.Integration;

public class TurnoOfertaNotificationResolverTests
{
    private static readonly DateTime Now = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static FirestoreDb Setup() => new FirestoreDbBuilder { ProjectId = "demo-resolver-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
    private static TurnoOutboxPersistence.BoundClaim Binding() => new(1, TurnoOutboxPersistence.Event.Offer, TurnoOutboxPersistence.Channel.WhatsApp, "offer");

    private static async Task Seed(FirestoreDb db)
    {
        await db.Collection("turno_ofertas").Document("offer").SetAsync(new Dictionary<string, object> {
            ["Estado"] = "Pendiente", ["CascadeId"] = "cascade", ["AppointmentId"] = "appointment", ["ClienteId"] = "client",
            ["CreatedUtc"] = Now.AddMinutes(-1), ["ExpiresUtc"] = Now.AddMinutes(10), ["OriginalUtc"] = Now.AddDays(1),
            ["AnchorUtc"] = Now.AddDays(1), ["ProposedUtc"] = Now.AddHours(1), ["ProposedEndUtc"] = Now.AddHours(2) });
        await db.Collection("turno_ofertas_pendientes").Document("appointment").SetAsync(new { OfferId = "offer", CascadeId = "cascade" });
        await db.Collection("turno_cascadas").Document("cascade").SetAsync(new { PendingId = "offer" });
        await db.Collection("turnos").Document("appointment").SetAsync(new { Id = "appointment", ClienteId = "client", Estado = "Pendiente", AceptaAdelanto = true, InicioUtc = Now.AddDays(1), FinUtc = Now.AddDays(1).AddHours(1) });
        await db.Collection("clientes").Document("client").SetAsync(new { Id = "client", Estado = "Activo", NotificarPorWhatsApp = true, Telefono = "3751590586" });
    }

    [LocalFirestoreFact]
    public async Task DispatcherUsesCommittedFirestoreSnapshotAndRejectsForgedAndExpiredOffers()
    {
        var db = Setup(); await Seed(db);
        await db.Collection("turno_ofertas").Document("offer").UpdateAsync("OutboxChannelsV1", 1L);
        var id = TurnoOutboxPersistence.OfferIntentId("offer", TurnoOutboxPersistence.Channel.WhatsApp);
        foreach (var key in new[] { id, "forged" })
            await db.RunTransactionAsync(tx => { TurnoOutboxPersistence.Enqueue(db, tx, new(key, TurnoOutboxPersistence.Event.Offer, TurnoOutboxPersistence.Channel.WhatsApp, "offer")); return Task.FromResult(0); });
        var sent = new List<TurnoWhatsAppOutboxDispatcher.Delivery>();
        var now = Now;
        var resolver = new TurnoOfertaNotificationResolver(db, () => now);
        var dispatcher = new TurnoWhatsAppOutboxDispatcher(db, async (recipient, message) => {
            Assert.Equal("Indeterminate", (await db.Collection("turno_outbox").Document(id).GetSnapshotAsync()).GetValue<string>("State"));
            sent.Add(new(recipient, message)); return true;
        }, resolver, () => now, TimeSpan.FromMinutes(1));
        Assert.Equal(TurnoWhatsAppOutboxDispatcher.Result.Unresolved, await dispatcher.Dispatch("forged", "token"));
        Assert.Empty(sent);
        now = Now.AddMinutes(10);
        Assert.Equal(TurnoWhatsAppOutboxDispatcher.Result.Unresolved, await dispatcher.Dispatch(id, "token"));
        Assert.Empty(sent);
        Assert.Equal("Released", (await db.Collection("turno_outbox").Document(id).GetSnapshotAsync()).GetValue<string>("State"));
        now = Now;
        Assert.Equal(TurnoWhatsAppOutboxDispatcher.Result.Completed, await dispatcher.Dispatch(id, "token"));
        Assert.Equal(new TurnoWhatsAppOutboxDispatcher.Delivery("543751590586", "Hay una oferta para adelantar su turno al 2025-01-01 10:00 (hora de Buenos Aires). Responda antes de 2025-01-01 09:10 (hora de Buenos Aires)."), Assert.Single(sent));
        Assert.Equal("Completed", (await db.Collection("turno_outbox").Document(id).GetSnapshotAsync()).GetValue<string>("State"));
        Assert.Equal(TurnoWhatsAppOutboxDispatcher.Result.ClaimUnavailable, await dispatcher.Dispatch(id, "token"));
        Assert.Single(sent);
    }

    [LocalFirestoreFact]
    public async Task AtomicBeginRejectsForgedIdMalformedProvenanceAndChangedDomainWithoutWrites()
    {
        var db = Setup(); await Seed(db);
        var id = TurnoOutboxPersistence.OfferIntentId("offer", TurnoOutboxPersistence.Channel.WhatsApp);
        var forged = "forged";
        foreach (var key in new[] { id, forged })
            await db.RunTransactionAsync(tx => { TurnoOutboxPersistence.Enqueue(db, tx, new(key, TurnoOutboxPersistence.Event.Offer, TurnoOutboxPersistence.Channel.WhatsApp, "offer")); return Task.FromResult(0); });
        var claim = (await TurnoOutboxPersistence.ClaimBound(db, id, "token", Now, TimeSpan.FromMinutes(1)))!;
        var forgedClaim = (await TurnoOutboxPersistence.ClaimBound(db, forged, "token", Now, TimeSpan.FromMinutes(1)))!;
        var doc = db.Collection("turno_outbox").Document(id); var offer = db.Collection("turno_ofertas").Document("offer");
        var resolver = new TurnoOfertaNotificationResolver(db, () => Now);
        var before = await doc.GetSnapshotAsync(); var forgedBefore = await db.Collection("turno_outbox").Document(forged).GetSnapshotAsync();
        foreach (var value in new object[] { 0L, 2L, 4L, "1", FieldValue.Delete }) {
            await offer.UpdateAsync("OutboxChannelsV1", value);
            Assert.Null(await TurnoOutboxPersistence.BeginWhatsAppOffer(db, id, "token", claim, Now, resolver));
            Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        }
        await offer.UpdateAsync("OutboxChannelsV1", 1L);
        Assert.Null(await TurnoOutboxPersistence.BeginWhatsAppOffer(db, forged, "token", forgedClaim, Now, resolver));
        Assert.Equal(forgedBefore.UpdateTime, (await db.Collection("turno_outbox").Document(forged).GetSnapshotAsync()).UpdateTime);
        Assert.Null(await TurnoOutboxPersistence.BeginWhatsAppOffer(db, id, "token", claim, Now.AddMinutes(1), resolver));
        await db.Collection("clientes").Document("client").UpdateAsync("NotificarPorWhatsApp", false);
        Assert.Null(await TurnoOutboxPersistence.BeginWhatsAppOffer(db, id, "token", claim, Now, resolver));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
    }

    [LocalFirestoreFact]
    public async Task AtomicBeginUsesSuppliedTimeAtOfferExpiryWithoutReadingResolverClock()
    {
        var expiry = Now.AddMinutes(10);
        foreach (var (time, expected) in new[] { (expiry.AddTicks(-10), true), (expiry, false), (expiry.AddTicks(10), false) })
        {
            var db = Setup(); await Seed(db);
            await db.Collection("turno_ofertas").Document("offer").UpdateAsync("OutboxChannelsV1", 1L);
            var id = TurnoOutboxPersistence.OfferIntentId("offer", TurnoOutboxPersistence.Channel.WhatsApp);
            await db.RunTransactionAsync(tx => { TurnoOutboxPersistence.Enqueue(db, tx, new(id, TurnoOutboxPersistence.Event.Offer, TurnoOutboxPersistence.Channel.WhatsApp, "offer")); return Task.FromResult(0); });
            var claim = (await TurnoOutboxPersistence.ClaimBound(db, id, "token", Now, TimeSpan.FromMinutes(20)))!;
            var doc = db.Collection("turno_outbox").Document(id); var before = await doc.GetSnapshotAsync();
            var reads = 0;
            var resolver = new TurnoOfertaNotificationResolver(db, () => { reads++; return expected ? expiry : Now; });
            var delivery = await TurnoOutboxPersistence.BeginWhatsAppOffer(db, id, "token", claim, time, resolver);
            Assert.Equal(expected, delivery != null);
            Assert.Equal(0, reads);
            var after = await doc.GetSnapshotAsync();
            Assert.Equal(expected ? "Indeterminate" : "Claimed", after.GetValue<string>("State"));
            Assert.Equal(claim.Generation, after.GetValue<long>("Generation"));
            if (!expected) Assert.Equal(before.UpdateTime, after.UpdateTime);
        }
    }

    [LocalFirestoreFact]
    public async Task AtomicBeginFencesProvenanceAndReturnsCommittedSnapshot()
    {
        var db = Setup(); await Seed(db);
        var id = TurnoOutboxPersistence.OfferIntentId("offer", TurnoOutboxPersistence.Channel.WhatsApp);
        await db.RunTransactionAsync(tx => { TurnoOutboxPersistence.Enqueue(db, tx, new(id, TurnoOutboxPersistence.Event.Offer, TurnoOutboxPersistence.Channel.WhatsApp, "offer")); return Task.FromResult(0); });
        var claim = (await TurnoOutboxPersistence.ClaimBound(db, id, "token", Now, TimeSpan.FromMinutes(1)))!;
        var doc = db.Collection("turno_outbox").Document(id);
        var resolver = new TurnoOfertaNotificationResolver(db, () => Now);
        var before = await doc.GetSnapshotAsync();
        Assert.Null(await TurnoOutboxPersistence.BeginWhatsAppOffer(db, id, "token", claim, Now, resolver));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        await db.Collection("turno_ofertas").Document("offer").UpdateAsync("OutboxChannelsV1", 1L);
        Assert.Null(await TurnoOutboxPersistence.BeginWhatsAppOffer(db, id, "wrong", claim, Now, resolver));
        Assert.Null(await TurnoOutboxPersistence.BeginWhatsAppOffer(db, id, "token", claim with { Generation = 2 }, Now, resolver));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        var delivery = await TurnoOutboxPersistence.BeginWhatsAppOffer(db, id, "token", claim, Now, resolver);
        Assert.Equal("543751590586", delivery!.Recipient);
        Assert.Contains("2025-01-01 10:00 (hora de Buenos Aires)", delivery.Message);
        Assert.Equal("Indeterminate", (await doc.GetSnapshotAsync()).GetValue<string>("State"));
        Assert.Null(await TurnoOutboxPersistence.BeginWhatsAppOffer(db, id, "token", claim, Now, resolver));
    }

    [LocalFirestoreFact]
    public async Task ExistingTransactionMatchesResolveAndAllowsFollowingWrite()
    {
        var db = Setup(); await Seed(db);
        var resolver = new TurnoOfertaNotificationResolver(db, () => Now);
        var expected = await resolver.Resolve(Binding());
        var marker = db.Collection("resolver_test_markers").Document("composed");
        var actual = await db.RunTransactionAsync(async tx => {
            var delivery = await resolver.ResolveInTransaction(tx, Binding());
            tx.Set(marker, new { Resolved = delivery != null });
            return delivery;
        });
        Assert.NotNull(expected);
        Assert.Equal(expected, actual);
        Assert.True((await marker.GetSnapshotAsync()).GetValue<bool>("Resolved"));

        await db.Collection("clientes").Document("client").UpdateAsync("NotificarPorWhatsApp", false);
        Assert.Null(await resolver.Resolve(Binding()));
        Assert.Null(await db.RunTransactionAsync(tx => resolver.ResolveInTransaction(tx, Binding())));
        Assert.Null(await db.RunTransactionAsync(tx => resolver.ResolveInTransaction(tx, Binding() with { ReferenceId = "bad/id" })));
    }

    [LocalFirestoreFact]
    public async Task ValidOfferResolvesPersistedRecipientWithoutWrites()
    {
        var db = Setup(); await Seed(db);
        var docs = new[] { db.Collection("turno_ofertas").Document("offer"), db.Collection("turno_ofertas_pendientes").Document("appointment"), db.Collection("turno_cascadas").Document("cascade"), db.Collection("turnos").Document("appointment"), db.Collection("clientes").Document("client") };
        var before = await Task.WhenAll(docs.Select(x => x.GetSnapshotAsync()));
        var result = await new TurnoOfertaNotificationResolver(db, () => Now).Resolve(Binding());
        Assert.NotNull(result); Assert.Equal("543751590586", result!.Recipient);
        Assert.Contains("2025-01-01 10:00 (hora de Buenos Aires)", result.Message);
        var after = await Task.WhenAll(docs.Select(x => x.GetSnapshotAsync()));
        Assert.Equal(before.Select(x => x.UpdateTime), after.Select(x => x.UpdateTime));
    }

    [LocalFirestoreFact]
    public async Task ClockBoundaryAndInvalidClockFailClosed()
    {
        var db = Setup(); await Seed(db);
        var offer = db.Collection("turno_ofertas").Document("offer");
        var before = await offer.GetSnapshotAsync();
        Assert.NotNull(await new TurnoOfertaNotificationResolver(db, () => Now.AddTicks(1)).Resolve(Binding()));
        Assert.Null(await new TurnoOfertaNotificationResolver(db, () => Now.AddMinutes(10)).Resolve(Binding()));
        Assert.Null(await new TurnoOfertaNotificationResolver(db, () => DateTime.SpecifyKind(Now, DateTimeKind.Unspecified)).Resolve(Binding()));
        Assert.Null(await new TurnoOfertaNotificationResolver(db, () => default).Resolve(Binding()));
        Assert.Equal(before.UpdateTime, (await offer.GetSnapshotAsync()).UpdateTime);
    }

    [LocalFirestoreFact]
    public async Task ForgedOrMissingIdentityPreferenceAndExpiredOfferFailClosed()
    {
        var changes = new (string Collection, string Id, string Field, object Value)[] {
            ("turno_ofertas", "offer", "Estado", "Rechazada"), ("turno_ofertas", "offer", "ExpiresUtc", Now),
            ("turno_ofertas", "offer", "ClienteId", "other"), ("turno_ofertas", "offer", "AppointmentId", "other"),
            ("turno_ofertas_pendientes", "appointment", "OfferId", "forged"), ("turno_ofertas_pendientes", "appointment", "CascadeId", "forged"),
            ("turno_cascadas", "cascade", "PendingId", "forged"), ("turnos", "appointment", "Id", "forged"),
            ("turnos", "appointment", "ClienteId", "forged"), ("turnos", "appointment", "Estado", "Cancelado"),
            ("turnos", "appointment", "InicioUtc", "invalid"), ("clientes", "client", "Id", "forged"),
            ("clientes", "client", "Estado", "Inactivo"), ("clientes", "client", "NotificarPorWhatsApp", false),
            ("clientes", "client", "NotificarPorWhatsApp", FieldValue.Delete), ("clientes", "client", "Telefono", FieldValue.Delete),
            ("clientes", "client", "Telefono", "bad") };
        foreach (var change in changes)
        {
            var db = Setup(); await Seed(db);
            var doc = db.Collection(change.Collection).Document(change.Id);
            await doc.UpdateAsync(change.Field, change.Value); var before = await doc.GetSnapshotAsync();
            Assert.Null(await new TurnoOfertaNotificationResolver(db, () => Now).Resolve(Binding()));
            Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        }
        var empty = Setup(); var resolver = new TurnoOfertaNotificationResolver(empty, () => Now);
        Assert.Null(await resolver.Resolve(Binding()));
        Assert.Null(await resolver.Resolve(Binding() with { EventType = TurnoOutboxPersistence.Event.Booking }));
        Assert.Null(await resolver.Resolve(Binding() with { ReferenceId = "bad/id" }));
    }
}

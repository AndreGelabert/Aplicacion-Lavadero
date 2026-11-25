using Google.Cloud.Firestore;
using Firebase.Models;
using System.Security.Cryptography;
using System.Text;

namespace Firebase.Services;

/// <summary>
/// Stages an internal notification intent alongside caller-owned domain writes.
/// IDs and claim tokens must be opaque, non-secret, non-sensitive identifiers, never
/// addresses, credentials or message content. Caller-supplied clocks are not verified as trusted.
/// A duplicate document fails the whole transaction, even for identical data; this is not
/// an exactly-once delivery guarantee. No delivery or retry processing is performed here.
/// </summary>
internal static class TurnoOutboxPersistence
{
    internal enum Event { Offer, Booking }
    internal enum Channel { WhatsApp, Email }
    internal sealed record Request(string Id, Event EventType, Channel DeliveryChannel, string ReferenceId);
    internal sealed record BoundClaim(long Generation, Event EventType, Channel DeliveryChannel, string ReferenceId);

    // A deliberately narrow ASCII identifier subset, with a bounded persisted size.
    private static bool SafeId(string? value) => value is { Length: > 0 and <= 128 } &&
        !(value.StartsWith("__", StringComparison.Ordinal) && value.EndsWith("__", StringComparison.Ordinal)) &&
        value.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_');

    internal static string OfferIntentId(string offerId, Channel channel) =>
        (channel == Channel.WhatsApp ? "offer-wa-" : channel == Channel.Email ? "offer-email-" : throw new ArgumentException("Invalid channel.")) +
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(offerId)));

    internal static bool ValidState(Dictionary<string, object> data)
    {
        if (!data.TryGetValue("Event", out var ev) || ev is not string eventName || !Enum.GetNames<Event>().Contains(eventName) ||
            !data.TryGetValue("Channel", out var ch) || ch is not string channelName || !Enum.GetNames<Channel>().Contains(channelName) ||
            !data.TryGetValue("ReferenceId", out var reference) || reference is not string referenceId || !SafeId(referenceId) ||
            !data.TryGetValue("State", out var rawState) || rawState is not string state ||
            !data.TryGetValue("Generation", out var rawGeneration) || rawGeneration is not long generation) return false;
        if (state == "Pending") return data.Count == 5 && generation == 0;
        if (state == "Released") return data.Count == 5 && generation > 0;
        return (state is "Claimed" or "Indeterminate" or "Completed") && data.Count == 7 && generation > 0 &&
            data.TryGetValue("ClaimToken", out var rawToken) && rawToken is string token && SafeId(token) &&
            data.TryGetValue("LeaseUntil", out var expiry) && expiry is Timestamp;
    }

    private static bool ValidClock(DateTime now) => now.Kind == DateTimeKind.Utc && now.Ticks % 10 == 0;

    // The returned generation fences even a reused token. BeginSend must commit before
    // any external send; an uncertain send stays indeterminate and is never reclaimed.
    internal static async Task<long?> Claim(FirestoreDb db, string id, string token, DateTime now, TimeSpan lease) =>
        (await ClaimBound(db, id, token, now, lease))?.Generation;

    internal static Task<BoundClaim?> ClaimBound(FirestoreDb db, string id, string token, DateTime now, TimeSpan lease)
    {
        Validate(db, id, token);
        if (!ValidClock(now) || lease <= TimeSpan.Zero || lease.Ticks % 10 != 0 || lease > TimeSpan.FromHours(1) || now > DateTime.MaxValue - lease)
            throw new ArgumentException("Invalid lease.");
        var doc = db.Collection("turno_outbox").Document(id);
        return db.RunTransactionAsync<BoundClaim?>(async tx =>
        {
            var snap = await tx.GetSnapshotAsync(doc);
            if (!snap.Exists) return null;
            var data = snap.ToDictionary();
            if (!ValidState(data) || !data.TryGetValue("State", out var state) || !data.TryGetValue("Generation", out var value) || value is not long generation || generation == long.MaxValue) return null;
            if (state is not string status) return null;
            if (status is "Pending" or "Released")
            {
                if (data.ContainsKey("ClaimToken") || data.ContainsKey("LeaseUntil")) return null;
            }
            else if (status == "Claimed")
            {
                if (generation == 0 || !data.TryGetValue("ClaimToken", out var prior) || prior is not string previous || !SafeId(previous) ||
                    !data.TryGetValue("LeaseUntil", out var expiry) || expiry is not Timestamp until || until.ToDateTime() > now) return null;
            }
            else return null;
            var next = checked(generation + 1);
            tx.Update(doc, new Dictionary<string, object> { ["State"] = "Claimed", ["ClaimToken"] = token, ["LeaseUntil"] = Timestamp.FromDateTime(now.Add(lease)), ["Generation"] = next });
            return new BoundClaim(next, Enum.Parse<Event>((string)data["Event"]), Enum.Parse<Channel>((string)data["Channel"]), (string)data["ReferenceId"]);
        });
    }

    internal static Task<bool> BeginSend(FirestoreDb db, string id, string token, long generation, DateTime now) =>
        BeginSendCore(db, id, token, generation, now, null);

    internal static Task<bool> BeginSendBound(FirestoreDb db, string id, string token, BoundClaim claim, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(claim);
        return BeginSendCore(db, id, token, claim.Generation, now, claim);
    }

    private static Task<bool> BeginSendCore(FirestoreDb db, string id, string token, long generation, DateTime now, BoundClaim? claim)
    {
        Validate(db, id, token);
        if (!ValidClock(now)) throw new ArgumentException("Invalid clock.");
        var doc = db.Collection("turno_outbox").Document(id);
        return db.RunTransactionAsync(async tx =>
        {
            var snap = await tx.GetSnapshotAsync(doc);
            if (!snap.Exists || generation <= 0) return false;
            var data = snap.ToDictionary();
            if (!ValidState(data) || !data.TryGetValue("State", out var state) || state is not string status || status != "Claimed" ||
                !data.TryGetValue("ClaimToken", out var current) || current is not string currentToken || currentToken != token ||
                !data.TryGetValue("Generation", out var value) || value is not long currentGeneration || currentGeneration != generation ||
                !data.TryGetValue("LeaseUntil", out var expiry) || expiry is not Timestamp until || until.ToDateTime() <= now) return false;
            if (!Equals(data["Event"], "Booking")) return false;
            if (claim != null && (data["Event"] is not string ev || ev != claim.EventType.ToString() ||
                data["Channel"] is not string channel || channel != claim.DeliveryChannel.ToString() ||
                data["ReferenceId"] is not string reference || reference != claim.ReferenceId)) return false;
            tx.Update(doc, new Dictionary<string, object> { ["State"] = "Indeterminate" });
            return true;
        });
    }

    internal static Task<TurnoWhatsAppOutboxDispatcher.Delivery?> BeginWhatsAppOffer(FirestoreDb db, string id, string token, BoundClaim claim, DateTime now, TurnoOfertaNotificationResolver resolver)
    {
        Validate(db, id, token);
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(resolver);
        if (!ValidClock(now)) throw new ArgumentException("Invalid clock.");
        if (claim.EventType != Event.Offer || claim.DeliveryChannel != Channel.WhatsApp || !SafeId(claim.ReferenceId) || id != OfferIntentId(claim.ReferenceId, Channel.WhatsApp))
            return Task.FromResult<TurnoWhatsAppOutboxDispatcher.Delivery?>(null);
        var doc = db.Collection("turno_outbox").Document(id);
        return db.RunTransactionAsync<TurnoWhatsAppOutboxDispatcher.Delivery?>(async tx =>
        {
            var snap = await tx.GetSnapshotAsync(doc);
            if (!snap.Exists || claim.Generation <= 0) return null;
            var data = snap.ToDictionary();
            if (!ValidState(data) || !Equals(data["State"], "Claimed") || !Equals(data["ClaimToken"], token) ||
                !Equals(data["Generation"], claim.Generation) || ((Timestamp)data["LeaseUntil"]).ToDateTime() <= now ||
                !Equals(data["Event"], "Offer") || !Equals(data["Channel"], "WhatsApp") || !Equals(data["ReferenceId"], claim.ReferenceId)) return null;
            var offer = await tx.GetSnapshotAsync(db.Collection("turno_ofertas").Document(claim.ReferenceId));
            if (!offer.Exists || !offer.ToDictionary().TryGetValue("OutboxChannelsV1", out var raw) || raw is not long channels || channels is not (1 or 3)) return null;
            var delivery = await resolver.ResolveInTransaction(tx, claim, now);
            if (delivery == null) return null;
            tx.Update(doc, new Dictionary<string, object> { ["State"] = "Indeterminate" });
            return delivery;
        });
    }

    internal static Task<EmailMessage?> BeginEmailOffer(FirestoreDb db, string id, string token, BoundClaim claim, DateTime now, TurnoOfertaEmail resolver)
    {
        Validate(db, id, token);
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(resolver);
        if (!ValidClock(now)) throw new ArgumentException("Invalid clock.");
        if (claim.EventType != Event.Offer || claim.DeliveryChannel != Channel.Email || !SafeId(claim.ReferenceId) || id != OfferIntentId(claim.ReferenceId, Channel.Email))
            return Task.FromResult<EmailMessage?>(null);
        if (!resolver.HasOrigin) return Task.FromResult<EmailMessage?>(null);
        var secret = TurnoOfertaEmailCapability.GenerateSecret();
        var doc = db.Collection("turno_outbox").Document(id);
        return db.RunTransactionAsync<EmailMessage?>(async tx =>
        {
            var snap = await tx.GetSnapshotAsync(doc);
            if (!snap.Exists || claim.Generation <= 0) return null;
            var data = snap.ToDictionary();
            if (!ValidState(data) || !Equals(data["State"], "Claimed") || !Equals(data["ClaimToken"], token) ||
                !Equals(data["Generation"], claim.Generation) || ((Timestamp)data["LeaseUntil"]).ToDateTime() <= now ||
                !Equals(data["Event"], "Offer") || !Equals(data["Channel"], "Email") || !Equals(data["ReferenceId"], claim.ReferenceId)) return null;
            var offer = await tx.GetSnapshotAsync(db.Collection("turno_ofertas").Document(claim.ReferenceId));
            if (!offer.Exists || !offer.ToDictionary().TryGetValue("OutboxChannelsV1", out var raw) || raw is not long channels || channels is not (2 or 3)) return null;
            var message = await resolver.ResolveInTransaction(db, tx, claim, now);
            if (message == null) return null;
            var cascade = offer.GetValue<string>("CascadeId");
            var client = offer.GetValue<string>("ClienteId");
            var expiry = offer.GetValue<DateTime>("ExpiresUtc");
            await new TurnoOfertaEmailCapability(db).IssueInTransaction(tx, cascade, claim.ReferenceId, client, now, expiry, secret);
            tx.Update(doc, new Dictionary<string, object> { ["State"] = "Indeterminate" });
            return resolver.AddLink(message, cascade, claim.ReferenceId, client, secret);
        });
    }

    internal static Task<bool> ReleaseBound(FirestoreDb db, string id, string token, BoundClaim claim, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(claim);
        Validate(db, id, token);
        if (!ValidClock(now)) throw new ArgumentException("Invalid clock.");
        var doc = db.Collection("turno_outbox").Document(id);
        return db.RunTransactionAsync(async tx =>
        {
            var snap = await tx.GetSnapshotAsync(doc);
            if (!snap.Exists || claim.Generation <= 0) return false;
            var data = snap.ToDictionary();
            if (!ValidState(data) || !Equals(data["State"], "Claimed") || !Equals(data["ClaimToken"], token) ||
                !Equals(data["Generation"], claim.Generation) || ((Timestamp)data["LeaseUntil"]).ToDateTime() <= now ||
                !Equals(data["Event"], claim.EventType.ToString()) || !Equals(data["Channel"], claim.DeliveryChannel.ToString()) ||
                !Equals(data["ReferenceId"], claim.ReferenceId)) return false;
            tx.Update(doc, new Dictionary<string, object> { ["State"] = "Released", ["ClaimToken"] = FieldValue.Delete, ["LeaseUntil"] = FieldValue.Delete });
            return true;
        });
    }

    internal static Task<bool> Complete(FirestoreDb db, string id, string token, long generation)
    {
        Validate(db, id, token);
        var doc = db.Collection("turno_outbox").Document(id);
        return db.RunTransactionAsync(async tx =>
        {
            var snap = await tx.GetSnapshotAsync(doc);
            if (!snap.Exists || generation <= 0) return false;
            var data = snap.ToDictionary();
            if (!ValidState(data) || !data.TryGetValue("State", out var state) || state is not string status ||
                !data.TryGetValue("ClaimToken", out var current) || current is not string currentToken || currentToken != token ||
                !data.TryGetValue("Generation", out var value) || value is not long currentGeneration || currentGeneration != generation ||
                !data.TryGetValue("LeaseUntil", out var expiry) || expiry is not Timestamp) return false;
            if (status == "Completed") return true;
            if (status != "Indeterminate") return false;
            tx.Update(doc, new Dictionary<string, object> { ["State"] = "Completed" });
            return true;
        });
    }

    private static void Validate(FirestoreDb db, string id, string token)
    {
        ArgumentNullException.ThrowIfNull(db);
        if (!SafeId(id) || !SafeId(token)) throw new ArgumentException("Invalid outbox identifier or token.");
    }

    internal static void Enqueue(FirestoreDb db, Transaction tx, Request request)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(tx);
        ArgumentNullException.ThrowIfNull(request);
        if (!SafeId(request.Id) || !SafeId(request.ReferenceId) ||
            !Enum.IsDefined(request.EventType) || !Enum.IsDefined(request.DeliveryChannel))
            throw new ArgumentException("Invalid outbox metadata.", nameof(request));
        tx.Create(db.Collection("turno_outbox").Document(request.Id), new Dictionary<string, object>
        {
            ["Event"] = request.EventType.ToString(),
            ["Channel"] = request.DeliveryChannel.ToString(),
            ["ReferenceId"] = request.ReferenceId,
            ["State"] = "Pending",
            ["Generation"] = 0L
        });
    }
}

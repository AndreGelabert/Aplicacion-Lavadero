using Google.Cloud.Firestore;

namespace Firebase.Services;

/// <summary>
/// Isolated single-message coordinator, not registered for production use. The clock is
/// the application server's UTC clock, not a Firestore commit timestamp. Completion is
/// fenced by token and generation and checked against the captured caller time within
/// the transaction; commit can occur after expiry. A newer committed claim conflicts
/// with a stale completion. This lease grants no authority over external effects, and
/// neither serializes messages for a phone nor guarantees exactly-once handling.
/// Handler failures may already have produced effects; no retry policy is implied.
/// </summary>
internal sealed class WhatsAppInboxProcessor
{
    internal enum Result { Completed, ClaimUnavailable, HandlerFailedAmbiguous, CompletionRejected }

    private readonly Func<string, DateTime, DateTime, Task<WhatsAppWebhookInbox.Lease?>> claim;
    private readonly Func<string, string, long, DateTime, Task<bool>> complete;
    private readonly Func<string, Task> handler;
    private readonly Func<DateTime> clock;
    private readonly TimeSpan leaseDuration;

    internal WhatsAppInboxProcessor(FirestoreDb db, Func<string, Task> handler, TimeSpan leaseDuration)
        : this((id, now, expiry) => WhatsAppWebhookInbox.Claim(db, id, now, expiry),
               (id, token, generation, now) => WhatsAppWebhookInbox.Complete(db, id, token, generation, now),
               handler, () => DateTime.UtcNow, leaseDuration)
    {
        ArgumentNullException.ThrowIfNull(db);
    }

    internal WhatsAppInboxProcessor(
        Func<string, DateTime, DateTime, Task<WhatsAppWebhookInbox.Lease?>> claim,
        Func<string, string, long, DateTime, Task<bool>> complete,
        Func<string, Task> handler, Func<DateTime> clock, TimeSpan leaseDuration)
    {
        this.claim = claim ?? throw new ArgumentNullException(nameof(claim));
        this.complete = complete ?? throw new ArgumentNullException(nameof(complete));
        this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        if (leaseDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        this.leaseDuration = leaseDuration;
    }

    internal async Task<Result> Process(string id)
    {
        var now = clock();
        var lease = await claim(id, now, now.Add(leaseDuration));
        if (lease == null) return Result.ClaimUnavailable;
        try { await handler(id); }
        catch (Exception) { return Result.HandlerFailedAmbiguous; }
        return await complete(id, lease.Token, lease.Generation, clock())
            ? Result.Completed : Result.CompletionRejected;
    }
}

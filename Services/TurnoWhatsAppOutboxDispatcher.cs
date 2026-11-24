using Google.Cloud.Firestore;

namespace Firebase.Services;

/// <summary>
/// Unregistered coordinator. Resolver must authorize the event and derive recipient and
/// content from trusted domain data; there is no production resolver here. Metadata is
/// fenced at BeginSend, but domain data may change after resolution. Meta acceptance
/// is not a delivery guarantee; failures after BeginSend remain indeterminate.
/// </summary>
internal sealed class TurnoWhatsAppOutboxDispatcher
{
    internal enum Result { Completed, ClaimUnavailable, Unresolved, InvalidRecipient, ClaimLost, Indeterminate, CompletionRejected }
    internal sealed record Delivery(string Recipient, string Message, IReadOnlyList<(string id, string title)>? Buttons = null)
    {
        public bool Equals(Delivery? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            if (Recipient != other.Recipient || Message != other.Message) return false;
            if (Buttons == null && other.Buttons == null) return true;
            if (Buttons == null || other.Buttons == null || Buttons.Count != other.Buttons.Count) return false;
            for (int i = 0; i < Buttons.Count; i++)
            {
                if (Buttons[i] != other.Buttons[i]) return false;
            }
            return true;
        }

        public override int GetHashCode()
        {
            var hash = HashCode.Combine(Recipient, Message);
            if (Buttons != null)
            {
                foreach (var b in Buttons) hash = HashCode.Combine(hash, b.GetHashCode());
            }
            return hash;
        }
    }
    // Phase identifies uncertainty: begin may have committed, send may have reached Meta,
    // complete may have committed, and release may have failed or committed.
    internal sealed class DispatchException(string phase, Exception cause) : Exception($"Outbox {phase} outcome is uncertain.", cause)
    {
        internal string Phase { get; } = phase;
    }

    private readonly Func<string, string, DateTime, TimeSpan, Task<TurnoOutboxPersistence.BoundClaim?>> claim;
    private readonly Func<string, string, TurnoOutboxPersistence.BoundClaim, DateTime, Task<bool>> begin;
    private readonly Func<string, string, TurnoOutboxPersistence.BoundClaim, DateTime, Task<bool>> release;
    private readonly Func<string, string, long, Task<bool>> complete;
    private readonly Func<TurnoOutboxPersistence.BoundClaim, Task<Delivery?>> resolve;
    private readonly Func<string, string, TurnoOutboxPersistence.BoundClaim, DateTime, Task<Delivery?>>? beginOffer;
    private readonly Func<string, string, Task<bool>> send;
    private readonly Func<Delivery, Task<bool>>? sendDelivery;
    private readonly Func<DateTime> clock;
    private readonly TimeSpan lease;

    internal TurnoWhatsAppOutboxDispatcher(FirestoreDb db, MetaWhatsAppService sender,
        Func<TurnoOutboxPersistence.BoundClaim, Task<Delivery?>> resolve, TimeSpan lease)
        : this((id, token, now, duration) => TurnoOutboxPersistence.ClaimBound(db, id, token, now, duration),
            (id, token, binding, now) => TurnoOutboxPersistence.BeginSendBound(db, id, token, binding, now),
            (id, token, binding, now) => TurnoOutboxPersistence.ReleaseBound(db, id, token, binding, now),
            (id, token, generation) => TurnoOutboxPersistence.Complete(db, id, token, generation),
            resolve, sender.SendTextMessage,
            () => { var now = DateTime.UtcNow; return now.AddTicks(-(now.Ticks % 10)); }, lease)
    {
        ArgumentNullException.ThrowIfNull(db);
    }

    internal TurnoWhatsAppOutboxDispatcher(FirestoreDb db, MetaWhatsAppService sender, TurnoOfertaNotificationResolver resolver, TimeSpan lease)
        : this(db, sender, resolver,
            () => { var now = DateTime.UtcNow; return now.AddTicks(-(now.Ticks % 10)); }, lease) { }

    internal TurnoWhatsAppOutboxDispatcher(FirestoreDb db, MetaWhatsAppService sender,
        TurnoOfertaNotificationResolver resolver, Func<DateTime> clock, TimeSpan lease)
        : this((id, token, now, duration) => TurnoOutboxPersistence.ClaimBound(db, id, token, now, duration),
            (id, token, binding, now) => TurnoOutboxPersistence.BeginWhatsAppOffer(db, id, token, binding, now, resolver),
            (id, token, binding, now) => TurnoOutboxPersistence.ReleaseBound(db, id, token, binding, now),
            (id, token, generation) => TurnoOutboxPersistence.Complete(db, id, token, generation),
            sender.SendTextMessage, clock, lease,
            async delivery =>
            {
                if (delivery.Buttons?.Count > 0)
                {
                    var sentButtons = await sender.SendButtonMessage(delivery.Recipient, delivery.Message, delivery.Buttons.ToList());
                    if (sentButtons) return true;
                }
                return await sender.SendTextMessage(delivery.Recipient, delivery.Message);
            }) { }

    internal TurnoWhatsAppOutboxDispatcher(FirestoreDb db, Func<string, string, Task<bool>> send,
        TurnoOfertaNotificationResolver resolver, Func<DateTime> clock, TimeSpan lease)
        : this((id, token, now, duration) => TurnoOutboxPersistence.ClaimBound(db, id, token, now, duration),
            (id, token, binding, now) => TurnoOutboxPersistence.BeginWhatsAppOffer(db, id, token, binding, now, resolver),
            (id, token, binding, now) => TurnoOutboxPersistence.ReleaseBound(db, id, token, binding, now),
            (id, token, generation) => TurnoOutboxPersistence.Complete(db, id, token, generation),
            send, clock, lease) { }

    internal TurnoWhatsAppOutboxDispatcher(
        Func<string, string, DateTime, TimeSpan, Task<TurnoOutboxPersistence.BoundClaim?>> claim,
        Func<string, string, TurnoOutboxPersistence.BoundClaim, DateTime, Task<bool>> begin,
        Func<string, string, TurnoOutboxPersistence.BoundClaim, DateTime, Task<bool>> release,
        Func<string, string, long, Task<bool>> complete,
        Func<TurnoOutboxPersistence.BoundClaim, Task<Delivery?>> resolve,
        Func<string, string, Task<bool>> send, Func<DateTime> clock, TimeSpan lease)
    {
        this.claim = claim ?? throw new ArgumentNullException(nameof(claim));
        this.begin = begin ?? throw new ArgumentNullException(nameof(begin));
        this.release = release ?? throw new ArgumentNullException(nameof(release));
        this.complete = complete ?? throw new ArgumentNullException(nameof(complete));
        this.resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
        this.send = send ?? throw new ArgumentNullException(nameof(send));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.lease = lease;
    }

    internal TurnoWhatsAppOutboxDispatcher(
        Func<string, string, DateTime, TimeSpan, Task<TurnoOutboxPersistence.BoundClaim?>> claim,
        Func<string, string, TurnoOutboxPersistence.BoundClaim, DateTime, Task<Delivery?>> beginOffer,
        Func<string, string, TurnoOutboxPersistence.BoundClaim, DateTime, Task<bool>> release,
        Func<string, string, long, Task<bool>> complete,
        Func<string, string, Task<bool>> send, Func<DateTime> clock, TimeSpan lease,
        Func<Delivery, Task<bool>>? sendDelivery = null)
        : this(claim, (id, token, binding, now) => Task.FromResult(false), release, complete,
            binding => Task.FromResult<Delivery?>(null), send, clock, lease)
    {
        this.beginOffer = beginOffer ?? throw new ArgumentNullException(nameof(beginOffer));
        this.sendDelivery = sendDelivery;
    }

    internal async Task<Result> Dispatch(string id, string token, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var binding = await claim(id, token, clock(), lease);
        if (binding == null) return Result.ClaimUnavailable;
        async Task<bool> ReleaseClaim()
        {
            try { return await release(id, token, binding, clock()); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { throw new DispatchException("release", ex); }
        }
        async Task<Result> Release(Result result) => await ReleaseClaim() ? result : Result.ClaimLost;
        if (cancellationToken.IsCancellationRequested)
        {
            var original = new OperationCanceledException(cancellationToken);
            try { if (!await ReleaseClaim()) throw new InvalidOperationException("Outbox release was rejected."); }
            catch (Exception cleanup) { original.Data["OutboxReleaseFailure"] = cleanup; }
            throw original;
        }
        if (binding.DeliveryChannel != TurnoOutboxPersistence.Channel.WhatsApp) return await Release(Result.Unresolved);
        if (binding.EventType == TurnoOutboxPersistence.Event.Offer && beginOffer == null) return await Release(Result.Unresolved);
        if (binding.EventType != TurnoOutboxPersistence.Event.Offer && beginOffer != null) return await Release(Result.Unresolved);
        Delivery? delivery;
        if (binding.EventType == TurnoOutboxPersistence.Event.Offer && beginOffer != null)
        {
            try { delivery = await beginOffer(id, token, binding, clock()); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { throw new DispatchException("begin", ex); }
            if (delivery == null) return await Release(Result.Unresolved);
            try { if (!await (sendDelivery != null ? sendDelivery(delivery) : send(delivery.Recipient, delivery.Message))) return Result.Indeterminate; }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { throw new DispatchException("send", ex); }
            try { return await complete(id, token, binding.Generation) ? Result.Completed : Result.CompletionRejected; }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { throw new DispatchException("complete", ex); }
        }
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            delivery = await resolve(binding);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (Exception original)
        {
            try
            {
                if (!await ReleaseClaim())
                    throw new InvalidOperationException("Outbox release was rejected.");
            }
            catch (Exception cleanup)
            {
                if (original is OperationCanceledException)
                {
                    // Keep cancellation semantics and attach the pre-send cleanup diagnostic.
                    original.Data["OutboxReleaseFailure"] = cleanup;
                }
                else throw new AggregateException("Resolution and cleanup failed.", original, cleanup);
            }
            throw;
        }
        if (delivery == null || string.IsNullOrWhiteSpace(delivery.Message)) return await Release(Result.Unresolved);
        var recipient = delivery.Recipient;
        if (string.IsNullOrEmpty(recipient) || recipient.Any(c => c is < '0' or > '9') ||
            !PhoneNumberHelper.IsValidPhoneNumber(recipient)) return await Release(Result.InvalidRecipient);
        // Once begin is invoked its commit may be indeterminate; never release on exception.
        try { if (!await begin(id, token, binding, clock())) return Result.ClaimLost; }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { throw new DispatchException("begin", ex); }
        try
        {
            if (!await (sendDelivery != null ? sendDelivery(delivery) : send(recipient, delivery.Message))) return Result.Indeterminate;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { throw new DispatchException("send", ex); }
        try { return await complete(id, token, binding.Generation) ? Result.Completed : Result.CompletionRejected; }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { throw new DispatchException("complete", ex); }
    }
}

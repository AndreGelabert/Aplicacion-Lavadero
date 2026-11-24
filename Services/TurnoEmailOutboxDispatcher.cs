using Firebase.Models;
using Google.Cloud.Firestore;

namespace Firebase.Services;

/// <summary>Unregistered coordinator. The trusted resolver is a snapshot and may become stale
/// before sending. Provider acceptance does not guarantee delivery. Indeterminate sends are not retried.</summary>
internal sealed class TurnoEmailOutboxDispatcher
{
    internal enum Result { Completed, ClaimUnavailable, Unresolved, ClaimLost, Indeterminate, CompletionRejected }
    internal sealed class DispatchException(string phase, Exception cause) : Exception($"Outbox {phase} outcome is uncertain.", cause)
    {
        internal string Phase { get; } = phase;
    }

    private readonly Func<string, string, DateTime, TimeSpan, Task<TurnoOutboxPersistence.BoundClaim?>> claim;
    private readonly Func<string, string, TurnoOutboxPersistence.BoundClaim, DateTime, Task<bool>> begin, release;
    private readonly Func<string, string, long, Task<bool>> complete;
    private readonly Func<TurnoOutboxPersistence.BoundClaim, Task<EmailMessage?>> resolve;
    private readonly Func<string, string, TurnoOutboxPersistence.BoundClaim, DateTime, Task<EmailMessage?>>? beginOffer;
    private readonly Func<EmailMessage, CancellationToken, Task<ResultadoEnvioEmail>> send;
    private readonly Func<DateTime> clock;
    private readonly TimeSpan lease;

    internal TurnoEmailOutboxDispatcher(FirestoreDb db, IEmailSender sender, TimeSpan lease)
        : this(db, sender, lease, null) { }

    internal TurnoEmailOutboxDispatcher(FirestoreDb db, IEmailSender sender, TimeSpan lease, string? origin)
        : this((id, token, now, duration) => TurnoOutboxPersistence.ClaimBound(db, id, token, now, duration),
            (id, token, binding, now) => TurnoOutboxPersistence.BeginSendBound(db, id, token, binding, now),
            (id, token, binding, now) => TurnoOutboxPersistence.ReleaseBound(db, id, token, binding, now),
            (id, token, generation) => TurnoOutboxPersistence.Complete(db, id, token, generation),
            binding => new TurnoOfertaEmail(sender, origin).Resolve(db, binding, UtcNow()),
            (message, cancellationToken) => sender.EnviarAsync(message, cancellationToken), UtcNow, lease)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(sender);
        var offerEmail = new TurnoOfertaEmail(sender, origin);
        beginOffer = (id, token, binding, now) => TurnoOutboxPersistence.BeginEmailOffer(db, id, token, binding, now, offerEmail);
    }

    private static DateTime UtcNow() { var now = DateTime.UtcNow; return now.AddTicks(-(now.Ticks % 10)); }

    internal TurnoEmailOutboxDispatcher(
        Func<string, string, DateTime, TimeSpan, Task<TurnoOutboxPersistence.BoundClaim?>> claim,
        Func<string, string, TurnoOutboxPersistence.BoundClaim, DateTime, Task<bool>> begin,
        Func<string, string, TurnoOutboxPersistence.BoundClaim, DateTime, Task<bool>> release,
        Func<string, string, long, Task<bool>> complete,
        Func<TurnoOutboxPersistence.BoundClaim, Task<EmailMessage?>> resolve,
        Func<EmailMessage, CancellationToken, Task<ResultadoEnvioEmail>> send, Func<DateTime> clock, TimeSpan lease)
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

    internal TurnoEmailOutboxDispatcher(
        Func<string, string, DateTime, TimeSpan, Task<TurnoOutboxPersistence.BoundClaim?>> claim,
        Func<string, string, TurnoOutboxPersistence.BoundClaim, DateTime, Task<EmailMessage?>> beginOffer,
        Func<string, string, TurnoOutboxPersistence.BoundClaim, DateTime, Task<bool>> release,
        Func<string, string, long, Task<bool>> complete,
        Func<EmailMessage, CancellationToken, Task<ResultadoEnvioEmail>> send, Func<DateTime> clock, TimeSpan lease)
        : this(claim, (id, token, binding, now) => Task.FromResult(false), release, complete,
            binding => Task.FromResult<EmailMessage?>(null), send, clock, lease)
    {
        this.beginOffer = beginOffer ?? throw new ArgumentNullException(nameof(beginOffer));
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
        if (binding.DeliveryChannel != TurnoOutboxPersistence.Channel.Email) return await Release(Result.Unresolved);
        if (binding.EventType == TurnoOutboxPersistence.Event.Offer)
        {
            if (beginOffer == null) return await Release(Result.Unresolved);
            if (cancellationToken.IsCancellationRequested)
            {
                try { cancellationToken.ThrowIfCancellationRequested(); }
                catch (OperationCanceledException original)
                {
                    try { if (!await ReleaseClaim()) throw new InvalidOperationException("Outbox release was rejected."); }
                    catch (Exception cleanup) { original.Data["OutboxReleaseFailure"] = cleanup; }
                    throw;
                }
            }
            EmailMessage? snapshot;
            try { snapshot = await beginOffer(id, token, binding, clock()); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { throw new DispatchException("begin", ex); }
            if (snapshot == null) return await Release(Result.Unresolved);
            try { if (!(await send(snapshot, cancellationToken)).Enviado) return Result.Indeterminate; }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { throw new DispatchException("send", ex); }
            try { return await complete(id, token, binding.Generation) ? Result.Completed : Result.CompletionRejected; }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { throw new DispatchException("complete", ex); }
        }
        EmailMessage? message;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            message = binding.DeliveryChannel == TurnoOutboxPersistence.Channel.Email ? await resolve(binding) : null;
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (Exception original)
        {
            try { if (!await ReleaseClaim()) throw new InvalidOperationException("Outbox release was rejected."); }
            catch (Exception cleanup)
            {
                if (original is OperationCanceledException) original.Data["OutboxReleaseFailure"] = cleanup;
                else throw new AggregateException("Resolution and cleanup failed.", original, cleanup);
            }
            throw;
        }
        if (message == null) return await Release(Result.Unresolved);
        try { if (!await begin(id, token, binding, clock())) return Result.ClaimLost; }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { throw new DispatchException("begin", ex); }
        // The sender contract does not distinguish definitive rejection from an uncertain outcome.
        try { if (!(await send(message, cancellationToken)).Enviado) return Result.Indeterminate; }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { throw new DispatchException("send", ex); }
        try { return await complete(id, token, binding.Generation) ? Result.Completed : Result.CompletionRejected; }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { throw new DispatchException("complete", ex); }
    }
}

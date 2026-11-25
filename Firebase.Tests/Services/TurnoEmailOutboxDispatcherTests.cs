using Firebase.Services;
using Firebase.Models;

namespace Firebase.Tests.Services;

public class TurnoEmailOutboxDispatcherTests
{
    private sealed class Fake
    {
        public string State = "Pending";
        public int Sends, Begins, Releases, Completes, Resolves;
        public bool Reject, Stale, Accepted = true, CancelOnResolve, CancelOnSend;
        public CancellationTokenSource Cancellation = new();
        public CancellationToken SentToken;
        public TurnoOutboxPersistence.Channel Channel = TurnoOutboxPersistence.Channel.Email;
        public TurnoOutboxPersistence.Event Event = TurnoOutboxPersistence.Event.Booking;
        public TurnoEmailOutboxDispatcher Make() => new(
            (id, token, now, lease) => { if (State != "Pending") return Task.FromResult<TurnoOutboxPersistence.BoundClaim?>(null); State = "Claimed"; return Task.FromResult<TurnoOutboxPersistence.BoundClaim?>(new(1, Event, Channel, "offer")); },
            (id, token, binding, now) => { Begins++; if (Stale) return Task.FromResult(false); State = "Indeterminate"; return Task.FromResult(true); },
            (id, token, binding, now) => { Releases++; State = "Pending"; return Task.FromResult(true); },
            (id, token, generation) => { Completes++; State = "Completed"; return Task.FromResult(true); },
            binding => { Resolves++; Assert.Equal("offer", binding.ReferenceId); if (CancelOnResolve) Cancellation.Cancel(); return Task.FromResult<EmailMessage?>(Reject ? null : new("a@example.com", "subject", "body")); },
            (message, cancellationToken) => { Assert.Equal("Indeterminate", State); Assert.Equal("a@example.com", message.To); SentToken = cancellationToken; Sends++; if (CancelOnSend) { Cancellation.Cancel(); cancellationToken.ThrowIfCancellationRequested(); } return Task.FromResult(new ResultadoEnvioEmail(Accepted, "result")); },
            () => new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(1));
    }

    [Fact] public async Task CancellationAfterOfferClaimReleasesBeforeBegin()
    {
        using var cts = new CancellationTokenSource(); int begins = 0, releases = 0, sends = 0;
        var dispatcher = new TurnoEmailOutboxDispatcher(
            (id, token, now, lease) => { cts.Cancel(); return Task.FromResult<TurnoOutboxPersistence.BoundClaim?>(new(1, TurnoOutboxPersistence.Event.Offer, TurnoOutboxPersistence.Channel.Email, "offer")); },
            (id, token, binding, now) => { begins++; return Task.FromResult<EmailMessage?>(null); },
            (id, token, binding, now) => { releases++; return Task.FromResult(true); },
            (id, token, generation) => Task.FromResult(true),
            (message, token) => { sends++; return Task.FromResult(new ResultadoEnvioEmail(true, "ok")); },
            () => new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(1));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dispatcher.Dispatch("id", "token", cts.Token));
        Assert.Equal(1, releases); Assert.Equal(0, begins); Assert.Equal(0, sends);
    }

    [Fact] public async Task OfferOnlyDispatcherReleasesBookingWithoutCallingOfferBoundary()
    {
        int begins = 0, releases = 0, sends = 0, completes = 0;
        var dispatcher = new TurnoEmailOutboxDispatcher(
            (id, token, now, lease) => Task.FromResult<TurnoOutboxPersistence.BoundClaim?>(new(1, TurnoOutboxPersistence.Event.Booking, TurnoOutboxPersistence.Channel.Email, "booking")),
            (id, token, claim, now) => { begins++; return Task.FromResult<EmailMessage?>(new("a@example.com", "subject", "body")); },
            (id, token, claim, now) => { releases++; return Task.FromResult(true); },
            (id, token, generation) => { completes++; return Task.FromResult(true); },
            (message, cancellationToken) => { sends++; return Task.FromResult(new ResultadoEnvioEmail(true, "ok")); },
            () => new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(1));
        Assert.Equal(TurnoEmailOutboxDispatcher.Result.Unresolved, await dispatcher.Dispatch("id", "token"));
        Assert.Equal(1, releases); Assert.Equal(0, begins); Assert.Equal(0, sends); Assert.Equal(0, completes);
    }

    [Fact] public async Task GenericRouteNeverSendsOffer()
    {
        var f = new Fake { Event = TurnoOutboxPersistence.Event.Offer };
        Assert.Equal(TurnoEmailOutboxDispatcher.Result.Unresolved, await f.Make().Dispatch("id", "token"));
        Assert.Equal(0, f.Resolves); Assert.Equal(0, f.Begins); Assert.Equal(0, f.Sends); Assert.Equal(1, f.Releases);
    }

    [Fact] public async Task AcceptedCompletesAndCannotSendTwice()
    {
        var f = new Fake(); var d = f.Make();
        Assert.Equal(TurnoEmailOutboxDispatcher.Result.Completed, await d.Dispatch("id", "token"));
        Assert.Equal(TurnoEmailOutboxDispatcher.Result.ClaimUnavailable, await d.Dispatch("id", "token"));
        Assert.Equal(1, f.Sends); Assert.Equal(1, f.Begins); Assert.Equal(1, f.Completes);
    }
    [Fact] public async Task UncertainSendIsNotRetried()
    {
        var f = new Fake { Accepted = false }; var d = f.Make();
        Assert.Equal(TurnoEmailOutboxDispatcher.Result.Indeterminate, await d.Dispatch("id", "token"));
        Assert.Equal(TurnoEmailOutboxDispatcher.Result.ClaimUnavailable, await d.Dispatch("id", "token"));
        Assert.Equal("Indeterminate", f.State); Assert.Equal(1, f.Sends); Assert.Equal(0, f.Completes); Assert.Equal(0, f.Releases);
    }
    [Fact] public async Task RejectedOrWrongChannelReleaseWithoutSending()
    {
        foreach (var f in new[] { new Fake { Reject = true }, new Fake { Channel = TurnoOutboxPersistence.Channel.WhatsApp } })
        {
            Assert.Equal(TurnoEmailOutboxDispatcher.Result.Unresolved, await f.Make().Dispatch("id", "token"));
            Assert.Equal("Pending", f.State); Assert.Equal(1, f.Releases); Assert.Equal(0, f.Begins); Assert.Equal(0, f.Sends);
        }
    }
    [Fact] public async Task CancellationBeforeClaimDoesNotClaim()
    {
        var f = new Fake(); f.Cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Make().Dispatch("id", "token", f.Cancellation.Token));
        Assert.Equal("Pending", f.State); Assert.Equal(0, f.Resolves); Assert.Equal(0, f.Sends);
    }
    [Fact] public async Task CancellationAfterResolveReleasesClaim()
    {
        var f = new Fake { CancelOnResolve = true };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Make().Dispatch("id", "token", f.Cancellation.Token));
        Assert.Equal("Pending", f.State); Assert.Equal(1, f.Releases); Assert.Equal(0, f.Begins); Assert.Equal(0, f.Sends);
    }
    [Fact] public async Task CancellationDuringSendPropagatesTokenAndDoesNotRetry()
    {
        var f = new Fake { CancelOnSend = true }; var d = f.Make();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => d.Dispatch("id", "token", f.Cancellation.Token));
        Assert.Equal(f.Cancellation.Token, f.SentToken); Assert.Equal("Indeterminate", f.State);
        Assert.Equal(0, f.Releases); Assert.Equal(0, f.Completes); Assert.Equal(1, f.Sends);
        Assert.Equal(TurnoEmailOutboxDispatcher.Result.ClaimUnavailable, await d.Dispatch("id", "token"));
        Assert.Equal(1, f.Sends);
    }
    [Fact] public async Task LostClaimNeverSends()
    {
        var f = new Fake { Stale = true };
        Assert.Equal(TurnoEmailOutboxDispatcher.Result.ClaimLost, await f.Make().Dispatch("id", "token"));
        Assert.Equal(0, f.Sends); Assert.Equal(0, f.Completes);
    }
}

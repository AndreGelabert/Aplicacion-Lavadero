using Firebase.Services;

namespace Firebase.Tests.Services;

public class TurnoWhatsAppOutboxDispatcherTests
{
    private sealed class Fake
    {
        public string State = "Pending";
        public int Sends, Completes, Resolves, Begins, Releases;
        public bool Stale, SendResult = true, Reject, ResolverThrows, ReleaseRejected;
        public Exception? ResolveError, ReleaseError, BeginError, SendError, CompleteError;
        public string Recipient = "543751590586";
        public TurnoOutboxPersistence.Channel Channel = TurnoOutboxPersistence.Channel.WhatsApp;
        public TurnoOutboxPersistence.Event EventType = TurnoOutboxPersistence.Event.Booking;
        public DateTime Now = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public TurnoWhatsAppOutboxDispatcher Make() => new(
            (id, token, now, lease) => { if (State != "Pending") return Task.FromResult<TurnoOutboxPersistence.BoundClaim?>(null); State = "Claimed"; return Task.FromResult<TurnoOutboxPersistence.BoundClaim?>(new(1, EventType, Channel, "offer-1")); },
            (id, token, claim, now) => { Begins++; if (BeginError != null) throw BeginError; if (Stale) return Task.FromResult(false); State = "Indeterminate"; return Task.FromResult(true); },
            (id, token, claim, now) => { Releases++; if (ReleaseError != null) throw ReleaseError; if (ReleaseRejected) return Task.FromResult(false); State = "Pending"; return Task.FromResult(true); },
            (id, token, generation) => { Completes++; if (CompleteError != null) throw CompleteError; State = "Completed"; return Task.FromResult(true); },
            claim => { Resolves++; if (ResolveError != null) throw ResolveError; if (ResolverThrows) throw new InvalidOperationException(); Assert.Equal("offer-1", claim.ReferenceId); return Task.FromResult<TurnoWhatsAppOutboxDispatcher.Delivery?>(Reject ? null : new(Recipient, "hello")); },
            (to, text) => { Assert.Equal("Indeterminate", State); Assert.Equal(Recipient, to); Assert.Equal("hello", text); Sends++; if (SendError != null) throw SendError; return Task.FromResult(SendResult); },
            () => Now, TimeSpan.FromMinutes(1));
    }
    [Fact]
    public async Task AtomicDispatcherSendsOnlyCommittedSnapshot() {
        var sent = new List<TurnoWhatsAppOutboxDispatcher.Delivery>();
        var snapshot = new TurnoWhatsAppOutboxDispatcher.Delivery("543751590586", "committed");
        var dispatcher = new TurnoWhatsAppOutboxDispatcher(
            (id, token, now, lease) => Task.FromResult<TurnoOutboxPersistence.BoundClaim?>(new(1, TurnoOutboxPersistence.Event.Offer, TurnoOutboxPersistence.Channel.WhatsApp, "offer")),
            (id, token, claim, now) => Task.FromResult<TurnoWhatsAppOutboxDispatcher.Delivery?>(snapshot),
            (id, token, claim, now) => Task.FromResult(true),
            (id, token, generation) => Task.FromResult(true),
            (recipient, message) => { sent.Add(new(recipient, message)); return Task.FromResult(true); },
            () => new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(1));
        Assert.Equal(TurnoWhatsAppOutboxDispatcher.Result.Completed, await dispatcher.Dispatch("one", "token"));
        Assert.Equal(snapshot, Assert.Single(sent));
    }
    [Fact]
    public async Task OfferOnlyDispatcherReleasesBookingWithoutCallingOfferBoundary() {
        int begins = 0, releases = 0, sends = 0, completes = 0;
        var dispatcher = new TurnoWhatsAppOutboxDispatcher(
            (id, token, now, lease) => Task.FromResult<TurnoOutboxPersistence.BoundClaim?>(new(1, TurnoOutboxPersistence.Event.Booking, TurnoOutboxPersistence.Channel.WhatsApp, "booking")),
            (id, token, claim, now) => { begins++; return Task.FromResult<TurnoWhatsAppOutboxDispatcher.Delivery?>(new("543751590586", "unexpected")); },
            (id, token, claim, now) => { releases++; return Task.FromResult(true); },
            (id, token, generation) => { completes++; return Task.FromResult(true); },
            (recipient, message) => { sends++; return Task.FromResult(true); },
            () => new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(1));
        Assert.Equal(TurnoWhatsAppOutboxDispatcher.Result.Unresolved, await dispatcher.Dispatch("one", "token"));
        Assert.Equal(1, releases); Assert.Equal(0, begins); Assert.Equal(0, sends); Assert.Equal(0, completes);
    }
    [Fact]
    public async Task GenericDispatcherRejectsOfferWithoutResolvingOrSending() {
        var f = new Fake { EventType = TurnoOutboxPersistence.Event.Offer };
        Assert.Equal(TurnoWhatsAppOutboxDispatcher.Result.Unresolved, await f.Make().Dispatch("one", "token"));
        Assert.Equal(0, f.Resolves); Assert.Equal(0, f.Begins); Assert.Equal(0, f.Sends); Assert.Equal(1, f.Releases);
    }
    [Fact]
    public async Task SuccessAndDuplicate() {
        var f = new Fake(); var d = f.Make();
        Assert.Equal(TurnoWhatsAppOutboxDispatcher.Result.Completed, await d.Dispatch("one", "token"));
        Assert.Equal(TurnoWhatsAppOutboxDispatcher.Result.ClaimUnavailable, await d.Dispatch("one", "token"));
        Assert.Equal("Completed", f.State); Assert.Equal(1, f.Sends); Assert.Equal(1, f.Completes); Assert.Equal(1, f.Resolves);
    }
    [Fact]
    public async Task UncertainNeverCompletesOrSendsAgain() {
        var f = new Fake { SendResult = false }; var d = f.Make();
        Assert.Equal(TurnoWhatsAppOutboxDispatcher.Result.Indeterminate, await d.Dispatch("one", "token"));
        Assert.Equal(TurnoWhatsAppOutboxDispatcher.Result.ClaimUnavailable, await d.Dispatch("one", "token"));
        Assert.Equal("Indeterminate", f.State); Assert.Equal(1, f.Sends); Assert.Equal(0, f.Completes);
    }
    [Fact]
    public async Task StaleClaimNeverSends() {
        var f = new Fake { Stale = true };
        Assert.Equal(TurnoWhatsAppOutboxDispatcher.Result.ClaimLost, await f.Make().Dispatch("one", "token"));
        Assert.Equal(0, f.Sends); Assert.Equal(0, f.Completes);
    }
    [Theory] [InlineData("")] [InlineData("123")] [InlineData("abc1234567890")]
    public async Task InvalidRecipientNeverBeginsOrSends(string recipient) {
        var f = new Fake { Recipient = recipient };
        Assert.Equal(TurnoWhatsAppOutboxDispatcher.Result.InvalidRecipient, await f.Make().Dispatch("one", "token"));
        Assert.Equal("Pending", f.State); Assert.Equal(0, f.Begins); Assert.Equal(0, f.Sends); Assert.Equal(0, f.Completes);
    }
    [Fact]
    public async Task ResolverExceptionReleasesBeforePropagating() {
        var f = new Fake { ResolverThrows = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Make().Dispatch("one", "token"));
        Assert.Equal("Pending", f.State); Assert.Equal(0, f.Begins); Assert.Equal(0, f.Sends);
    }
    [Theory] [InlineData("begin")] [InlineData("send")] [InlineData("complete")] [InlineData("release")]
    public async Task BoundaryErrorsPreserveCauseAndNeverRetry(string boundary) {
        var error = new InvalidOperationException(boundary); var f = new Fake();
        if (boundary == "begin") f.BeginError = error;
        if (boundary == "send") f.SendError = error;
        if (boundary == "complete") f.CompleteError = error;
        if (boundary == "release") { f.Reject = true; f.ReleaseError = error; }
        var ex = await Assert.ThrowsAsync<TurnoWhatsAppOutboxDispatcher.DispatchException>(() => f.Make().Dispatch("one", "token"));
        Assert.Same(error, ex.InnerException); Assert.Equal(boundary, ex.Phase);
        Assert.Equal(boundary == "release" ? 1 : 0, f.Releases);
        Assert.True(f.Sends <= 1); Assert.True(f.Begins <= 1); Assert.True(f.Completes <= 1);
    }
    [Theory] [InlineData("resolve")] [InlineData("release")] [InlineData("begin")] [InlineData("send")] [InlineData("complete")]
    public async Task CancellationIsPropagatedWithoutRetry(string boundary) {
        var error = new OperationCanceledException(boundary); var f = new Fake();
        if (boundary == "resolve") f.ResolveError = error;
        if (boundary == "release") { f.Reject = true; f.ReleaseError = error; }
        if (boundary == "begin") f.BeginError = error;
        if (boundary == "send") f.SendError = error;
        if (boundary == "complete") f.CompleteError = error;
        Assert.Same(error, await Assert.ThrowsAsync<OperationCanceledException>(() => f.Make().Dispatch("one", "token")));
        Assert.Equal(boundary is "resolve" or "release" ? 1 : 0, f.Releases);
        Assert.True(f.Sends <= 1); Assert.True(f.Completes <= 1);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task ResolverCancellationSurvivesCleanupFailure(bool rejected) {
        var cancellation = new OperationCanceledException("resolver"); var cleanup = new InvalidOperationException("cleanup");
        var f = new Fake { ResolveError = cancellation, ReleaseError = rejected ? null : cleanup, ReleaseRejected = rejected };
        var ex = await Assert.ThrowsAsync<OperationCanceledException>(() => f.Make().Dispatch("one", "token"));
        Assert.Same(cancellation, ex);
        var diagnostic = Assert.IsAssignableFrom<Exception>(ex.Data["OutboxReleaseFailure"]);
        if (rejected) Assert.Equal("Outbox release was rejected.", diagnostic.Message);
        else { var wrapped = Assert.IsType<TurnoWhatsAppOutboxDispatcher.DispatchException>(diagnostic); Assert.Same(cleanup, wrapped.InnerException); }
        Assert.Equal(1, f.Releases); Assert.Equal(0, f.Begins); Assert.Equal(0, f.Sends); Assert.Equal(0, f.Completes);
    }
    [Fact]
    public async Task TurnoOfferCompositionTests_CancellationDuringResolveReleasesBeforeBegin() {
        using var cts = new CancellationTokenSource();
        int releases = 0, begins = 0, sends = 0, completes = 0;
        var claim = new TurnoOutboxPersistence.BoundClaim(7, TurnoOutboxPersistence.Event.Booking, TurnoOutboxPersistence.Channel.WhatsApp, "booking");
        var dispatcher = new TurnoWhatsAppOutboxDispatcher(
            (id, token, now, lease) => Task.FromResult<TurnoOutboxPersistence.BoundClaim?>(claim),
            (id, token, binding, now) => { begins++; return Task.FromResult(true); },
            (id, token, binding, now) => { Assert.Equal("id", id); Assert.Equal("token", token); Assert.Same(claim, binding); releases++; return Task.FromResult(true); },
            (id, token, generation) => { completes++; return Task.FromResult(true); },
            binding => { cts.Cancel(); return Task.FromResult<TurnoWhatsAppOutboxDispatcher.Delivery?>(new("543751590586", "hello")); },
            (recipient, message) => { sends++; return Task.FromResult(true); }, () => new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(1));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dispatcher.Dispatch("id", "token", cts.Token));
        Assert.Equal(1, releases); Assert.Equal(0, begins); Assert.Equal(0, sends); Assert.Equal(0, completes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TurnoOfferCompositionTests_CancelledAmbiguousBeginNeverReleasesOrSends(bool offer) {
        var state = "Claimed"; int releases = 0, sends = 0, completes = 0;
        var claim = new TurnoOutboxPersistence.BoundClaim(3, offer ? TurnoOutboxPersistence.Event.Offer : TurnoOutboxPersistence.Event.Booking, TurnoOutboxPersistence.Channel.WhatsApp, "ref");
        Task<TurnoOutboxPersistence.BoundClaim?> Claim(string id, string token, DateTime now, TimeSpan duration) => Task.FromResult<TurnoOutboxPersistence.BoundClaim?>(claim);
        Task<bool> Release(string id, string token, TurnoOutboxPersistence.BoundClaim binding, DateTime now) { releases++; state = "Pending"; return Task.FromResult(true); }
        Task<bool> Complete(string id, string token, long generation) { completes++; return Task.FromResult(true); }
        Task<bool> Send(string recipient, string message) { sends++; return Task.FromResult(true); }
        Func<DateTime> clock = () => new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var dispatcher = offer ? new TurnoWhatsAppOutboxDispatcher(Claim,
            (id, token, binding, now) => { state = "Indeterminate"; throw new OperationCanceledException(); }, Release, Complete, Send, clock, TimeSpan.FromMinutes(1))
            : new TurnoWhatsAppOutboxDispatcher(Claim,
            (id, token, binding, now) => { state = "Indeterminate"; throw new OperationCanceledException(); }, Release, Complete,
            binding => Task.FromResult<TurnoWhatsAppOutboxDispatcher.Delivery?>(new("543751590586", "hello")), Send, clock, TimeSpan.FromMinutes(1));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dispatcher.Dispatch("id", "token"));
        Assert.Equal("Indeterminate", state); Assert.Equal(0, releases); Assert.Equal(0, sends); Assert.Equal(0, completes);
    }

    [Fact]
    public async Task ResolverAndCleanupErrorsBothSurvive() {
        var original = new InvalidOperationException("resolver"); var cleanup = new InvalidOperationException("cleanup");
        var f = new Fake { ResolveError = original, ReleaseError = cleanup };
        var ex = await Assert.ThrowsAsync<AggregateException>(() => f.Make().Dispatch("one", "token"));
        Assert.Same(original, ex.InnerExceptions[0]);
        var wrapped = Assert.IsType<TurnoWhatsAppOutboxDispatcher.DispatchException>(ex.InnerExceptions[1]);
        Assert.Equal("release", wrapped.Phase); Assert.Same(cleanup, wrapped.InnerException);
        Assert.Equal(1, f.Releases); Assert.Equal(0, f.Begins); Assert.Equal(0, f.Sends);
    }
    [Fact]
    public async Task ResolverRejectionAndWrongChannelFailClosed() {
        var rejected = new Fake { Reject = true };
        Assert.Equal(TurnoWhatsAppOutboxDispatcher.Result.Unresolved, await rejected.Make().Dispatch("one", "token"));
        Assert.Equal("Pending", rejected.State); Assert.Equal(0, rejected.Begins); Assert.Equal(0, rejected.Sends);
        var wrong = new Fake { Channel = TurnoOutboxPersistence.Channel.Email };
        Assert.Equal(TurnoWhatsAppOutboxDispatcher.Result.Unresolved, await wrong.Make().Dispatch("one", "token"));
        Assert.Equal("Pending", wrong.State); Assert.Equal(0, wrong.Resolves); Assert.Equal(0, wrong.Begins); Assert.Equal(0, wrong.Sends);
    }
}

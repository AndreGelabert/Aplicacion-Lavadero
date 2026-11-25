using Firebase.Services;

namespace Firebase.Tests.Services;

public class WhatsAppInboxProcessorTests
{
    private static readonly DateTime Start = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private sealed class Inbox
    {
        public DateTime Now = Start;
        public WhatsAppWebhookInbox.Lease? Lease;
        public DateTime Expiry;
        public bool Completed;
        public int Claims, Completions, Handled;
        public Func<Task>? OnHandle;

        public Task<WhatsAppWebhookInbox.Lease?> Claim(string id, DateTime now, DateTime expiry)
        {
            Claims++;
            if (Completed || (Lease != null && Expiry > now)) return Task.FromResult<WhatsAppWebhookInbox.Lease?>(null);
            Lease = new WhatsAppWebhookInbox.Lease(Guid.NewGuid().ToString(), (Lease?.Generation ?? 0) + 1);
            Expiry = expiry;
            return Task.FromResult<WhatsAppWebhookInbox.Lease?>(Lease);
        }
        public Task<bool> Complete(string id, string token, long generation, DateTime now)
        {
            Completions++;
            if (Completed || Lease?.Token != token || Lease.Generation != generation || now >= Expiry) return Task.FromResult(false);
            Completed = true;
            return Task.FromResult(true);
        }
        public async Task Handle(string id) { Handled++; if (OnHandle != null) await OnHandle(); }
        public WhatsAppInboxProcessor Processor() => new(Claim, Complete, Handle, () => Now, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task SuccessAndDuplicateDoNotHandleTwice()
    {
        var inbox = new Inbox(); var processor = inbox.Processor();
        Assert.Equal(WhatsAppInboxProcessor.Result.Completed, await processor.Process("one"));
        Assert.Equal(WhatsAppInboxProcessor.Result.ClaimUnavailable, await processor.Process("one"));
        Assert.True(inbox.Completed); Assert.Equal(1, inbox.Handled); Assert.Equal(1, inbox.Completions);
    }

    [Fact]
    public async Task UnavailableClaimHasNoHandlerOrCompletionEffects()
    {
        var inbox = new Inbox(); await inbox.Claim("one", Start, Start.AddMinutes(1));
        var original = inbox.Lease;
        Assert.Equal(WhatsAppInboxProcessor.Result.ClaimUnavailable, await inbox.Processor().Process("one"));
        Assert.Same(original, inbox.Lease); Assert.False(inbox.Completed); Assert.Equal(0, inbox.Handled); Assert.Equal(0, inbox.Completions);
    }

    [Fact]
    public async Task HandlerFailureIsAmbiguousAndNeverCompletesOrRetries()
    {
        var inbox = new Inbox { OnHandle = () => throw new InvalidOperationException("effect may have occurred") };
        Assert.Equal(WhatsAppInboxProcessor.Result.HandlerFailedAmbiguous, await inbox.Processor().Process("one"));
        Assert.False(inbox.Completed); Assert.Equal(1, inbox.Handled); Assert.Equal(1, inbox.Claims); Assert.Equal(0, inbox.Completions);
    }

    [Fact]
    public async Task ExpiredLeaseIsNotCompleted()
    {
        var inbox = new Inbox(); inbox.OnHandle = () => { inbox.Now = Start.AddMinutes(1); return Task.CompletedTask; };
        Assert.Equal(WhatsAppInboxProcessor.Result.CompletionRejected, await inbox.Processor().Process("one"));
        Assert.False(inbox.Completed); Assert.Equal(1, inbox.Handled);
    }

    [Fact]
    public async Task ReclaimedLeaseFencesOldOwner()
    {
        var inbox = new Inbox();
        inbox.OnHandle = async () => { inbox.Now = Start.AddMinutes(1); await inbox.Claim("one", inbox.Now, inbox.Now.AddMinutes(1)); };
        Assert.Equal(WhatsAppInboxProcessor.Result.CompletionRejected, await inbox.Processor().Process("one"));
        Assert.False(inbox.Completed); Assert.Equal(2, inbox.Lease!.Generation); Assert.Equal(1, inbox.Handled);
    }
}

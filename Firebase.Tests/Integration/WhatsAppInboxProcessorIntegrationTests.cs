using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using System.Security.Cryptography;
using System.Text;

namespace Firebase.Tests.Integration;

public class WhatsAppInboxProcessorIntegrationTests
{
    private static readonly DateTime Start = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static FirestoreDb Setup() => new FirestoreDbBuilder { ProjectId = "demo-processor-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
    private static DocumentReference Doc(FirestoreDb db, string id) => db.Collection("whatsapp_inbox").Document(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id))));
    private static WhatsAppInboxProcessor Processor(FirestoreDb db, Func<string, Task> handler, Func<DateTime> clock) =>
        new((id, now, expiry) => WhatsAppWebhookInbox.Claim(db, id, now, expiry),
            (id, token, generation, now) => WhatsAppWebhookInbox.Complete(db, id, token, generation, now),
            handler, clock, TimeSpan.FromMinutes(1));

    [LocalFirestoreFact]
    public async Task RegisteredMessageCompletesAndCannotBeProcessedAgain()
    {
        var db = Setup(); const string id = "wamid.success"; var calls = 0;
        Assert.Equal(WhatsAppWebhookInbox.Result.Created, await WhatsAppWebhookInbox.Register(db, id, "payload"));
        var processor = Processor(db, received => { Assert.Equal(id, received); calls++; return Task.CompletedTask; }, () => Start);
        Assert.Equal(WhatsAppInboxProcessor.Result.Completed, await processor.Process(id));
        var completed = await Doc(db, id).GetSnapshotAsync();
        Assert.Equal("Completed", completed.GetValue<string>("State"));
        Assert.Equal(1L, completed.GetValue<long>("Generation"));
        Assert.Equal(WhatsAppInboxProcessor.Result.ClaimUnavailable, await processor.Process(id));
        Assert.Equal(1, calls);
        Assert.Equal(completed.UpdateTime, (await Doc(db, id).GetSnapshotAsync()).UpdateTime);
    }

    [LocalFirestoreFact]
    public async Task ActiveHandlerRefusesConcurrentProcessorWithoutWriting()
    {
        var db = Setup(); const string id = "wamid.concurrent"; var calls = 0; var competingCalls = 0;
        Assert.Equal(WhatsAppWebhookInbox.Result.Created, await WhatsAppWebhookInbox.Register(db, id, "payload"));
        var competitor = Processor(db, _ => { competingCalls++; return Task.CompletedTask; }, () => Start);
        var processor = Processor(db, async _ => {
            calls++;
            var before = await Doc(db, id).GetSnapshotAsync();
            Assert.Equal("Claimed", before.GetValue<string>("State"));
            Assert.Equal(WhatsAppInboxProcessor.Result.ClaimUnavailable, await competitor.Process(id));
            Assert.Equal(before.UpdateTime, (await Doc(db, id).GetSnapshotAsync()).UpdateTime);
        }, () => Start);
        Assert.Equal(WhatsAppInboxProcessor.Result.Completed, await processor.Process(id));
        Assert.Equal(1, calls); Assert.Equal(0, competingCalls);
        Assert.Equal("Completed", (await Doc(db, id).GetSnapshotAsync()).GetValue<string>("State"));
    }

    [LocalFirestoreFact]
    public async Task FailedHandlerRemainsClaimedUntilExpiryAndCanBeReclaimed()
    {
        var db = Setup(); const string id = "wamid.failure"; var now = Start; var calls = 0;
        Assert.Equal(WhatsAppWebhookInbox.Result.Created, await WhatsAppWebhookInbox.Register(db, id, "payload"));
        var failed = Processor(db, _ => { calls++; throw new InvalidOperationException(); }, () => now);
        Assert.Equal(WhatsAppInboxProcessor.Result.HandlerFailedAmbiguous, await failed.Process(id));
        var before = await Doc(db, id).GetSnapshotAsync();
        Assert.Equal("Claimed", before.GetValue<string>("State"));
        Assert.Equal(1L, before.GetValue<long>("Generation"));
        var retry = Processor(db, _ => { calls++; return Task.CompletedTask; }, () => now);
        Assert.Equal(WhatsAppInboxProcessor.Result.ClaimUnavailable, await retry.Process(id));
        Assert.Equal(before.UpdateTime, (await Doc(db, id).GetSnapshotAsync()).UpdateTime);
        now = Start.AddMinutes(1);
        Assert.Equal(WhatsAppInboxProcessor.Result.Completed, await retry.Process(id));
        var after = await Doc(db, id).GetSnapshotAsync();
        Assert.Equal("Completed", after.GetValue<string>("State"));
        Assert.Equal(2L, after.GetValue<long>("Generation")); Assert.Equal(2, calls);
    }

    [LocalFirestoreFact]
    public async Task ExpiredAndSupersededOwnersCannotComplete()
    {
        var db = Setup(); const string expiredId = "wamid.expired"; const string oldId = "wamid.old"; var now = Start;
        Assert.Equal(WhatsAppWebhookInbox.Result.Created, await WhatsAppWebhookInbox.Register(db, expiredId, "payload"));
        var expired = Processor(db, _ => { now = Start.AddMinutes(1); return Task.CompletedTask; }, () => now);
        Assert.Equal(WhatsAppInboxProcessor.Result.CompletionRejected, await expired.Process(expiredId));
        var expiredSnapshot = await Doc(db, expiredId).GetSnapshotAsync();
        Assert.Equal("Claimed", expiredSnapshot.GetValue<string>("State"));
        Assert.Equal(1L, expiredSnapshot.GetValue<long>("Generation"));

        now = Start;
        Assert.Equal(WhatsAppWebhookInbox.Result.Created, await WhatsAppWebhookInbox.Register(db, oldId, "payload"));
        DocumentSnapshot? reclaimed = null;
        var old = Processor(db, async _ => {
            now = Start.AddMinutes(1);
            Assert.NotNull(await WhatsAppWebhookInbox.Claim(db, oldId, now, now.AddMinutes(1)));
            reclaimed = await Doc(db, oldId).GetSnapshotAsync();
        }, () => now);
        Assert.Equal(WhatsAppInboxProcessor.Result.CompletionRejected, await old.Process(oldId));
        var after = await Doc(db, oldId).GetSnapshotAsync();
        Assert.NotNull(reclaimed); Assert.Equal(reclaimed!.UpdateTime, after.UpdateTime);
        Assert.Equal("Claimed", after.GetValue<string>("State")); Assert.Equal(2L, after.GetValue<long>("Generation"));
        Assert.True(await WhatsAppWebhookInbox.Complete(db, oldId, after.GetValue<string>("Token"), 2, now));
        Assert.Equal("Completed", (await Doc(db, oldId).GetSnapshotAsync()).GetValue<string>("State"));
    }
}

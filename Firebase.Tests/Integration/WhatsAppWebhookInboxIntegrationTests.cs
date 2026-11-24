using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using System.Security.Cryptography;
using System.Text;

namespace Firebase.Tests.Integration;

public class WhatsAppWebhookInboxIntegrationTests
{
    private static FirestoreDb Setup() => new FirestoreDbBuilder { ProjectId = "demo-inbox-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
    private static DocumentReference Doc(FirestoreDb db, string id) => db.Collection("whatsapp_inbox").Document(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id))));

    [LocalFirestoreFact]
    public async Task DuplicateAndConflictLeaveOriginalUntouched()
    {
        var db = Setup(); var doc = Doc(db, "wamid.test=");
        Assert.Equal(WhatsAppWebhookInbox.Result.Created, await WhatsAppWebhookInbox.Register(db, "wamid.test=", "payload"));
        var before = await doc.GetSnapshotAsync();
        Assert.Equal("Pending", before.GetValue<string>("State"));
        Assert.Equal("payload", before.GetValue<string>("Payload"));
        Assert.Equal(WhatsAppWebhookInbox.Result.Duplicate, await WhatsAppWebhookInbox.Register(db, "wamid.test=", "payload"));
        Assert.Equal(WhatsAppWebhookInbox.Result.Conflict, await WhatsAppWebhookInbox.Register(db, "wamid.test=", "other"));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        Assert.Single((await db.Collection("whatsapp_inbox").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task ClaimsAreFencedAndRegistrationRemainsIdempotent()
    {
        var db = Setup(); const string id = "wamid.claim"; var doc = Doc(db, id);
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await WhatsAppWebhookInbox.Register(db, id, "payload");
        var claims = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => WhatsAppWebhookInbox.Claim(db, id, now, now.AddMinutes(1))));
        var first = Assert.Single(claims.Where(x => x != null))!;
        Assert.Null(await WhatsAppWebhookInbox.Claim(db, id, now, now.AddMinutes(1)));
        var before = await doc.GetSnapshotAsync();
        Assert.Equal(WhatsAppWebhookInbox.Result.Duplicate, await WhatsAppWebhookInbox.Register(db, id, "payload"));
        Assert.Equal(WhatsAppWebhookInbox.Result.Conflict, await WhatsAppWebhookInbox.Register(db, id, "other"));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        var second = await WhatsAppWebhookInbox.Claim(db, id, now.AddMinutes(1), now.AddMinutes(2));
        Assert.NotNull(second); Assert.True(second!.Generation > first.Generation); Assert.NotEqual(first.Token, second.Token);
        before = await doc.GetSnapshotAsync();
        Assert.False(await WhatsAppWebhookInbox.Complete(db, id, first.Token, first.Generation, now.AddMinutes(1)));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        Assert.True(await WhatsAppWebhookInbox.Complete(db, id, second.Token, second.Generation, now.AddMinutes(1)));
        before = await doc.GetSnapshotAsync();
        Assert.Equal(WhatsAppWebhookInbox.Result.Duplicate, await WhatsAppWebhookInbox.Register(db, id, "payload"));
        Assert.Equal(WhatsAppWebhookInbox.Result.Conflict, await WhatsAppWebhookInbox.Register(db, id, "other"));
        Assert.Null(await WhatsAppWebhookInbox.Claim(db, id, now.AddMinutes(3), now.AddMinutes(4)));
        Assert.False(await WhatsAppWebhookInbox.Complete(db, id, second.Token, second.Generation, now.AddMinutes(1)));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
    }

    [LocalFirestoreFact]
    public async Task LegacyPendingAndCompletionExpiryBoundary()
    {
        var db = Setup(); const string id = "wamid.legacy"; var doc = Doc(db, id);
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        // Exact three-field record created by the previous Register implementation.
        await doc.SetAsync(new Dictionary<string, object> { ["Id"] = id, ["Payload"] = "payload", ["State"] = "Pending" });
        var before = await doc.GetSnapshotAsync();
        Assert.Equal(WhatsAppWebhookInbox.Result.Duplicate, await WhatsAppWebhookInbox.Register(db, id, "payload"));
        Assert.Equal(WhatsAppWebhookInbox.Result.Conflict, await WhatsAppWebhookInbox.Register(db, id, "other"));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        var first = await WhatsAppWebhookInbox.Claim(db, id, now, now.AddMinutes(1));
        Assert.NotNull(first);
        before = await doc.GetSnapshotAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => WhatsAppWebhookInbox.Complete(db, id, first!.Token, first.Generation, DateTime.SpecifyKind(now, DateTimeKind.Unspecified)));
        Assert.False(await WhatsAppWebhookInbox.Complete(db, id, first!.Token, first.Generation, now.AddMinutes(1)));
        Assert.False(await WhatsAppWebhookInbox.Complete(db, id, first.Token, first.Generation, now.AddMinutes(2)));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        var second = await WhatsAppWebhookInbox.Claim(db, id, now.AddMinutes(1), now.AddMinutes(2));
        Assert.NotNull(second);
        before = await doc.GetSnapshotAsync();
        Assert.False(await WhatsAppWebhookInbox.Complete(db, id, first.Token, first.Generation, now.AddMinutes(1)));
        Assert.False(await WhatsAppWebhookInbox.Complete(db, id, second!.Token, first.Generation, now.AddMinutes(1)));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        Assert.True(await WhatsAppWebhookInbox.Complete(db, id, second.Token, second.Generation, now.AddMinutes(2).AddTicks(-10)));
    }

    [LocalFirestoreFact]
    public async Task MalformedLifecycleNeverWrites()
    {
        var db = Setup(); const string id = "wamid.badclaim"; var doc = Doc(db, id);
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await doc.SetAsync(new Dictionary<string, object> { ["Id"] = id, ["Payload"] = "payload", ["State"] = "Claimed", ["Generation"] = 1L });
        var before = await doc.GetSnapshotAsync();
        Assert.Equal(WhatsAppWebhookInbox.Result.Corrupt, await WhatsAppWebhookInbox.Register(db, id, "payload"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => WhatsAppWebhookInbox.Claim(db, id, now, now.AddMinutes(1)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => WhatsAppWebhookInbox.Complete(db, id, "bad", 1, now));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        await Assert.ThrowsAsync<ArgumentException>(() => WhatsAppWebhookInbox.Claim(db, id, now, now));
        await Assert.ThrowsAsync<ArgumentException>(() => WhatsAppWebhookInbox.Claim(db, id, DateTime.SpecifyKind(now, DateTimeKind.Unspecified), now.AddMinutes(1)));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
    }

    [LocalFirestoreFact]
    public async Task ConcurrentRegistrationCreatesOneRecord()
    {
        var db = Setup();
        var results = await Task.WhenAll(WhatsAppWebhookInbox.Register(db, "wamid.concurrent", "payload"), WhatsAppWebhookInbox.Register(db, "wamid.concurrent", "payload"));
        Assert.Single(results.Where(x => x == WhatsAppWebhookInbox.Result.Created));
        Assert.Single(results.Where(x => x == WhatsAppWebhookInbox.Result.Duplicate));
        Assert.Single((await db.Collection("whatsapp_inbox").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task InvalidInputAndCorruptionDoNotWrite()
    {
        var db = Setup();
        foreach (var id in new[] { "", "a/b", "a b", new string('a', 513), "ñ" })
            await Assert.ThrowsAsync<ArgumentException>(() => WhatsAppWebhookInbox.Register(db, id, "payload"));
        await Assert.ThrowsAsync<ArgumentException>(() => WhatsAppWebhookInbox.Register(db, "ok", ""));
        Assert.Empty((await db.Collection("whatsapp_inbox").GetSnapshotAsync()).Documents);
        var doc = Doc(db, "wamid.corrupt");
        foreach (var data in new[] {
            new Dictionary<string, object> { ["State"] = "Pending" },
            new Dictionary<string, object> { ["State"] = "Pending", ["Id"] = "wrong", ["Payload"] = "payload" },
            new Dictionary<string, object> { ["State"] = "Done", ["Id"] = "wamid.corrupt", ["Payload"] = "payload" },
            new Dictionary<string, object> { ["State"] = "Pending", ["Id"] = "wamid.corrupt", ["Payload"] = 42L }
        })
        {
            await doc.SetAsync(data); var before = await doc.GetSnapshotAsync();
            Assert.Equal(WhatsAppWebhookInbox.Result.Corrupt, await WhatsAppWebhookInbox.Register(db, "wamid.corrupt", "payload"));
            Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        }
    }
}

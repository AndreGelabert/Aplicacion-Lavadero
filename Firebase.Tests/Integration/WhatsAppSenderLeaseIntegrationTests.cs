using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using System.Security.Cryptography;
using System.Text;

namespace Firebase.Tests.Integration;

public class WhatsAppSenderLeaseIntegrationTests
{
    private static FirestoreDb Setup() => new FirestoreDbBuilder { ProjectId = "demo-sender-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
    private static DocumentReference Doc(FirestoreDb db, string sender) => db.Collection("whatsapp_sender_leases").Document(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sender))));
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [LocalFirestoreFact]
    public async Task DifferentMessagesContendButDifferentSendersAreIndependent()
    {
        var db = Setup();
        var results = await Task.WhenAll(WhatsAppSenderLease.Claim(db, "sender-a", Now, Now.AddMinutes(1)), WhatsAppSenderLease.Claim(db, "sender-a", Now, Now.AddMinutes(1)));
        Assert.Single(results.Where(x => x != null));
        Assert.NotNull(await WhatsAppSenderLease.Claim(db, "sender-b", Now, Now.AddMinutes(1)));
        var snapshot = await Doc(db, "sender-a").GetSnapshotAsync();
        Assert.DoesNotContain("sender-a", string.Join(" ", snapshot.ToDictionary().Values));
        Assert.Equal(2, (await db.Collection("whatsapp_sender_leases").GetSnapshotAsync()).Count);
    }

    [LocalFirestoreFact]
    public async Task ReclaimFencesOldReleaseAndReleaseAllowsNewClaim()
    {
        var db = Setup(); const string sender = "sender-reclaim"; var doc = Doc(db, sender);
        var first = (await WhatsAppSenderLease.Claim(db, sender, Now, Now.AddMinutes(1)))!;
        var before = await doc.GetSnapshotAsync();
        Assert.Null(await WhatsAppSenderLease.Claim(db, sender, Now, Now.AddMinutes(1)));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        var second = (await WhatsAppSenderLease.Claim(db, sender, Now.AddMinutes(1), Now.AddMinutes(2)))!;
        Assert.NotEqual(first.Token, second.Token); Assert.True(second.Generation > first.Generation);
        before = await doc.GetSnapshotAsync();
        Assert.False(await WhatsAppSenderLease.Release(db, sender, first, Now.AddMinutes(1)));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        Assert.True(await WhatsAppSenderLease.Release(db, sender, second, Now.AddMinutes(1)));
        var third = await WhatsAppSenderLease.Claim(db, sender, Now.AddMinutes(1), Now.AddMinutes(2));
        Assert.NotNull(third); Assert.True(third!.Generation > second.Generation);
    }

    [LocalFirestoreFact]
    public async Task CorruptDocumentAndInvalidTimesFailClosedWithoutWrites()
    {
        var db = Setup(); const string sender = "sender-corrupt"; var doc = Doc(db, sender);
        await doc.SetAsync(new Dictionary<string, object> { ["Generation"] = 1L });
        var before = await doc.GetSnapshotAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => WhatsAppSenderLease.Claim(db, sender, Now, Now.AddMinutes(1)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => WhatsAppSenderLease.Release(db, sender, new WhatsAppSenderLease.Lease(new string('A', 64), 1), Now));
        await Assert.ThrowsAsync<ArgumentException>(() => WhatsAppSenderLease.Claim(db, sender, Now, Now));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
    }
}

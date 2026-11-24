using Google.Cloud.Firestore;
using System.Security.Cryptography;
using System.Text;

namespace Firebase.Services;

/// <summary>
/// Isolated per-sender lease; caller supplies a stable, already canonical sender identifier
/// and UTC times. Ordering is unspecified, not chronological or FIFO. Expiry uses the
/// supplied clock, not commit time; work may outlive it. This does not fence external
/// effects or guarantee exactly-once processing. No existing inbox records are modified.
/// </summary>
internal static class WhatsAppSenderLease
{
    internal sealed record Lease(string Token, long Generation);
    private static readonly UTF8Encoding Utf8 = new(false, true);

    private static DocumentReference Document(FirestoreDb db, string sender)
    {
        ArgumentNullException.ThrowIfNull(db);
        if (string.IsNullOrEmpty(sender) || sender.Length > 512) throw new ArgumentException("Invalid sender.");
        try { return db.Collection("whatsapp_sender_leases").Document(Convert.ToHexString(SHA256.HashData(Utf8.GetBytes(sender)))); }
        catch (EncoderFallbackException) { throw new ArgumentException("Invalid sender."); }
    }

    private static (string State, string Token, long Generation, Timestamp Expiry) Read(DocumentSnapshot snapshot)
    {
        var data = snapshot.ToDictionary();
        if (data.Count != 4 || !data.TryGetValue("State", out var s) || s is not string state || (state != "Held" && state != "Released") ||
            !data.TryGetValue("Token", out var t) || t is not string token || token.Length != 64 || !token.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F') ||
            !data.TryGetValue("Generation", out var g) || g is not long generation || generation <= 0 ||
            !data.TryGetValue("Expiry", out var e) || e is not Timestamp expiry)
            throw new InvalidOperationException("Corrupt sender lease.");
        return (state, token, generation, expiry);
    }

    internal static Task<Lease?> Claim(FirestoreDb db, string sender, DateTime now, DateTime expiry)
    {
        var doc = Document(db, sender);
        if (now.Kind != DateTimeKind.Utc || expiry.Kind != DateTimeKind.Utc || expiry <= now) throw new ArgumentException("Invalid lease time.");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        return db.RunTransactionAsync<Lease?>(async tx =>
        {
            var snapshot = await tx.GetSnapshotAsync(doc);
            long generation = 1;
            if (snapshot.Exists)
            {
                var current = Read(snapshot);
                if (current.State == "Held" && current.Expiry.ToDateTime() > now) return null;
                generation = checked(current.Generation + 1);
            }
            var values = new Dictionary<string, object> { ["State"] = "Held", ["Token"] = token, ["Generation"] = generation, ["Expiry"] = Timestamp.FromDateTime(expiry) };
            if (snapshot.Exists) tx.Set(doc, values); else tx.Create(doc, values);
            return new Lease(token, generation);
        });
    }

    internal static Task<bool> Release(FirestoreDb db, string sender, Lease lease, DateTime now)
    {
        var doc = Document(db, sender);
        if (lease == null || string.IsNullOrEmpty(lease.Token) || lease.Generation <= 0 || now.Kind != DateTimeKind.Utc) throw new ArgumentException("Invalid lease release.");
        return db.RunTransactionAsync(async tx =>
        {
            var snapshot = await tx.GetSnapshotAsync(doc);
            if (!snapshot.Exists) return false;
            var current = Read(snapshot);
            if (current.State != "Held" || current.Token != lease.Token || current.Generation != lease.Generation || current.Expiry.ToDateTime() <= now) return false;
            tx.Update(doc, new Dictionary<string, object> { ["State"] = "Released" });
            return true;
        });
    }
}

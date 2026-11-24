using Google.Cloud.Firestore;
using System.Security.Cryptography;
using System.Text;

namespace Firebase.Services;

/// <summary>Stores caller-validated messages; does not authenticate, process or deliver them.
/// Payload identity is exact ordinal text. Claims do not guarantee exactly-once processing:
/// effects must be idempotent because a worker can crash after applying them.</summary>
internal static class WhatsAppWebhookInbox
{
    internal enum Result { Created, Duplicate, Conflict, Corrupt }
    internal sealed record Lease(string Token, long Generation);
    private const int MaxPayloadBytes = 256 * 1024;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    private static bool ValidId(string? id) => id is { Length: > 0 and <= 512 } &&
        id.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_' or '.' or '=');

    private static bool ValidPayload(string? payload)
    {
        if (string.IsNullOrEmpty(payload)) return false;
        try { return Utf8.GetByteCount(payload) <= MaxPayloadBytes; }
        catch (EncoderFallbackException) { return false; }
    }

    private static DocumentReference Document(FirestoreDb db, string id) =>
        db.Collection("whatsapp_inbox").Document(Convert.ToHexString(SHA256.HashData(Utf8.GetBytes(id))));

    private static bool ValidRecord(Dictionary<string, object> data, string id)
    {
        if (!data.TryGetValue("Id", out var storedId) || storedId is not string existingId || existingId != id ||
            !data.TryGetValue("Payload", out var payload) || payload is not string text || !ValidPayload(text) ||
            !data.TryGetValue("State", out var state) || state is not string status) return false;
        if (status == "Pending") return data.Count == 3;
        return (status == "Claimed" || status == "Completed") && data.Count == 6 &&
            data.TryGetValue("Token", out var token) && token is string t && t.Length == 64 && t.All(Uri.IsHexDigit) &&
            data.TryGetValue("Generation", out var generation) && generation is long g && g > 0 &&
            data.TryGetValue("LeaseExpiry", out var expiry) && expiry is Timestamp;
    }

    internal static Task<Result> Register(FirestoreDb db, string id, string payload)
    {
        ArgumentNullException.ThrowIfNull(db);
        if (!ValidId(id) || !ValidPayload(payload)) throw new ArgumentException("Invalid inbox message.");
        var doc = Document(db, id);
        return db.RunTransactionAsync(async tx =>
        {
            var snapshot = await tx.GetSnapshotAsync(doc);
            if (!snapshot.Exists)
            {
                tx.Create(doc, new Dictionary<string, object> { ["Id"] = id, ["Payload"] = payload, ["State"] = "Pending" });
                return Result.Created;
            }
            var data = snapshot.ToDictionary();
            if (!ValidRecord(data, id)) return Result.Corrupt;
            return (string)data["Payload"] == payload ? Result.Duplicate : Result.Conflict;
        });
    }

    internal static Task<Lease?> Claim(FirestoreDb db, string id, DateTime now, DateTime expiry)
    {
        ArgumentNullException.ThrowIfNull(db);
        if (!ValidId(id) || now.Kind != DateTimeKind.Utc || expiry.Kind != DateTimeKind.Utc || expiry <= now)
            throw new ArgumentException("Invalid inbox claim.");
        var doc = Document(db, id);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        return db.RunTransactionAsync<Lease?>(async tx =>
        {
            var snapshot = await tx.GetSnapshotAsync(doc);
            if (!snapshot.Exists) return null;
            var data = snapshot.ToDictionary();
            if (!ValidRecord(data, id)) throw new InvalidOperationException("Corrupt inbox message.");
            var state = (string)data["State"];
            if (state == "Completed" || (state == "Claimed" && ((Timestamp)data["LeaseExpiry"]).ToDateTime() > now)) return null;
            var generation = state == "Pending" ? 1 : checked((long)data["Generation"] + 1);
            tx.Update(doc, new Dictionary<string, object> { ["State"] = "Claimed", ["Token"] = token, ["Generation"] = generation, ["LeaseExpiry"] = Timestamp.FromDateTime(expiry) });
            return new Lease(token, generation);
        });
    }

    internal static Task<bool> Complete(FirestoreDb db, string id, string token, long generation, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(db);
        if (!ValidId(id) || string.IsNullOrEmpty(token) || generation <= 0 || now.Kind != DateTimeKind.Utc) throw new ArgumentException("Invalid inbox completion.");
        var doc = Document(db, id);
        return db.RunTransactionAsync(async tx =>
        {
            var snapshot = await tx.GetSnapshotAsync(doc);
            if (!snapshot.Exists) return false;
            var data = snapshot.ToDictionary();
            if (!ValidRecord(data, id)) throw new InvalidOperationException("Corrupt inbox message.");
            if ((string)data["State"] != "Claimed" || (string)data["Token"] != token || (long)data["Generation"] != generation ||
                ((Timestamp)data["LeaseExpiry"]).ToDateTime() <= now) return false;
            tx.Update(doc, new Dictionary<string, object> { ["State"] = "Completed" });
            return true;
        });
    }
}

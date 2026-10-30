using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Google.Cloud.Firestore;

namespace Firebase.Services;

/// <summary>Stores independent last-known-good snapshots without modifying vehicle records.</summary>
public sealed class FirestoreVehicleCatalogueSnapshotStore : IVehicleCatalogueSnapshotStore
{
    // Leave room for document metadata and Firestore's one MiB document limit.
    private const int MaxEncodedBytes = 800_000;
    private readonly FirestoreDb _firestore;

    public FirestoreVehicleCatalogueSnapshotStore(FirestoreDb firestore) => _firestore = firestore;

    public async Task<string?> ReadAsync(string key)
    {
        var document = await Document(key).GetSnapshotAsync();
        if (!document.Exists || !document.ContainsField("Data")) return null;
        var bytes = Convert.FromBase64String(document.GetValue<string>("Data"));
        using var input = new MemoryStream(bytes);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    public Task<long> ReserveAsync(string key) => _firestore.RunTransactionAsync(async transaction =>
    {
        var document = Document(key);
        var snapshot = await transaction.GetSnapshotAsync(document);
        var current = snapshot.Exists && snapshot.ContainsField("ReservedRevision")
            ? snapshot.GetValue<long>("ReservedRevision") : 0;
        var next = checked(current + 1);
        transaction.Set(document, new Dictionary<string, object> { ["ReservedRevision"] = next }, SetOptions.MergeAll);
        return next;
    });

    public async Task WriteAsync(string key, string value, long revision)
    {
        using var output = new MemoryStream();
        await using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
        await using (var writer = new StreamWriter(gzip, Encoding.UTF8))
            await writer.WriteAsync(value);

        var data = Convert.ToBase64String(output.ToArray());
        if (data.Length > MaxEncodedBytes)
            throw new InvalidOperationException("Vehicle catalogue snapshot exceeds the storage limit.");

        await _firestore.RunTransactionAsync(async transaction =>
        {
            var document = Document(key);
            var snapshot = await transaction.GetSnapshotAsync(document);
            var saved = snapshot.Exists && snapshot.ContainsField("SavedRevision")
                ? snapshot.GetValue<long>("SavedRevision") : 0;
            if (revision <= saved) return;
            transaction.Set(document, new Dictionary<string, object>
            {
                ["Data"] = data,
                ["UpdatedAt"] = Timestamp.GetCurrentTimestamp(),
                ["SavedRevision"] = revision
            }, SetOptions.MergeAll);
        });
    }

    private DocumentReference Document(string key)
    {
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
        return _firestore.Collection("vehicleCatalogueSnapshots").Document(id);
    }
}

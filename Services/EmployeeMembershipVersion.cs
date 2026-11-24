using Google.Cloud.Firestore;

namespace Firebase.Services;

/// <summary>Shared serialization point for employee membership mutations.</summary>
internal static class EmployeeMembershipVersion
{
    internal static DocumentReference Reference(FirestoreDb db) => db.Collection("coordinacion").Document("empleados");

    internal static async Task<DocumentSnapshot> ReadAsync(FirestoreDb db, Transaction transaction) =>
        await transaction.GetSnapshotAsync(Reference(db));

    internal static void Advance(Transaction transaction, DocumentSnapshot snapshot)
    {
        var version = snapshot.Exists ? snapshot.GetValue<long>("Version") : 0;
        transaction.Set(snapshot.Reference, new Dictionary<string, object> { ["Version"] = checked(version + 1) });
    }
}

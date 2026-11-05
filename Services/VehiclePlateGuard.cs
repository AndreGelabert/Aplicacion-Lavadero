using System.Security.Cryptography;
using System.Text;
using Google.Cloud.Firestore;

// Shared by every vehicle creation path. Legacy vehicles remain authoritative;
// this document only serializes competing creators, not ownership or deletion.
internal static class VehiclePlateGuard
{
    internal static string Normalize(string plate) => plate.Trim().ToUpperInvariant();

    internal static DocumentReference Reference(FirestoreDb db, string plate) =>
        db.Collection("vehiclePlateGuards").Document(Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(plate)))));

    internal static void Touch(Transaction tx, DocumentReference reference) =>
        tx.Set(reference, new Dictionary<string, object> { ["UpdatedAt"] = FieldValue.ServerTimestamp });
}

public sealed class VehiclePlateAlreadyExistsException : InvalidOperationException
{
    public VehiclePlateAlreadyExistsException() : base("La patente ya está registrada. Busque el vehículo para asociarlo.") { }
}

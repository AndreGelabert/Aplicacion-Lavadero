using System.Text;
using Google.Cloud.Firestore;

namespace Firebase.Services;

public sealed record TurnoCuponDisponible(string Id, double Percentage, DateTime ExpiresUtc);

public sealed class TurnoCuponConsultaService(FirestoreDb db)
{
    private const int MaxCoupons = 100;

    public static bool EsIdValido(string? id) =>
        !string.IsNullOrWhiteSpace(id) && !id.Contains('/') && id is not "." and not ".." &&
        !(id.StartsWith("__", StringComparison.Ordinal) && id.EndsWith("__", StringComparison.Ordinal)) &&
        Encoding.UTF8.GetByteCount(id) <= 1500;

    public async Task<IReadOnlyList<TurnoCuponDisponible>?> Consultar(string clienteId)
    {
        if (!EsIdValido(clienteId)) throw new ArgumentException("Cliente inválido.", nameof(clienteId));
        if (!(await db.Collection("clientes").Document(clienteId).GetSnapshotAsync()).Exists) return null;

        var snapshot = await db.Collection("turno_cupones").WhereEqualTo("Owner", clienteId).Limit(MaxCoupons + 1).GetSnapshotAsync();
        if (snapshot.Count > MaxCoupons) throw new InvalidOperationException("Demasiados cupones.");
        var now = DateTime.UtcNow;
        var result = new List<TurnoCuponDisponible>();
        foreach (var document in snapshot.Documents)
        {
            var fields = document.ToDictionary();
            if (fields.TryGetValue("Owner", out var owner) && owner is string ownerText &&
                string.Equals(ownerText, clienteId, StringComparison.Ordinal) &&
                fields.TryGetValue("Consumed", out var consumed) && consumed is false &&
                fields.TryGetValue("ExpiresUtc", out var expires) && expires is Timestamp expiry && expiry.ToDateTime() > now &&
                fields.TryGetValue("Percentage", out var value) && value is double percentage &&
                double.IsFinite(percentage) && percentage >= 0 && percentage <= 100)
                result.Add(new(document.Id, percentage, expiry.ToDateTime()));
        }
        return result;
    }
}

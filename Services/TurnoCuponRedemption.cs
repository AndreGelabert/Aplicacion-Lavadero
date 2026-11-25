using Google.Cloud.Firestore;

namespace Firebase.Services;

// Read only: the eventual wash transaction must re-read and validate before any writes.
// Owner and discount state must come from trusted server-side wash inputs.
internal static class TurnoCuponRedemption
{
    internal static async Task<double> Preparar(Transaction transaction, DocumentReference coupon, string trustedOwner, bool hasOtherDiscount)
    {
        var snapshot = await transaction.GetSnapshotAsync(coupon);
        if (!snapshot.Exists || string.IsNullOrWhiteSpace(trustedOwner) || hasOtherDiscount)
            throw new InvalidOperationException("Cupón inválido.");

        var fields = snapshot.ToDictionary();
        if (!fields.TryGetValue("Owner", out var owner) || owner is not string ownerText ||
            !string.Equals(ownerText, trustedOwner, StringComparison.Ordinal) ||
            !fields.TryGetValue("Consumed", out var consumed) || consumed is not false ||
            !fields.TryGetValue("ExpiresUtc", out var expires) || expires is not Timestamp expiry ||
            expiry.ToDateTime() <= DateTime.UtcNow ||
            !fields.TryGetValue("Percentage", out var value) || value is not double percentage ||
            !double.IsFinite(percentage) || percentage < 0 || percentage > 100)
            throw new InvalidOperationException("Cupón inválido.");
        return percentage;
    }
}

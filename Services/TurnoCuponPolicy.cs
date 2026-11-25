namespace Firebase.Services;

internal sealed record TurnoCupon(string Owner, decimal Percentage, DateTime ExpiresUtc, bool Consumed);

internal static class TurnoCuponPolicy
{
    internal static TurnoCupon Crear(string owner, decimal percentage, DateTime cancelledUtc, int expiryDays, bool consumed)
    {
        if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("Owner is required.", nameof(owner));
        if (percentage < 0 || percentage > 100) throw new ArgumentOutOfRangeException(nameof(percentage));
        if (expiryDays < 1 || expiryDays > 365) throw new ArgumentOutOfRangeException(nameof(expiryDays));
        ValidarUtc(cancelledUtc, nameof(cancelledUtc));
        return new TurnoCupon(owner, percentage, cancelledUtc.AddDays(expiryDays), consumed);
    }

    internal static bool EsValido(TurnoCupon coupon, string owner, DateTime nowUtc, bool hasOtherDiscount)
    {
        ValidarUtc(nowUtc, nameof(nowUtc));
        return coupon != null && !string.IsNullOrWhiteSpace(owner)
            && string.Equals(coupon.Owner, owner, StringComparison.Ordinal)
            && !coupon.Consumed && !hasOtherDiscount && nowUtc < coupon.ExpiresUtc;
    }

    private static void ValidarUtc(DateTime value, string name)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC required.", name);
    }
}

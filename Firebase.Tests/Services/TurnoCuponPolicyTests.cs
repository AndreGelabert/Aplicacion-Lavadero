using Firebase.Services;

namespace Firebase.Tests.Services;

public class TurnoCuponPolicyTests
{
    private static readonly DateTime Cancelled = new(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ValidCouponExpiresExclusivelyAndPreservesTerms()
    {
        var coupon = TurnoCuponPolicy.Crear("owner", 0m, Cancelled, 30, false);
        Assert.Equal("owner", coupon.Owner);
        Assert.Equal(0m, coupon.Percentage);
        Assert.Equal(Cancelled.AddDays(30), coupon.ExpiresUtc);
        Assert.False(coupon.Consumed);
        Assert.True(TurnoCuponPolicy.EsValido(coupon, "owner", coupon.ExpiresUtc.AddTicks(-1), false));
        Assert.False(TurnoCuponPolicy.EsValido(coupon, "owner", coupon.ExpiresUtc, false));
        Assert.False(TurnoCuponPolicy.EsValido(coupon, "owner", coupon.ExpiresUtc.AddTicks(1), false));
        Assert.Equal("owner", coupon.Owner);
        Assert.Equal(0m, coupon.Percentage);
        Assert.Equal(Cancelled.AddDays(30), coupon.ExpiresUtc);
        Assert.False(coupon.Consumed);
    }

    [Fact]
    public void RejectsWrongOwnerConsumedAndStackingWithoutMutation()
    {
        var coupon = TurnoCuponPolicy.Crear("owner", 100m, Cancelled, 1, false);
        var consumed = TurnoCuponPolicy.Crear("owner", 10m, Cancelled, 1, true);
        Assert.True(TurnoCuponPolicy.EsValido(coupon, "owner", Cancelled, false));
        Assert.False(TurnoCuponPolicy.EsValido(coupon, "Owner", Cancelled, false));
        Assert.False(TurnoCuponPolicy.EsValido(coupon, "owner", Cancelled, true));
        Assert.False(TurnoCuponPolicy.EsValido(consumed, "owner", Cancelled, false));
        Assert.False(coupon.Consumed);
        Assert.True(consumed.Consumed);
    }

    [Fact]
    public void InvalidTermsAndUtcFailClosed()
    {
        foreach (var owner in new string?[] { null, "", " " })
            Assert.ThrowsAny<ArgumentException>(() => TurnoCuponPolicy.Crear(owner!, 10, Cancelled, 1, false));
        foreach (var percentage in new[] { -0.01m, 100.01m })
            Assert.ThrowsAny<ArgumentException>(() => TurnoCuponPolicy.Crear("owner", percentage, Cancelled, 1, false));
        foreach (var days in new[] { 0, 366 })
            Assert.ThrowsAny<ArgumentException>(() => TurnoCuponPolicy.Crear("owner", 10, Cancelled, days, false));
        foreach (var kind in new[] { DateTimeKind.Local, DateTimeKind.Unspecified })
        {
            Assert.ThrowsAny<ArgumentException>(() => TurnoCuponPolicy.Crear("owner", 10, DateTime.SpecifyKind(Cancelled, kind), 1, false));
            var coupon = TurnoCuponPolicy.Crear("owner", 10, Cancelled, 1, false);
            Assert.ThrowsAny<ArgumentException>(() => TurnoCuponPolicy.EsValido(coupon, "owner", DateTime.SpecifyKind(Cancelled, kind), false));
        }
        Assert.ThrowsAny<ArgumentException>(() => TurnoCuponPolicy.Crear("owner", 10, DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc), 1, false));
        var valid = TurnoCuponPolicy.Crear("owner", 10, Cancelled, 365, false);
        Assert.False(TurnoCuponPolicy.EsValido(valid, null!, Cancelled, false));
        Assert.False(TurnoCuponPolicy.EsValido(valid, " ", Cancelled, false));
    }
}

using Firebase.Models;
using Firebase.Services;

namespace Firebase.Tests.Services;

public class TurnoCancelacionPolicyTests
{
    private static readonly DateTime Start = new(2030, 1, 10, 12, 0, 0, DateTimeKind.Utc);
    private static Configuracion Config() => new() { CancelacionAnticipadaHorasMinimas = 24, CancelacionAnticipadaDescuento = 10, CancelacionAnticipadaValidezDias = 30 };

    [Fact]
    public void ThresholdIsInclusiveAndReturnsRawTermsWithoutMutatingConfiguration()
    {
        var config = Config();
        Assert.False(TurnoCancelacionPolicy.Evaluar(Start, Start.AddHours(-24).AddTicks(1), false, config).Elegible);
        var exact = TurnoCancelacionPolicy.Evaluar(Start, Start.AddHours(-24), false, config);
        Assert.True(exact.Elegible);
        Assert.Equal(10, exact.Porcentaje);
        Assert.Equal(30, exact.ValidezDias);
        Assert.True(TurnoCancelacionPolicy.Evaluar(Start, Start.AddHours(-24).AddTicks(-1), false, config).Elegible);
        Assert.Equal(24, config.CancelacionAnticipadaHorasMinimas);
        Assert.Equal(10, config.CancelacionAnticipadaDescuento);
        Assert.Equal(30, config.CancelacionAnticipadaValidezDias);
    }

    [Fact]
    public void NoShowNeverQualifiesAndZeroPercentIsNotSpecial()
    {
        var config = Config();
        config.CancelacionAnticipadaDescuento = 0;
        var ordinary = TurnoCancelacionPolicy.Evaluar(Start, Start.AddHours(-24), false, config);
        var noShow = TurnoCancelacionPolicy.Evaluar(Start, Start.AddHours(-24), true, config);
        Assert.True(ordinary.Elegible);
        Assert.False(noShow.Elegible);
        Assert.Equal(0, ordinary.Porcentaje);
        Assert.Equal(0, noShow.Porcentaje);
        Assert.Equal(30, noShow.ValidezDias);
    }

    [Fact]
    public void ConfigurationBoundsAreValidatedIncludingForNoShow()
    {
        foreach (var hours in new[] { 0, 169 })
        {
            var config = Config(); config.CancelacionAnticipadaHorasMinimas = hours;
            Assert.Throws<ArgumentOutOfRangeException>(() => TurnoCancelacionPolicy.Evaluar(Start, Start, true, config));
        }
        foreach (var percent in new[] { -0.01m, 100.01m })
        {
            var config = Config(); config.CancelacionAnticipadaDescuento = percent;
            Assert.Throws<ArgumentOutOfRangeException>(() => TurnoCancelacionPolicy.Evaluar(Start, Start, false, config));
        }
        foreach (var days in new[] { 0, 366 })
        {
            var config = Config(); config.CancelacionAnticipadaValidezDias = days;
            Assert.Throws<ArgumentOutOfRangeException>(() => TurnoCancelacionPolicy.Evaluar(Start, Start, false, config));
        }
        var bounds = Config();
        bounds.CancelacionAnticipadaHorasMinimas = 1; bounds.CancelacionAnticipadaValidezDias = 1;
        Assert.True(TurnoCancelacionPolicy.Evaluar(Start, Start.AddHours(-1), false, bounds).Elegible);
        bounds.CancelacionAnticipadaHorasMinimas = 168; bounds.CancelacionAnticipadaDescuento = 100; bounds.CancelacionAnticipadaValidezDias = 365;
        var result = TurnoCancelacionPolicy.Evaluar(Start, Start.AddHours(-168), false, bounds);
        Assert.True(result.Elegible);
        Assert.Equal(100, result.Porcentaje);
        Assert.Equal(365, result.ValidezDias);
    }

    [Fact]
    public void RejectsNonUtcInputsAndNullConfiguration()
    {
        foreach (var kind in new[] { DateTimeKind.Local, DateTimeKind.Unspecified })
        {
            Assert.Throws<ArgumentException>(() => TurnoCancelacionPolicy.Evaluar(DateTime.SpecifyKind(Start, kind), Start, false, Config()));
            Assert.Throws<ArgumentException>(() => TurnoCancelacionPolicy.Evaluar(Start, DateTime.SpecifyKind(Start, kind), false, Config()));
        }
        Assert.Throws<ArgumentNullException>(() => TurnoCancelacionPolicy.Evaluar(Start, Start, false, null!));
    }
}

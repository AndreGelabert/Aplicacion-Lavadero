using Firebase.Converters;
using Firebase.Services;
using Xunit;

namespace Firebase.Tests.Services;

public class StockPrecisionTests
{
    [Fact]
    public void AceptaValoresQueRecuperaElConversorReal()
    {
        var converter = new DecimalConverter();
        foreach (var valor in new[] { 0m, 0.1m, 1.5m, -250m, 1000m })
        {
            Assert.Equal(valor, converter.FromFirestore(converter.ToFirestore(valor)));
            Assert.Equal(valor, StockPrecision.ExigirRepresentable(valor));
        }
    }

    [Fact]
    public void RechazaCantidadesConPerdidaSinRedondear()
    {
        foreach (var valor in new[] { 1.0000000000000001m, 9007199254740993m, -9007199254740993m, decimal.MaxValue })
        {
            var error = Assert.Throws<InvalidOperationException>(() => StockPrecision.ExigirRepresentable(valor));
            Assert.Equal("La cantidad no puede persistirse sin pérdida de precisión.", error.Message);
        }
    }

    [Fact]
    public void RechazaAcumuladoAunqueLosOperandosSeanRepresentables()
    {
        Assert.Equal(1000000000000000m, StockPrecision.Sumar(999999999999999m, 1m));
        Assert.Throws<InvalidOperationException>(() => StockPrecision.Sumar(999999999999999m, 2m));
        Assert.Throws<InvalidOperationException>(() => StockPrecision.Restar(999999999999999m, -2m));
    }

    [Fact]
    public void RechazaOperandosNoRepresentablesYDesbordamiento()
    {
        Assert.Throws<InvalidOperationException>(() => StockPrecision.Sumar(1.0000000000000001m, -1m));
        Assert.Throws<InvalidOperationException>(() => StockPrecision.Restar(1m, 0.00000000000000001m));
        Assert.Throws<InvalidOperationException>(() => StockPrecision.Sumar(decimal.MaxValue, decimal.MaxValue));
        Assert.Equal(0.75m, StockPrecision.Restar(1m, 0.25m));
        Assert.Throws<InvalidOperationException>(() => StockPrecision.Sumar(1m, 0.0000000000000000000000000001m));
        Assert.Throws<InvalidOperationException>(() => StockPrecision.Restar(1m, 0.0000000000000000000000000001m));
    }
}

using Firebase.Converters;

namespace Firebase.Services;

/// <summary>Comprueba la recuperación decimal mediante el conversor vigente, no la exactitud binaria universal.</summary>
public static class StockPrecision
{
    private const string ErrorPrecision = "La cantidad no puede persistirse sin pérdida de precisión.";

    public static decimal ExigirRepresentable(decimal valor)
    {
        try
        {
            var converter = new DecimalConverter();
            if (converter.FromFirestore(converter.ToFirestore(valor)) == valor) return valor;
        }
        catch (OverflowException)
        {
            // La conversión de vuelta puede exceder el rango de decimal.
        }
        throw new InvalidOperationException(ErrorPrecision);
    }

    public static decimal Sumar(decimal primero, decimal segundo) => Operar(primero, segundo, false);
    public static decimal Restar(decimal primero, decimal segundo) => Operar(primero, segundo, true);

    private static decimal Operar(decimal primero, decimal segundo, bool restar)
    {
        ExigirRepresentable(primero);
        ExigirRepresentable(segundo);
        try
        {
            var resultado = restar ? checked(primero - segundo) : checked(primero + segundo);
            // Decimal también puede perder dígitos por escala aun sin desbordar.
            if ((restar ? checked(resultado + segundo) : checked(resultado - segundo)) != primero ||
                (restar ? checked(primero - resultado) : checked(resultado - primero)) != segundo)
                throw new InvalidOperationException(ErrorPrecision);
            return ExigirRepresentable(resultado);
        }
        catch (OverflowException)
        {
            throw new InvalidOperationException(ErrorPrecision);
        }
    }
}

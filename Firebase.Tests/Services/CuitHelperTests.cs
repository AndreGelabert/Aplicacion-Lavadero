using Firebase.Utils;
using Xunit;

namespace Firebase.Tests.Services;

public class CuitHelperTests
{
    [Theory]
    [InlineData("20-27362459-8")] // Hombre (con guiones)
    [InlineData("20273624598")]   // Hombre (sin guiones)
    [InlineData("27-28033514-8")] // Mujer (con guiones)
    [InlineData("27280335148")]   // Mujer (sin guiones)
    [InlineData("30-50001091-2")] // Persona jurídica (Banco Nación)
    [InlineData("30500010912")]   // Persona jurídica (sin guiones)
    [InlineData("23-00000002-7")] // Prefijo 23
    [InlineData("20-00000006-0")] // Caso suma múltiplo de 11 -> resto 0 -> DV = 0
    [InlineData("20-00000497-9")] // Caso suma con resto 1 -> delta 10 -> DV = 9
    public void EsValido_CuitsValidos_RetornaTrue(string cuit)
    {
        var esValido = CuitHelper.EsValido(cuit, out var error);

        Assert.True(esValido);
        Assert.Empty(error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EsValido_NuloOVacio_RetornaFalse(string? cuit)
    {
        var esValido = CuitHelper.EsValido(cuit, out var error);

        Assert.False(esValido);
        Assert.Equal("La identificación fiscal (CUIT) es obligatoria.", error);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("202736245")]
    [InlineData("20-2736245-8")]
    [InlineData("2027362459899")]
    public void EsValido_LongitudIncorrecta_RetornaFalse(string cuit)
    {
        var esValido = CuitHelper.EsValido(cuit, out var error);

        Assert.False(esValido);
        Assert.NotEmpty(error);
    }

    [Theory]
    [InlineData("201-2345678-9")]
    [InlineData("20-1234567-89")]
    [InlineData("20-123456789-0")]
    public void EsValido_GuionesMalUbicados_RetornaFalse(string cuit)
    {
        var esValido = CuitHelper.EsValido(cuit, out var error);

        Assert.False(esValido);
        Assert.Equal("El CUIT con guiones debe respetar el formato XX-XXXXXXXX-X.", error);
    }

    [Theory]
    [InlineData("10-27362459-8")]
    [InlineData("99-27362459-8")]
    [InlineData("40-50001091-2")]
    public void EsValido_PrefijoInvalido_RetornaFalse(string cuit)
    {
        var esValido = CuitHelper.EsValido(cuit, out var error);

        Assert.False(esValido);
        Assert.Contains("no corresponde a un CUIT/CUIL argentino válido", error);
    }

    [Theory]
    [InlineData("20-27362459-1")] // Debería ser 8
    [InlineData("27-28033514-0")] // Debería ser 8
    [InlineData("30-50001091-5")] // Debería ser 2
    [InlineData("20-00000006-1")] // Debería ser 0
    [InlineData("20-00000497-2")] // Debería ser 9
    public void EsValido_DigitoVerificadorIncorrecto_RetornaFalse(string cuit)
    {
        var esValido = CuitHelper.EsValido(cuit, out var error);

        Assert.False(esValido);
        Assert.Equal("El CUIT ingresado no es válido (dígito verificador incorrecto).", error);
    }

    [Fact]
    public void Formatear_ConvierteAFormatoCanonico()
    {
        Assert.Equal("20-27362459-8", CuitHelper.Formatear("20273624598"));
        Assert.Equal("20-27362459-8", CuitHelper.Formatear(" 20-27362459-8 "));
        Assert.Equal("30-50001091-2", CuitHelper.Formatear("30500010912"));
        // Si no tiene 11 dígitos, devuelve el string trimmed sin romper
        Assert.Equal("123", CuitHelper.Formatear(" 123 "));
    }

    [Fact]
    public void Limpiar_RemueveGuionesEspaciosYPuntos()
    {
        Assert.Equal("20273624598", CuitHelper.Limpiar("20-27362459-8"));
        Assert.Equal("20273624598", CuitHelper.Limpiar(" 20.27362459 8 "));
        Assert.Equal("", CuitHelper.Limpiar(null));
        Assert.Equal("", CuitHelper.Limpiar("   "));
    }
}

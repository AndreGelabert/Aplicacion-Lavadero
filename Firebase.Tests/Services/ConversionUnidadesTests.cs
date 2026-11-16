using Firebase.Converters;
using Firebase.Models;
using Firebase.Services;
using Xunit;

namespace Firebase.Tests.Services;

public class ConversionUnidadesTests
{
    private readonly ConversionUnidadesService _service = new();

    #region 1. Reglas de Conversión de Volumen (S25, T12)

    [Theory]
    [InlineData(1000, "ml", "l", 1.0)]
    [InlineData(1500, "ml", "l", 1.5)]
    [InlineData(250, "ml", "l", 0.25)]
    [InlineData(1, "l", "ml", 1000.0)]
    [InlineData(2.5, "l", "ml", 2500.0)]
    [InlineData(0.001, "l", "ml", 1.0)]
    public void Convertir_Volumen_MililitrosLitros_ConversionExacta(decimal cantidad, string origen, string destino, decimal esperado)
    {
        var resultado = _service.Convertir(cantidad, origen, destino);
        Assert.Equal(esperado, resultado);

        var ok = _service.TryConvertir(cantidad, origen, destino, out var resTry, out var error);
        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(esperado, resTry);

        var resObj = _service.ConvertirConResultado(cantidad, origen, destino);
        Assert.True(resObj.EsExitoso);
        Assert.Equal(esperado, resObj.Valor);
        Assert.Equal(TipoInsumo.Categorias.Volumen, resObj.Categoria);
    }

    [Theory]
    [InlineData(1000, "mililitros", "litros", 1.0)]
    [InlineData(1500, "mililitro", "litro", 1.5)]
    [InlineData(2, "lt", "ml", 2000.0)]
    [InlineData(500, "cm3", "l", 0.5)]
    [InlineData(750, "cc", "l", 0.75)]
    [InlineData(1, "l", "cm3", 1000.0)]
    [InlineData(1, "l", "cc", 1000.0)]
    [InlineData(100, "cc", "ml", 100.0)]
    public void Convertir_Volumen_AliasPermitidosEnTipoInsumo_ConvierteCorrectamente(decimal cantidad, string origen, string destino, decimal esperado)
    {
        var resultado = _service.Convertir(cantidad, origen, destino);
        Assert.Equal(esperado, resultado);
    }

    [Fact]
    public void Convertir_Volumen_MismaUnidadOIdentidad_MantieneValorExacto()
    {
        Assert.Equal(123.456m, _service.Convertir(123.456m, "l", "l"));
        Assert.Equal(500m, _service.Convertir(500m, "ml", "mililitros"));
        Assert.Equal(0m, _service.Convertir(0m, "ml", "l"));
        Assert.Equal(-250m, _service.Convertir(-250m, "ml", "ml"));
        Assert.Equal(-0.5m, _service.Convertir(-500m, "ml", "l"));
    }

    #endregion

    #region 2. Reglas de Conversión de Masa (S25, T12)

    [Theory]
    [InlineData(1000, "g", "kg", 1.0)]
    [InlineData(1500, "g", "kg", 1.5)]
    [InlineData(500, "g", "kg", 0.5)]
    [InlineData(1, "kg", "g", 1000.0)]
    [InlineData(2.5, "kg", "g", 2500.0)]
    [InlineData(0.001, "kg", "g", 1.0)]
    public void Convertir_Masa_GramosKilogramos_ConversionExacta(decimal cantidad, string origen, string destino, decimal esperado)
    {
        var resultado = _service.Convertir(cantidad, origen, destino);
        Assert.Equal(esperado, resultado);

        var ok = _service.TryConvertir(cantidad, origen, destino, out var resTry, out var error);
        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(esperado, resTry);

        var resObj = _service.ConvertirConResultado(cantidad, origen, destino);
        Assert.True(resObj.EsExitoso);
        Assert.Equal(esperado, resObj.Valor);
        Assert.Equal(TipoInsumo.Categorias.Masa, resObj.Categoria);
    }

    [Theory]
    [InlineData(1000, "mg", "g", 1.0)]
    [InlineData(2500, "mg", "g", 2.5)]
    [InlineData(1, "g", "mg", 1000.0)]
    [InlineData(1000000, "mg", "kg", 1.0)]
    [InlineData(1, "kg", "mg", 1000000.0)]
    [InlineData(0.5, "kg", "mg", 500000.0)]
    [InlineData(1, "mg", "kg", 0.000001)]
    public void Convertir_Masa_Miligramos_ConvierteConPotenciasExactas(decimal cantidad, string origen, string destino, decimal esperado)
    {
        var resultado = _service.Convertir(cantidad, origen, destino);
        Assert.Equal(esperado, resultado);
    }

    [Theory]
    [InlineData(1500, "gramos", "kilogramos", 1.5)]
    [InlineData(2, "kilo", "gramo", 2000.0)]
    [InlineData(500, "gr", "kg", 0.5)]
    [InlineData(1000, "miligramo", "gramos", 1.0)]
    [InlineData(2500, "miligramos", "gr", 2.5)]
    public void Convertir_Masa_AliasPermitidosEnTipoInsumo_ConvierteCorrectamente(decimal cantidad, string origen, string destino, decimal esperado)
    {
        var resultado = _service.Convertir(cantidad, origen, destino);
        Assert.Equal(esperado, resultado);
    }

    [Fact]
    public void Convertir_Masa_MismaUnidadOIdentidad_MantieneValorExacto()
    {
        Assert.Equal(75.5m, _service.Convertir(75.5m, "kg", "kg"));
        Assert.Equal(200m, _service.Convertir(200m, "g", "gramos"));
        Assert.Equal(0m, _service.Convertir(0m, "g", "kg"));
        Assert.Equal(-500m, _service.Convertir(-500m, "g", "kg") * 1000);
    }

    #endregion

    #region 3. Reglas de Conversión de Conteo / Unidades (S24, S25)

    [Theory]
    [InlineData(10, "unidad", "unidades", 10.0)]
    [InlineData(5, "unidades", "u", 5.0)]
    [InlineData(1, "u", "unidad", 1.0)]
    [InlineData(0, "unidad", "u", 0.0)]
    [InlineData(-3, "unidades", "unidad", -3.0)]
    public void Convertir_Unidades_AliasCompatibles_ProporcionUnoAUno(decimal cantidad, string origen, string destino, decimal esperado)
    {
        var resultado = _service.Convertir(cantidad, origen, destino);
        Assert.Equal(esperado, resultado);

        var ok = _service.TryConvertir(cantidad, origen, destino, out var resTry, out var error);
        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(esperado, resTry);

        var resObj = _service.ConvertirConResultado(cantidad, origen, destino);
        Assert.True(resObj.EsExitoso);
        Assert.Equal(esperado, resObj.Valor);
        Assert.Equal(TipoInsumo.Categorias.Unidades, resObj.Categoria);
    }

    #endregion

    #region 4. Validación de Incompatibilidad Inter-Magnitud (S25, T12)

    [Theory]
    [InlineData("ml", "kg")]
    [InlineData("l", "g")]
    [InlineData("cm3", "mg")]
    [InlineData("litro", "kilo")]
    [InlineData("kg", "l")]
    [InlineData("g", "ml")]
    [InlineData("mg", "cc")]
    [InlineData("l", "unidad")]
    [InlineData("ml", "u")]
    [InlineData("kg", "unidad")]
    [InlineData("g", "unidades")]
    [InlineData("unidad", "l")]
    [InlineData("u", "kg")]
    public void Convertir_UnidadesDeDistintaMagnitud_SonRechazadas(string origen, string destino)
    {
        Assert.False(_service.SonCompatibles(origen, destino));
        Assert.False(TipoInsumo.SonUnidadesCompatibles(origen, destino));

        var ex = Assert.Throws<ArgumentException>(() => _service.Convertir(10m, origen, destino));
        Assert.Contains("magnitud", ex.Message, StringComparison.OrdinalIgnoreCase);

        var ok = _service.TryConvertir(10m, origen, destino, out var resultado, out var error);
        Assert.False(ok);
        Assert.Equal(0m, resultado);
        Assert.NotNull(error);
        Assert.Contains("magnitud", error, StringComparison.OrdinalIgnoreCase);

        var resObj = _service.ConvertirConResultado(10m, origen, destino);
        Assert.False(resObj.EsExitoso);
        Assert.NotNull(resObj.Error);
        Assert.Contains("magnitud", resObj.Error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 5. Manejo de Errores: Unidades Nulas, Vacías o No Reconocidas

    [Theory]
    [InlineData(null, "l")]
    [InlineData("", "l")]
    [InlineData("   ", "l")]
    [InlineData("ml", null)]
    [InlineData("ml", "")]
    [InlineData("ml", "   ")]
    [InlineData("invalido", "l")]
    [InlineData("ml", "desconocido")]
    [InlineData("metros", "yardas")]
    [InlineData("bidon", "tambor")] // S25: sin gestión de envases
    public void Convertir_UnidadesInvalidasONoReconocidas_SonRechazadas(string? origen, string? destino)
    {
        Assert.False(_service.SonCompatibles(origen, destino));

        Assert.Throws<ArgumentException>(() => _service.Convertir(10m, origen, destino));

        var ok = _service.TryConvertir(10m, origen, destino, out var res, out var error);
        Assert.False(ok);
        Assert.Equal(0m, res);
        Assert.False(string.IsNullOrWhiteSpace(error));

        var resObj = _service.ConvertirConResultado(10m, origen, destino);
        Assert.False(resObj.EsExitoso);
        Assert.False(string.IsNullOrWhiteSpace(resObj.Error));
    }

    #endregion

    #region 6. Límites Numéricos y Overflow

    [Fact]
    public void Convertir_OverflowDecimal_LanzaOInformaDesbordamientoSinCaidaIncontrolada()
    {
        // decimal.MaxValue * 1_000_000m desborda el límite de decimal
        var maxDecimal = decimal.MaxValue;

        Assert.Throws<OverflowException>(() => _service.Convertir(maxDecimal, "kg", "mg"));

        var ok = _service.TryConvertir(maxDecimal, "kg", "mg", out var resultado, out var error);
        Assert.False(ok);
        Assert.Equal(0m, resultado);
        Assert.NotNull(error);
        Assert.Contains("límite", error, StringComparison.OrdinalIgnoreCase);

        var resObj = _service.ConvertirConResultado(maxDecimal, "kg", "mg");
        Assert.False(resObj.EsExitoso);
        Assert.NotNull(resObj.Error);
        Assert.Contains("límite", resObj.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Convertir_ValoresGrandesSinOverflow_CalculaConExactitudDecimal()
    {
        // 10 mil millones de kg a g -> 10 billones de g
        decimal diezMilMillones = 10_000_000_000m;
        var res = _service.Convertir(diezMilMillones, "kg", "g");
        Assert.Equal(10_000_000_000_000m, res);
    }

    #endregion

    #region 7. Precisión Decimal y Serialización Double Firestore (D6, S25, DecimalConverter)

    [Fact]
    public void PrecisionDecimal_EnMemoriaPreservaExactitudSinRedondeoForzado()
    {
        // En memoria, decimal no sufre imprecisiones binarias para potencias de 10
        decimal valor = 0.123456789123456789m; // 18 decimales
        var convertido = _service.Convertir(valor, "l", "ml");
        Assert.Equal(123.456789123456789m, convertido);

        var revertido = _service.Convertir(convertido, "ml", "l");
        Assert.Equal(valor, revertido);
    }

    [Fact]
    public void SerializacionDoubleFirestore_ValoresOrdinariosDeInventario_PreservanFidelidad()
    {
        // Valores habituales en el dominio del lavadero (dosis en ml, pesos en g, volúmenes en l)
        var converter = new DecimalConverter();

        decimal[] valoresOrdinarios = [0m, 1.5m, 250m, 0.75m, 12m, 0.05m, 1000m, 50000m];

        foreach (var val in valoresOrdinarios)
        {
            // Simular ToFirestore (decimal -> double) y FromFirestore (double -> decimal)
            var firestoreObj = converter.ToFirestore(val);
            Assert.IsType<double>(firestoreObj);

            var recuperado = converter.FromFirestore(firestoreObj);
            Assert.Equal(val, recuperado);

            // Coincide con la simulación del servicio
            Assert.True(_service.EsPreservadoExactamenteEnFirestore(val));
            Assert.Equal(val, _service.SimularSerializacionFirestore(val));
        }
    }

    [Fact]
    public void SerializacionDoubleFirestore_ValoresExtremos_DemuestranPerdidaDePrecisionEnDouble()
    {
        // D6/S25: no se afirma exactitud persistida sin prueba.
        // Double (IEEE 754) tiene ~15-17 dígitos significativos.
        // Valores con más de 17 dígitos significativos pierden precisión al pasar por double.
        var converter = new DecimalConverter();

        // 1 + 10^-16 requiere más de 53 bits de mantisa
        decimal ultraPreciso = 1.0000000000000001m; // 17 decimales
        var firestoreObj = converter.ToFirestore(ultraPreciso);
        var recuperado = converter.FromFirestore(firestoreObj);

        // Se prueba documentalmente que double NO preserva la exactitud de 17+ dígitos
        Assert.NotEqual(ultraPreciso, recuperado);
        Assert.Equal(1.0m, recuperado); // El bit menos significativo se trunca en IEEE 754

        Assert.False(_service.EsPreservadoExactamenteEnFirestore(ultraPreciso));
        Assert.Equal(1.0m, _service.SimularSerializacionFirestore(ultraPreciso));

        // Enteros por encima de 2^53 (9_007_199_254_740_992) pierden discriminación entre enteros contiguos
        decimal enteroMayorADouble = 9_007_199_254_740_993m;
        var enteroRecuperado = converter.FromFirestore(converter.ToFirestore(enteroMayorADouble));
        Assert.NotEqual(enteroMayorADouble, enteroRecuperado);
    }

    #endregion

    #region 8. Helpers en TipoInsumo y Normalización

    [Fact]
    public void TipoInsumo_ObtenerCategoriaDeUnidad_IdentificaCorrectamente()
    {
        Assert.Equal(TipoInsumo.Categorias.Volumen, TipoInsumo.ObtenerCategoriaDeUnidad("ml"));
        Assert.Equal(TipoInsumo.Categorias.Volumen, TipoInsumo.ObtenerCategoriaDeUnidad("l"));
        Assert.Equal(TipoInsumo.Categorias.Volumen, TipoInsumo.ObtenerCategoriaDeUnidad("litros"));
        Assert.Equal(TipoInsumo.Categorias.Volumen, TipoInsumo.ObtenerCategoriaDeUnidad("cm3"));

        Assert.Equal(TipoInsumo.Categorias.Masa, TipoInsumo.ObtenerCategoriaDeUnidad("g"));
        Assert.Equal(TipoInsumo.Categorias.Masa, TipoInsumo.ObtenerCategoriaDeUnidad("kg"));
        Assert.Equal(TipoInsumo.Categorias.Masa, TipoInsumo.ObtenerCategoriaDeUnidad("mg"));
        Assert.Equal(TipoInsumo.Categorias.Masa, TipoInsumo.ObtenerCategoriaDeUnidad("kilogramos"));

        Assert.Equal(TipoInsumo.Categorias.Unidades, TipoInsumo.ObtenerCategoriaDeUnidad("unidad"));
        Assert.Equal(TipoInsumo.Categorias.Unidades, TipoInsumo.ObtenerCategoriaDeUnidad("unidades"));
        Assert.Equal(TipoInsumo.Categorias.Unidades, TipoInsumo.ObtenerCategoriaDeUnidad("u"));

        Assert.Null(TipoInsumo.ObtenerCategoriaDeUnidad("metros"));
        Assert.Null(TipoInsumo.ObtenerCategoriaDeUnidad(""));
        Assert.Null(TipoInsumo.ObtenerCategoriaDeUnidad(null));
    }

    [Fact]
    public void ConversionUnidadesService_NormalizarUnidad_RetornaSimboloCanonico()
    {
        Assert.Equal("ml", _service.NormalizarUnidad("mililitros"));
        Assert.Equal("ml", _service.NormalizarUnidad("ml"));
        Assert.Equal("ml", _service.NormalizarUnidad("cm3"));
        Assert.Equal("ml", _service.NormalizarUnidad("cc"));

        Assert.Equal("l", _service.NormalizarUnidad("litros"));
        Assert.Equal("l", _service.NormalizarUnidad("l"));
        Assert.Equal("l", _service.NormalizarUnidad("lt"));

        Assert.Equal("g", _service.NormalizarUnidad("gramos"));
        Assert.Equal("g", _service.NormalizarUnidad("gr"));
        Assert.Equal("g", _service.NormalizarUnidad("g"));

        Assert.Equal("kg", _service.NormalizarUnidad("kilogramos"));
        Assert.Equal("kg", _service.NormalizarUnidad("kilo"));
        Assert.Equal("kg", _service.NormalizarUnidad("kg"));

        Assert.Equal("mg", _service.NormalizarUnidad("miligramo"));
        Assert.Equal("mg", _service.NormalizarUnidad("mg"));

        Assert.Equal("unidad", _service.NormalizarUnidad("unidades"));
        Assert.Equal("unidad", _service.NormalizarUnidad("u"));
        Assert.Equal("unidad", _service.NormalizarUnidad("unidad"));
    }

    #endregion

    #region 9. Preservación de Comportamiento Previo Tocable (TipoInsumo)

    [Fact]
    public void Preservacion_TipoInsumo_ComportamientoExistenteSinAlteracion()
    {
        // EsCategoriaValida
        Assert.True(TipoInsumo.EsCategoriaValida("Volumen"));
        Assert.True(TipoInsumo.EsCategoriaValida("Masa"));
        Assert.True(TipoInsumo.EsCategoriaValida("Unidades"));
        Assert.False(TipoInsumo.EsCategoriaValida("Invalida"));

        // NormalizarCategoria
        Assert.Equal("Volumen", TipoInsumo.NormalizarCategoria("volumen"));
        Assert.Equal("Masa", TipoInsumo.NormalizarCategoria("MASA"));
        Assert.Equal("Unidades", TipoInsumo.NormalizarCategoria("unidades"));

        // EsUnidadValidaParaCategoria
        Assert.True(TipoInsumo.EsUnidadValidaParaCategoria("Volumen", "ml"));
        Assert.True(TipoInsumo.EsUnidadValidaParaCategoria("Masa", "kg"));
        Assert.True(TipoInsumo.EsUnidadValidaParaCategoria("Unidades", "unidad"));
        Assert.False(TipoInsumo.EsUnidadValidaParaCategoria("Volumen", "kg"));
        Assert.False(TipoInsumo.EsUnidadValidaParaCategoria("Masa", "ml"));
    }

    #endregion
}

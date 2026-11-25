using System.Collections.Generic;
using Firebase.Models;
using Firebase.Services;
using Xunit;

namespace Firebase.Tests.Services;

public class InsumoMinMaxPoliticaStockTests
{
    [Fact]
    public void Insumo_LegacyFallback_CalculaStockMaximoCorrectamente()
    {
        // Registro existente en Firestore con esquema previo (sin StockMaximo persistido)
        var insumo = new Insumo
        {
            Id = "ins-legacy",
            Nombre = "Shampoo Neutro",
            Tipo = "Quimico",
            StockMinimo = 5m,
            CantidadReabastecimiento = 10m
        };

        Assert.Equal(15m, insumo.StockMaximo);
        Assert.Equal(10m, insumo.CantidadReabastecimiento);
    }

    [Fact]
    public void Insumo_NuevoModelo_CalculaCantidadReabastecimientoCorrectamente()
    {
        // Registro nuevo con política Min-Max explícita
        var insumo = new Insumo
        {
            Id = "ins-nuevo",
            Nombre = "Cera Rápida",
            Tipo = "Quimico",
            StockMinimo = 5m,
            StockMaximo = 25m
        };

        Assert.Equal(25m, insumo.StockMaximo);
        Assert.Equal(20m, insumo.CantidadReabastecimiento);
    }

    [Fact]
    public void Insumo_CalcularCantidadReposicion_CompensaDesviosCriticos()
    {
        // Escenario del usuario: accidente/derrame donde el stock cae a 0.5l
        // Con lote fijo de 2l quedaría en 2.5l (por debajo del mínimo de 5l).
        // Con política Min-Max dinámica, debe pedir 14.5l para restablecer el stock a 15l.
        var insumo = new Insumo
        {
            Id = "ins-liquido",
            Nombre = "Detergente Espuma Activa",
            Tipo = "Quimico",
            StockMinimo = 5m,
            StockMaximo = 15m,
            CantidadReabastecimiento = 2m,
            StockActual = 0.5m
        };

        var cantidadAPedir = insumo.CalcularCantidadReposicion();

        Assert.Equal(14.5m, cantidadAPedir);
        Assert.Equal(15m, insumo.StockActual + cantidadAPedir);
    }

    [Fact]
    public void Insumo_CalcularCantidadReposicion_RespetaLoteMinimoSiFaltanteEsMenor()
    {
        var insumo = new Insumo
        {
            Id = "ins-microfibra",
            Nombre = "Paño Microfibra",
            Tipo = "Consumible",
            StockMinimo = 10m,
            StockMaximo = 20m,
            CantidadReabastecimiento = 15m, // Lote mínimo de compra por paquete de proveedor
            StockActual = 10m
        };

        var cantidadAPedir = insumo.CalcularCantidadReposicion();

        // Faltante es 10, pero el lote mínimo es 15 -> pide 15
        Assert.Equal(15m, cantidadAPedir);
    }

    [Fact]
    public void AuditCampos_Insumo_IncluyeStockMaximo()
    {
        Assert.Contains("StockMaximo", AuditCampos.Insumo);

        var insumo = new Insumo
        {
            Nombre = "Silicona",
            Tipo = "Quimico",
            StockMinimo = 4m,
            StockMaximo = 16m
        };

        var campos = AuditCampos.CapturarInsumo(insumo);
        Assert.Equal("16", campos["StockMaximo"]);
    }

    [Fact]
    public void TipoInsumo_EsUnidadEntera_IdentificaUnidadesDiscretasCorrectamente()
    {
        Assert.True(TipoInsumo.EsUnidadEntera("unidad"));
        Assert.True(TipoInsumo.EsUnidadEntera("unidades"));
        Assert.True(TipoInsumo.EsUnidadEntera("u"));
        Assert.True(TipoInsumo.EsUnidadEntera(" UNIDAD "));

        Assert.False(TipoInsumo.EsUnidadEntera("l"));
        Assert.False(TipoInsumo.EsUnidadEntera("ml"));
        Assert.False(TipoInsumo.EsUnidadEntera("kg"));
        Assert.False(TipoInsumo.EsUnidadEntera("g"));
        Assert.False(TipoInsumo.EsUnidadEntera(""));
        Assert.False(TipoInsumo.EsUnidadEntera(null));
    }

    [Fact]
    public void TipoInsumo_TieneDecimales_DetectaFracciones()
    {
        Assert.True(TipoInsumo.TieneDecimales(2.5m));
        Assert.True(TipoInsumo.TieneDecimales(0.01m));
        Assert.True(TipoInsumo.TieneDecimales(1.0001m));

        Assert.False(TipoInsumo.TieneDecimales(0m));
        Assert.False(TipoInsumo.TieneDecimales(1m));
        Assert.False(TipoInsumo.TieneDecimales(5.00m));
        Assert.False(TipoInsumo.TieneDecimales(100m));
    }
}

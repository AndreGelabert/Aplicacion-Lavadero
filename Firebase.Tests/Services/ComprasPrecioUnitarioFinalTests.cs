using Firebase.Models;
using Firebase.Services;

namespace Firebase.Tests.Services;

public sealed class ComprasPrecioUnitarioFinalTests
{
    [Fact]
    public void NormalizarPrecioUnitario_UnitarioYPorDefecto_DevuelvePrecioSinCambios()
    {
        Assert.True(ComprasService.NormalizarPrecioUnitario(125.50m, 10m, "Unitario", out var u1));
        Assert.Equal(125.50m, u1);

        Assert.True(ComprasService.NormalizarPrecioUnitario(125.50m, 10m, null, out var u2));
        Assert.Equal(125.50m, u2);

        Assert.True(ComprasService.NormalizarPrecioUnitario(0m, 10m, "", out var u3));
        Assert.Equal(0m, u3);
    }

    [Fact]
    public void NormalizarPrecioUnitario_FinalExactoYPeriodico_PreservaTotalEnComparativa()
    {
        Assert.True(ComprasService.NormalizarPrecioUnitario(2500m, 500m, "Final", out var uExacto));
        Assert.Equal(5m, uExacto);

        Assert.True(ComprasService.NormalizarPrecioUnitario(10000m, 3m, "final", out var uPeriodico));
        var total = 3m * uPeriodico;
        Assert.Equal("10.000,00", total.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("es-AR")));
    }

    [Theory]
    [InlineData(-1, 5, "Unitario")]
    [InlineData(-1, 5, "Final")]
    [InlineData(100, 0, "Final")]
    [InlineData(100, -2, "Final")]
    [InlineData(100, 5, "Otro")]
    public void NormalizarPrecioUnitario_EntradasInvalidas_RetornaFalse(decimal precio, decimal cantidad, string? tipo)
    {
        Assert.False(ComprasService.NormalizarPrecioUnitario(precio, cantidad, tipo, out _));
    }

    [Fact]
    public void ValidarYNormalizarRespuesta_MezclaUnitarioYFinal_NormalizaYPreservaCantidadesOriginales()
    {
        var solicitud = new SolicitudCotizacion
        {
            Id = "sol-1",
            Items = new()
            {
                new OrdenCompraItem { InsumoId = "i1", NombreInsumo = "Cera pulidora", Cantidad = 1000m },
                new OrdenCompraItem { InsumoId = "i2", NombreInsumo = "Shampoo", Cantidad = 5m }
            }
        };

        var respuesta = new CotizacionRespuesta
        {
            SolicitudId = "sol-1",
            PlazoEntrega = "3",
            Items = new()
            {
                new OrdenCompraItem { InsumoId = "i1", Cantidad = 999m, PrecioUnitario = 15000m, TipoPrecio = "Final", Disponible = true },
                new OrdenCompraItem { InsumoId = "i2", Cantidad = 1m, PrecioUnitario = 2000m, TipoPrecio = "Unitario", Disponible = false }
            }
        };

        var resultado = ComprasService.ValidarYNormalizarRespuesta(solicitud, respuesta);
        Assert.True(resultado.EsExitoso);

        var cera = Assert.Single(respuesta.Items, x => x.InsumoId == "i1");
        Assert.Equal("Cera pulidora", cera.NombreInsumo);
        Assert.Equal(1000m, cera.Cantidad);
        Assert.Equal(15m, cera.PrecioUnitario);

        var shampoo = Assert.Single(respuesta.Items, x => x.InsumoId == "i2");
        Assert.Equal("Shampoo", shampoo.NombreInsumo);
        Assert.Equal(5m, shampoo.Cantidad);
        Assert.Equal(2000m, shampoo.PrecioUnitario);
        Assert.False(shampoo.Disponible);
    }

    [Fact]
    public void ValidarYNormalizarRespuesta_TipoPrecioInvalido_RetornaError()
    {
        var solicitud = new SolicitudCotizacion
        {
            Id = "sol-1",
            Items = new() { new OrdenCompraItem { InsumoId = "i1", NombreInsumo = "Cera", Cantidad = 10m } }
        };
        var respuesta = new CotizacionRespuesta
        {
            SolicitudId = "sol-1",
            PlazoEntrega = "2",
            Items = new() { new OrdenCompraItem { InsumoId = "i1", PrecioUnitario = 100m, TipoPrecio = "Desconocido" } }
        };

        var resultado = ComprasService.ValidarYNormalizarRespuesta(solicitud, respuesta);
        Assert.False(resultado.EsExitoso);
    }
}

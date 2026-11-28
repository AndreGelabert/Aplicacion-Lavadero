using Firebase.Models;
using Firebase.Services;

namespace Firebase.Tests.Services;

public sealed class CotizacionProveedorLinkSecurityTests
{
    [Theory]
    [InlineData("http://example.com")]
    [InlineData("https://user:pass@example.com")]
    [InlineData("https://example.com/subpath")]
    [InlineData("https://example.com/?q=1")]
    [InlineData("https://example.com/#frag")]
    [InlineData("ftp://example.com")]
    [InlineData("ftp://localhost:7059")]
    [InlineData("not-a-url")]
    [InlineData(null)]
    public void ValidarOrigen_RechazaOrigenesInsegurosOMalformados(string? origin)
    {
        Assert.Null(CotizacionProveedorLinkService.ValidarOrigen(origin));
    }

    [Theory]
    [InlineData("https://lavadero.example.com/", "https://lavadero.example.com")]
    [InlineData("https://lavadero.example.com:8443", "https://lavadero.example.com:8443")]
    [InlineData("http://localhost:7059", "http://localhost:7059")]
    [InlineData("https://127.0.0.1:7059/", "https://127.0.0.1:7059")]
    public void ValidarOrigen_AceptaHttpsYLoopback(string origin, string expected)
    {
        Assert.Equal(expected, CotizacionProveedorLinkService.ValidarOrigen(origin));
    }

    [Fact]
    public void ConstruirUrl_ColocaCredencialesEnFragmentoYNuncaEnQuery()
    {
        var service = new CotizacionProveedorLinkService(null, "https://lavadero.example.com/");
        var url = service.ConstruirUrl("sol/1", "AABBCC");
        Assert.Equal("https://lavadero.example.com/cotizacion-proveedor/responder#SolicitudId=sol%2F1&Token=AABBCC", url);
        Assert.DoesNotContain("?", url);
    }

    [Fact]
    public void FormatearSolicitud_ConLink_AnexaOUsaMarcadorYEscapaHref()
    {
        var directivaDefault = new DirectivaReabastecimiento();
        var solicitud = new SolicitudCotizacion { Id = "s1", Items = new() { new OrdenCompraItem { InsumoId = "i1", NombreInsumo = "Cera", Cantidad = 10m } } };
        var proveedor = new Proveedor { EmailOrdenes = "p@example.invalid", RazonSocial = "Prov" };
        var link = "https://lavadero.example.com/cotizacion-proveedor/responder#SolicitudId=s1&Token=ABCD";
        var encodedLink = System.Net.WebUtility.HtmlEncode(link);

        var msgAnexado = InsumosEmailFormatter.FormatearSolicitud(directivaDefault, solicitud, proveedor, null, null, link);
        Assert.Contains($"<a href=\"{encodedLink}\">Cargar cotización</a>", msgAnexado.Html);

        var directivaMarcador = new DirectivaReabastecimiento { CuerpoCotizacion = "Cotizar aqui: {link_respuesta}" };
        var msgMarcador = InsumosEmailFormatter.FormatearSolicitud(directivaMarcador, solicitud, proveedor, null, null, link);
        Assert.Contains($"Cotizar aqui: <a href=\"{encodedLink}\">Cargar cotizaci", msgMarcador.Html);
        Assert.DoesNotContain("Puede cargar su cotización directamente", msgMarcador.Html);

        Assert.False(InsumosEmailFormatter.PlantillaValida("Asunto {link_respuesta}", true));
        Assert.True(InsumosEmailFormatter.PlantillaValida("Cuerpo {link_respuesta}", false));
    }
}

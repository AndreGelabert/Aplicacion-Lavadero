using System.Net;
using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Firebase.Tests.Integration;

// Pure formatting tests: no Firestore, network, or real mail transport.
public sealed class InsumosEmailFormattingIntegrationTests
{
    [Fact]
    public async Task OrdenConSeisInsumosIncluyeImportesYDatosDesconocidosSinInventarlos()
    {
        var proveedor = new Proveedor { RazonSocial = "Proveedor <script>", Contacto = "Ana & Cía", EmailOrdenes = "proveedor@example.invalid" };
        var orden = new OrdenCompra { Items = new()
        {
            Item("[PRUEBA] Shampoo", 20, 1000), Item("[PRUEBA] Desengrasante", 10, 2000),
            Item("[PRUEBA] Limpiavidrios", 5, 1000), Item("[PRUEBA] Interior", 5, 800),
            Item("[PRUEBA] Neumaticos", 3, 500), Item("[PRUEBA] Microfibras", 12, 2500)
        } };
        var sender = new FakeSender();
        var message = InsumosEmailFormatter.FormatearOrden(new DirectivaReabastecimiento(), orden, proveedor, null, null);
        await sender.EnviarAsync(message);

        var sent = Assert.Single(sender.Messages);
        Assert.Equal(proveedor.EmailOrdenes, sent.To);
        Assert.Contains("Orden de compra", sent.Subject);
        foreach (var item in orden.Items) Assert.Contains(item.NombreInsumo, sent.Html);
        Assert.Contains("1.000,00", sent.Html);
        Assert.Contains("20.000,00", sent.Html);
        Assert.Contains("80.500,00", sent.Html);
        Assert.Contains("No informado", sent.Html);
        Assert.Contains("Proveedor &lt;script&gt;", sent.Html);
        Assert.Contains("Ana & Cía", WebUtility.HtmlDecode(sent.Html));
        Assert.Contains("Ana &amp; C", sent.Html);
        Assert.DoesNotContain("<script>", sent.Html);
    }

    [Fact]
    public async Task SolicitudUsaAsuntoLegacyYEscapaDatosNoConfiables()
    {
        var directiva = new DirectivaReabastecimiento { PlantillaEmail = "Asunto anterior" };
        var proveedor = new Proveedor { EmailOrdenes = "proveedor@example.invalid", RazonSocial = "<Proveedor>" };
        var solicitud = new SolicitudCotizacion { Notas = "<img src=x>", Items = new() { Item("Jabón & <otro>", 2, 0) } };
        var sender = new FakeSender();
        await sender.EnviarAsync(InsumosEmailFormatter.FormatearSolicitud(directiva, solicitud, proveedor, null));
        var sent = Assert.Single(sender.Messages);
        Assert.Equal("Asunto anterior", sent.Subject);
        Assert.Contains("Jabón & <otro>", WebUtility.HtmlDecode(sent.Html));
        Assert.Contains("&amp; &lt;otro&gt;", sent.Html);
        Assert.DoesNotContain("<otro>", sent.Html);
        Assert.Contains("&lt;img src=x&gt;", sent.Html);
        Assert.DoesNotContain("<img src=x>", sent.Html);
        Assert.Contains("No informado", sent.Html);
    }

    [Fact]
    public async Task AsuntoRechazaSaltosDeLineaEnDatosAntesDeEnviar()
    {
        var sender = new FakeSender();
        var directiva = new DirectivaReabastecimiento { AsuntoCotizacion = "Solicitud {proveedor}" };
        var proveedor = new Proveedor { RazonSocial = "Proveedor\r\nBcc: atacante@example.invalid", EmailOrdenes = "p@example.invalid" };
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await sender.EnviarAsync(InsumosEmailFormatter.FormatearSolicitud(directiva, new SolicitudCotizacion(), proveedor, null)));
        Assert.Empty(sender.Messages);
    }

    [Theory]
    [InlineData("{desconocido}", false)]
    [InlineData("{items}", true)]
    [InlineData("{proveedor", false)]
    [InlineData("Asunto\r\nBcc: x", true)]
    public void RechazaMarcadoresYAsuntosInvalidos(string plantilla, bool asunto)
    {
        Assert.False(InsumosEmailFormatter.PlantillaValida(plantilla, asunto));
    }

    [Fact]
    public void AceptaMarcadoresDocumentadosYEscapaTextoDePlantilla()
    {
        Assert.True(InsumosEmailFormatter.PlantillaValida("{proveedor}: {items}", false));
        var directiva = new DirectivaReabastecimiento { CuerpoOrden = "<script>{proveedor}</script> {items}" };
        var message = InsumosEmailFormatter.FormatearOrden(directiva, new OrdenCompra(), new Proveedor { RazonSocial = "<X>" }, null, null);
        Assert.DoesNotContain("<script>", message.Html);
        Assert.Contains("&lt;script&gt;&lt;X&gt;&lt;/script&gt;", message.Html);
    }

    [LocalFirestoreFact]
    public async Task SolicitudAutomaticaUsaPlantillaYNoReenviaAlReevaluar()
    {
        var db = Db();
        var sender = new FakeSender();
        await db.Collection("proveedores").Document("p").SetAsync(new Proveedor { Id = "p", RazonSocial = "Proveedor", EmailOrdenes = "p@example.invalid" });
        await db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "Jabón", UnidadMedida = "ml", StockActual = 1, StockMinimo = 2, CantidadReabastecimiento = 5, ReabastecimientoAutomatico = true, ProveedoresIds = new() { "p" } });
        await db.Collection("directivasReabastecimiento").Document("principal").SetAsync(new DirectivaReabastecimiento { ModoEmision = "EnvioDirecto", AsuntoCotizacion = "Cotización personalizada", CuerpoCotizacion = "Pedido {items}" });
        var stock = new StockService(db, sender, new ProveedorService(db), NullLogger<StockService>.Instance);
        await stock.EvaluarReabastecimiento("i");
        await stock.EvaluarReabastecimiento("i");
        var sent = Assert.Single(sender.Messages);
        Assert.Equal("Cotización personalizada", sent.Subject);
        Assert.Contains("Jabón", WebUtility.HtmlDecode(sent.Html));
        Assert.Contains("ml", sent.Html);
        Assert.Single(await new ComprasService(db).ObtenerSolicitudes());
    }

    [LocalFirestoreFact]
    public async Task OrdenFallidaQuedaEnRevisionManualYSuReintentoNoEnvia()
    {
        var db = Db();
        var sender = new FakeSender { Enviado = false };
        await db.Collection("proveedores").Document("p").SetAsync(new Proveedor { Id = "p", EmailOrdenes = "p@example.invalid" });
        await db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "Jabón", UnidadMedida = "ml" });
        await db.Collection("ordenesCompra").Document("o").SetAsync(new OrdenCompra { Id = "o", ProveedorId = "p", Estado = "PendienteAprobacion", Items = new() { new OrdenCompraItem { InsumoId = "i", NombreInsumo = "Jabón", Cantidad = 2, PrecioUnitario = 1000 } } });
        await db.Collection("directivasReabastecimiento").Document("principal").SetAsync(new DirectivaReabastecimiento { AsuntoOrden = "Orden personalizada" });
        var compras = new ComprasService(db);
        var resultado = await compras.AprobarOrden("o", sender);
        var reintento = await compras.AprobarOrden("o", sender);
        Assert.False(resultado.EsExitoso);
        Assert.False(reintento.EsExitoso);
        var sent = Assert.Single(sender.Messages);
        Assert.Equal("Orden personalizada", sent.Subject);
        Assert.Contains("2.000,00", sent.Html);
        Assert.Equal("RevisionManual", Assert.Single(await compras.ObtenerOrdenes()).Estado);
    }

    private static FirestoreDb Db() => new FirestoreDbBuilder
    {
        ProjectId = "demo-email-" + Guid.NewGuid().ToString("N")[..16],
        EmulatorDetection = EmulatorDetection.EmulatorOnly
    }.Build();

    private static OrdenCompraItem Item(string nombre, decimal cantidad, decimal precio) =>
        new() { NombreInsumo = nombre, Cantidad = cantidad, PrecioUnitario = precio };

    private sealed class FakeSender : IEmailSender
    {
        public List<EmailMessage> Messages { get; } = new();
        public bool Enviado { get; set; } = true;
        public Task<ResultadoEnvioEmail> EnviarAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.FromResult(new ResultadoEnvioEmail(Enviado, "Resultado del transporte de prueba."));
        }
    }
}

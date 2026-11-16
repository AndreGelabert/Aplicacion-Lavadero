using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Firebase.Controllers;
using Firebase.Models;
using Firebase.Security;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Firebase.Tests.Integration;

public sealed class RecepcionParcialCierreIntegrationTests
{
    public sealed class Auth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "test"), new Claim(ClaimTypes.Email, "test@example.invalid"),
                new Claim("permiso", "Insumos.Consultar"), new Claim("permiso", "Insumos.Modificar"), new Claim("permiso", "Insumos.Recepcionar") };
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }

    private static async Task<FirestoreDb> Fixture()
    {
        Assert.Equal("127.0.0.1:8293", Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST"));
        var db = new FirestoreDbBuilder { ProjectId = "demo-partial-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("proveedores").Document("p").SetAsync(new Proveedor { Id = "p", IdentificacionTributaria = "123", RazonSocial = "Proveedor", Contacto = "1234567890", EmailOrdenes = "p@example.invalid", Estado = "Activo" });
        await db.Collection("insumos").Document("a").SetAsync(new Insumo { Id = "a", Nombre = "Jabon", StockActual = 3, Estado = "Activo" });
        await db.Collection("insumos").Document("b").SetAsync(new Insumo { Id = "b", Nombre = "Microfibra", StockActual = 5, Estado = "Activo" });
        await db.Collection("ordenesCompra").Document("o").SetAsync(new OrdenCompra { Id = "o", ProveedorId = "p", Estado = "PendienteAprobacion", Items = new() {
            new() { InsumoId = "a", NombreInsumo = "Jabon", Cantidad = 2, PrecioUnitario = 1 },
            new() { InsumoId = "b", NombreInsumo = "Microfibra", Cantidad = 12, PrecioUnitario = 1 } } });
        return db;
    }

    private static async Task Despachar(FirestoreDb db, Mock<IEmailSender> email)
    {
        email.Setup(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ResultadoEnvioEmail(true, "Aceptado"));
        var compras = new ComprasService(db);
        Assert.True((await compras.AprobarOrden("o", email.Object)).EsExitoso);
        Assert.True((await compras.CambiarEstadoOrden("o", "Confirmada")).EsExitoso);
        Assert.True((await compras.CambiarEstadoOrden("o", "Despachada")).EsExitoso);
    }

    [LocalFirestoreFact]
    public async Task RecepcionParcialCierraYReintentoNoModificaStockNiAuditoria()
    {
        var db = await Fixture(); var email = new Mock<IEmailSender>(); await Despachar(db, email);
        var compras = new ComprasService(db); var insumos = new InsumoService(db); var stock = new StockService(db); var audit = new AuditService(db);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(InsumosController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, Auth>("Test", _ => { });
        builder.Services.AddAuthorization(); builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db); builder.Services.AddMemoryCache(); builder.Services.AddScoped<ConfiguracionService>();
        builder.Services.AddScoped<InsumoService>(); builder.Services.AddScoped<ProveedorService>(); builder.Services.AddScoped<StockService>(); builder.Services.AddScoped<ComprasService>();
        builder.Services.AddScoped<IAuditWriter, AuditService>(); builder.Services.AddSingleton(email.Object);
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"); await app.StartAsync();
        try
        {
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            var page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Ordenes"));
            var token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value; Assert.NotEmpty(token);
            async Task<HttpResponseMessage> Recibir(string a, string b) => await client.PostAsync("/Insumos/RecibirOrden", new FormUrlEncodedContent(new[] {
                new KeyValuePair<string,string>("__RequestVerificationToken", token), new("id", "o"), new("insumoIds", "a"), new("cantidades", a), new("insumoIds", "b"), new("cantidades", b) }));
            var auditAntes = await audit.ObtenerRegistros(pageSize: 100);
            var movimientosAntes = await stock.ObtenerMovimientos();
            foreach (var caso in new[] {
                ("a", "0", "b", "0", "Las cantidades de recepción deben ser válidas y mayores a cero."),
                ("a", "-1", "b", "10", "Las cantidades de recepción deben ser válidas y mayores a cero."),
                ("a", "1", "a", "1", "Las cantidades de recepción deben ser válidas y mayores a cero."),
                ("a", "2", "b", "13", "La recepción de 'Microfibra' supera la cantidad pendiente.") })
            {
                var fields = new[] { new KeyValuePair<string,string>("__RequestVerificationToken", token), new("id", "o"),
                    new("insumoIds", caso.Item1), new("cantidades", caso.Item2), new("insumoIds", caso.Item3), new("cantidades", caso.Item4) };
                var rechazo = await client.PostAsync("/Insumos/RecibirOrden", new FormUrlEncodedContent(fields));
                Assert.Equal(HttpStatusCode.Redirect, rechazo.StatusCode);
                page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Ordenes")); Assert.Contains(caso.Item5, page);
                var sinCambios = Assert.Single(await compras.ObtenerOrdenes()); Assert.Equal("Despachada", sinCambios.Estado);
                Assert.Equal(new decimal[] { 0, 0 }, sinCambios.Items.Select(i => i.CantidadRecibida));
                Assert.Equal(3, (await insumos.Obtener("a"))!.StockActual); Assert.Equal(5, (await insumos.Obtener("b"))!.StockActual);
                Assert.Equal(movimientosAntes.Select(m => m.Id).OrderBy(x => x), (await stock.ObtenerMovimientos()).Select(m => m.Id).OrderBy(x => x));
                Assert.Equal(auditAntes.Select(l => l.DocId).OrderBy(x => x), (await audit.ObtenerRegistros(pageSize: 100)).Select(l => l.DocId).OrderBy(x => x));
                Assert.Empty(await compras.ObtenerSolicitudes());
            }
            Assert.Equal(HttpStatusCode.Redirect, (await Recibir("2", "10")).StatusCode);
            var orden = Assert.Single(await compras.ObtenerOrdenes()); Assert.Equal("RecibidaParcial", orden.Estado);
            Assert.Equal(new decimal[] { 2, 10 }, orden.Items.Select(i => i.CantidadRecibida));
            Assert.Equal(5, (await insumos.Obtener("a"))!.StockActual); Assert.Equal(15, (await insumos.Obtener("b"))!.StockActual);
            var movimientos = await stock.ObtenerMovimientos(); Assert.Equal(2, movimientos.Count);
            Assert.Contains(movimientos, m => m.InsumoId == "a" && m.Cantidad == 2 && m.SaldoResultante == 5 && m.ReferenciaId == "o");
            Assert.Contains(movimientos, m => m.InsumoId == "b" && m.Cantidad == 10 && m.SaldoResultante == 15 && m.ReferenciaId == "o");
            var logs = await audit.ObtenerRegistros(pageSize: 100); var recepcion = Assert.Single(logs, l => l.Action == "Recepción de orden de compra" && l.TargetId == "o");
            Assert.Equal("Despachada", recepcion.Antes["Estado"]); Assert.Equal("RecibidaParcial", recepcion.Despues["Estado"]);
            page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Ordenes"));
            Assert.Contains("Recibida parcialmente", page, StringComparison.OrdinalIgnoreCase);
            var modal = Regex.Match(page, "<div id=\"order-modal-o\"[\\s\\S]*?(?=<div id=\"order-modal-|</body>)", RegexOptions.IgnoreCase).Value;
            Assert.NotEmpty(modal); Assert.Contains("Microfibra", modal);
            Assert.Contains("Estado actual: Recibida parcialmente (orden cerrada)", modal);
            Assert.DoesNotMatch("(?i)<form[^>]*action=[\"'][^\"']*CambiarEstadoOrden", modal);
            Assert.DoesNotContain("Cancelar orden", modal);
            var texto = Regex.Replace(Regex.Replace(modal, "<[^>]+>", " "), @"\s+", " ");
            Assert.Matches(@"(?i)(recibid[oa]s?|recibieron)\D*10\b", texto);
            Assert.Matches(@"(?i)(esperad[oa]s?|esperaban)\D*12\b", texto);
            Assert.Matches(@"(?i)(faltan|faltante|diferencia)\D*2\b", texto);
            Assert.Matches(@"(?i)contact\w*\s+(?:con\s+|al\s+|a\s+)?(?:el\s+)?proveedor", texto);
            Assert.DoesNotMatch("(?i)<form[^>]*action=[\"'][^\"']*RecibirOrden", modal);
            Assert.DoesNotMatch("(?i)<input[^>]*name=[\"']cantidades[\"']", modal);
            Assert.Equal(HttpStatusCode.Redirect, (await Recibir("0", "2")).StatusCode);
            page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Ordenes")); Assert.Contains("La orden no está disponible para recepción.", page);
            modal = Regex.Match(page, "<div id=\"order-modal-o\"[\\s\\S]*?(?=<div id=\"order-modal-|</body>)", RegexOptions.IgnoreCase).Value;
            Assert.Contains("Estado actual: Recibida parcialmente (orden cerrada)", modal);
            Assert.DoesNotMatch("(?i)<form[^>]*action=[\"'][^\"']*(RecibirOrden|CambiarEstadoOrden)", modal);
            Assert.DoesNotContain("Cancelar orden", modal);
            Assert.False((await compras.CambiarEstadoOrden("o", "Cancelada", "prueba")).EsExitoso);
            Assert.False((await compras.CambiarEstadoOrden("o", "Enviada")).EsExitoso);
            Assert.False((await compras.CambiarEstadoOrden("o", "Recibida")).EsExitoso);
            Assert.Equal("RecibidaParcial", Assert.Single(await compras.ObtenerOrdenes()).Estado);
            Assert.Equal(new decimal[] { 2, 10 }, Assert.Single(await compras.ObtenerOrdenes()).Items.Select(i => i.CantidadRecibida));
            Assert.Equal(5, (await insumos.Obtener("a"))!.StockActual); Assert.Equal(15, (await insumos.Obtener("b"))!.StockActual);
            Assert.Equal(movimientos.Select(m => m.Id).OrderBy(x => x), (await stock.ObtenerMovimientos()).Select(m => m.Id).OrderBy(x => x));
            Assert.Equal(logs.Select(l => l.DocId).OrderBy(x => x), (await audit.ObtenerRegistros(pageSize: 100)).Select(l => l.DocId).OrderBy(x => x));
            email.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        }
        finally { await app.StopAsync(); }
    }

    [LocalFirestoreFact]
    public async Task CeroPorItemPermitido()
    {
        var db = await Fixture(); var email = new Mock<IEmailSender>(); await Despachar(db, email);
        var compras = new ComprasService(db); var stock = new StockService(db); var insumos = new InsumoService(db); var audit = new AuditService(db);
        Assert.True((await compras.Recibir("o", new Dictionary<string, decimal> { ["a"] = 0, ["b"] = 10 })).EsExitoso);
        var orden = Assert.Single(await compras.ObtenerOrdenes()); Assert.Equal("RecibidaParcial", orden.Estado);
        Assert.Equal(new decimal[] { 0, 10 }, orden.Items.Select(i => i.CantidadRecibida));
        Assert.Equal(3, (await insumos.Obtener("a"))!.StockActual); Assert.Equal(15, (await insumos.Obtener("b"))!.StockActual);
        var mov = Assert.Single(await stock.ObtenerMovimientos()); Assert.Equal("b", mov.InsumoId); Assert.Equal(10, mov.Cantidad); Assert.Equal(15, mov.SaldoResultante);
        Assert.Single(await audit.ObtenerRegistros(pageSize: 100), l => l.Action == "Recepción de orden de compra" && l.TargetId == "o");
    }

    [LocalFirestoreFact]
    public async Task ParcialTerminalLiberaProveedorYReabastecimientoEvaluaStockReal()
    {
        var db = await Fixture(); var email = new Mock<IEmailSender>(); await Despachar(db, email);
        var compras = new ComprasService(db); var proveedores = new ProveedorService(db);
        var insumos = new InsumoService(db);
        var stock = new StockService(db, email.Object, proveedores, NullLogger<StockService>.Instance);
        await db.Collection("insumos").Document("b").UpdateAsync(new Dictionary<string, object> {
            ["StockMinimo"] = 16, ["CantidadReabastecimiento"] = 3, ["ReabastecimientoAutomatico"] = true, ["ProveedoresIds"] = new[] { "p" } });
        Assert.True((await proveedores.Obtener("p"))!.EmailOrdenesBloqueado);
        Assert.True((await compras.Recibir("o", new Dictionary<string, decimal> { ["a"] = 2, ["b"] = 10 })).EsExitoso);
        Assert.Equal("RecibidaParcial", Assert.Single(await compras.ObtenerOrdenes()).Estado);
        Assert.Equal(15, (await insumos.Obtener("b"))!.StockActual);
        Assert.Empty(await compras.ObtenerSolicitudes()); Assert.Single(await compras.ObtenerOrdenes());
        Assert.False((await proveedores.Obtener("p"))!.EmailOrdenesBloqueado);
        var editado = (await proveedores.Obtener("p"))!; editado.EmailOrdenes = "nuevo@example.invalid";
        Assert.True((await proveedores.Guardar(editado, false)).EsExitoso);
        Assert.Equal("nuevo@example.invalid", (await proveedores.Obtener("p"))!.EmailOrdenes);
        await stock.EvaluarReabastecimiento("b");
        var ordenes = await compras.ObtenerOrdenes(); var solicitudes = await compras.ObtenerSolicitudes();
        Assert.Equal(1, ordenes.Count(o => o.Estado == "PendienteAprobacion") + solicitudes.Count(s => s.Estado == "PendienteAprobacion"));
        Assert.True(ordenes.Any(o => o.Estado == "PendienteAprobacion" && o.Items.Any(i => i.InsumoId == "b" && i.Cantidad == 3)) ||
                    solicitudes.Any(s => s.Estado == "PendienteAprobacion" && s.Items.Any(i => i.InsumoId == "b" && i.Cantidad == 3)));
        Assert.Equal(15, (await insumos.Obtener("b"))!.StockActual);
        email.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [LocalFirestoreFact]
    public async Task RecepcionCompletaConservaEstadoYStock()
    {
        var db = await Fixture(); var email = new Mock<IEmailSender>(); await Despachar(db, email);
        var compras = new ComprasService(db); var insumos = new InsumoService(db);
        Assert.True((await compras.Recibir("o", new Dictionary<string, decimal> { ["a"] = 2, ["b"] = 12 })).EsExitoso);
        var orden = Assert.Single(await compras.ObtenerOrdenes()); Assert.Equal("Recibida", orden.Estado);
        Assert.Equal(new decimal[] { 2, 12 }, orden.Items.Select(i => i.CantidadRecibida));
        Assert.Equal(5, (await insumos.Obtener("a"))!.StockActual); Assert.Equal(17, (await insumos.Obtener("b"))!.StockActual);
    }
}

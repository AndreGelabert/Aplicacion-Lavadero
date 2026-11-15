using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Firebase.Controllers;
using Firebase.Models;
using Firebase.Security;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Firebase.Tests.Integration;

// This handler simulates claims; it does not authenticate with Firebase Auth.
public sealed class InsumosPermisosHttpIntegrationTests
{
    [LocalFirestoreFact]
    public async Task GuardarSeparaCrearYModificarConAntiforgeryValido()
    {
        await using var fixture = await Fixture.Start();
        foreach (var (permission, crear) in new[] { ("Insumos.Crear", "false"), ("Insumos.Modificar", "true") })
        {
            using var client = fixture.Client(permission);
            var token = await client.GetStringAsync("/test-token");
            var before = await fixture.Snapshot();
            var response = await client.PostAsync("/Insumos/Guardar", Form(token, crear));
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(before, await fixture.Snapshot());
        }
    }

    [LocalFirestoreFact]
    public async Task GuardarSinAntiforgeryRechazaSinEscrituras()
    {
        await using var fixture = await Fixture.Start();
        using var client = fixture.Client("Insumos.Crear");
        var before = await fixture.Snapshot();
        var response = await client.PostAsync("/Insumos/Guardar", Form(null, "true"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await fixture.Snapshot());
    }

    [LocalFirestoreFact]
    public async Task EmpleadoNoPuedeAjustarStock()
    {
        await using var fixture = await Fixture.Start();
        var employee = RolCatalogo.RolesBase.Single(x => x.Nombre == "Empleado");
        Assert.DoesNotContain("Insumos.AjustarStock", employee.Permisos);
        using var client = fixture.Client(string.Join(",", employee.Permisos));
        var token = await client.GetStringAsync("/test-token");
        var before = await fixture.Snapshot();
        var response = await client.PostAsync("/Insumos/Ajustar", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["__RequestVerificationToken"] = token, ["insumoId"] = "existing", ["cantidad"] = "1", ["ingreso"] = "true", ["justificacion"] = "prueba"
        }));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(before, await fixture.Snapshot());
    }

    [LocalFirestoreFact]
    public async Task RecepcionHttpRechazaLineasInvalidasYReintentoNoDuplica()
    {
        await using var fixture = await Fixture.Start();
        // Quote and award are direct service setup, not HTTP coverage. The sender is fake.
        var insumos = new InsumoService(fixture.Db);
        var a = new Insumo { Nombre = "A", Tipo = "Quimico" };
        var b = new Insumo { Nombre = "B", Tipo = "Quimico" };
        Assert.True((await insumos.Guardar(a, true)).EsExitoso);
        Assert.True((await insumos.Guardar(b, true)).EsExitoso);
        var proveedor = new Proveedor { RazonSocial = "Proveedor", IdentificacionTributaria = "123", EmailOrdenes = "p@example.invalid" };
        Assert.True((await new ProveedorService(fixture.Db).Guardar(proveedor, true)).EsExitoso);
        var compras = new ComprasService(fixture.Db);
        var items = new List<OrdenCompraItem> {
            new() { InsumoId = a.Id, NombreInsumo = a.Nombre, Cantidad = 2 },
            new() { InsumoId = b.Id, NombreInsumo = b.Nombre, Cantidad = 3 }
        };
        Assert.True((await compras.CrearSolicitudes(new() { proveedor.Id }, items, null)).EsExitoso);
        var solicitud = Assert.Single(await compras.ObtenerSolicitudes());
        var respuesta = new CotizacionRespuesta { SolicitudId = solicitud.Id, PlazoEntrega = "1", Items = items.Select(i => new OrdenCompraItem { InsumoId = i.InsumoId, PrecioUnitario = 1 }).ToList() };
        Assert.True((await compras.RegistrarRespuesta(respuesta)).EsExitoso);
        var sender = new Mock<IEmailSender>();
        sender.Setup(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ResultadoEnvioEmail(true, "Aceptado simulado"));
        Assert.True((await compras.Adjudicar(solicitud.Id, respuesta.Id, sender.Object)).EsExitoso);
        sender.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        var orden = Assert.Single(await compras.ObtenerOrdenes());
        Assert.Equal("Enviada", orden.Estado); // Fake acceptance only; no external email was sent.
        using var client = fixture.Client("Insumos.Recepcionar");
        var token = await client.GetStringAsync("/test-token");
        var before = await fixture.Snapshot();
        var auditsBefore = (await fixture.Db.Collection("registros_auditoria").GetSnapshotAsync()).Count;

        async Task<HttpResponseMessage> Recibir(string cantidadA, string cantidadB) =>
            await client.PostAsync("/Insumos/RecibirOrden", new FormUrlEncodedContent(new List<KeyValuePair<string, string>> {
                new("__RequestVerificationToken", token), new("id", orden.Id),
                new("insumoIds", a.Id), new("insumoIds", b.Id),
                new("cantidades", cantidadA), new("cantidades", cantidadB)
            }));
        foreach (var invalid in new[] { "0", "4" })
        {
            using var rejected = await Recibir("2", invalid);
            Assert.Equal(HttpStatusCode.Redirect, rejected.StatusCode);
            Assert.Equal("/Insumos/Ordenes", rejected.Headers.Location?.OriginalString);
            Assert.Equal(before, await fixture.Snapshot());
            Assert.Equal(0m, (await insumos.Obtener(a.Id))!.StockActual);
            Assert.Equal(0m, (await insumos.Obtener(b.Id))!.StockActual);
            Assert.Equal("Enviada", Assert.Single(await compras.ObtenerOrdenes()).Estado);
        }
        using var accepted = await Recibir("2", "3");
        Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
        Assert.Equal("/Insumos/Ordenes", accepted.Headers.Location?.OriginalString);
        Assert.Equal(2m, (await insumos.Obtener(a.Id))!.StockActual);
        Assert.Equal(3m, (await insumos.Obtener(b.Id))!.StockActual);
        var recibida = Assert.Single(await compras.ObtenerOrdenes());
        Assert.Equal("Recibida", recibida.Estado);
        Assert.Equal(new decimal[] { 2, 3 }, recibida.Items.Select(i => i.CantidadRecibida));
        var movimientos = await new StockService(fixture.Db).ObtenerMovimientos();
        Assert.Equal(2, movimientos.Count);
        Assert.Contains(movimientos, m => m.InsumoId == a.Id && m.Cantidad == 2 && m.SaldoResultante == 2 && m.Tipo == nameof(TipoMovimientoStock.RecepcionOrden) && m.ReferenciaId == orden.Id);
        Assert.Contains(movimientos, m => m.InsumoId == b.Id && m.Cantidad == 3 && m.SaldoResultante == 3 && m.Tipo == nameof(TipoMovimientoStock.RecepcionOrden) && m.ReferenciaId == orden.Id);
        var audits = await fixture.Db.Collection("registros_auditoria").GetSnapshotAsync();
        Assert.Equal(auditsBefore + 1, audits.Count);
        var audit = Assert.Single(audits.Documents.Where(d => d.GetValue<string>("Action") == "Recepción de orden de compra"));
        Assert.Equal(orden.Id, audit.GetValue<string>("TargetId"));
        Assert.Equal("test", audit.GetValue<string>("UserId"));
        Assert.Equal("test@example.invalid", audit.GetValue<string>("UserEmail"));
        var after = await fixture.Snapshot();
        using var repeat = await Recibir("2", "3");
        Assert.Equal(HttpStatusCode.Redirect, repeat.StatusCode);
        Assert.Equal("/Insumos/Ordenes", repeat.Headers.Location?.OriginalString);
        Assert.Equal(after, await fixture.Snapshot());
        sender.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [LocalFirestoreFact]
    public async Task CotizacionAgrupadaHastaRecepcionPorHttpConTransporteFalso()
    {
        await using var fixture = await Fixture.Start();
        // Only catalogue setup is direct; all workflow transitions below are HTTP.
        var insumos = new InsumoService(fixture.Db);
        var insumo = new Insumo { Nombre = "Jabon", Tipo = "Quimico" };
        Assert.True((await insumos.Guardar(insumo, true)).EsExitoso);
        var proveedores = new ProveedorService(fixture.Db);
        var p1 = new Proveedor { RazonSocial = "Proveedor A", IdentificacionTributaria = "123", EmailOrdenes = "a@example.invalid" };
        var p2 = new Proveedor { RazonSocial = "Proveedor B", IdentificacionTributaria = "456", EmailOrdenes = "b@example.invalid" };
        Assert.True((await proveedores.Guardar(p1, true)).EsExitoso);
        Assert.True((await proveedores.Guardar(p2, true)).EsExitoso);
        using var client = fixture.Client("Insumos.Crear,Insumos.Modificar,Insumos.Recepcionar");
        using var denied = fixture.Client("Insumos.Consultar");
        var token = await client.GetStringAsync("/test-token");
        var deniedToken = await denied.GetStringAsync("/test-token");
        FormUrlEncodedContent Crear(string csrf) => new(new List<KeyValuePair<string, string>> {
            new("__RequestVerificationToken", csrf), new("proveedorIds", p1.Id), new("proveedorIds", p2.Id),
            new("items[0].InsumoId", insumo.Id), new("items[0].Cantidad", "2")
        });
        var before = await fixture.Snapshot();
        using (var forbidden = await denied.PostAsync("/Insumos/CrearSolicitudes", Crear(deniedToken)))
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(before, await fixture.Snapshot());
        Assert.Empty(fixture.Messages);
        using (var created = await client.PostAsync("/Insumos/CrearSolicitudes", Crear(token)))
        {
            Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
            Assert.Equal("/Insumos/Cotizaciones", created.Headers.Location?.OriginalString);
        }
        var compras = new ComprasService(fixture.Db);
        var solicitudes = await compras.ObtenerSolicitudes();
        Assert.Equal(2, solicitudes.Count);
        Assert.Single(solicitudes.Select(s => s.GrupoId).Distinct());
        Assert.All(solicitudes, s => { Assert.Equal("Pendiente", s.Estado); Assert.Equal(2m, Assert.Single(s.Items).Cantidad); });
        Assert.Equal(2, fixture.Messages.Count);
        foreach (var proveedor in new[] { p1, p2 })
            Assert.Contains(new EmailMessage(proveedor.EmailOrdenes, "Solicitud de cotización de insumos", "<p>Solicitamos cotización para los siguientes insumos:</p><p>Jabon: 2</p>"), fixture.Messages);
        var chosen = Assert.Single(solicitudes.Where(s => s.ProveedorId == p1.Id));
        var other = Assert.Single(solicitudes.Where(s => s.ProveedorId == p2.Id));
        using (var response = await client.PostAsync("/Insumos/RegistrarRespuesta", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["__RequestVerificationToken"] = token, ["SolicitudId"] = chosen.Id, ["PlazoEntrega"] = "1",
            ["Items[0].InsumoId"] = insumo.Id, ["Items[0].PrecioUnitario"] = "5", ["Items[0].Disponible"] = "true"
        })))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Insumos/Cotizaciones", response.Headers.Location?.OriginalString);
        }
        var respuesta = Assert.Single((await fixture.Db.Collection("cotizacionesRespuestas").GetSnapshotAsync()).Documents).ConvertTo<CotizacionRespuesta>();
        Assert.Equal(chosen.Id, respuesta.SolicitudId);
        Assert.Equal(5m, Assert.Single(respuesta.Items).PrecioUnitario);
        using (var awarded = await client.PostAsync("/Insumos/Adjudicar", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["__RequestVerificationToken"] = token, ["id"] = chosen.Id, ["respuestaId"] = respuesta.Id
        })))
        {
            Assert.Equal(HttpStatusCode.Redirect, awarded.StatusCode);
            Assert.Equal("/Insumos/Ordenes", awarded.Headers.Location?.OriginalString);
        }
        var resolved = await compras.ObtenerSolicitudes();
        Assert.Equal("Adjudicada", resolved.Single(s => s.Id == chosen.Id).Estado);
        Assert.Equal("Descartada", resolved.Single(s => s.Id == other.Id).Estado);
        var orden = Assert.Single(await compras.ObtenerOrdenes());
        Assert.Equal("Enviada", orden.Estado); // Fake acceptance; no actual delivery.
        Assert.Equal(p1.Id, orden.ProveedorId);
        Assert.Equal(5m, Assert.Single(orden.Items).PrecioUnitario);
        Assert.Equal(3, fixture.Messages.Count);
        Assert.Equal(new EmailMessage("a@example.invalid", "Orden de compra de insumos", "<p>Orden de compra:</p><p>Jabon: 2 × 5</p>"), fixture.Messages[2]);
        var auditBefore = (await fixture.Db.Collection("registros_auditoria").GetSnapshotAsync()).Count;
        using (var receipt = await client.PostAsync("/Insumos/RecibirOrden", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["__RequestVerificationToken"] = token, ["id"] = orden.Id, ["insumoIds"] = insumo.Id, ["cantidades"] = "2"
        })))
        {
            Assert.Equal(HttpStatusCode.Redirect, receipt.StatusCode);
            Assert.Equal("/Insumos/Ordenes", receipt.Headers.Location?.OriginalString);
        }
        Assert.Equal("Recibida", Assert.Single(await compras.ObtenerOrdenes()).Estado);
        Assert.Equal(2m, (await insumos.Obtener(insumo.Id))!.StockActual);
        var movimiento = Assert.Single(await new StockService(fixture.Db).ObtenerMovimientos());
        Assert.Equal(nameof(TipoMovimientoStock.RecepcionOrden), movimiento.Tipo);
        Assert.Equal(2m, movimiento.Cantidad);
        Assert.Equal(orden.Id, movimiento.ReferenciaId);
        var audit = await fixture.Db.Collection("registros_auditoria").GetSnapshotAsync();
        Assert.Equal(auditBefore + 1, audit.Count);
        Assert.Single(audit.Documents.Where(d => d.GetValue<string>("Action") == "Recepción de orden de compra" && d.GetValue<string>("TargetId") == orden.Id && d.GetValue<string>("UserId") == "test"));
        Assert.Equal(3, fixture.Messages.Count);
    }

    private static FormUrlEncodedContent Form(string? token, string crear)
    {
        var fields = new Dictionary<string, string> { ["crear"] = crear, ["Id"] = "existing", ["Nombre"] = "Cambio", ["Tipo"] = "Quimico", ["UnidadMedida"] = "unidad" };
        if (token != null) fields["__RequestVerificationToken"] = token;
        return new FormUrlEncodedContent(fields);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly WebApplication app;
        private readonly FirestoreDb db;
        public FirestoreDb Db => db;
        public List<EmailMessage> Messages { get; }
        private Fixture(WebApplication app, FirestoreDb db, List<EmailMessage> messages) { this.app = app; this.db = db; Messages = messages; }
        public static async Task<Fixture> Start()
        {
            if (Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST") != "127.0.0.1:8245") throw new InvalidOperationException("Only the disposable local emulator is permitted.");
            var db = new FirestoreDbBuilder { ProjectId = "demo-ins-http-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
            await db.Collection("insumos").Document("existing").SetAsync(new Insumo { Id = "existing", Nombre = "Original", StockActual = 5 });
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
            builder.Services.AddControllersWithViews().AddApplicationPart(typeof(InsumosController).Assembly);
            builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, HeaderAuth>("Test", _ => { });
            builder.Services.AddAuthorization();
            builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
            builder.Services.AddSingleton(db); builder.Services.AddMemoryCache();
            builder.Services.AddScoped<ConfiguracionService>(); builder.Services.AddScoped<InsumoService>();
            builder.Services.AddScoped<ProveedorService>(); builder.Services.AddScoped<StockService>(); builder.Services.AddScoped<ComprasService>();
            var messages = new List<EmailMessage>();
            var sender = new Mock<IEmailSender>(MockBehavior.Strict);
            sender.Setup(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
                .Callback<EmailMessage, CancellationToken>((message, _) => messages.Add(message))
                .ReturnsAsync(new ResultadoEnvioEmail(true, "Aceptado simulado"));
            builder.Services.AddSingleton(Mock.Of<IAuditWriter>()); builder.Services.AddSingleton(sender.Object);
            var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization();
            app.MapGet("/test-token", (HttpContext context, IAntiforgery antiforgery) => antiforgery.GetAndStoreTokens(context).RequestToken!).RequireAuthorization();
            app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
            await app.StartAsync(); return new Fixture(app, db, messages);
        }
        public HttpClient Client(string permissions)
        {
            var client = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            client.DefaultRequestHeaders.Add("X-Test-Permissions", permissions); return client;
        }
        public async Task<string[]> Snapshot()
        {
            var result = new List<string>();
            foreach (var name in new[] { "insumos", "tiposInsumo", "movimientosStock", "registros_auditoria", "alertasStock", "solicitudesCotizacion", "cotizacionesRespuestas", "proveedores", "ordenesCompra" })
            {
                var snapshot = await db.Collection(name).GetSnapshotAsync();
                result.Add(name + ":" + snapshot.Count);
                result.AddRange(snapshot.Documents.OrderBy(d => d.Id).Select(d => name + "/" + d.Id + ":" + d.UpdateTime));
            }
            return result.ToArray();
        }
        public async ValueTask DisposeAsync() { await app.StopAsync(); await app.DisposeAsync(); }
    }

    public sealed class HeaderAuth : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public HeaderAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : base(options, logger, encoder) { }
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Permissions", out var header)) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "test"), new(ClaimTypes.Email, "test@example.invalid") };
            claims.AddRange(header.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries).Select(p => new Claim("permiso", p)));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }
}

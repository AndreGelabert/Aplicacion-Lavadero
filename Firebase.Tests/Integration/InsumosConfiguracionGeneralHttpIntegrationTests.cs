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
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Firebase.Tests.Integration;

public sealed class InsumosConfiguracionGeneralHttpIntegrationTests
{
    [LocalFirestoreFact]
    public async Task PermisosConfiguracionYGuardadoLegacyPorHttp()
    {
        await using var f = await Fixture.Start();
        Assert.True((await f.Compras.GuardarDirectiva(new DirectivaReabastecimiento { ModoEmision = "RequiereAprobacion", PlantillaEmail = "Original" })).EsExitoso);
        var before = await f.State();
        for (var mask = 0; mask < 8; mask++)
        {
            var consultar = (mask & 1) != 0;
            var modificar = (mask & 2) != 0;
            var insumos = (mask & 4) != 0;
            var permissions = new List<string>();
            if (consultar) permissions.Add("Configuracion.Consultar");
            if (modificar) permissions.Add("Configuracion.Modificar");
            if (insumos) permissions.Add("Insumos.Modificar");
            using var client = f.Client(string.Join(',', permissions));
            var token = await client.GetStringAsync("/test-token");
            using (var page = await client.GetAsync("/Configuracion/Index"))
            {
                Assert.Equal(consultar ? HttpStatusCode.OK : HttpStatusCode.Forbidden, page.StatusCode);
                if (consultar)
                {
                    var body = WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync());
                    Assert.Equal(insumos, body.Contains("id=\"reabastecimiento\""));
                    if (insumos)
                    {
                        Assert.Contains("Original", body);
                        Assert.Equal(modificar, body.Contains("Guardar reabastecimiento"));
                    }
                }
            }
            using (var legacy = await client.GetAsync("/Insumos/Configuracion"))
            {
                Assert.Equal(insumos && consultar ? HttpStatusCode.Redirect : HttpStatusCode.Forbidden, legacy.StatusCode);
                if (insumos && consultar) Assert.Equal("/Configuracion#reabastecimiento", legacy.Headers.Location?.OriginalString);
            }
            if (!(consultar && modificar && insumos))
            {
                using var denied = await client.PostAsync("/Insumos/GuardarConfiguracion", Form(token, "EnvioDirecto", "Cambio"));
                Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
                Assert.Null(denied.Headers.Location);
                Assert.Equal(before, await f.State());
            }
        }
        using var authorized = f.Client("Configuracion.Consultar,Configuracion.Modificar,Insumos.Modificar");
        var csrf = await authorized.GetStringAsync("/test-token");
        using (var missing = await authorized.PostAsync("/Insumos/GuardarConfiguracion", Form(null, "EnvioDirecto", "Cambio")))
        {
            Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
            Assert.Null(missing.Headers.Location);
        }
        Assert.Equal(before, await f.State());
        using (var invalid = await authorized.PostAsync("/Insumos/GuardarConfiguracion", Form(csrf, "ModoInvalido", "Cambio")))
        {
            Assert.Equal(HttpStatusCode.Redirect, invalid.StatusCode);
            Assert.Equal("/Configuracion#reabastecimiento", invalid.Headers.Location?.OriginalString);
        }
        Assert.Equal(before, await f.State());
        using (var errorPage = await authorized.GetAsync("/Configuracion/Index"))
        {
            Assert.Equal(HttpStatusCode.OK, errorPage.StatusCode);
            Assert.Contains("El modo de emisión no es válido.", WebUtility.HtmlDecode(await errorPage.Content.ReadAsStringAsync()));
        }
        using (var badTemplate = await authorized.PostAsync("/Insumos/GuardarConfiguracion", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["__RequestVerificationToken"] = csrf, ["ModoEmision"] = "EnvioDirecto", ["PlantillaEmail"] = "Cambio", ["CuerpoOrden"] = "{invalido}"
        })))
        {
            Assert.Equal(HttpStatusCode.Redirect, badTemplate.StatusCode);
            Assert.Equal("/Configuracion#reabastecimiento", badTemplate.Headers.Location?.OriginalString);
        }
        Assert.Equal(before, await f.State());
        using (var errorPage = await authorized.GetAsync("/Configuracion/Index"))
            Assert.Contains("La plantilla contiene marcadores no válidos o excede la longitud permitida.", WebUtility.HtmlDecode(await errorPage.Content.ReadAsStringAsync()));
        using (var saved = await authorized.PostAsync("/Insumos/GuardarConfiguracion", Form(csrf, "EnvioDirecto", "Plantilla actualizada")))
        {
            Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
            Assert.Equal("/Configuracion#reabastecimiento", saved.Headers.Location?.OriginalString);
        }
        var directive = await f.Compras.ObtenerDirectiva();
        Assert.Equal("principal", directive.Id);
        Assert.Equal("EnvioDirecto", directive.ModoEmision);
        Assert.Equal("Plantilla actualizada", directive.PlantillaEmail);
        using var successPage = await authorized.GetAsync("/Configuracion/Index");
        Assert.Equal(HttpStatusCode.OK, successPage.StatusCode);
        var html = WebUtility.HtmlDecode(await successPage.Content.ReadAsStringAsync());
        Assert.Contains("Directiva de reabastecimiento guardada.", html);
        Assert.Contains("Plantilla actualizada", html);
    }

    [LocalFirestoreFact]
    public async Task CrearSolicitudesRechazaAsuntoInvalidoAntesDePersistirOEnviar()
    {
        await using var f = await Fixture.Start();
        await f.PrepararProveedoresEInsumo();
        Assert.True((await f.Compras.GuardarDirectiva(new DirectivaReabastecimiento { AsuntoCotizacion = "Solicitud {proveedor}" })).EsExitoso);
        using var client = f.Client("Insumos.Crear");
        var token = await client.GetStringAsync("/test-token");
        var solicitudesAntes = (await f.Compras.ObtenerSolicitudes()).Count;
        var auditoriasAntes = await f.AuditCount();
        using var response = await client.PostAsync("/Insumos/CrearSolicitudes", new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
            new KeyValuePair<string, string>("proveedorIds", "bueno"),
            new KeyValuePair<string, string>("proveedorIds", "malo"),
            new KeyValuePair<string, string>("items[0].InsumoId", "insumo"),
            new KeyValuePair<string, string>("items[0].Cantidad", "2")
        }));
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("El asunto generado no puede contener saltos de línea.", body);
        Assert.Empty(f.Sender.Messages);
        Assert.Equal(solicitudesAntes, (await f.Compras.ObtenerSolicitudes()).Count);
        Assert.Equal(auditoriasAntes, await f.AuditCount());
    }

    private static FormUrlEncodedContent Form(string? token, string mode, string template)
    {
        var fields = new Dictionary<string, string> { ["ModoEmision"] = mode, ["PlantillaEmail"] = template };
        if (token != null) fields["__RequestVerificationToken"] = token;
        return new(fields);
    }

    public sealed class HeaderAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "test"), new(ClaimTypes.Email, "test@example.invalid") };
            claims.AddRange(Request.Headers["X-Test-Permissions"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries).Select(p => new Claim("permiso", p)));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }

    private sealed class FakeSender : IEmailSender
    {
        public List<EmailMessage> Messages { get; } = new();
        public Task<ResultadoEnvioEmail> EnviarAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.FromResult(new ResultadoEnvioEmail(true, "Aceptado por el transporte de prueba."));
        }
    }

    private sealed class Fixture(WebApplication app, FirestoreDb db, FakeSender sender) : IAsyncDisposable
    {
        public ComprasService Compras { get; } = new(db);
        public FakeSender Sender { get; } = sender;
        public async Task PrepararProveedoresEInsumo()
        {
            await db.Collection("proveedores").Document("bueno").SetAsync(new Proveedor { Id = "bueno", RazonSocial = "Proveedor bueno", EmailOrdenes = "bueno@example.invalid" });
            await db.Collection("proveedores").Document("malo").SetAsync(new Proveedor { Id = "malo", RazonSocial = "Proveedor\r\nBcc: atacante@example.invalid", EmailOrdenes = "malo@example.invalid" });
            await db.Collection("insumos").Document("insumo").SetAsync(new Insumo { Id = "insumo", Nombre = "Jabón", UnidadMedida = "ml" });
        }
        public async Task<int> AuditCount() => (await db.Collection("registros_auditoria").GetSnapshotAsync()).Count;
        public static async Task<Fixture> Start()
        {
            if (Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST") != "127.0.0.1:8190") throw new InvalidOperationException("Only the disposable local emulator is permitted.");
            var db = new FirestoreDbBuilder { ProjectId = "demo-cfg-http-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
            builder.Services.AddControllersWithViews().AddApplicationPart(typeof(InsumosController).Assembly);
            builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, HeaderAuth>("Test", _ => { });
            builder.Services.AddAuthorization(); builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
            builder.Services.AddSingleton(db); builder.Services.AddMemoryCache(); builder.Services.AddScoped<ConfiguracionService>();
            builder.Services.AddScoped<InsumoService>(); builder.Services.AddScoped<ProveedorService>(); builder.Services.AddScoped<StockService>(); builder.Services.AddScoped<ComprasService>();
            var sender = new FakeSender();
            builder.Services.AddSingleton(Mock.Of<IAuditWriter>()); builder.Services.AddSingleton<IEmailSender>(sender);
            var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization();
            app.MapGet("/test-token", (HttpContext context, IAntiforgery antiforgery) => antiforgery.GetAndStoreTokens(context).RequestToken!).RequireAuthorization();
            app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
            await app.StartAsync(); return new(app, db, sender);
        }
        public HttpClient Client(string permissions)
        {
            var client = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Test-Permissions", permissions); return client;
        }
        public async Task<string> State()
        {
            var d = await Compras.ObtenerDirectiva();
            var orders = await Compras.ObtenerOrdenes();
            var requests = await Compras.ObtenerSolicitudes();
            return $"{d.Id}|{d.ModoEmision}|{d.PlantillaEmail}|{orders.Count}|{requests.Count}";
        }
        public async ValueTask DisposeAsync() { await app.StopAsync(); await app.DisposeAsync(); }
    }
}

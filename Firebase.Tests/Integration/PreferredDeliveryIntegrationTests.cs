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

public sealed class PreferredDeliveryIntegrationTests
{
    [LocalFirestoreFact]
    public async Task FechaPasadaYCamposIncompletosNoCreanNiAuditanNiEnvian()
    {
        await using var f = await Fixture.Start();
        using var client = f.Client();
        var token = await client.GetStringAsync("/test-token");
        foreach (var pair in new[] { ("2000-01-01", "12:00"), ("2099-01-01", ""), ("", "12:00") })
        {
            using var response = await client.PostAsync("/Insumos/CrearSolicitudes", Form(token, pair.Item1, pair.Item2));
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.Redirect);
            Assert.Empty(await f.Compras.ObtenerSolicitudes());
            Assert.Equal(0, await f.AuditCount());
            Assert.Empty(f.Sender.Messages);
        }
    }

    [LocalFirestoreFact]
    public async Task PreferenciaFuturaSeGuardaYEnviaATodosConNombreConfigurado()
    {
        await using var f = await Fixture.Start();
        using var client = f.Client();
        var token = await client.GetStringAsync("/test-token");
        using var response = await client.PostAsync("/Insumos/CrearSolicitudes", Form(token, "2099-01-02", "10:30"));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var solicitudes = await f.Compras.ObtenerSolicitudes();
        Assert.Equal(2, solicitudes.Count);
        Assert.All(solicitudes, s => Assert.Equal(new DateTime(2099, 1, 2, 13, 30, 0, DateTimeKind.Utc), s.FechaEntregaPreferidaUtc));
        Assert.Equal(2, f.Sender.Messages.Count);
        foreach (var message in f.Sender.Messages)
        {
            var html = WebUtility.HtmlDecode(message.Html);
            Assert.Contains("02/01/2099", html);
            Assert.Contains("10:30", html);
            Assert.Contains("preferida", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Lavadero configurado", html);
        }
    }

    [LocalFirestoreFact]
    public async Task EmisionManualAuditaPreferenciaDeCadaProveedor()
    {
        await using var f = await Fixture.Start();
        using var client = f.Client();
        var token = await client.GetStringAsync("/test-token");
        using var response = await client.PostAsync("/Insumos/CrearSolicitudes", Form(token, "2099-01-02", "10:30"));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var solicitudes = await f.Compras.ObtenerSolicitudes();
        Assert.Equal(2, solicitudes.Count);
        var audit = Assert.Single(await new AuditService(f.Db).ObtenerRegistros());
        Assert.Equal("Emision Solicitud Cotizacion", audit.Action);
        Assert.Equal(solicitudes[0].GrupoId, audit.TargetId);
        Assert.Equal(solicitudes[0].GrupoId, audit.Despues["GrupoId"]);
        using var json = System.Text.Json.JsonDocument.Parse(audit.Despues["Solicitudes"]);
        var entries = json.RootElement.EnumerateArray().ToList();
        Assert.Equal(2, entries.Count);
        foreach (var solicitud in solicitudes)
        {
            var entry = Assert.Single(entries, x => x.GetProperty("Id").GetString() == solicitud.Id);
            Assert.Equal(solicitud.GrupoId, entry.GetProperty("GrupoId").GetString());
            Assert.Equal(solicitud.ProveedorId, entry.GetProperty("ProveedorId").GetString());
            Assert.Equal("2099-01-02T13:30:00.0000000Z", entry.GetProperty("FechaEntregaPreferidaUtc").GetString());
        }
        Assert.Equal(2, f.Sender.Messages.Count);
    }

    [LocalFirestoreFact]
    public async Task ValidacionUsaRelojFijoYZonaArgentina()
    {
        await using var f = await Fixture.Start();
        var compras = new ComprasService(f.Db, reloj: new FixedClock());
        foreach (var p in new[] { ("2026-11-16", "20:59"), ("2026-11-15", "23:59"), ("2026-11-17", ""), ("", "12:00"), ("invalido", "12:00") })
            Assert.False(compras.ValidarPreferencia(p.Item1, p.Item2, out _).EsExitoso);
        Assert.True(compras.ValidarPreferencia(null, null, out var sin).EsExitoso);
        Assert.Null(sin);
        Assert.True(compras.ValidarPreferencia("2026-11-16", "21:01", out var actual).EsExitoso);
        Assert.Equal(new DateTime(2026, 11, 17, 0, 1, 0, DateTimeKind.Utc), actual);
        Assert.True(compras.ValidarPreferencia("2026-11-17", "00:00", out _).EsExitoso);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 17, 0, 0, 0, TimeSpan.Zero);
    }

    [LocalFirestoreFact]
    public async Task ServicioRechazaInstantePasadoSinEfectos()
    {
        await using var f = await Fixture.Start();
        var result = await f.Compras.CrearSolicitudes(new() { "p1", "p2" }, new() { new OrdenCompraItem { InsumoId = "i", Cantidad = 2 } }, null, fechaEntregaPreferidaUtc: new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        Assert.False(result.EsExitoso);
        Assert.Empty(await f.Compras.ObtenerSolicitudes());
        Assert.Equal(0, await f.AuditCount());
        Assert.Empty(f.Sender.Messages);
    }

    private static FormUrlEncodedContent Form(string token, string fecha, string hora) => new(new[] {
        new KeyValuePair<string,string>("__RequestVerificationToken",token),
        new KeyValuePair<string,string>("proveedorIds","p1"), new KeyValuePair<string,string>("proveedorIds","p2"),
        new KeyValuePair<string,string>("items[0].InsumoId","i"), new KeyValuePair<string,string>("items[0].Cantidad","2"),
        new KeyValuePair<string,string>("fechaEntregaPreferida",fecha), new KeyValuePair<string,string>("horaEntregaPreferida",hora)
    });

    public sealed class HeaderAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier,"test"), new Claim(ClaimTypes.Email,"test@example.invalid"), new Claim("permiso","Insumos.Crear") };
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }
    public sealed class FakeSender : IEmailSender
    {
        public List<EmailMessage> Messages { get; } = new();
        public Task<ResultadoEnvioEmail> EnviarAsync(EmailMessage message, CancellationToken cancellationToken = default) { Messages.Add(message); return Task.FromResult(new ResultadoEnvioEmail(true,"OK")); }
    }
    private sealed class Fixture(WebApplication app, FirestoreDb db, FakeSender sender) : IAsyncDisposable
    {
        public FirestoreDb Db { get; } = db;
        public ComprasService Compras { get; } = new(db);
        public FakeSender Sender { get; } = sender;
        public async Task<int> AuditCount() => (await db.Collection("registros_auditoria").GetSnapshotAsync()).Count;
        public static async Task<Fixture> Start()
        {
            if (Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST") != "127.0.0.1:8190") throw new InvalidOperationException("Disposable emulator required.");
            var db = new FirestoreDbBuilder { ProjectId = "demo-preferred-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
            foreach (var id in new[] { "p1", "p2" }) await db.Collection("proveedores").Document(id).SetAsync(new Proveedor { Id=id, RazonSocial=id, EmailOrdenes=id+"@example.invalid" });
            await db.Collection("insumos").Document("i").SetAsync(new Insumo { Id="i", Nombre="Jabón", UnidadMedida="ml" });
            await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion { NombreLavadero="Lavadero configurado", Ubicacion="Calle de prueba" });
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName="Production" });
            builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
            builder.Services.AddControllersWithViews().AddApplicationPart(typeof(InsumosController).Assembly);
            builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, HeaderAuth>("Test", _ => { });
            builder.Services.AddAuthorization(); builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
            builder.Services.AddSingleton(db); builder.Services.AddMemoryCache(); builder.Services.AddScoped<ConfiguracionService>();
            builder.Services.AddScoped<InsumoService>(); builder.Services.AddScoped<ProveedorService>(); builder.Services.AddScoped<StockService>(); builder.Services.AddScoped<ComprasService>();
            var sender = new FakeSender(); builder.Services.AddSingleton(Mock.Of<IAuditWriter>()); builder.Services.AddSingleton<IEmailSender>(sender);
            var app=builder.Build(); app.UseAuthentication(); app.UseAuthorization();
            app.MapGet("/test-token", (HttpContext c, IAntiforgery a) => a.GetAndStoreTokens(c).RequestToken!).RequireAuthorization();
            app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"); await app.StartAsync(); return new(app,db,sender);
        }
        public HttpClient Client() => new(new HttpClientHandler { CookieContainer=new CookieContainer(), AllowAutoRedirect=false }) { BaseAddress=new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
        public async ValueTask DisposeAsync() { await app.StopAsync(); await app.DisposeAsync(); }
    }
}

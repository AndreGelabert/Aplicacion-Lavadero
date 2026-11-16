using System.Net;
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
using Moq;

namespace Firebase.Tests.Integration;

public sealed class CotizacionesHttpIntegrationTests
{
    [LocalFirestoreFact]
    public async Task RespuestaUnicaCambiaEstadoYPermiteAdjudicar()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-cotiza-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var proveedor = new Proveedor { Id = "p", RazonSocial = "Proveedor", Estado = "Activo" };
        var solicitud = new SolicitudCotizacion { Id = "s", GrupoId = "g", ProveedorId = "p", Items = new() { new OrdenCompraItem { InsumoId = "i", NombreInsumo = "Jabon", Cantidad = 2 } } };
        proveedor.EmailOrdenes = "p@example.invalid";
        await db.Collection("proveedores").Document("p").SetAsync(proveedor);
        await db.Collection("proveedores").Document("p2").SetAsync(new Proveedor { Id = "p2", RazonSocial = "Segundo", EmailOrdenes = "p2@example.invalid" });
        await db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "Jabon" });
        var email = new Mock<IEmailSender>();
        email.Setup(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ResultadoEnvioEmail(true, "Aceptado"));
        await db.Collection("solicitudesCotizacion").Document("s").SetAsync(solicitud);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(InsumosController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, ProveedorCatalogIntegrationTests.HeaderAuth>("Test", _ => { });
        builder.Services.AddAuthorization(); builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db); builder.Services.AddMemoryCache(); builder.Services.AddScoped<ConfiguracionService>();
        builder.Services.AddScoped<InsumoService>(); builder.Services.AddScoped<ProveedorService>(); builder.Services.AddScoped<StockService>(); builder.Services.AddScoped<ComprasService>();
        builder.Services.AddSingleton(Mock.Of<IAuditWriter>()); builder.Services.AddSingleton(email.Object);
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"); await app.StartAsync();
        try
        {
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Modificar");
            var page = await client.GetStringAsync("/Insumos/Cotizaciones?grupoId=g");
            var token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value; Assert.NotEmpty(token);
            Assert.Contains("Crear solicitud", page);
            Assert.Contains("Registrar respuesta", page);
            Assert.Contains("Items[0].Disponible", page);
            Assert.DoesNotContain("asp-action=\"Adjudicar\"", page);
            var rechazo = await client.PostAsync("/Insumos/Adjudicar", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["id"] = "s" }));
            Assert.Equal(HttpStatusCode.Redirect, rechazo.StatusCode);
            Assert.Equal("Pendiente", (await db.Collection("solicitudesCotizacion").Document("s").GetSnapshotAsync()).ConvertTo<SolicitudCotizacion>().Estado);
            Assert.Empty((await db.Collection("ordenesCompra").GetSnapshotAsync()).Documents);
            email.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
            var crear = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", token), new("proveedorIds", "p"), new("proveedorIds", "p2"), new("items[0].InsumoId", "i"), new("items[0].Cantidad", "3") };
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/Insumos/CrearSolicitudes", new FormUrlEncodedContent(crear))).StatusCode);
            client.DefaultRequestHeaders.Remove("X-Test-Permission"); client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Crear,Insumos.Modificar");
            Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Insumos/CrearSolicitudes", new FormUrlEncodedContent(crear))).StatusCode);
            email.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Cotizaciones"));
            var grupos = Regex.Matches(page, "/Insumos/Cotizaciones\\?grupoId=([^\"&]+)").Select(m => m.Groups[1].Value).ToArray();
            Assert.Equal(3, grupos.Length); Assert.Equal(2, grupos.Distinct().Count());
            var fields = new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["SolicitudId"] = "s", ["PlazoEntrega"] = "2", ["Items[0].InsumoId"] = "i", ["Items[0].Cantidad"] = "2", ["Items[0].PrecioUnitario"] = "5", ["Items[0].Disponible"] = "false" };
            Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Insumos/RegistrarRespuesta", new FormUrlEncodedContent(fields))).StatusCode);
            page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Cotizaciones?grupoId=g"));
            Assert.Contains("Respondida", page);
            Assert.Contains("10,00", page);
            Assert.Contains("Sin disponibilidad", page);
            Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Insumos/RegistrarRespuesta", new FormUrlEncodedContent(fields))).StatusCode);
            page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Cotizaciones?grupoId=g"));
            Assert.Contains("La solicitud ya no está pendiente.", page);
            var respuestaId = Regex.Match(page, "name=\"respuestaId\" value=\"([^\"]+)\"").Groups[1].Value; Assert.NotEmpty(respuestaId);
            Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Insumos/Adjudicar", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["id"] = "s", ["respuestaId"] = respuestaId }))).StatusCode);
            page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Cotizaciones?grupoId=g")); Assert.Contains("Adjudicada", page);
            Assert.Contains("Adjudicada", page);

            var historica = new SolicitudCotizacion { Id = "historica", GrupoId = "historico", ProveedorId = "p", Estado = "Pendiente", Items = new() { new OrdenCompraItem { InsumoId = "i", NombreInsumo = "Jabon", Cantidad = 2 } } };
            var rival = new SolicitudCotizacion { Id = "rival", GrupoId = "historico", ProveedorId = "p2", Estado = "Respondida", Items = historica.Items };
            await db.Collection("solicitudesCotizacion").Document(historica.Id).SetAsync(historica);
            await db.Collection("solicitudesCotizacion").Document(rival.Id).SetAsync(rival);
            await db.Collection("cotizacionesRespuestas").Document("hist").SetAsync(new CotizacionRespuesta { Id = "hist", SolicitudId = "historica", ProveedorId = "p", PlazoEntrega = "3", Items = new() { new OrdenCompraItem { InsumoId = "i", NombreInsumo = "Jabon", Cantidad = 2, PrecioUnitario = 17 } } });
            await db.Collection("cotizacionesRespuestas").Document("ajena").SetAsync(new CotizacionRespuesta { Id = "ajena", SolicitudId = "rival", ProveedorId = "p2", Items = new() { new OrdenCompraItem { InsumoId = "i", Cantidad = 2, PrecioUnitario = 99 } } });
            var antes = (await db.Collection("ordenesCompra").GetSnapshotAsync()).Count;
            var ajena = await client.PostAsync("/Insumos/Adjudicar", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["id"] = "historica", ["respuestaId"] = "ajena" }));
            Assert.Equal(HttpStatusCode.Redirect, ajena.StatusCode);
            Assert.Contains("La respuesta no corresponde a la solicitud.", WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Ordenes")));
            Assert.Equal(antes, (await db.Collection("ordenesCompra").GetSnapshotAsync()).Count);
            Assert.Equal("Pendiente", (await db.Collection("solicitudesCotizacion").Document("historica").GetSnapshotAsync()).GetValue<string>("Estado"));
            Assert.Equal("Respondida", (await db.Collection("solicitudesCotizacion").Document("rival").GetSnapshotAsync()).GetValue<string>("Estado"));
            page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Cotizaciones?grupoId=historico"));
            Assert.Contains("name=\"respuestaId\" value=\"hist\"", page);
            var adjudicacion = await client.PostAsync("/Insumos/Adjudicar", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["id"] = "historica", ["respuestaId"] = "hist" }));
            Assert.Equal(HttpStatusCode.Redirect, adjudicacion.StatusCode);
            var ordenes = (await db.Collection("ordenesCompra").GetSnapshotAsync()).Documents.Select(d => d.ConvertTo<OrdenCompra>()).ToList();
            Assert.Equal(antes + 1, ordenes.Count);
            var ordenHistorica = Assert.Single(ordenes, o => o.GrupoCotizacionId == "historico");
            Assert.Equal(17, Assert.Single(ordenHistorica.Items).PrecioUnitario);
            Assert.Equal("Adjudicada", (await db.Collection("solicitudesCotizacion").Document("historica").GetSnapshotAsync()).GetValue<string>("Estado"));
            Assert.Equal("Descartada", (await db.Collection("solicitudesCotizacion").Document("rival").GetSnapshotAsync()).GetValue<string>("Estado"));
            email.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }
        finally { await app.StopAsync(); }
    }

    [LocalFirestoreFact]
    public async Task AprobacionSolicitudHttpUsaClaimsYRegistraAuditoriaTransaccionalSinDuplicar()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-aprob-http-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var solicitud = new SolicitudCotizacion
        {
            Id = "sol-http-1",
            GrupoId = "grupo-http",
            ProveedorId = "p-http",
            Estado = "PendienteAprobacion",
            Items = new() { new OrdenCompraItem { InsumoId = "ins-1", NombreInsumo = "Jabon", Cantidad = 10m } }
        };
        await db.Collection("solicitudesCotizacion").Document(solicitud.Id).SetAsync(solicitud);
        await db.Collection("proveedores").Document("p-http").SetAsync(new Proveedor { Id = "p-http", RazonSocial = "Proveedor HTTP", EmailOrdenes = "prov@example.invalid", Estado = "Activo" });

        var auditMock = new Mock<IAuditWriter>();
        var email = new Mock<IEmailSender>();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(InsumosController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, ProveedorCatalogIntegrationTests.HeaderAuth>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db);
        builder.Services.AddMemoryCache();
        builder.Services.AddScoped<ConfiguracionService>();
        builder.Services.AddScoped<InsumoService>();
        builder.Services.AddScoped<ProveedorService>();
        builder.Services.AddScoped<StockService>();
        builder.Services.AddScoped<ComprasService>();
        builder.Services.AddSingleton(auditMock.Object);
        builder.Services.AddSingleton(email.Object);

        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();

        try
        {
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() })
            {
                BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single())
            };
            client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Modificar");

            var page = await client.GetStringAsync("/Insumos/Cotizaciones?grupoId=grupo-http");
            var token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.NotEmpty(token);

            var post = await client.PostAsync("/Insumos/AprobarSolicitud", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = "sol-http-1"
            }));
            Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);

            // Mock IAuditWriter never called (moved to transaction)
            auditMock.Verify(a => a.LogEvent(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);

            var solDb = (await db.Collection("solicitudesCotizacion").Document("sol-http-1").GetSnapshotAsync()).ConvertTo<SolicitudCotizacion>();
            Assert.Equal("Pendiente", solDb.Estado);

            // Audit record persisted in Firestore transaction with actor claims
            var auditorias = (await db.Collection("registros_auditoria").WhereEqualTo("TargetId", "sol-http-1").GetSnapshotAsync()).Documents
                .Select(d => d.ConvertTo<AuditLog>()).ToList();
            var log = Assert.Single(auditorias);
            Assert.Equal("test", log.UserId);
            Assert.Equal("test@example.invalid", log.UserEmail);
            Assert.Equal("Aprobación de solicitud de cotización", log.Action);
            Assert.Equal("SolicitudCotizacion", log.TargetType);
        }
        finally
        {
            await app.StopAsync();
        }
    }
}

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

public sealed class InsumosEstadoFiltroIntegrationTests
{
    [LocalFirestoreFact]
    public async Task EstadosAgrupacionFiltrosPaginacionYPermisosPorHttp()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-estado-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        async Task Seed(string id, string nombre, string tipo, string estado, decimal stock = 1)
            => await db.Collection("insumos").Document(id).SetAsync(new Insumo { Id = id, Nombre = nombre, Tipo = tipo, Estado = estado, StockActual = stock, StockMinimo = 2 });
        await db.Collection(TipoInsumoService.Collection).Document("mixto").SetAsync(new TipoInsumo { Id = "mixto", Nombre = "Mixto" });
        await Seed("active", "Visible Activo", "Mixto", "Activo");
        await Seed("inactive", "Oculto Inactivo", "Mixto", "Inactivo");
        await Seed("only", "Solo Inactivo", "UnicoInactivo", "Inactivo");
        for (int i = 0; i < 21; i++) await Seed("g" + i.ToString("D2"), "GrupoProducto" + i.ToString("D2"), "Tipo" + i.ToString("D2"), "Activo");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(InsumosController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, ProveedorCatalogIntegrationTests.HeaderAuth>("Test", _ => { });
        builder.Services.AddAuthorization(); builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db); builder.Services.AddMemoryCache(); builder.Services.AddScoped<ConfiguracionService>();
        builder.Services.AddScoped<InsumoService>(); builder.Services.AddScoped<TipoInsumoService>(); builder.Services.AddScoped<ProveedorService>(); builder.Services.AddScoped<StockService>(); builder.Services.AddScoped<ComprasService>();
        builder.Services.AddSingleton(Mock.Of<IAuditWriter>()); builder.Services.AddSingleton(Mock.Of<IEmailSender>());
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"); await app.StartAsync();
        try
        {
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Consultar,Insumos.Modificar");
            async Task<string> Get(string query) { var r = await client.GetAsync("/Insumos" + query); Assert.Equal(HttpStatusCode.OK, r.StatusCode); return WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync()); }
            var before = (await db.Collection("insumos").GetSnapshotAsync()).Documents.ToDictionary(d => d.Id, d => d.GetValue<string>("Estado"));
            var initial = await Get("?search=Inactivo");
            Assert.DoesNotContain("Solo Inactivo", initial); Assert.DoesNotContain("UnicoInactivo — stock", initial); Assert.DoesNotContain("Oculto Inactivo", initial);
            var active = await Get("?tipos=Mixto&estados=Activo"); Assert.Contains("Visible Activo", active); Assert.DoesNotContain("Oculto Inactivo", active); Assert.Matches("(?s)stock físico total:.*?2", active);
            var inactive = await Get("?estados=Inactivo"); Assert.Contains("Solo Inactivo", inactive); Assert.Contains("UnicoInactivo — stock", inactive); Assert.Contains("Oculto Inactivo", inactive); Assert.DoesNotContain("Visible Activo", inactive);
            var both = await Get("?tipos=Mixto&estados=Activo&estados=Inactivo"); Assert.Contains("Visible Activo", both); Assert.Contains("Oculto Inactivo", both);
            var blank = await Get("?tipos=Mixto&estados="); Assert.Contains("Visible Activo", blank); Assert.Contains("Oculto Inactivo", blank);
            var combined = await Get("?search=Oculto&tipos=Mixto&soloBajoMinimo=true&estados=Inactivo"); Assert.Contains("Oculto Inactivo", combined); Assert.DoesNotContain("Visible Activo", combined);
            var paging = await Get("?search=GrupoProducto&soloBajoMinimo=true&estados=Activo&estados=Inactivo");
            Assert.Contains("de <span", paging); Assert.Contains(">2</span>", paging);
            var link = Regex.Match(paging, "href=\"([^\"]+)\"[^>]*data-page=\"2\""); Assert.True(link.Success);
            var url = link.Groups[1].Value; Assert.Contains("estados=Activo", url); Assert.Contains("estados=Inactivo", url); Assert.Contains("search=GrupoProducto", url); Assert.Contains("soloBajoMinimo=true", url);
            var page2 = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos" + url)); Assert.Contains("GrupoProducto", page2); Assert.DoesNotContain("Página 3", page2);
            var oneGroup = await Get("?search=Solo&estados=Inactivo"); Assert.DoesNotContain("Paginación de grupos de insumos", oneGroup);
            var after = (await db.Collection("insumos").GetSnapshotAsync()).Documents.ToDictionary(d => d.Id, d => d.GetValue<string>("Estado")); Assert.Equal(before.OrderBy(x => x.Key), after.OrderBy(x => x.Key));
            var token = Regex.Match(active, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value; Assert.NotEmpty(token);
            async Task Change(string state) { var response = await client.PostAsync("/Insumos/CambiarEstado", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["id"] = "active", ["estado"] = state })); Assert.Equal(HttpStatusCode.Redirect, response.StatusCode); }
            await Change("Inactivo"); Assert.DoesNotContain("Visible Activo", await Get("?search=Visible")); Assert.Contains("Visible Activo", await Get("?search=Visible&estados=Inactivo"));
            await Change("Activo"); Assert.Contains("Visible Activo", await Get("?search=Visible"));
            var priorItem = (await db.Collection("insumos").Document("active").GetSnapshotAsync()).ConvertTo<Insumo>();
            var priorAudits = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Count;
            var priorMovements = (await new StockService(db).ObtenerMovimientos()).Count;
            client.DefaultRequestHeaders.Remove("X-Test-Permission"); client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Consultar");
            var rejected = await client.PostAsync("/Insumos/CambiarEstado", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["id"] = "active", ["estado"] = "Inactivo" }));
            Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
            var afterItem = (await db.Collection("insumos").Document("active").GetSnapshotAsync()).ConvertTo<Insumo>();
            Assert.Equal(priorItem.Estado, afterItem.Estado); Assert.Equal(priorItem.StockActual, afterItem.StockActual);
            Assert.Equal(priorAudits, (await db.Collection("registros_auditoria").GetSnapshotAsync()).Count); Assert.Equal(priorMovements, (await new StockService(db).ObtenerMovimientos()).Count);
            client.DefaultRequestHeaders.Remove("X-Test-Permission");
            var forbidden = await client.GetAsync("/Insumos?estados=Inactivo"); Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }
        finally { await app.StopAsync(); }
    }
}

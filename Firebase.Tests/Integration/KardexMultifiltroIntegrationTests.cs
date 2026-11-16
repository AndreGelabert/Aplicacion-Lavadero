using System.Net;
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
using System.Text.RegularExpressions;

namespace Firebase.Tests.Integration;

public sealed class KardexMultifiltroIntegrationTests
{
    [LocalFirestoreFact]
    public async Task RepetidosCombinanFiltrosYPreservanCursores()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-multikardex-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        foreach (var id in new[] { "a", "b", "c" })
            await db.Collection("insumos").Document(id).SetAsync(new Insumo { Id = id, Nombre = "Insumo-" + id });
        for (var n = 0; n < 25; n++)
        {
            var id = "m" + n.ToString("D2");
            await db.Collection("movimientosStock").Document(id).SetAsync(new MovimientoStock { Id = id, InsumoId = n % 2 == 0 ? "a" : "b", Tipo = n % 2 == 0 ? "IngresoManual" : "EgresoManual", Fecha = new DateTime(2025, 2, 1, 12, 0, 0, DateTimeKind.Utc), Justificacion = "fila-" + id });
        }
        await db.Collection("movimientosStock").Document("excluded").SetAsync(new MovimientoStock { Id = "excluded", InsumoId = "c", Tipo = "IngresoManual", Fecha = new DateTime(2025, 2, 1, 12, 0, 0, DateTimeKind.Utc), Justificacion = "fila-excluida" });
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(InsumosController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, ProveedorCatalogIntegrationTests.HeaderAuth>("Test", _ => { });
        builder.Services.AddAuthorization(); builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db); builder.Services.AddMemoryCache(); builder.Services.AddScoped<ConfiguracionService>();
        builder.Services.AddScoped<InsumoService>(); builder.Services.AddScoped<ProveedorService>(); builder.Services.AddScoped<StockService>(); builder.Services.AddScoped<ComprasService>();
        builder.Services.AddSingleton(Mock.Of<IAuditWriter>()); builder.Services.AddSingleton(Mock.Of<IEmailSender>());
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"); await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Kardex");
            async Task<string> Get(string query) => WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Kardex" + query));
            const string query = "?insumoId=a&insumoId=b&tipo=IngresoManual&tipo=EgresoManual&desde=2025-02-01&hasta=2025-02-01&sortOrder=asc";
            var first = await Get(query);
            Assert.Contains("fila-m00", first); Assert.Contains("fila-m19", first); Assert.DoesNotContain("fila-m20", first); Assert.DoesNotContain("fila-excluida", first);
            foreach (var name in new[] { "insumoId", "tipo" }) Assert.Equal(2, Regex.Matches(first, "name=\"" + name + "\"[^>]*checked").Count);
            string Link(string html, string label) { var match = Regex.Match(html, "href=\"([^\"]+)\"[^>]*aria-label=\"" + label + "\""); Assert.True(match.Success, label); return match.Groups[1].Value; }
            var next = Link(first, "Siguiente");
            foreach (var part in new[] { "insumoId=a", "insumoId=b", "tipo=IngresoManual", "tipo=EgresoManual", "desde=2025-02-01", "hasta=2025-02-01", "sortOrder=asc" }) Assert.Contains(part, next);
            var second = WebUtility.HtmlDecode(await client.GetStringAsync(next));
            Assert.Contains("fila-m20", second); Assert.Contains("fila-m24", second); Assert.DoesNotContain("fila-m00", second);
            var back = WebUtility.HtmlDecode(await client.GetStringAsync(Link(second, "Anterior")));
            Assert.Contains("fila-m00", back); Assert.Contains("fila-m19", back); Assert.DoesNotContain("fila-m20", back);
            var onlyItems = await Get("?insumoId=a&insumoId=b&sortOrder=asc");
            Assert.Contains("fila-m00", onlyItems); Assert.Contains("fila-m01", onlyItems); Assert.DoesNotContain("fila-excluida", onlyItems);
            var onlyTypes = await Get("?tipo=IngresoManual&tipo=EgresoManual&sortOrder=asc");
            Assert.Contains("fila-m00", onlyTypes); Assert.Contains("fila-m01", onlyTypes); Assert.Contains("fila-excluida", onlyTypes);
            var andFilter = await Get("?insumoId=a&insumoId=b&tipo=EgresoManual&sortOrder=asc");
            Assert.Contains("fila-m01", andFilter); Assert.DoesNotContain("fila-m00", andFilter); Assert.DoesNotContain("fila-excluida", andFilter);
            var desc = await Get("?insumoId=a&insumoId=b&tipo=IngresoManual&tipo=EgresoManual&sortOrder=desc");
            Assert.Contains("fila-m24", desc); Assert.Contains("fila-m05", desc); Assert.DoesNotContain("fila-m04", desc);
            var descNext = WebUtility.HtmlDecode(await client.GetStringAsync(Link(desc, "Siguiente")));
            Assert.Contains("fila-m04", descNext); Assert.DoesNotContain("fila-m05", descNext);
            var descBack = WebUtility.HtmlDecode(await client.GetStringAsync(Link(descNext, "Anterior")));
            Assert.Contains("fila-m24", descBack); Assert.DoesNotContain("fila-m04", descBack);
            var outsideDate = await Get("?insumoId=a&insumoId=b&desde=2025-02-02"); Assert.Contains("No hay movimientos", outsideDate);
            var defaults = await Get(""); Assert.Contains("fila-m24", defaults); Assert.DoesNotContain("fila-excluida", defaults);
            var blank = await Get("?insumoId=&tipo="); Assert.Contains("fila-m24", blank); Assert.DoesNotContain("fila-excluida", blank);
            var defaultNext = WebUtility.HtmlDecode(await client.GetStringAsync(Link(defaults, "Siguiente")));
            var blankNext = WebUtility.HtmlDecode(await client.GetStringAsync(Link(blank, "Siguiente")));
            Assert.Contains("fila-excluida", defaultNext); Assert.Contains("fila-excluida", blankNext);
            var all = await Get("?insumoId=a&insumoId=b&insumoId=c&" + string.Join("&", Enum.GetNames<TipoMovimientoStock>().Select(t => "tipo=" + Uri.EscapeDataString(t))) + "&sortOrder=asc");
            Assert.Contains("fila-excluida", all); Assert.Contains("fila-m00", all); Assert.Contains("fila-m01", all);
            var escaped = await Get("?insumoId=a&insumoId=a&insumoId=b&insumoId=missing%26value&tipo=IngresoManual&tipo=EgresoManual&tipo=IngresoManual&sortOrder=asc");
            var escapedLink = Link(escaped, "Siguiente");
            Assert.Contains("insumoId=missing%26value", escapedLink);
            Assert.Equal(1, Regex.Matches(escapedLink, @"(?:\?|&)insumoId=a(?:&|$)").Count);
            Assert.Equal(1, Regex.Matches(escapedLink, @"(?:\?|&)tipo=IngresoManual(?:&|$)").Count);
            var escapedSecond = WebUtility.HtmlDecode(await client.GetStringAsync(escapedLink)); Assert.Contains("fila-m24", escapedSecond);
            var single = await Get("?insumoId=a&tipo=IngresoManual");
            Assert.Contains("fila-m00", single); Assert.DoesNotContain("fila-m01", single);
            var none = await Get("?insumoId=missing"); Assert.Contains("No hay movimientos", none);
            var largeQuery = "?" + string.Join("&", Enumerable.Range(0, 35).Select(i => "insumoId=" + (i == 0 ? "a" : i == 1 ? "b" : "missing" + i))) + "&tipo=IngresoManual&tipo=EgresoManual&sortOrder=asc";
            var large = await Get(largeQuery);
            Assert.Contains("fila-m00", large); Assert.Contains("fila-m19", large); Assert.DoesNotContain("fila-excluida", large);
            var largeNext = Link(large, "Siguiente");
            Assert.Contains("insumoId=missing34", largeNext); Assert.Contains("tipo=EgresoManual", largeNext);
            var largeSecond = WebUtility.HtmlDecode(await client.GetStringAsync(largeNext));
            Assert.Contains("fila-m24", largeSecond); Assert.DoesNotContain("fila-m00", largeSecond);
            var largeBack = WebUtility.HtmlDecode(await client.GetStringAsync(Link(largeSecond, "Anterior")));
            Assert.Contains("fila-m00", largeBack); Assert.DoesNotContain("fila-m20", largeBack);
            var stock = new StockService(db);
            Assert.Equal(26, (await stock.ObtenerMovimientos()).Count);
            Assert.Equal(13, (await stock.ObtenerMovimientos("a", "IngresoManual")).Count);
            Assert.Equal(13, (await stock.ObtenerKardexPaginado("a", "IngresoManual")).Movimientos.Count);
        }
        finally { await app.StopAsync(); }
    }
}

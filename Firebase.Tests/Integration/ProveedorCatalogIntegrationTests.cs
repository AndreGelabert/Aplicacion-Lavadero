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
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Firebase.Tests.Integration;

public sealed class ProveedorCatalogIntegrationTests
{
    public sealed class HeaderAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "test"), new(ClaimTypes.Email, "test@example.invalid") };
            claims.AddRange(Request.Headers["X-Test-Permission"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => new Claim("permiso", p)));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }

    [LocalFirestoreFact]
    public async Task EstadosAbiertosImpidenDesactivarYEstadosCerradosLoPermitenPorHttp()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-proveedor-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(InsumosController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, InsumoCatalogIntegrationTests.Auth>("Test", _ => { });
        builder.Services.AddAuthorization(); builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db); builder.Services.AddMemoryCache(); builder.Services.AddScoped<ConfiguracionService>();
        builder.Services.AddScoped<InsumoService>(); builder.Services.AddScoped<ProveedorService>(); builder.Services.AddScoped<StockService>(); builder.Services.AddScoped<ComprasService>();
        builder.Services.AddSingleton(Mock.Of<IAuditWriter>()); builder.Services.AddSingleton(Mock.Of<IEmailSender>());
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"); await app.StartAsync();
        try
        {
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            async Task<string> Page(string path) { var r = await client.GetAsync(path); Assert.Equal(HttpStatusCode.OK, r.StatusCode); return WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync()); }
            var token = Regex.Match(await Page("/Insumos/Proveedores"), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.NotEmpty(token);
            foreach (var estado in new[] { "PendienteAprobacion", "RevisionManual", "Enviada", "Confirmada", "Despachada", "Recibida", "Cancelada" })
            {
                var proveedor = new Proveedor { Id = "p-" + estado, IdentificacionTributaria = estado, RazonSocial = estado, Estado = "Activo" };
                await db.Collection("proveedores").Document(proveedor.Id).SetAsync(proveedor);
                var orden = new OrdenCompra { Id = "o-" + estado, ProveedorId = proveedor.Id, Estado = estado };
                await db.Collection("ordenesCompra").Document(orden.Id).SetAsync(orden);
                var beforeOrders = await Page("/Insumos/Ordenes");
                var response = await client.PostAsync("/Insumos/EstadoProveedor", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["id"] = proveedor.Id, ["estado"] = "Inactivo" }));
                Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
                var page = await Page("/Insumos/Proveedores");
                var row = Assert.Single(Regex.Matches(page, "<tr class=\"border-b[^\"]*\">.*?</tr>", RegexOptions.Singleline).Select(m => m.Value), r => r.Contains(">" + estado + "<"));
                var abierto = estado is "PendienteAprobacion" or "RevisionManual" or "Enviada" or "Confirmada" or "Despachada";
                Assert.Contains(abierto ? "Desactivar" : "Reactivar", row);
                if (abierto) Assert.Contains("No se puede desactivar un proveedor con órdenes pendientes.", page);
                static string OrderTable(string html)
                {
                    var table = Regex.Match(html, "<table class=\"w-full text-sm[^\"]*\"[^>]*>.*?</table>", RegexOptions.Singleline).Value;
                    Assert.NotEmpty(table);
                    return Regex.Replace(table, "(name=\"__RequestVerificationToken\"[^>]*value=\")[^\"]+", "$1TOKEN");
                }
                Assert.Equal(OrderTable(beforeOrders), OrderTable(await Page("/Insumos/Ordenes")));
            }
        }
        finally { await app.StopAsync(); }
    }

    [LocalFirestoreFact]
    public async Task PermisosExistentesProtegenRutasDeProveedorPorHttp()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-proveedor-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(InsumosController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, HeaderAuth>("Test", _ => { });
        builder.Services.AddAuthorization(); builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db); builder.Services.AddMemoryCache(); builder.Services.AddScoped<ConfiguracionService>();
        builder.Services.AddScoped<InsumoService>(); builder.Services.AddScoped<ProveedorService>(); builder.Services.AddScoped<StockService>(); builder.Services.AddScoped<ComprasService>();
        builder.Services.AddSingleton(Mock.Of<IAuditWriter>()); builder.Services.AddSingleton(Mock.Of<IEmailSender>());
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"); await app.StartAsync();
        try
        {
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Crear,Insumos.Modificar");
            var initial = await client.GetAsync("/Insumos/Proveedores"); Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
            var token = Regex.Match(await initial.Content.ReadAsStringAsync(), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value; Assert.NotEmpty(token);
            var create = new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["crear"] = "true", ["IdentificacionTributaria"] = "30-00000001-5", ["RazonSocial"] = "Original", ["Contacto"] = "1234567890", ["EmailOrdenes"] = "original@example.invalid" };
            Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Insumos/GuardarProveedor", new FormUrlEncodedContent(create))).StatusCode);
            var page = WebUtility.HtmlDecode(await (await client.GetAsync("/Insumos/Proveedores")).Content.ReadAsStringAsync());
            var id = Regex.Match(page, "data-provider-edit data-id=\"([^\"]+)\"").Groups[1].Value; Assert.NotEmpty(id);
            client.DefaultRequestHeaders.Remove("X-Test-Permission"); client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Consultar");
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/Insumos/Proveedores")).StatusCode);
            create["IdentificacionTributaria"] = "30-00000002-3"; create["RazonSocial"] = "No autorizado";
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/Insumos/GuardarProveedor", new FormUrlEncodedContent(create))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/Insumos/EstadoProveedor", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["id"] = id, ["estado"] = "Inactivo" }))).StatusCode);
            client.DefaultRequestHeaders.Remove("X-Test-Permission"); client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Modificar");
            page = WebUtility.HtmlDecode(await (await client.GetAsync("/Insumos/Proveedores")).Content.ReadAsStringAsync());
            Assert.Single(Regex.Matches(page, "<tr class=\"border-b[^\"]*\">.*?</tr>", RegexOptions.Singleline));
            Assert.Contains("Original", page); Assert.DoesNotContain("No autorizado", page); Assert.Contains("Desactivar", page);
            client.DefaultRequestHeaders.Remove("X-Test-Permission"); client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Crear");
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Insumos/Proveedores")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/Insumos/EstadoProveedor", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["id"] = id, ["estado"] = "Inactivo" }))).StatusCode);
        }
        finally { await app.StopAsync(); }
    }

    [LocalFirestoreFact]
    public async Task UnicidadYOrdenPendienteSeAplicanPorHttpSinMutarProveedores()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-proveedor-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(InsumosController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, InsumoCatalogIntegrationTests.Auth>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db);
        builder.Services.AddMemoryCache(); builder.Services.AddScoped<ConfiguracionService>();
        builder.Services.AddScoped<InsumoService>(); builder.Services.AddScoped<ProveedorService>();
        builder.Services.AddScoped<StockService>(); builder.Services.AddScoped<ComprasService>();
        builder.Services.AddSingleton(Mock.Of<IAuditWriter>()); builder.Services.AddSingleton(Mock.Of<IEmailSender>());
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(address) };
            async Task<string> Page(string path = "/Insumos/Proveedores") { var r = await client.GetAsync(path); Assert.Equal(HttpStatusCode.OK, r.StatusCode); return WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync()); }
            var page = await Page();
            var token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.NotEmpty(token);
            async Task Post(string action, params (string Key, string Value)[] fields)
            {
                var data = fields.Select(x => new KeyValuePair<string, string>(x.Key, x.Value)).Append(new("__RequestVerificationToken", token));
                var response = await client.PostAsync("/Insumos/" + action, new FormUrlEncodedContent(data));
                Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            }
            await Post("GuardarProveedor", ("crear", "true"), ("IdentificacionTributaria", "30-00000001-5"), ("RazonSocial", "Alfa"), ("Contacto", "1234567890"), ("EmailOrdenes", "alfa@example.invalid"));
            await Post("GuardarProveedor", ("crear", "true"), ("IdentificacionTributaria", "30-00000002-3"), ("RazonSocial", "Beta"), ("Contacto", "1234567890"), ("EmailOrdenes", "beta@example.invalid"));
            page = await Page();
            var rows = Regex.Matches(page, "<tr class=\"border-b[^\"]*\">.*?</tr>", RegexOptions.Singleline).Select(m => m.Value).ToArray();
            Assert.Equal(2, rows.Length);
            var alfa = Assert.Single(rows, r => r.Contains(">Alfa<"));
            var beta = Assert.Single(rows, r => r.Contains(">Beta<"));
            var id = Regex.Match(alfa, "data-provider-edit data-id=\"([^\"]+)\"").Groups[1].Value;
            var betaId = Regex.Match(beta, "data-provider-edit data-id=\"([^\"]+)\"").Groups[1].Value;
            Assert.NotEmpty(id); Assert.NotEmpty(betaId);
            async Task VerifyUnchanged(string message)
            {
                var html = await Page();
                Assert.Contains(message, html);
                var current = Regex.Matches(html, "<tr class=\"border-b[^\"]*\">.*?</tr>", RegexOptions.Singleline).Select(m => m.Value).ToArray();
                static string WithoutTokens(string row) => Regex.Replace(row, "(name=\"__RequestVerificationToken\"[^>]*value=\")[^\"]+", "$1TOKEN");
                Assert.Equal(rows.Select(WithoutTokens), current.Select(WithoutTokens));
            }
            await Post("GuardarProveedor", ("crear", "true"), ("IdentificacionTributaria", " 30-00000001-5 "), ("RazonSocial", "Otro"), ("Contacto", "1234567890"), ("EmailOrdenes", "otro@example.invalid"));
            await VerifyUnchanged("Ya existe un proveedor con esa identificación fiscal.");
            await Post("GuardarProveedor", ("crear", "true"), ("IdentificacionTributaria", "30-00000003-1"), ("RazonSocial", " alfa "), ("Contacto", "1234567890"), ("EmailOrdenes", "otro@example.invalid"));
            await VerifyUnchanged("Ya existe un proveedor con esa razón social.");
            await Post("GuardarProveedor", ("crear", "false"), ("Id", betaId), ("IdentificacionTributaria", "30-00000002-3"), ("RazonSocial", " ALFA "), ("Contacto", "1234567890"), ("EmailOrdenes", "beta@example.invalid"));
            await VerifyUnchanged("Ya existe un proveedor con esa razón social.");
            var orden = new OrdenCompra { Id = "orden-prueba", ProveedorId = id, Estado = "RevisionManual" };
            await db.Collection("ordenesCompra").Document(orden.Id).SetAsync(orden);
            Assert.Equal("RevisionManual", Assert.Single(await new ComprasService(db).ObtenerOrdenes()).Estado);
            var beforeAudit = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Count;
            var orders = await Page("/Insumos/Ordenes");
            Assert.Contains("Alfa", orders); Assert.Contains("RevisionManual", orders); Assert.DoesNotContain("Enviada", orders);
            // La orden activa cambia la proyección informativa, no los datos persistidos del proveedor.
            page = await Page();
            rows = Regex.Matches(page, "<tr class=\"border-b[^\"]*\">.*?</tr>", RegexOptions.Singleline).Select(m => m.Value).ToArray();
            await Post("EstadoProveedor", ("id", id), ("estado", "Inactivo"));
            await VerifyUnchanged("No se puede desactivar un proveedor con órdenes pendientes.");
            Assert.Equal("Activo", (await new ProveedorService(db).Obtener(id))!.Estado);
            Assert.Equal(beforeAudit, (await db.Collection("registros_auditoria").GetSnapshotAsync()).Count);
            Assert.Equal("RevisionManual", Assert.Single(await new ComprasService(db).ObtenerOrdenes()).Estado);
            var afterOrders = await Page("/Insumos/Ordenes");
            Assert.Contains("Alfa", afterOrders); Assert.Contains("RevisionManual", afterOrders); Assert.DoesNotContain("Enviada", afterOrders);
        }
        finally { await app.StopAsync(); }
    }

    [LocalFirestoreFact]
    public async Task ProveedoresHttpPermiteEditarSinCambiarIdentificacionNiEstado()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-proveedor-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(InsumosController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, InsumoCatalogIntegrationTests.Auth>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db);
        builder.Services.AddMemoryCache(); builder.Services.AddScoped<ConfiguracionService>();
        builder.Services.AddScoped<InsumoService>(); builder.Services.AddScoped<ProveedorService>();
        builder.Services.AddScoped<StockService>(); builder.Services.AddScoped<ComprasService>();
        builder.Services.AddSingleton(Mock.Of<IAuditWriter>()); builder.Services.AddSingleton(Mock.Of<IEmailSender>());
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(address) };
            async Task<string> Page() { var r = await client.GetAsync("/Insumos/Proveedores"); Assert.Equal(HttpStatusCode.OK, r.StatusCode); return await r.Content.ReadAsStringAsync(); }
            var page = await Page();
            var token = Regex.Match(page, "name=\\\"__RequestVerificationToken\\\"[^>]*value=\\\"([^\\\"]+)\\\"").Groups[1].Value;
            Assert.NotEmpty(token);
            async Task Post(string action, params (string Key, string Value)[] fields)
            {
                var data = fields.Select(x => new KeyValuePair<string, string>(x.Key, x.Value)).Append(new("__RequestVerificationToken", token));
                var response = await client.PostAsync("/Insumos/" + action, new FormUrlEncodedContent(data));
                Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            }
            await Post("GuardarProveedor", ("crear", "true"), ("IdentificacionTributaria", "30-00000001-5"), ("RazonSocial", "Original"), ("Contacto", "1234567890"), ("EmailOrdenes", "uno@example.invalid"));
            var service = new ProveedorService(db);
            var item = Assert.Single(await service.ObtenerTodos());
            page = await Page();
            Assert.Contains("Editar", page);
            Assert.Contains("Original", page);
            Assert.Contains("data-provider-edit data-id=\"" + item.Id + "\"", page);
            Assert.Contains("data-tax=\"30-00000001-5\"", page);
            await Post("EstadoProveedor", ("id", item.Id), ("estado", "Inactivo"));
            await Post("GuardarProveedor", ("crear", "false"), ("Id", item.Id), ("IdentificacionTributaria", "30-00000001-5"), ("RazonSocial", "Editado"), ("Contacto", "0987654321"), ("EmailOrdenes", "dos@example.invalid"), ("Estado", "Activo"));
            item = (await service.Obtener(item.Id))!;
            Assert.Equal("Editado", item.RazonSocial); Assert.Equal("0987654321", item.Contacto); Assert.Equal("dos@example.invalid", item.EmailOrdenes); Assert.Equal("30-00000001-5", item.IdentificacionTributaria); Assert.Equal("Inactivo", item.Estado);
            await Post("GuardarProveedor", ("crear", "false"), ("Id", item.Id), ("IdentificacionTributaria", "30-00000002-3"), ("RazonSocial", "Alterado"), ("Contacto", "0987654321"), ("EmailOrdenes", "dos@example.invalid"));
            page = await Page(); Assert.Contains("La identificación fiscal no se puede modificar", System.Net.WebUtility.HtmlDecode(page));
            var after = (await service.Obtener(item.Id))!;
            Assert.Equal("30-00000001-5", after.IdentificacionTributaria); Assert.Equal("Editado", after.RazonSocial); Assert.Equal("0987654321", after.Contacto); Assert.Equal("dos@example.invalid", after.EmailOrdenes); Assert.Equal("Inactivo", after.Estado);
            Assert.Single(await service.ObtenerTodos());
            await Post("EstadoProveedor", ("id", item.Id), ("estado", "Activo"));
            Assert.Equal("Activo", (await service.Obtener(item.Id))!.Estado);
        }
        finally { await app.StopAsync(); }
    }

    [LocalFirestoreFact]
    public async Task GuardarProveedorHttpRegistraAuditoriaEnCreacionYActualizacionYSinEventoEnRechazo()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-proveedor-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(InsumosController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, HeaderAuth>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db);
        builder.Services.AddMemoryCache(); builder.Services.AddScoped<ConfiguracionService>();
        builder.Services.AddScoped<InsumoService>(); builder.Services.AddScoped<ProveedorService>();
        builder.Services.AddScoped<StockService>(); builder.Services.AddScoped<ComprasService>();
        builder.Services.AddScoped<IAuditWriter, AuditService>(); builder.Services.AddSingleton(Mock.Of<IEmailSender>());
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(address) };
            client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Crear,Insumos.Modificar");

            async Task<string> Page()
            {
                var r = await client.GetAsync("/Insumos/Proveedores");
                Assert.Equal(HttpStatusCode.OK, r.StatusCode);
                return WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync());
            }

            var page = await Page();
            var token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.NotEmpty(token);

            async Task<HttpResponseMessage> PostProveedor(params (string Key, string Value)[] fields)
            {
                var data = fields.Select(x => new KeyValuePair<string, string>(x.Key, x.Value)).Append(new("__RequestVerificationToken", token));
                return await client.PostAsync("/Insumos/GuardarProveedor", new FormUrlEncodedContent(data));
            }

            var createResp = await PostProveedor(
                ("crear", "true"),
                ("IdentificacionTributaria", " 30-00000001-5 "),
                ("RazonSocial", " Proveedor Uno "),
                ("Contacto", "1234567890"),
                ("EmailOrdenes", "uno@example.invalid"));
            Assert.Equal(HttpStatusCode.Redirect, createResp.StatusCode);

            var service = new ProveedorService(db);
            var uno = Assert.Single(await service.ObtenerTodos());
            var esperadoUno = new Dictionary<string, string>
            {
                ["IdentificacionTributaria"] = "30-00000001-5",
                ["RazonSocial"] = "Proveedor Uno",
                ["Contacto"] = "1234567890",
                ["EmailOrdenes"] = "uno@example.invalid",
                ["Estado"] = "Activo"
            };

            async Task AssertAuditEvents(params (string Action, string TargetId, string NombreHistorico, Dictionary<string, string> Antes, Dictionary<string, string> Despues)[] expected)
            {
                var docs = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents;
                Assert.Equal(expected.Length, docs.Count);
                foreach (var e in expected)
                {
                    var doc = Assert.Single(docs, d => d.GetValue<string>("Action") == e.Action && d.GetValue<string>("TargetId") == e.TargetId);
                    Assert.Equal("test", doc.GetValue<string>("UserId"));
                    Assert.Equal("test@example.invalid", doc.GetValue<string>("UserEmail"));
                    Assert.Equal("Proveedor", doc.GetValue<string>("TargetType"));
                    Assert.Equal(e.NombreHistorico, doc.GetValue<string>("NombreHistorico"));
                    Assert.Equal(e.Antes.OrderBy(x => x.Key), doc.GetValue<Dictionary<string, string>>("Antes").OrderBy(x => x.Key));
                    Assert.Equal(e.Despues.OrderBy(x => x.Key), doc.GetValue<Dictionary<string, string>>("Despues").OrderBy(x => x.Key));
                    var json = System.Text.Json.JsonSerializer.Serialize(doc.ToDictionary());
                    Assert.DoesNotContain("Password", json, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("Token", json, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("Secret", json, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("ApiKey", json, StringComparison.OrdinalIgnoreCase);
                }
            }

            await AssertAuditEvents(("Creacion de proveedor", uno.Id, "Proveedor Uno", new(), esperadoUno));

            // Creación rechazada por CUIT duplicado no emite auditoría ni crea proveedor
            Assert.Equal(HttpStatusCode.Redirect, (await PostProveedor(
                ("crear", "true"),
                ("IdentificacionTributaria", "30-00000001-5"),
                ("RazonSocial", "Otro"), ("Contacto", "1234567890"), ("EmailOrdenes", "otro@example.invalid"))).StatusCode);
            Assert.Contains("Ya existe un proveedor con esa identificación fiscal.", await Page());
            Assert.Single(await service.ObtenerTodos());
            await AssertAuditEvents(("Creacion de proveedor", uno.Id, "Proveedor Uno", new(), esperadoUno));

            // Creación rechazada por razón social duplicada no emite auditoría ni crea proveedor
            Assert.Equal(HttpStatusCode.Redirect, (await PostProveedor(
                ("crear", "true"),
                ("IdentificacionTributaria", "30-00000002-3"),
                ("RazonSocial", "proveedor uno"), ("Contacto", "1234567890"), ("EmailOrdenes", "otro@example.invalid"))).StatusCode);
            Assert.Contains("Ya existe un proveedor con esa razón social.", await Page());
            Assert.Single(await service.ObtenerTodos());
            await AssertAuditEvents(("Creacion de proveedor", uno.Id, "Proveedor Uno", new(), esperadoUno));

            // Segundo proveedor válido para probar rechazo por razón social duplicada en edición
            Assert.Equal(HttpStatusCode.Redirect, (await PostProveedor(
                ("crear", "true"),
                ("IdentificacionTributaria", "30-00000003-1"),
                ("RazonSocial", "Beta"), ("Contacto", "1234567890"), ("EmailOrdenes", "beta@example.invalid"))).StatusCode);
            var beta = Assert.Single(await service.ObtenerTodos(), p => p.IdentificacionTributaria == "30-00000003-1");
            var esperadoBeta = new Dictionary<string, string>
            {
                ["IdentificacionTributaria"] = "30-00000003-1",
                ["RazonSocial"] = "Beta",
                ["Contacto"] = "1234567890",
                ["EmailOrdenes"] = "beta@example.invalid",
                ["Estado"] = "Activo"
            };

            // Actualización válida (incluye intento ignorado de cambiar Estado)
            Assert.Equal(HttpStatusCode.Redirect, (await PostProveedor(
                ("crear", "false"),
                ("Id", uno.Id),
                ("IdentificacionTributaria", "30-00000001-5"),
                ("RazonSocial", " Proveedor Editado "),
                ("Contacto", "0987654321"),
                ("EmailOrdenes", "dos@example.invalid"),
                ("Estado", "Inactivo"))).StatusCode);
            var esperadoEditado = new Dictionary<string, string>
            {
                ["IdentificacionTributaria"] = "30-00000001-5",
                ["RazonSocial"] = "Proveedor Editado",
                ["Contacto"] = "0987654321",
                ["EmailOrdenes"] = "dos@example.invalid",
                ["Estado"] = "Activo"
            };
            await AssertAuditEvents(
                ("Creacion de proveedor", uno.Id, "Proveedor Uno", new(), esperadoUno),
                ("Creacion de proveedor", beta.Id, "Beta", new(), esperadoBeta),
                ("Actualizacion de proveedor", uno.Id, "Proveedor Editado", esperadoUno, esperadoEditado));

            // Actualizaciones rechazadas (CUIT modificado, razón social duplicada, id inexistente) y sin permiso no emiten evento
            Assert.Equal(HttpStatusCode.Redirect, (await PostProveedor(
                ("crear", "false"),
                ("Id", uno.Id),
                ("IdentificacionTributaria", "30-00000009-0"),
                ("RazonSocial", "Alterado"), ("Contacto", "1234567890"), ("EmailOrdenes", "uno@example.invalid"))).StatusCode);
            Assert.Contains("La identificación fiscal no se puede modificar.", await Page());

            Assert.Equal(HttpStatusCode.Redirect, (await PostProveedor(
                ("crear", "false"),
                ("Id", uno.Id),
                ("IdentificacionTributaria", "30-00000001-5"),
                ("RazonSocial", "BETA"), ("Contacto", "1234567890"), ("EmailOrdenes", "uno@example.invalid"))).StatusCode);
            Assert.Contains("Ya existe un proveedor con esa razón social.", await Page());

            Assert.Equal(HttpStatusCode.Redirect, (await PostProveedor(
                ("crear", "false"),
                ("Id", "no-existe"),
                ("IdentificacionTributaria", "30-00000008-2"),
                ("RazonSocial", "Fantasma"), ("Contacto", "1234567890"), ("EmailOrdenes", "otro@example.invalid"))).StatusCode);
            Assert.Contains("El proveedor no existe.", await Page());

            client.DefaultRequestHeaders.Remove("X-Test-Permission");
            client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Consultar");
            Assert.Equal(HttpStatusCode.Forbidden, (await PostProveedor(
                ("crear", "false"),
                ("Id", uno.Id),
                ("IdentificacionTributaria", "30-00000001-5"),
                ("RazonSocial", "Sin Permiso"))).StatusCode);

            var persisted = (await service.Obtener(uno.Id))!;
            Assert.Equal("Proveedor Editado", persisted.RazonSocial);
            Assert.Equal("0987654321", persisted.Contacto);
            Assert.Equal("dos@example.invalid", persisted.EmailOrdenes);
            await AssertAuditEvents(
                ("Creacion de proveedor", uno.Id, "Proveedor Uno", new(), esperadoUno),
                ("Creacion de proveedor", beta.Id, "Beta", new(), esperadoBeta),
                ("Actualizacion de proveedor", uno.Id, "Proveedor Editado", esperadoUno, esperadoEditado));
        }
        finally { await app.StopAsync(); }
    }

    [LocalFirestoreFact]
    public async Task EstadoProveedorHttpRegistraAuditoriaEnDesactivacionYReactivacionYSinEventoEnRechazo()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-proveedor-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(InsumosController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, HeaderAuth>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db);
        builder.Services.AddMemoryCache(); builder.Services.AddScoped<ConfiguracionService>();
        builder.Services.AddScoped<InsumoService>(); builder.Services.AddScoped<ProveedorService>();
        builder.Services.AddScoped<StockService>(); builder.Services.AddScoped<ComprasService>();
        builder.Services.AddScoped<IAuditWriter, AuditService>();
        var emailSender = new Mock<IEmailSender>();
        emailSender.Setup(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoEnvioEmail(true, "Fake ok"));
        builder.Services.AddSingleton(emailSender.Object);
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(address) };
            client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Crear,Insumos.Modificar");

            async Task<string> Page(string path = "/Insumos/Proveedores")
            {
                var r = await client.GetAsync(path);
                Assert.Equal(HttpStatusCode.OK, r.StatusCode);
                return WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync());
            }

            var page = await Page();
            var token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.NotEmpty(token);

            async Task<HttpResponseMessage> PostForm(string action, params (string Key, string Value)[] fields)
            {
                var data = fields.Select(x => new KeyValuePair<string, string>(x.Key, x.Value)).Append(new("__RequestVerificationToken", token));
                return await client.PostAsync("/Insumos/" + action, new FormUrlEncodedContent(data));
            }

            Assert.Equal(HttpStatusCode.Redirect, (await PostForm("GuardarProveedor",
                ("crear", "true"),
                ("IdentificacionTributaria", "30-00000004-9"),
                ("RazonSocial", "Proveedor Estado"),
                ("Contacto", "1234567890"),
                ("EmailOrdenes", "estado@example.invalid"))).StatusCode);

            var service = new ProveedorService(db);
            var proveedor = Assert.Single(await service.ObtenerTodos());
            var esperadoActivo = new Dictionary<string, string>
            {
                ["IdentificacionTributaria"] = "30-00000004-9",
                ["RazonSocial"] = "Proveedor Estado",
                ["Contacto"] = "1234567890",
                ["EmailOrdenes"] = "estado@example.invalid",
                ["Estado"] = "Activo"
            };
            var esperadoInactivo = new Dictionary<string, string>(esperadoActivo) { ["Estado"] = "Inactivo" };

            async Task AssertProveedorAuditEvents(params (string Action, string TargetId, string NombreHistorico, Dictionary<string, string> Antes, Dictionary<string, string> Despues)[] expected)
            {
                var docs = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents
                    .Where(d => d.GetValue<string>("TargetType") == "Proveedor")
                    .ToList();
                Assert.Equal(expected.Length, docs.Count);
                foreach (var e in expected)
                {
                    var doc = Assert.Single(docs, d => d.GetValue<string>("Action") == e.Action && d.GetValue<string>("TargetId") == e.TargetId);
                    Assert.Equal("test", doc.GetValue<string>("UserId"));
                    Assert.Equal("test@example.invalid", doc.GetValue<string>("UserEmail"));
                    Assert.Equal("Proveedor", doc.GetValue<string>("TargetType"));
                    Assert.Equal(e.NombreHistorico, doc.GetValue<string>("NombreHistorico"));
                    Assert.Equal(e.Antes.OrderBy(x => x.Key), doc.GetValue<Dictionary<string, string>>("Antes").OrderBy(x => x.Key));
                    Assert.Equal(e.Despues.OrderBy(x => x.Key), doc.GetValue<Dictionary<string, string>>("Despues").OrderBy(x => x.Key));
                    var json = System.Text.Json.JsonSerializer.Serialize(doc.ToDictionary());
                    Assert.DoesNotContain("Password", json, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("Token", json, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("Secret", json, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("ApiKey", json, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("Clave", json, StringComparison.OrdinalIgnoreCase);
                }
            }

            // Desactivación exitosa
            Assert.Equal(HttpStatusCode.Redirect, (await PostForm("EstadoProveedor", ("id", proveedor.Id), ("estado", "Inactivo"))).StatusCode);
            page = await Page();
            Assert.Contains("Estado del proveedor actualizado.", page);
            Assert.Contains("Reactivar", page);
            Assert.Equal("Inactivo", (await service.Obtener(proveedor.Id))!.Estado);
            await AssertProveedorAuditEvents(
                ("Creacion de proveedor", proveedor.Id, "Proveedor Estado", new(), esperadoActivo),
                ("Desactivacion de proveedor", proveedor.Id, "Proveedor Estado", esperadoActivo, esperadoInactivo));

            // Reactivación exitosa
            Assert.Equal(HttpStatusCode.Redirect, (await PostForm("EstadoProveedor", ("id", proveedor.Id), ("estado", "Activo"))).StatusCode);
            page = await Page();
            Assert.Contains("Estado del proveedor actualizado.", page);
            Assert.Contains("Desactivar", page);
            Assert.Equal("Activo", (await service.Obtener(proveedor.Id))!.Estado);
            await AssertProveedorAuditEvents(
                ("Creacion de proveedor", proveedor.Id, "Proveedor Estado", new(), esperadoActivo),
                ("Desactivacion de proveedor", proveedor.Id, "Proveedor Estado", esperadoActivo, esperadoInactivo),
                ("Reactivacion de proveedor", proveedor.Id, "Proveedor Estado", esperadoInactivo, esperadoActivo));

            // Rechazo por orden pendiente no emite auditoría ni muta estado
            var orden = new OrdenCompra { Id = "orden-prueba", ProveedorId = proveedor.Id, Estado = "RevisionManual" };
            await db.Collection("ordenesCompra").Document(orden.Id).SetAsync(orden);
            Assert.Equal("RevisionManual", Assert.Single(await new ComprasService(db).ObtenerOrdenes()).Estado);
            var auditBeforeRejection = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Count;
            Assert.Equal(HttpStatusCode.Redirect, (await PostForm("EstadoProveedor", ("id", proveedor.Id), ("estado", "Inactivo"))).StatusCode);
            page = await Page();
            Assert.Contains("No se puede desactivar un proveedor con órdenes pendientes.", page);
            Assert.Equal("Activo", (await service.Obtener(proveedor.Id))!.Estado);
            Assert.Equal("RevisionManual", Assert.Single(await new ComprasService(db).ObtenerOrdenes()).Estado);
            Assert.Equal(auditBeforeRejection, (await db.Collection("registros_auditoria").GetSnapshotAsync()).Count);
            await AssertProveedorAuditEvents(
                ("Creacion de proveedor", proveedor.Id, "Proveedor Estado", new(), esperadoActivo),
                ("Desactivacion de proveedor", proveedor.Id, "Proveedor Estado", esperadoActivo, esperadoInactivo),
                ("Reactivacion de proveedor", proveedor.Id, "Proveedor Estado", esperadoInactivo, esperadoActivo));

            // Rechazo por permisos insuficientes (403) no emite auditoría ni muta estado
            foreach (var permiso in new[] { "Insumos.Consultar", "Insumos.Crear" })
            {
                client.DefaultRequestHeaders.Remove("X-Test-Permission");
                client.DefaultRequestHeaders.Add("X-Test-Permission", permiso);
                Assert.Equal(HttpStatusCode.Forbidden, (await PostForm("EstadoProveedor", ("id", proveedor.Id), ("estado", "Inactivo"))).StatusCode);
            }
            Assert.Equal("Activo", (await service.Obtener(proveedor.Id))!.Estado);
            await AssertProveedorAuditEvents(
                ("Creacion de proveedor", proveedor.Id, "Proveedor Estado", new(), esperadoActivo),
                ("Desactivacion de proveedor", proveedor.Id, "Proveedor Estado", esperadoActivo, esperadoInactivo),
                ("Reactivacion de proveedor", proveedor.Id, "Proveedor Estado", esperadoInactivo, esperadoActivo));
        }
        finally { await app.StopAsync(); }
    }

    [Fact]
    public void ProveedorService_NoExponeGanchoGlobalEstatico()
    {
        var propiedadEstatica = typeof(ProveedorService).GetProperty(
            "InterceptorAuditoriaGlobal",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

        Assert.Null(propiedadEstatica);
    }

    [LocalFirestoreFact]
    public async Task ProveedorService_FalloSimuladoEnAuditoriaAltaHaceRollbackTotal()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-prov-audit-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var proveedores = new ProveedorService(db);
        proveedores.InterceptorAuditoria = log =>
        {
            if (log.Action == "Creacion de proveedor")
                throw new InvalidOperationException("Fallo forzado al auditar creacion de proveedor");
        };

        var prov = new Proveedor
        {
            IdentificacionTributaria = "20-99999999-9",
            RazonSocial = "Proveedor Fallo",
            Contacto = "1234567890",
            EmailOrdenes = "carlos@example.invalid"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            proveedores.GuardarConAuditoria(prov, true, "actor-uid", "actor@example.invalid"));

        Assert.Empty((await db.Collection("proveedores").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task ProveedorService_FalloSimuladoEnAuditoriaEdicionHaceRollbackTotal()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-prov-audit-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var proveedores = new ProveedorService(db);

        var prov = new Proveedor
        {
            Id = "prov-original",
            IdentificacionTributaria = "30-00000001-5",
            RazonSocial = "Proveedor Original",
            Contacto = "1234567890",
            EmailOrdenes = "orig@example.invalid",
            Estado = "Activo"
        };
        await db.Collection("proveedores").Document(prov.Id).SetAsync(prov);

        proveedores.InterceptorAuditoria = log =>
        {
            if (log.Action == "Actualizacion de proveedor")
                throw new InvalidOperationException("Fallo forzado al auditar edicion de proveedor");
        };

        var edicion = new Proveedor
        {
            Id = prov.Id,
            IdentificacionTributaria = prov.IdentificacionTributaria,
            RazonSocial = "Proveedor Modificado",
            Contacto = "0987654321",
            EmailOrdenes = "nuevo@example.invalid"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            proveedores.GuardarConAuditoria(edicion, false, "actor-uid", "actor@example.invalid"));

        var persistido = (await db.Collection("proveedores").Document(prov.Id).GetSnapshotAsync()).ConvertTo<Proveedor>();
        Assert.Equal("Proveedor Original", persistido.RazonSocial);
        Assert.Equal("1234567890", persistido.Contacto);
        Assert.Equal("orig@example.invalid", persistido.EmailOrdenes);
        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task ProveedorService_FalloSimuladoEnAuditoriaCambioEstadoHaceRollbackTotal()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-prov-audit-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var proveedores = new ProveedorService(db);

        var prov = new Proveedor
        {
            Id = "prov-estado",
            IdentificacionTributaria = "30-00000002-3",
            RazonSocial = "Proveedor Estado",
            Estado = "Activo"
        };
        await db.Collection("proveedores").Document(prov.Id).SetAsync(prov);

        proveedores.InterceptorAuditoria = log =>
        {
            if (log.Action == "Desactivacion de proveedor")
                throw new InvalidOperationException("Fallo forzado al auditar desactivacion");
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            proveedores.CambiarEstadoConAuditoria(prov.Id, "Inactivo", "actor-uid", "actor@example.invalid"));

        var persistido = (await db.Collection("proveedores").Document(prov.Id).GetSnapshotAsync()).ConvertTo<Proveedor>();
        Assert.Equal("Activo", persistido.Estado);
        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task ProveedorService_EdicionNoOpNoMutaDocumentoNiGeneraAuditoria()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-prov-audit-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var proveedores = new ProveedorService(db);

        var prov = new Proveedor
        {
            Id = "prov-noop",
            IdentificacionTributaria = "30-00000003-1",
            RazonSocial = "Proveedor Noop",
            Contacto = "1234567890",
            EmailOrdenes = "noop@example.invalid",
            Estado = "Activo"
        };
        await db.Collection("proveedores").Document(prov.Id).SetAsync(prov);

        var edicionIdentica = new Proveedor
        {
            Id = prov.Id,
            IdentificacionTributaria = prov.IdentificacionTributaria,
            RazonSocial = "Proveedor Noop",
            Contacto = "1234567890",
            EmailOrdenes = "noop@example.invalid"
        };

        var resultado = await proveedores.GuardarConAuditoria(edicionIdentica, false, "actor-uid", "actor@example.invalid");
        Assert.True(resultado.Resultado.EsExitoso);

        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task ProveedorService_CambioEstadoNoOpNoMutaDocumentoNiGeneraAuditoria()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-prov-audit-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var proveedores = new ProveedorService(db);

        var prov = new Proveedor
        {
            Id = "prov-estado-noop",
            IdentificacionTributaria = "30-00000004-9",
            RazonSocial = "Proveedor Estado Noop",
            Estado = "Activo"
        };
        await db.Collection("proveedores").Document(prov.Id).SetAsync(prov);

        var resultado = await proveedores.CambiarEstadoConAuditoria(prov.Id, "Activo", "actor-uid", "actor@example.invalid");
        Assert.True(resultado.Resultado.EsExitoso);

        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task ProveedorService_RechazoValidacionNoMutaNiGeneraAuditoria()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-prov-audit-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var proveedores = new ProveedorService(db);

        var existente = new Proveedor
        {
            Id = "prov-existente",
            IdentificacionTributaria = "30-00000005-8",
            RazonSocial = "Proveedor Existente",
            Estado = "Activo"
        };
        await db.Collection("proveedores").Document(existente.Id).SetAsync(existente);

        // CUIT duplicado
        var resCuit = await proveedores.GuardarConAuditoria(new Proveedor
        {
            IdentificacionTributaria = "30-00000005-8",
            RazonSocial = "Otro Proveedor", Contacto = "1234567890", EmailOrdenes = "otro@example.invalid"
        }, true, "actor-uid", "actor@example.invalid");
        Assert.False(resCuit.Resultado.EsExitoso);

        // Razón social duplicada
        var resRazon = await proveedores.GuardarConAuditoria(new Proveedor
        {
            IdentificacionTributaria = "30-00000006-6",
            RazonSocial = "proveedor existente", Contacto = "1234567890", EmailOrdenes = "otro@example.invalid"
        }, true, "actor-uid", "actor@example.invalid");
        Assert.False(resRazon.Resultado.EsExitoso);

        // Modificación de CUIT
        var resModCuit = await proveedores.GuardarConAuditoria(new Proveedor
        {
            Id = existente.Id,
            IdentificacionTributaria = "30-00000007-4",
            RazonSocial = "Proveedor Existente", Contacto = "1234567890", EmailOrdenes = "otro@example.invalid"
        }, false, "actor-uid", "actor@example.invalid");
        Assert.False(resModCuit.Resultado.EsExitoso);

        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task ProveedorService_ConcurrenciaDesactivacionYEdicionRespetaConsistencia()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-prov-audit-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var proveedores = new ProveedorService(db);

        var prov = new Proveedor
        {
            Id = "prov-carrera",
            IdentificacionTributaria = "30-00000008-2",
            RazonSocial = "Proveedor Carrera",
            Contacto = "1234567890",
            EmailOrdenes = "ana@example.invalid",
            Estado = "Activo"
        };
        await db.Collection("proveedores").Document(prov.Id).SetAsync(prov);

        var tarea1 = proveedores.GuardarConAuditoria(new Proveedor
        {
            Id = prov.Id,
            IdentificacionTributaria = prov.IdentificacionTributaria,
            RazonSocial = "Proveedor Carrera Editado",
            Contacto = "0987654321",
            EmailOrdenes = "ana.editada@example.invalid"
        }, false, "u1", "u1@example.invalid");

        var tarea2 = proveedores.CambiarEstadoConAuditoria(prov.Id, "Inactivo", "u2", "u2@example.invalid");

        await Task.WhenAll(tarea1, tarea2);

        var persistido = (await db.Collection("proveedores").Document(prov.Id).GetSnapshotAsync()).ConvertTo<Proveedor>();
        Assert.Equal("Inactivo", persistido.Estado);

        var logs = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents
            .Select(d => d.ConvertTo<AuditLog>())
            .ToList();

        Assert.NotEmpty(logs);
        Assert.All(logs, l => Assert.Equal("Proveedor", l.TargetType));
        Assert.All(logs, l => Assert.Equal(prov.Id, l.TargetId));
    }
}

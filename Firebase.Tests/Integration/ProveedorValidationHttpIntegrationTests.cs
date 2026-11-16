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

public sealed class ProveedorValidationHttpIntegrationTests
{
    [LocalFirestoreFact]
    public async Task PostForjadoYRechazosDeSeguridadNoCambianProveedorNiAuditoria()
    {
        Assert.Equal("127.0.0.1:8255", Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST"));
        var db = new FirestoreDbBuilder { ProjectId = "demo-prov-http-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var original = new Proveedor { Id = "p", IdentificacionTributaria = "123", RazonSocial = "Original", Contacto = "1234567890", EmailOrdenes = "original@example.invalid" };
        await db.Collection("proveedores").Document("p").SetAsync(original);
        await db.Collection("solicitudesCotizacion").Document("s").SetAsync(new SolicitudCotizacion { Id = "s", ProveedorId = "p", Estado = "Pendiente" });
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
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Modificar");
            var page = await client.GetAsync("/Insumos/Proveedores"); Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            var token = Regex.Match(await page.Content.ReadAsStringAsync(), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value; Assert.NotEmpty(token);
            var fields = new Dictionary<string, string> { ["crear"] = "false", ["Id"] = "p", ["IdentificacionTributaria"] = "123", ["RazonSocial"] = "Alterado", ["Contacto"] = "1234567890", ["EmailOrdenes"] = "nuevo@example.invalid" };
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Insumos/GuardarProveedor", new FormUrlEncodedContent(fields))).StatusCode);
            fields["__RequestVerificationToken"] = token;
            client.DefaultRequestHeaders.Remove("X-Test-Permission"); client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Consultar");
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/Insumos/GuardarProveedor", new FormUrlEncodedContent(fields))).StatusCode);
            client.DefaultRequestHeaders.Remove("X-Test-Permission"); client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Modificar");
            Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Insumos/GuardarProveedor", new FormUrlEncodedContent(fields))).StatusCode);
            var after = (await new ProveedorService(db).Obtener("p"))!;
            Assert.Equal("Original", after.RazonSocial); Assert.Equal("original@example.invalid", after.EmailOrdenes); Assert.Equal("1234567890", after.Contacto);
            Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
            Assert.Equal("Pendiente", Assert.Single(await new ComprasService(db).ObtenerSolicitudes()).Estado);
            Assert.Empty(await new ComprasService(db).ObtenerOrdenes());
        }
        finally { await app.StopAsync(); }
    }
}

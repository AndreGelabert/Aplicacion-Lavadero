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
using Xunit;

namespace Firebase.Tests.Integration;

public sealed class ProveedorValidationHttpIntegrationTests
{
    private static async Task<(WebApplication App, HttpClient Client)> CreateTestServerAsync(FirestoreDb db)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
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
        builder.Services.AddSingleton(Mock.Of<IAuditWriter>());
        builder.Services.AddSingleton(Mock.Of<IEmailSender>());
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();

        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() })
        {
            BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single())
        };
        return (app, client);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        var page = await client.GetAsync("/Insumos/Proveedores");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var match = Regex.Match(await page.Content.ReadAsStringAsync(), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "Token CSRF no encontrado en /Insumos/Proveedores");
        return match.Groups[1].Value;
    }

    [LocalFirestoreFact]
    public async Task PostForjadoYRechazosDeSeguridadNoCambianProveedorNiAuditoria()
    {
        Assert.Equal("127.0.0.1:8190", Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST"));
        var db = new FirestoreDbBuilder { ProjectId = "demo-prov-http-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var original = new Proveedor { Id = "p", IdentificacionTributaria = "123", RazonSocial = "Original", Contacto = "1234567890", EmailOrdenes = "original@example.invalid" };
        await db.Collection("proveedores").Document("p").SetAsync(original);
        await db.Collection("solicitudesCotizacion").Document("s").SetAsync(new SolicitudCotizacion { Id = "s", ProveedorId = "p", Estado = "Pendiente" });
        var (app, client) = await CreateTestServerAsync(db);
        try
        {
            client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Modificar");
            var token = await GetAntiforgeryTokenAsync(client);
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

    [LocalFirestoreFact]
    public async Task ProveedorHttp_CuitInvalidoModulo11_RechazaYNoPersisteNiAudita()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-prov-cuit-inv-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var (app, client) = await CreateTestServerAsync(db);
        try
        {
            client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Crear");
            var token = await GetAntiforgeryTokenAsync(client);

            // CUIT con DV inválido (20-27362459-0 tiene DV esperado 8)
            var fields = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["crear"] = "true",
                ["IdentificacionTributaria"] = "20-27362459-0",
                ["RazonSocial"] = "Proveedor Invalido DV",
                ["Contacto"] = "1134567890",
                ["EmailOrdenes"] = "invalido@example.invalid"
            };

            var response = await client.PostAsync("/Insumos/GuardarProveedor", new FormUrlEncodedContent(fields));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            // Verificar que no se persistió en Firestore
            var snapshot = await db.Collection("proveedores").GetSnapshotAsync();
            Assert.Empty(snapshot.Documents);

            // Verificar que no se generó registro de auditoría
            var auditSnapshot = await db.Collection("registros_auditoria").GetSnapshotAsync();
            Assert.Empty(auditSnapshot.Documents);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [LocalFirestoreFact]
    public async Task ProveedorHttp_CuitValidoNormalizaFormatoYPersisteConAuditoria()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-prov-cuit-val-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var (app, client) = await CreateTestServerAsync(db);
        try
        {
            client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Crear");
            var token = await GetAntiforgeryTokenAsync(client);

            // CUIT válido sin guiones: "20273624598" -> debe normalizarse a "20-27362459-8"
            var fields = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["crear"] = "true",
                ["IdentificacionTributaria"] = "20273624598",
                ["RazonSocial"] = "Proveedor Valido Normalizado",
                ["Contacto"] = "1134567890",
                ["EmailOrdenes"] = "valido@example.invalid"
            };

            var response = await client.PostAsync("/Insumos/GuardarProveedor", new FormUrlEncodedContent(fields));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            // Verificar persistencia y normalización en Firestore
            var snapshot = await db.Collection("proveedores").GetSnapshotAsync();
            var doc = Assert.Single(snapshot.Documents);
            var proveedor = doc.ConvertTo<Proveedor>();
            Assert.Equal("20-27362459-8", proveedor.IdentificacionTributaria);
            Assert.Equal("Proveedor Valido Normalizado", proveedor.RazonSocial);
            Assert.Equal("Activo", proveedor.Estado);

            // Verificar auditoría
            var auditSnapshot = await db.Collection("registros_auditoria").GetSnapshotAsync();
            var auditDoc = Assert.Single(auditSnapshot.Documents);
            var audit = auditDoc.ConvertTo<AuditLog>();
            Assert.Equal("Creacion de proveedor", audit.Action);
            Assert.Equal("Proveedor", audit.TargetType);
            Assert.Equal(proveedor.Id, audit.TargetId);
            Assert.NotNull(audit.Despues);
            Assert.Equal("20-27362459-8", audit.Despues["IdentificacionTributaria"]);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [LocalFirestoreFact]
    public async Task ProveedorHttp_CuitDuplicado_RechazaSegundoAltaSinDuplicar()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-prov-cuit-dup-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var (app, client) = await CreateTestServerAsync(db);
        try
        {
            client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Crear");
            var token = await GetAntiforgeryTokenAsync(client);

            // Primer proveedor con CUIT con guiones
            var fields1 = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["crear"] = "true",
                ["IdentificacionTributaria"] = "20-27362459-8",
                ["RazonSocial"] = "Proveedor Original",
                ["Contacto"] = "1134567890",
                ["EmailOrdenes"] = "original@example.invalid"
            };
            var resp1 = await client.PostAsync("/Insumos/GuardarProveedor", new FormUrlEncodedContent(fields1));
            Assert.Equal(HttpStatusCode.Redirect, resp1.StatusCode);

            // Segundo proveedor con mismo CUIT pero sin guiones y distinta razón social
            var token2 = await GetAntiforgeryTokenAsync(client);
            var fields2 = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token2,
                ["crear"] = "true",
                ["IdentificacionTributaria"] = "20273624598",
                ["RazonSocial"] = "Proveedor Duplicado Intento",
                ["Contacto"] = "1134567891",
                ["EmailOrdenes"] = "duplicado@example.invalid"
            };
            var resp2 = await client.PostAsync("/Insumos/GuardarProveedor", new FormUrlEncodedContent(fields2));
            Assert.Equal(HttpStatusCode.Redirect, resp2.StatusCode);

            // La base de datos debe contener solo el primer proveedor
            var snapshot = await db.Collection("proveedores").GetSnapshotAsync();
            var doc = Assert.Single(snapshot.Documents);
            var proveedor = doc.ConvertTo<Proveedor>();
            Assert.Equal("Proveedor Original", proveedor.RazonSocial);

            // Solo 1 registro de auditoría de creación
            var auditSnapshot = await db.Collection("registros_auditoria").GetSnapshotAsync();
            Assert.Single(auditSnapshot.Documents);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [LocalFirestoreFact]
    public async Task ProveedorHttp_EdicionNoPermiteModificarCuit()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-prov-cuit-inm-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var (app, client) = await CreateTestServerAsync(db);
        try
        {
            // Proveedor existente pre-insertado con CUIT original
            var original = new Proveedor
            {
                Id = "prov-inmutable-1",
                IdentificacionTributaria = "20-27362459-8",
                RazonSocial = "Proveedor Inmutable S.A.",
                Contacto = "1134567890",
                EmailOrdenes = "inmutable@example.invalid",
                Estado = "Activo"
            };
            await db.Collection("proveedores").Document(original.Id).SetAsync(original);

            client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Modificar");
            var token = await GetAntiforgeryTokenAsync(client);

            // Intentar modificar el CUIT en la edición (crear = false) usando otro CUIT válido ("27-28033514-8")
            var fields = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["crear"] = "false",
                ["Id"] = original.Id,
                ["IdentificacionTributaria"] = "27-28033514-8",
                ["RazonSocial"] = "Proveedor Nombre Cambiado",
                ["Contacto"] = "1134567890",
                ["EmailOrdenes"] = "inmutable@example.invalid"
            };

            var response = await client.PostAsync("/Insumos/GuardarProveedor", new FormUrlEncodedContent(fields));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            // En Firestore el CUIT y la razón social deben mantenerse inalterados
            var actual = (await new ProveedorService(db).Obtener(original.Id))!;
            Assert.Equal("20-27362459-8", actual.IdentificacionTributaria);
            Assert.Equal("Proveedor Inmutable S.A.", actual.RazonSocial);

            // Ninguna auditoría de actualización
            var auditSnapshot = await db.Collection("registros_auditoria").GetSnapshotAsync();
            Assert.Empty(auditSnapshot.Documents);
        }
        finally
        {
            await app.StopAsync();
        }
    }
}

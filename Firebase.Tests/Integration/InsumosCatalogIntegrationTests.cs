using System.Reflection;
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
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Xunit;

namespace Firebase.Tests.Integration;

public sealed class InsumosCatalogIntegrationTests
{
    private static FirestoreDb CrearDb() => new FirestoreDbBuilder
    {
        ProjectId = "demo-insumo-atomic-" + Guid.NewGuid().ToString("N")[..16],
        EmulatorDetection = EmulatorDetection.EmulatorOnly
    }.Build();

    public sealed class HeaderAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "test-user"),
                new(ClaimTypes.Email, "test-user@example.invalid"),
                new("permiso", "Insumos.Crear"),
                new("permiso", "Insumos.Modificar"),
                new("permiso", "Insumos.Consultar")
            };
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }

    [Fact]
    public void InsumoService_NoExponeGanchoGlobalEstatico()
    {
        var propiedadEstatica = typeof(InsumoService).GetProperty(
            "InterceptorAuditoriaGlobal",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.Null(propiedadEstatica);
    }

    [LocalFirestoreFact]
    public async Task InsumoService_FalloSimuladoEnAuditoriaAltaHaceRollbackTotal()
    {
        var db = CrearDb();
        var insumos = new InsumoService(db);
        insumos.InterceptorAuditoria = log =>
        {
            if (log.Action == "Creacion de insumo")
                throw new InvalidOperationException("Fallo forzado al auditar creacion de insumo");
        };

        var insumo = new Insumo
        {
            Nombre = "Shampoo Neutro",
            Tipo = "Quimico",
            UnidadMedida = "ml",
            StockMinimo = 5,
            CantidadReabastecimiento = 20
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            insumos.GuardarConAuditoria(insumo, true, "actor-uid", "actor@example.invalid"));

        Assert.Empty((await db.Collection("insumos").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task InsumoService_FalloSimuladoEnAuditoriaEdicionHaceRollbackTotal()
    {
        var db = CrearDb();
        var insumos = new InsumoService(db);

        var insumo = new Insumo
        {
            Id = "ins-original",
            Nombre = "Shampoo Neutro",
            Tipo = "Quimico",
            UnidadMedida = "ml",
            StockActual = 10,
            StockMinimo = 5,
            CantidadReabastecimiento = 20,
            Estado = "Activo"
        };
        await db.Collection("insumos").Document(insumo.Id).SetAsync(insumo);

        insumos.InterceptorAuditoria = log =>
        {
            if (log.Action == "Actualizacion de insumo")
                throw new InvalidOperationException("Fallo forzado al auditar edicion de insumo");
        };

        var edicion = new Insumo
        {
            Id = insumo.Id,
            Nombre = "Shampoo Modificado",
            Tipo = "Quimico",
            UnidadMedida = "ml",
            StockMinimo = 15,
            CantidadReabastecimiento = 30
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            insumos.GuardarConAuditoria(edicion, false, "actor-uid", "actor@example.invalid"));

        var persistido = (await db.Collection("insumos").Document(insumo.Id).GetSnapshotAsync()).ConvertTo<Insumo>();
        Assert.Equal("Shampoo Neutro", persistido.Nombre);
        Assert.Equal(5, persistido.StockMinimo);
        Assert.Equal(20, persistido.CantidadReabastecimiento);
        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task InsumoService_FalloSimuladoEnAuditoriaCambioEstadoHaceRollbackTotal()
    {
        var db = CrearDb();
        var insumos = new InsumoService(db);

        var insumo = new Insumo
        {
            Id = "ins-estado",
            Nombre = "Cera Liquida",
            Tipo = "Quimico",
            UnidadMedida = "ml",
            Estado = "Activo"
        };
        await db.Collection("insumos").Document(insumo.Id).SetAsync(insumo);

        insumos.InterceptorAuditoria = log =>
        {
            if (log.Action == "Desactivacion de insumo")
                throw new InvalidOperationException("Fallo forzado al auditar desactivacion");
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            insumos.CambiarEstadoConAuditoria(insumo.Id, "Inactivo", "actor-uid", "actor@example.invalid"));

        var persistido = (await db.Collection("insumos").Document(insumo.Id).GetSnapshotAsync()).ConvertTo<Insumo>();
        Assert.Equal("Activo", persistido.Estado);
        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task InsumoService_EdicionNoOpNoMutaDocumentoNiGeneraAuditoria()
    {
        var db = CrearDb();
        var insumos = new InsumoService(db);

        var insumo = new Insumo
        {
            Id = "ins-noop",
            Nombre = "Shampoo Neutro",
            Tipo = "Quimico",
            UnidadMedida = "ml",
            StockActual = 10,
            StockMinimo = 5,
            CantidadReabastecimiento = 20,
            ReabastecimientoAutomatico = false,
            ProveedoresIds = new(),
            Estado = "Activo"
        };
        await db.Collection("insumos").Document(insumo.Id).SetAsync(insumo);

        var edicionIdentica = new Insumo
        {
            Id = insumo.Id,
            Nombre = "Shampoo Neutro",
            Tipo = "Quimico",
            UnidadMedida = "ml",
            StockMinimo = 5,
            CantidadReabastecimiento = 20,
            ReabastecimientoAutomatico = false,
            ProveedoresIds = new()
        };

        var resultado = await insumos.GuardarConAuditoria(edicionIdentica, false, "actor-uid", "actor@example.invalid");
        Assert.True(resultado.Resultado.EsExitoso);

        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task InsumoService_CambioEstadoNoOpNoMutaDocumentoNiGeneraAuditoria()
    {
        var db = CrearDb();
        var insumos = new InsumoService(db);

        var insumo = new Insumo
        {
            Id = "ins-estado-noop",
            Nombre = "Cera Liquida",
            Tipo = "Quimico",
            UnidadMedida = "ml",
            Estado = "Activo"
        };
        await db.Collection("insumos").Document(insumo.Id).SetAsync(insumo);

        var resultado = await insumos.CambiarEstadoConAuditoria(insumo.Id, "Activo", "actor-uid", "actor@example.invalid");
        Assert.True(resultado.Resultado.EsExitoso);

        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task InsumoService_RechazoValidacionNoMutaNiGeneraAuditoria()
    {
        var db = CrearDb();
        var insumos = new InsumoService(db);

        var insumoExistente = new Insumo
        {
            Id = "ins-existente",
            Nombre = "Jabon Liquido",
            Tipo = "Quimico",
            UnidadMedida = "ml",
            Estado = "Activo"
        };
        await db.Collection("insumos").Document(insumoExistente.Id).SetAsync(insumoExistente);

        // Nombre duplicado en alta
        var resNombreDuplicado = await insumos.GuardarConAuditoria(new Insumo
        {
            Nombre = "jabon liquido",
            Tipo = "Quimico",
            UnidadMedida = "ml"
        }, true, "actor-uid", "actor@example.invalid");
        Assert.False(resNombreDuplicado.Resultado.EsExitoso);

        // Niveles negativos
        var resNivelNegativo = await insumos.GuardarConAuditoria(new Insumo
        {
            Nombre = "Otro Insumo",
            Tipo = "Quimico",
            UnidadMedida = "ml",
            StockMinimo = -1
        }, true, "actor-uid", "actor@example.invalid");
        Assert.False(resNivelNegativo.Resultado.EsExitoso);

        // Reabastecimiento sin proveedor
        var resSinProveedor = await insumos.GuardarConAuditoria(new Insumo
        {
            Nombre = "Insumo Con Toggle",
            Tipo = "Quimico",
            UnidadMedida = "ml",
            ReabastecimientoAutomatico = true,
            ProveedoresIds = new()
        }, true, "actor-uid", "actor@example.invalid");
        Assert.False(resSinProveedor.Resultado.EsExitoso);

        // Modificación de tipo o unidad
        var resModInmutable = await insumos.GuardarConAuditoria(new Insumo
        {
            Id = insumoExistente.Id,
            Nombre = "Jabon Liquido",
            Tipo = "Fisico",
            UnidadMedida = "unidad"
        }, false, "actor-uid", "actor@example.invalid");
        Assert.False(resModInmutable.Resultado.EsExitoso);

        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task InsumoService_ConcurrenciaDesactivacionYEdicionRespetaConsistencia()
    {
        var db = CrearDb();
        var insumos = new InsumoService(db);

        var insumo = new Insumo
        {
            Id = "ins-carrera",
            Nombre = "Insumo Carrera",
            Tipo = "Quimico",
            UnidadMedida = "ml",
            StockMinimo = 5,
            CantidadReabastecimiento = 20,
            Estado = "Activo"
        };
        await db.Collection("insumos").Document(insumo.Id).SetAsync(insumo);

        var tarea1 = insumos.GuardarConAuditoria(new Insumo
        {
            Id = insumo.Id,
            Nombre = "Insumo Carrera Editado",
            Tipo = "Quimico",
            UnidadMedida = "ml",
            StockMinimo = 10,
            CantidadReabastecimiento = 30
        }, false, "u1", "u1@example.invalid");

        var tarea2 = insumos.CambiarEstadoConAuditoria(insumo.Id, "Inactivo", "u2", "u2@example.invalid");

        await Task.WhenAll(tarea1, tarea2);

        var persistido = (await db.Collection("insumos").Document(insumo.Id).GetSnapshotAsync()).ConvertTo<Insumo>();
        Assert.Equal("Inactivo", persistido.Estado);

        var logs = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents
            .Select(d => d.ConvertTo<AuditLog>())
            .ToList();

        Assert.NotEmpty(logs);
        Assert.All(logs, l => Assert.Equal("Insumo", l.TargetType));
        Assert.All(logs, l => Assert.Equal(insumo.Id, l.TargetId));
    }

    [LocalFirestoreFact]
    public async Task InsumosController_GuardarYCambiarEstadoHttpPersistenAuditoriaTransaccionalConClaimsSinDuplicar()
    {
        var db = CrearDb();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(InsumosController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, HeaderAuth>("Test", _ => { });
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

        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();

        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() })
            {
                BaseAddress = new Uri(address)
            };

            var pageResp = await client.GetAsync("/Insumos/Index");
            Assert.Equal(HttpStatusCode.OK, pageResp.StatusCode);
            var token = Regex.Match(await pageResp.Content.ReadAsStringAsync(), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.NotEmpty(token);

            // 1. Alta vía HTTP
            var altaData = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["crear"] = "true",
                ["Nombre"] = "Shampoo Concentrado",
                ["Tipo"] = "Quimico",
                ["UnidadMedida"] = "ml",
                ["StockMinimo"] = "5",
                ["CantidadReabastecimiento"] = "20"
            });
            var altaResp = await client.PostAsync("/Insumos/Guardar", altaData);
            Assert.Equal(HttpStatusCode.Redirect, altaResp.StatusCode);

            var insumosList = (await db.Collection("insumos").GetSnapshotAsync()).Documents;
            var insumoDoc = Assert.Single(insumosList);
            var insumoId = insumoDoc.Id;

            // Verificar auditoría de alta en Firestore
            var auditAlta = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents
                .Select(d => d.ConvertTo<AuditLog>())
                .ToList();
            var logAlta = Assert.Single(auditAlta);
            Assert.Equal("Creacion de insumo", logAlta.Action);
            Assert.Equal(insumoId, logAlta.TargetId);
            Assert.Equal("Insumo", logAlta.TargetType);
            Assert.Equal("test-user", logAlta.UserId);
            Assert.Equal("test-user@example.invalid", logAlta.UserEmail);
            Assert.Empty(logAlta.Antes ?? new());
            Assert.Equal("Shampoo Concentrado", logAlta.Despues?["Nombre"]);

            // 2. Edición vía HTTP
            var edicionData = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["crear"] = "false",
                ["Id"] = insumoId,
                ["Nombre"] = "Shampoo Ultra Concentrado",
                ["Tipo"] = "Quimico",
                ["UnidadMedida"] = "ml",
                ["StockMinimo"] = "10",
                ["CantidadReabastecimiento"] = "30"
            });
            var edicionResp = await client.PostAsync("/Insumos/Guardar", edicionData);
            Assert.Equal(HttpStatusCode.Redirect, edicionResp.StatusCode);

            // Verificar que hay exactamente 2 registros de auditoría (alta + edición, sin duplicado!)
            var auditEdicion = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents
                .Select(d => d.ConvertTo<AuditLog>())
                .OrderBy(l => l.Timestamp)
                .ToList();
            Assert.Equal(2, auditEdicion.Count);
            var logEdicion = auditEdicion[1];
            Assert.Equal("Actualizacion de insumo", logEdicion.Action);
            Assert.Equal(insumoId, logEdicion.TargetId);
            Assert.Equal("test-user", logEdicion.UserId);
            Assert.Equal("Shampoo Concentrado", logEdicion.Antes?["Nombre"]);
            Assert.Equal("Shampoo Ultra Concentrado", logEdicion.Despues?["Nombre"]);

            // 3. Cambio de estado vía HTTP
            var estadoData = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = insumoId,
                ["estado"] = "Inactivo"
            });
            var estadoResp = await client.PostAsync("/Insumos/CambiarEstado", estadoData);
            Assert.Equal(HttpStatusCode.Redirect, estadoResp.StatusCode);

            var auditEstado = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents
                .Select(d => d.ConvertTo<AuditLog>())
                .OrderBy(l => l.Timestamp)
                .ToList();
            Assert.Equal(3, auditEstado.Count);
            var logEstado = auditEstado[2];
            Assert.Equal("Desactivacion de insumo", logEstado.Action);
            Assert.Equal("Activo", logEstado.Antes?["Estado"]);
            Assert.Equal("Inactivo", logEstado.Despues?["Estado"]);
        }
        finally
        {
            await app.StopAsync();
        }
    }
}

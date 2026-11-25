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

public sealed class ConfiguracionStockIntegrationTests
{
    public sealed class ConfigAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "test"), new Claim(ClaimTypes.Email, "test@example.invalid"), new Claim("permiso", "Insumos.Consultar"), new Claim("permiso", "Configuracion.Consultar"), new Claim("permiso", "Configuracion.Modificar") }, Scheme.Name)), Scheme.Name)));
    }

    [LocalFirestoreFact]
    public async Task GuardadoYUmbralesHttpPreservanConfiguracion()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-config-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var doc = db.Collection("configuracion").Document("system_config");
        var horarios = new Dictionary<string, string> { ["Lunes"] = "08:00-17:00", ["Martes"] = "09:00-18:00", ["Miércoles"] = "CERRADO", ["Jueves"] = "10:00-19:00", ["Viernes"] = "08:00-12:00,14:00-18:00", ["Sábado"] = "09:00-13:00", ["Domingo"] = "CERRADO" };
        var original = new Dictionary<string, object> {
            ["Id"] = "system_config", ["NombreLavadero"] = "Lavadero previo", ["Ubicacion"] = "Dirección previa",
            ["CancelacionAnticipadaDescuento"] = 17.0, ["CancelacionAnticipadaHorasMinimas"] = 48, ["CancelacionAnticipadaValidezDias"] = 45,
            ["PaquetesDescuentoStep"] = 10, ["HorariosOperacion"] = horarios,
            ["CapacidadMaximaConcurrente"] = 9, ["ConsiderarEmpleadosActivos"] = false, ["EmpleadosMaximosPorLavado"] = 7,
            ["TiempoNotificacionMinutos"] = 37, ["TiempoToleranciaMinutos"] = 42, ["IntervaloPreguntas"] = 11,
            ["SesionDuracionHoras"] = 12, ["SesionInactividadMinutos"] = 35, ["CampoHistorico"] = "conservar"
        };
        await doc.SetAsync(original);
        var insumos = new InsumoService(db);
        foreach (var (name, amount, minimum) in new[] { ("Igual", 10.0, 10m), ("Menor", 9.0, 10m), ("Umbral", 25.0, 10m), ("Fuera", 25.01, 10m), ("MinimoCero", 0.0, 0m) })
        {
            var item = new Insumo { Nombre = name, StockMinimo = minimum };
            Assert.True((await insumos.Guardar(item, true)).EsExitoso);
            await db.Collection("insumos").Document(item.Id).UpdateAsync("StockActual", amount);
        }
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(InsumosController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, ConfigAuth>("Test", _ => { });
        builder.Services.AddAuthorization(); builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db); builder.Services.AddMemoryCache(); builder.Services.AddScoped<ConfiguracionService>();
        builder.Services.AddScoped<InsumoService>(); builder.Services.AddScoped<ProveedorService>(); builder.Services.AddScoped<StockService>(); builder.Services.AddScoped<ComprasService>();
        builder.Services.AddSingleton(Mock.Of<IAuditWriter>()); builder.Services.AddSingleton(Mock.Of<IEmailSender>());
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"); await app.StartAsync();
        try
        {
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            var page = WebUtility.HtmlDecode(await client.GetStringAsync("/Configuracion/Index"));
            Assert.DoesNotContain("Empleados Máximos por Lavado", page);
            Assert.DoesNotContain("Considerar Empleados Activos", page);
            Assert.DoesNotContain("name=\"EmpleadosMaximosPorLavado\"", page);
            Assert.DoesNotContain("name=\"ConsiderarEmpleadosActivos\"", page);
            Assert.Matches("name=\"PorcentajeAlertaStock\"[^>]*value=\"20\"", page);
            Assert.Matches("name=\"VigenciaCompraRecienteDias\"[^>]*value=\"14\"", page);
            var token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.NotEmpty(token);
            async Task<HttpResponseMessage> Save(string value, string dias = "21")
            {
                var form = new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["PorcentajeAlertaStock"] = value, ["Id"] = "system_config", ["NombreLavadero"] = "Lavadero previo", ["Ubicacion"] = "Dirección previa", ["PaquetesDescuentoStep"] = "10", ["CancelacionAnticipadaDescuento"] = "17", ["CancelacionAnticipadaHorasMinimas"] = "48", ["CancelacionAnticipadaValidezDias"] = "45", ["CapacidadMaximaConcurrente"] = "9", ["SesionDuracionHoras"] = "12", ["SesionInactividadMinutos"] = "35" };
                form["VigenciaCompraRecienteDias"] = dias;
                foreach (var (day, horario) in horarios)
                    form[$"HorariosOperacion[{day}]"] = horario;
                return await client.PostAsync("/Configuracion/Actualizar", new FormUrlEncodedContent(form));
            }
            // La lectura GET llena la caché; otra sesión cambia los campos antes del POST.
            await doc.UpdateAsync(new Dictionary<string, object> { ["ConsiderarEmpleadosActivos"] = true, ["EmpleadosMaximosPorLavado"] = 11 });
            original["ConsiderarEmpleadosActivos"] = true;
            original["EmpleadosMaximosPorLavado"] = 11;
            var saved = await Save("150");
            Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
            var audits = (await db.Collection("registros_auditoria").WhereEqualTo("TargetType", "Configuracion").GetSnapshotAsync()).Documents;
            var auditDoc = Assert.Single(audits);
            var audit = auditDoc.ConvertTo<AuditLog>();
            Assert.Equal("test", audit.UserId);
            Assert.Equal("test@example.invalid", audit.UserEmail);
            Assert.Equal("Actualización de configuración del sistema", audit.Action);
            Assert.Equal("system_config", audit.TargetId);
            Assert.Equal("Configuracion", audit.TargetType);
            Assert.Equal("Lavadero previo", audit.NombreHistorico);
            Assert.NotNull(audit.Antes);
            Assert.NotNull(audit.Despues);
            Assert.Equal("20", audit.Antes["PorcentajeAlertaStock"]);
            Assert.Equal("150", audit.Despues["PorcentajeAlertaStock"]);
            Assert.Equal("14", audit.Antes["VigenciaCompraRecienteDias"]);
            Assert.Equal("21", audit.Despues["VigenciaCompraRecienteDias"]);

            var noOpSaved = await Save("150");
            Assert.Equal(HttpStatusCode.Redirect, noOpSaved.StatusCode);
            var auditsAfterNoOp = (await db.Collection("registros_auditoria").WhereEqualTo("TargetType", "Configuracion").GetSnapshotAsync()).Documents;
            Assert.Single(auditsAfterNoOp);

            var persisted = (await doc.GetSnapshotAsync()).ToDictionary();
            foreach (var (key, value) in original.Where(pair => pair.Key != "HorariosOperacion"))
                Assert.Equal(value is int number ? (object)(long)number : value, persisted[key]);
            var persistedHours = Assert.IsAssignableFrom<IDictionary<string, object>>(persisted["HorariosOperacion"]);
            foreach (var (day, horario) in horarios) Assert.Equal(horario, persistedHours[day]);
            Assert.Equal(150.0, persisted["PorcentajeAlertaStock"]);
            Assert.Equal  (21L, persisted["VigenciaCompraRecienteDias"]);
            var savedPage = WebUtility.HtmlDecode(await client.GetStringAsync("/Configuracion/Index"));
            Assert.Matches("name=\"PorcentajeAlertaStock\"[^>]*value=\"150\"", savedPage);
            Assert.Contains("Dirección previa", savedPage); Assert.Contains("08:00-17:00", savedPage);
            async Task<string> Row(string name) { var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Index?search=" + name)); return html; }
            Assert.Contains("Crítico", await Row("Igual")); Assert.Contains("Crítico", await Row("Menor")); Assert.Contains("Crítico", await Row("MinimoCero"));
            Assert.Contains("Próximo al mínimo", await Row("Umbral")); Assert.DoesNotContain("Próximo al mínimo", await Row("Fuera"));
            Assert.Matches("name=\"VigenciaCompraRecienteDias\"[^>]*value=\"21\"", savedPage);
            var beforeInvalidDias = await doc.GetSnapshotAsync();
            var invalidDias = await Save("150", "-1");
            Assert.Equal(HttpStatusCode.OK, invalidDias.StatusCode);
            Assert.Contains("no negativo", WebUtility.HtmlDecode(await invalidDias.Content.ReadAsStringAsync()));
            Assert.Equal(beforeInvalidDias.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(HttpStatusCode.Redirect, (await Save("150", "0")).StatusCode);
            Assert.Equal(0L, (await doc.GetSnapshotAsync()).ToDictionary()["VigenciaCompraRecienteDias"]);
            var beforeInvalid = await doc.GetSnapshotAsync();
            var invalid = await Save("-1"); Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
            Assert.Contains("no negativo", WebUtility.HtmlDecode(await invalid.Content.ReadAsStringAsync()));
            var afterInvalid = await doc.GetSnapshotAsync();
            Assert.Equal(beforeInvalid.UpdateTime, afterInvalid.UpdateTime);
            Assert.Equal(persisted["PorcentajeAlertaStock"], afterInvalid.ToDictionary()["PorcentajeAlertaStock"]);
            Assert.Equal(HttpStatusCode.Redirect, (await Save("0")).StatusCode);
            Assert.DoesNotContain("Próximo al mínimo", await Row("Umbral"));
        }
        finally { await app.StopAsync(); }
    }

    [LocalFirestoreFact]
    public async Task FalloSimuladoDeAuditoriaEnConfiguracionHaceRollbackYSinEvento()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-cfg-roll-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var service = new ConfiguracionService(db, cache);
        var initial = await service.ObtenerConfiguracion();
        var originalName = initial.NombreLavadero;

        service.InterceptorAuditoria = log =>
        {
            if (log.Action == "Actualización de configuración del sistema")
                throw new InvalidOperationException("Fallo simulado en registro de auditoria.");
        };

        var updated = new Configuracion
        {
            NombreLavadero = "Lavadero Modificado Fallo",
            PorcentajeAlertaStock = 88m,
            VigenciaCompraRecienteDias = 30,
            HorariosOperacion = initial.HorariosOperacion
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ActualizarConfiguracion(updated, "actor@example.invalid", "actor-id"));

        var current = await service.ObtenerConfiguracion();
        Assert.Equal(originalName, current.NombreLavadero);
        Assert.Equal(20m, current.PorcentajeAlertaStock);

        var audits = await db.Collection("registros_auditoria").GetSnapshotAsync();
        Assert.Empty(audits.Documents);

        service.InterceptorAuditoria = null;
        await service.ActualizarConfiguracion(updated, "actor@example.invalid", "actor-id");

        var persisted = await service.ObtenerConfiguracion();
        Assert.Equal("Lavadero Modificado Fallo", persisted.NombreLavadero);
        Assert.Equal(88m, persisted.PorcentajeAlertaStock);

        var auditsSuccess = await db.Collection("registros_auditoria").GetSnapshotAsync();
        var auditDoc = Assert.Single(auditsSuccess.Documents);
        var audit = auditDoc.ConvertTo<AuditLog>();
        Assert.Equal("actor-id", audit.UserId);
        Assert.Equal("actor@example.invalid", audit.UserEmail);
        Assert.Equal("Actualización de configuración del sistema", audit.Action);
        Assert.Equal("system_config", audit.TargetId);
        Assert.Equal("Configuracion", audit.TargetType);
        Assert.Equal("20", audit.Antes["PorcentajeAlertaStock"]);
        Assert.Equal("88", audit.Despues["PorcentajeAlertaStock"]);
        Assert.Equal("14", audit.Antes["VigenciaCompraRecienteDias"]);
        Assert.Equal("30", audit.Despues["VigenciaCompraRecienteDias"]);
    }

    [LocalFirestoreFact]
    public async Task AuditoriaHttpRegistraCambiosIndividualesPorCampoYNoOp()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-cfg-ind-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var doc = db.Collection("configuracion").Document("system_config");
        var initialConfig = new Dictionary<string, object>
        {
            ["Id"] = "system_config",
            ["NombreLavadero"] = "Lavadero Individual",
            ["Ubicacion"] = "Calle 123",
            ["CancelacionAnticipadaDescuento"] = 10.0,
            ["CancelacionAnticipadaHorasMinimas"] = 24,
            ["CancelacionAnticipadaValidezDias"] = 30,
            ["PaquetesDescuentoStep"] = 5,
            ["CapacidadMaximaConcurrente"] = 5,
            ["ConsiderarEmpleadosActivos"] = true,
            ["EmpleadosMaximosPorLavado"] = 3,
            ["TiempoNotificacionMinutos"] = 15,
            ["TiempoToleranciaMinutos"] = 10,
            ["IntervaloPreguntas"] = 5,
            ["SesionDuracionHoras"] = 8,
            ["SesionInactividadMinutos"] = 15,
            ["PorcentajeAlertaStock"] = 20.0,
            ["VigenciaCompraRecienteDias"] = 14L,
            ["HorariosOperacion"] = new Dictionary<string, string>
            {
                ["Lunes"] = "09:00-18:00", ["Martes"] = "09:00-18:00", ["Miércoles"] = "09:00-18:00",
                ["Jueves"] = "09:00-18:00", ["Viernes"] = "09:00-18:00", ["Sábado"] = "09:00-13:00",
                ["Domingo"] = "CERRADO"
            }
        };
        await doc.SetAsync(initialConfig);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(InsumosController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, ConfigAuth>("Test", _ => { });
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
            var serverAddress = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(serverAddress) };

            var page = WebUtility.HtmlDecode(await client.GetStringAsync("/Configuracion/Index"));
            var token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.NotEmpty(token);

            async Task<HttpResponseMessage> PostForm(string porcentaje, string vigencia)
            {
                var form = new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = token,
                    ["Id"] = "system_config",
                    ["NombreLavadero"] = "Lavadero Individual",
                    ["Ubicacion"] = "Calle 123",
                    ["CancelacionAnticipadaDescuento"] = "10",
                    ["CancelacionAnticipadaHorasMinimas"] = "24",
                    ["CancelacionAnticipadaValidezDias"] = "30",
                    ["PaquetesDescuentoStep"] = "5",
                    ["CapacidadMaximaConcurrente"] = "5",
                    ["ConsiderarEmpleadosActivos"] = "false",
                    ["EmpleadosMaximosPorLavado"] = "7",
                    ["SesionDuracionHoras"] = "8",
                    ["SesionInactividadMinutos"] = "15",
                    ["PorcentajeAlertaStock"] = porcentaje,
                    ["VigenciaCompraRecienteDias"] = vigencia,
                    ["HorariosOperacion[Lunes]"] = "09:00-18:00",
                    ["HorariosOperacion[Martes]"] = "09:00-18:00",
                    ["HorariosOperacion[Miércoles]"] = "09:00-18:00",
                    ["HorariosOperacion[Jueves]"] = "09:00-18:00",
                    ["HorariosOperacion[Viernes]"] = "09:00-18:00",
                    ["HorariosOperacion[Sábado]"] = "09:00-13:00",
                    ["HorariosOperacion[Domingo]"] = "CERRADO"
                };
                return await client.PostAsync("/Configuracion/Actualizar", new FormUrlEncodedContent(form));
            }

            // 1. Cambio solo de PorcentajeAlertaStock (20 -> 35), VigenciaCompraRecienteDias se mantiene en 14
            var resp1 = await PostForm("35", "14");
            Assert.Equal(HttpStatusCode.Redirect, resp1.StatusCode);
            var legacySaved = (await doc.GetSnapshotAsync()).ToDictionary();
            Assert.Equal(false, legacySaved["ConsiderarEmpleadosActivos"]);
            Assert.Equal  (7L, legacySaved["EmpleadosMaximosPorLavado"]);

            var auditsQuery = db.Collection("registros_auditoria").WhereEqualTo("TargetType", "Configuracion");
            var audits1 = (await auditsQuery.GetSnapshotAsync()).Documents;
            var docAudit1 = Assert.Single(audits1);
            var log1 = docAudit1.ConvertTo<AuditLog>();
            Assert.NotNull(log1.Antes);
            Assert.NotNull(log1.Despues);
            Assert.Equal("20", log1.Antes["PorcentajeAlertaStock"]);
            Assert.Equal("35", log1.Despues["PorcentajeAlertaStock"]);
            Assert.Equal("14", log1.Antes["VigenciaCompraRecienteDias"]);
            Assert.Equal("14", log1.Despues["VigenciaCompraRecienteDias"]);

            // 2. No-op: enviar los mismos valores (35 y 14) -> no debe crear auditoría nueva
            var respNoOp1 = await PostForm("35", "14");
            Assert.Equal(HttpStatusCode.Redirect, respNoOp1.StatusCode);
            var auditsNoOp1 = (await auditsQuery.GetSnapshotAsync()).Documents;
            Assert.Single(auditsNoOp1);

            // 3. Cambio solo de VigenciaCompraRecienteDias (14 -> 28), PorcentajeAlertaStock se mantiene en 35
            var resp2 = await PostForm("35", "28");
            Assert.Equal(HttpStatusCode.Redirect, resp2.StatusCode);
            var audits2 = (await auditsQuery.OrderBy("Timestamp").GetSnapshotAsync()).Documents;
            Assert.Equal(2, audits2.Count);

            var log2 = audits2[1].ConvertTo<AuditLog>();
            Assert.NotNull(log2.Antes);
            Assert.NotNull(log2.Despues);
            Assert.Equal("35", log2.Antes["PorcentajeAlertaStock"]);
            Assert.Equal("35", log2.Despues["PorcentajeAlertaStock"]);
            Assert.Equal("14", log2.Antes["VigenciaCompraRecienteDias"]);
            Assert.Equal("28", log2.Despues["VigenciaCompraRecienteDias"]);

            // 4. No-op: enviar los mismos valores (35 y 28) -> no debe crear auditoría nueva
            var respNoOp2 = await PostForm("35", "28");
            Assert.Equal(HttpStatusCode.Redirect, respNoOp2.StatusCode);
            var auditsNoOp2 = (await auditsQuery.GetSnapshotAsync()).Documents;
            Assert.Equal(2, auditsNoOp2.Count);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [LocalFirestoreFact]
    public async Task InventarioMuestraAlertaEnUmbralHistorico()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-stock-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("configuracion").Document("system_config").SetAsync(new Dictionary<string, object> { ["NombreLavadero"] = "Lavadero histórico" });
        var items = new InsumoService(db);
        var item = new Insumo { Nombre = "Alerta histórica", Tipo = "Quimico", UnidadMedida = "ml", StockMinimo = 10 };
        Assert.True((await items.Guardar(item, true)).EsExitoso);
        await db.Collection("insumos").Document(item.Id).UpdateAsync("StockActual", 12.0);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(InsumosController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, InsumoCatalogIntegrationTests.Auth>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db); builder.Services.AddMemoryCache(); builder.Services.AddScoped<ConfiguracionService>();
        builder.Services.AddScoped<InsumoService>(); builder.Services.AddScoped<ProveedorService>(); builder.Services.AddScoped<StockService>(); builder.Services.AddScoped<ComprasService>();
        builder.Services.AddSingleton(Mock.Of<IAuditWriter>()); builder.Services.AddSingleton(Mock.Of<IEmailSender>());
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization(); app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            var response = await client.GetAsync("/Insumos/Index");
            var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Alerta histórica", html);
            Assert.Contains("Próximo al mínimo", html);
        }
        finally { await app.StopAsync(); }
    }
}

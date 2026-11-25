using Firebase.Models;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Caching.Memory;
using Firebase.Services;
using Firebase.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Text.RegularExpressions;

namespace Firebase.Tests.Integration;

public class ConfiguracionServiceIntegrationTests
{
    private static FirestoreDb Database() => new FirestoreDbBuilder
    {
        ProjectId = "demo-grid-" + Guid.NewGuid().ToString("N")[..16],
        EmulatorDetection = EmulatorDetection.EmulatorOnly
    }.Build();

    [LocalFirestoreFact]
    public async Task FormularioAnteriorPreservaIntervaloActualYCampos()
    {
        var db = Database();
        var doc = db.Collection("configuracion").Document("system_config");
        var initial = new Configuracion { Id = "system_config", NombreLavadero = "Lavadero anterior", Ubicacion = "Calle anterior", IntervaloInicioTurnosMinutos = 20, CapacidadMaximaConcurrente = 9, EmpleadosMaximosPorLavado = 7, ConsiderarEmpleadosActivos = false };
        await doc.SetAsync(initial);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(ConfiguracionController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, ConfiguracionStockIntegrationTests.ConfigAuth>("Test", _ => { });
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
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            var page = WebUtility.HtmlDecode(await client.GetStringAsync("/Configuracion/Index"));
            var token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.NotEmpty(token);
            await doc.UpdateAsync(new Dictionary<string, object> { ["IntervaloInicioTurnosMinutos"] = 27, ["RespuestaOfertaAdelantoMinutos"] = 35, ["ToleranciaLlegadaTurnosMinutos"] = 42 });
            var form = new Dictionary<string, string> {
                ["__RequestVerificationToken"] = token, ["Id"] = "system_config", ["NombreLavadero"] = "Lavadero actualizado", ["Ubicacion"] = initial.Ubicacion,
                ["CancelacionAnticipadaDescuento"] = "0", ["CancelacionAnticipadaHorasMinimas"] = "24", ["CancelacionAnticipadaValidezDias"] = "30", ["PaquetesDescuentoStep"] = "5", ["CapacidadMaximaConcurrente"] = "9", ["SesionDuracionHoras"] = "8", ["SesionInactividadMinutos"] = "15", ["PorcentajeAlertaStock"] = "20", ["VigenciaCompraRecienteDias"] = "14"
            };
            foreach (var (day, hours) in initial.HorariosOperacion) form[$"HorariosOperacion[{day}]"] = hours;
            var beforeInvalid = await doc.GetSnapshotAsync();
            var audits = db.Collection("registros_auditoria");
            var auditCount = (await audits.GetSnapshotAsync()).Count;
            foreach (var invalid in new[] { "-1", "525601" })
            {
                form["ToleranciaLlegadaTurnosMinutos"] = invalid;
                var rejected = await client.PostAsync("/Configuracion/Actualizar", new FormUrlEncodedContent(form));
                Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
                var html = WebUtility.HtmlDecode(await rejected.Content.ReadAsStringAsync());
                var error = Regex.Match(html, "<span[^>]*data-valmsg-for=\"ToleranciaLlegadaTurnosMinutos\"[^>]*>(.*?)</span>", RegexOptions.Singleline);
                Assert.True(error.Success);
                Assert.Equal("La tolerancia de llegada debe estar entre 0 y 525600 minutos", error.Groups[1].Value.Trim());
                var afterInvalid = await doc.GetSnapshotAsync();
                Assert.Equal(beforeInvalid.UpdateTime, afterInvalid.UpdateTime);
                Assert.Equal(42, afterInvalid.ConvertTo<Configuracion>().ToleranciaLlegadaTurnosMinutos);
                Assert.Equal("Lavadero anterior", afterInvalid.ConvertTo<Configuracion>().NombreLavadero);
                Assert.Equal(auditCount, (await audits.GetSnapshotAsync()).Count);
            }
            form.Remove("ToleranciaLlegadaTurnosMinutos");
            var response = await client.PostAsync("/Configuracion/Actualizar", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Configuracion", response.Headers.Location?.OriginalString);
            var saved = (await doc.GetSnapshotAsync()).ConvertTo<Configuracion>();
            Assert.Equal(27, saved.IntervaloInicioTurnosMinutos);
            Assert.Equal(35, saved.RespuestaOfertaAdelantoMinutos);
            Assert.Equal(42, saved.ToleranciaLlegadaTurnosMinutos);
            Assert.Equal("Lavadero actualizado", saved.NombreLavadero);
            Assert.Equal(initial.Ubicacion, saved.Ubicacion);
            Assert.Equal(9, saved.CapacidadMaximaConcurrente);
            Assert.Equal(7, saved.EmpleadosMaximosPorLavado);
            Assert.False(saved.ConsiderarEmpleadosActivos);
            Assert.Equal(initial.HorariosOperacion, saved.HorariosOperacion);
            var audit = Assert.Single((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents).ConvertTo<AuditLog>();
            Assert.Equal("27", audit.Antes["IntervaloInicioTurnosMinutos"]);
            Assert.Equal("27", audit.Despues["IntervaloInicioTurnosMinutos"]);
            Assert.Equal("35", audit.Antes["RespuestaOfertaAdelantoMinutos"]);
            Assert.Equal("35", audit.Despues["RespuestaOfertaAdelantoMinutos"]);
            Assert.Equal("42", audit.Antes["ToleranciaLlegadaTurnosMinutos"]);
            Assert.Equal("42", audit.Despues["ToleranciaLlegadaTurnosMinutos"]);

            form["ToleranciaLlegadaTurnosMinutos"] = "";
            response = await client.PostAsync("/Configuracion/Actualizar", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Null((await doc.GetSnapshotAsync()).ConvertTo<Configuracion>().ToleranciaLlegadaTurnosMinutos);
            Assert.Equal(2, (await db.Collection("registros_auditoria").GetSnapshotAsync()).Count);
        }
        finally { await app.StopAsync(); }
    }

    [LocalFirestoreFact]
    public async Task OfertaPersisteAuditaNoOpYRechazaInvalidos()
    {
        var db = Database();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new ConfiguracionService(db, cache);
        var config = await service.ObtenerConfiguracion();
        Assert.Equal(10, config.RespuestaOfertaAdelantoMinutos);
        config.RespuestaOfertaAdelantoMinutos = 1234;
        await service.ActualizarConfiguracion(config, "actor", "id");
        var doc = db.Collection("configuracion").Document("system_config");
        var before = await doc.GetSnapshotAsync();
        Assert.Equal(1234, before.ConvertTo<Configuracion>().RespuestaOfertaAdelantoMinutos);
        var audits = db.Collection("registros_auditoria");
        var audit = Assert.Single((await audits.GetSnapshotAsync()).Documents).ConvertTo<AuditLog>();
        Assert.Equal("10", audit.Antes["RespuestaOfertaAdelantoMinutos"]);
        Assert.Equal("1234", audit.Despues["RespuestaOfertaAdelantoMinutos"]);
        await service.ActualizarConfiguracion(await service.ObtenerConfiguracion(), "actor", "id");
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        Assert.Single((await audits.GetSnapshotAsync()).Documents);
        foreach (var value in new[] { 0, -1 })
        {
            config.RespuestaOfertaAdelantoMinutos = value;
            var error = await Assert.ThrowsAsync<ArgumentException>(() => service.ActualizarConfiguracion(config, "actor", "id"));
            Assert.Equal("El tiempo de respuesta de oferta de adelanto debe ser positivo", error.Message);
            Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(1234, (await doc.GetSnapshotAsync()).ConvertTo<Configuracion>().RespuestaOfertaAdelantoMinutos);
            Assert.Single((await audits.GetSnapshotAsync()).Documents);
        }
    }

    [LocalFirestoreFact]
    public async Task IntervaloSoloPersisteAuditaYNoOp()
    {
        var db = Database();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new ConfiguracionService(db, cache);
        var config = await service.ObtenerConfiguracion();
        config.IntervaloInicioTurnosMinutos = 20;
        await service.ActualizarConfiguracion(config, "actor", "id");
        Assert.Equal(20, (await service.ObtenerConfiguracion()).IntervaloInicioTurnosMinutos);
        var audit = Assert.Single((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents).ConvertTo<AuditLog>();
        Assert.Equal("15", audit.Antes["IntervaloInicioTurnosMinutos"]);
        Assert.Equal("20", audit.Despues["IntervaloInicioTurnosMinutos"]);
        await service.ActualizarConfiguracion(await service.ObtenerConfiguracion(), "actor", "id");
        Assert.Single((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task OmitidoPreservaSnapshotActualNoCache()
    {
        var db = Database();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new ConfiguracionService(db, cache);
        var stale = await service.ObtenerConfiguracion();
        await db.Collection("configuracion").Document("system_config").UpdateAsync("IntervaloInicioTurnosMinutos", 27);
        stale.NombreLavadero = "Nombre actualizado";
        await service.ActualizarConfiguracion(stale, "actor", "id", false, false, true);
        Assert.Equal(27, (await service.ObtenerConfiguracion()).IntervaloInicioTurnosMinutos);
        var audit = Assert.Single((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents).ConvertTo<AuditLog>();
        Assert.Equal("27", audit.Antes["IntervaloInicioTurnosMinutos"]);
        Assert.Equal("27", audit.Despues["IntervaloInicioTurnosMinutos"]);
        Assert.Equal("Nombre actualizado", (await service.ObtenerConfiguracion()).NombreLavadero);
    }

    [LocalFirestoreFact]
    public async Task DocumentoLegadoUsaQuince()
    {
        var db = Database();
        await db.Collection("configuracion").Document("system_config").SetAsync(new Dictionary<string, object> { ["NombreLavadero"] = "Legado" });
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new ConfiguracionService(db, cache);
        Assert.Equal(15, (await service.ObtenerConfiguracion()).IntervaloInicioTurnosMinutos);
        Assert.Equal(10, (await service.ObtenerConfiguracion()).RespuestaOfertaAdelantoMinutos);
        Assert.Equal(5, (await service.ObtenerConfiguracion()).ToleranciaLlegadaTurnosMinutos);
    }

    [LocalFirestoreFact]
    public async Task ToleranciaPersisteLimitesYRechazaSinEscrituras()
    {
        var db = Database();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new ConfiguracionService(db, cache);
        var config = await service.ObtenerConfiguracion();
        var doc = db.Collection("configuracion").Document("system_config");
        var audits = db.Collection("registros_auditoria");
        Assert.Equal(5, config.ToleranciaLlegadaTurnosMinutos);
        foreach (var value in new[] { 5, 525600 })
        {
            config.ToleranciaLlegadaTurnosMinutos = value;
            await service.ActualizarConfiguracion(config, "actor", "id");
            Assert.Equal(value, (await doc.GetSnapshotAsync()).ConvertTo<Configuracion>().ToleranciaLlegadaTurnosMinutos);
            Assert.Equal(value, (await service.ObtenerConfiguracion()).ToleranciaLlegadaTurnosMinutos);
        }
        var before = await doc.GetSnapshotAsync();
        var count = (await audits.GetSnapshotAsync()).Count;
        Assert.Equal(2, count);
        foreach (var value in new[] { -1, 0, 4, 525601 })
        {
            config.ToleranciaLlegadaTurnosMinutos = value;
            var error = await Assert.ThrowsAsync<ArgumentException>(() => service.ActualizarConfiguracion(config, "actor", "id"));
            Assert.Equal("La tolerancia de llegada debe estar entre 5 y 525600 minutos", error.Message);
            var after = await doc.GetSnapshotAsync();
            Assert.Equal(before.UpdateTime, after.UpdateTime);
            Assert.Equal(525600, after.ConvertTo<Configuracion>().ToleranciaLlegadaTurnosMinutos);
            Assert.Equal(count, (await audits.GetSnapshotAsync()).Count);
        }
    }

    [LocalFirestoreFact]
    public async Task IntervaloNoPositivoRechazadoSinEscrituras()
    {
        var db = Database();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new ConfiguracionService(db, cache);
        var config = await service.ObtenerConfiguracion();
        var doc = db.Collection("configuracion").Document("system_config");
        var before = await doc.GetSnapshotAsync();
        var audits = db.Collection("registros_auditoria");
        var count = (await audits.GetSnapshotAsync()).Count;
        foreach (var value in new[] { 0, -1 })
        {
            config.IntervaloInicioTurnosMinutos = value;
            var error = await Assert.ThrowsAsync<ArgumentException>(() => service.ActualizarConfiguracion(config, "actor", "id"));
            Assert.Equal("El intervalo de inicio de turnos debe ser positivo", error.Message);
            var after = await doc.GetSnapshotAsync();
            Assert.Equal(before.UpdateTime, after.UpdateTime);
            Assert.Equal(15, after.ConvertTo<Configuracion>().IntervaloInicioTurnosMinutos);
            Assert.Equal(count, (await audits.GetSnapshotAsync()).Count);
        }
    }
}

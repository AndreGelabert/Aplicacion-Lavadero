using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Firebase.Models;
using Firebase.Security;
using Firebase.Services;
using FirebaseLoginCustom.Controllers;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Firebase.Tests.Integration;

public class TurnoCuponWebIntegrationTests
{
    private sealed record Tokens(string Token, string Identity);

    [LocalFirestoreFact]
    public async Task StaffBookingChecksCouponOwnerAndReplaysCouponPackageWithoutWrites()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-web-coupon-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("empleados").Document("e").SetAsync(new { Estado = "Activo" });
        await db.Collection("clientes").Document("c").SetAsync(new { Id = "c" });
        await db.Collection("vehiculos").Document("v").SetAsync(new Vehiculo { Id = "v", ClienteId = "c", TipoVehiculo = "Auto", Patente = "ABC123", Marca = "a", Modelo = "b", Color = "c" });
        await db.Collection("tiposVehiculos").Document("t").SetAsync(new TipoVehiculo { Id = "t", Nombre = "Auto", FormatoPatente = "lllnnn", CantidadEmpleadosRequeridos = 1 });
        await db.Collection("servicios").Document("s").SetAsync(new Servicio { Id = "s", Nombre = "Lavado", Tipo = "Lavado", TipoVehiculo = "Auto", Descripcion = "x", Estado = "Activo", Precio = 100, TiempoEstimado = 30 });
        await db.Collection("paquetes_servicios").Document("p").SetAsync(new PaqueteServicio { Id = "p", Nombre = "Pack", Estado = "Activo", TipoVehiculo = "Auto", ServiciosIds = ["s"], PorcentajeDescuento = 10 });
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion { CapacidadMaximaConcurrente = 1, HorariosOperacion = new() { ["Martes"] = "09:00-18:00" } });
        var good = db.Collection("turno_cupones").Document("good");
        var wrong = db.Collection("turno_cupones").Document("wrong");
        await good.SetAsync(new { Owner = "c", Percentage = 20d, ExpiresUtc = DateTime.UtcNow.AddDays(1), Consumed = false });
        await wrong.SetAsync(new { Owner = "other", Percentage = 20d, ExpiresUtc = DateTime.UtcNow.AddDays(1), Consumed = false });
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(TurnosController).Assembly.GetName().Name, EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(System.Net.IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews();
        builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TurnoBookingHttpIntegrationTests.Auth>("Test", _ => { });
        builder.Services.AddAuthorization(); builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db); builder.Services.AddSingleton(Mock.Of<ITurnoConsultaService>());
        builder.Services.AddMemoryCache();
        builder.Services.AddScoped<ConfiguracionService>(); builder.Services.AddScoped<InsumoService>();
        builder.Services.AddSingleton<TurnoFormIdentity>(); builder.Services.AddScoped<TurnoService>(); builder.Services.AddScoped<TurnoWebBookingAdapter>();
        await using var app = builder.Build();
        app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
        app.MapGet("/test-form", (HttpContext context, IAntiforgery anti, TurnoFormIdentity identity) => Results.Json(new { Token = anti.GetAndStoreTokens(context).RequestToken, Identity = identity.Issue(context.User) })).RequireAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();
        try
        {
            var address = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
            using var client = new HttpClient(new HttpClientHandler { UseCookies = true }) { BaseAddress = address };
            client.DefaultRequestHeaders.Add("X-User", "staff"); client.DefaultRequestHeaders.Add("X-Permission", "Turnos.Crear");
            using var rendered = await client.GetAsync("/Turnos/Crear");
            var html = await rendered.Content.ReadAsStringAsync();
            Assert.True(rendered.StatusCode == HttpStatusCode.OK, $"Render returned {rendered.StatusCode}: {html}");
            var form = Regex.Match(html, "<form\\b[^>]*id=\"turno-form\"[^>]*>", RegexOptions.IgnoreCase);
            Assert.True(form.Success);
            var action = Regex.Match(form.Value, "\\baction=\"([^\"]+)\"", RegexOptions.IgnoreCase);
            Assert.True(action.Success);
            var actionPath = WebUtility.HtmlDecode(action.Groups[1].Value);
            Assert.Equal("/Turnos/Crear", actionPath);
            Assert.Matches("(?i)method=\"post\"", form.Value);
            string Hidden(string name)
            {
                var input = Regex.Match(html, "<input\\b[^>]*name=\"" + Regex.Escape(name) + "\"[^>]*>", RegexOptions.IgnoreCase);
                Assert.True(input.Success, name);
                var value = Regex.Match(input.Value, "\\bvalue=\"([^\"]*)\"", RegexOptions.IgnoreCase);
                Assert.True(value.Success, name);
                return WebUtility.HtmlDecode(value.Groups[1].Value);
            }
            Assert.Contains("name=\"CuponId\"", html);
            var values = new Dictionary<string, string> { ["__RequestVerificationToken"] = Hidden("__RequestVerificationToken"), ["FormIdentity"] = Hidden("FormIdentity"), ["ClienteId"] = "c", ["VehiculoId"] = "v", ["ClienteTrajoId"] = "c", ["ClienteRetiraId"] = "c", ["Fecha"] = "2030-01-01", ["Hora"] = "09:00", ["Servicios[0].ServicioId"] = "s", ["Servicios[0].PaqueteId"] = "p", ["CuponId"] = "wrong" };
            async Task<string> State() {
                var turns = await db.Collection("turnos").GetSnapshotAsync(); var markers = await db.Collection("turno_solicitudes").GetSnapshotAsync();
                var capacity = await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync(); var membership = await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync();
                var a = await good.GetSnapshotAsync(); var b = await wrong.GetSnapshotAsync();
                return string.Join("|", turns.Documents.Select(x => $"{x.Id}:{x.UpdateTime}")) + ";" + string.Join("|", markers.Documents.Select(x => $"{x.Id}:{x.UpdateTime}")) + $";{capacity.UpdateTime};{membership.UpdateTime};{a.UpdateTime};{b.UpdateTime}";
            }
            var empty = await State();
            using var anonymous = new HttpClient { BaseAddress = address };
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/Turnos/Crear", new FormUrlEncodedContent(values))).StatusCode); Assert.Equal(empty, await State());
            client.DefaultRequestHeaders.Remove("X-Permission"); client.DefaultRequestHeaders.Add("X-Permission", "Turnos.Consultar");
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/Turnos/Crear", new FormUrlEncodedContent(values))).StatusCode); Assert.Equal(empty, await State());
            client.DefaultRequestHeaders.Remove("X-Permission"); client.DefaultRequestHeaders.Add("X-Permission", "Turnos.Crear");
            var noCsrf = new Dictionary<string, string>(values); noCsrf.Remove("__RequestVerificationToken");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Turnos/Crear", new FormUrlEncodedContent(noCsrf))).StatusCode); Assert.Equal(empty, await State());
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/Turnos/Crear", new FormUrlEncodedContent(values))).StatusCode); Assert.Equal(empty, await State());
            values["CuponId"] = "good";
            var extra = new Dictionary<string, string>(values) { ["Descuento"] = "5" };
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Turnos/Crear", new FormUrlEncodedContent(extra))).StatusCode); Assert.Equal(empty, await State());
            var mismatched = new Dictionary<string, string>(values) { ["ClienteId"] = "other" };
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/Turnos/Crear", new FormUrlEncodedContent(mismatched))).StatusCode); Assert.Equal(empty, await State());
            using var multipart = new MultipartFormDataContent();
            foreach (var (key, value) in values) multipart.Add(new StringContent(value), key);
            using var submitted = await client.PostAsync(actionPath, multipart);
            Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
            var appointment = Assert.Single((await db.Collection("turnos").GetSnapshotAsync()).Documents).ConvertTo<Turno>();
            Assert.Equal("c", appointment.ClienteId); Assert.Equal("good", appointment.CuponId); Assert.Equal(72m, appointment.PrecioAcordado);
            Assert.True((await good.GetSnapshotAsync()).GetValue<bool>("Consumed"));
            var booked = await State(); Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/Turnos/Crear", new FormUrlEncodedContent(values))).StatusCode); Assert.Equal(booked, await State());
            var fresh = (await client.GetFromJsonAsync<Tokens>("/test-form"))!;
            var noCoupon = new Dictionary<string, string>(values) { ["__RequestVerificationToken"] = fresh.Token, ["FormIdentity"] = fresh.Identity, ["Hora"] = "10:00" };
            noCoupon.Remove("CuponId");
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/Turnos/Crear", new FormUrlEncodedContent(noCoupon))).StatusCode);
            var turns = (await db.Collection("turnos").GetSnapshotAsync()).Documents.Select(x => x.ConvertTo<Turno>()).ToArray();
            Assert.Equal(2, turns.Length); Assert.Equal(90m, turns.Single(x => x.CuponId == null).PrecioAcordado);
        }
        finally { await app.StopAsync(); }
    }
}

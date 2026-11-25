using System.Net;
using System.Net.Http.Json;
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

public class TurnoEditHttpIntegrationTests
{
    [LocalFirestoreFact]
    public async Task ScheduleEditIsAuthorizedTargetBoundAndIdempotent()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-http-edit-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("empleados").Document("e").SetAsync(new { Estado = "Activo" });
        await db.Collection("clientes").Document("c").SetAsync(new { Id = "c" });
        await db.Collection("vehiculos").Document("v").SetAsync(new Vehiculo { Id = "v", ClienteId = "c", TipoVehiculo = "Auto", Patente = "ABC123", Marca = "a", Modelo = "b", Color = "c" });
        await db.Collection("tiposVehiculos").Document("t").SetAsync(new TipoVehiculo { Id = "t", Nombre = "Auto", FormatoPatente = "lllnnn", CantidadEmpleadosRequeridos = 1 });
        await db.Collection("servicios").Document("s").SetAsync(new Servicio { Id = "s", Nombre = "Lavado", Tipo = "Lavado", TipoVehiculo = "Auto", Descripcion = "x", Estado = "Activo", Precio = 123, TiempoEstimado = 30 });
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion { ToleranciaLlegadaTurnosMinutos = 15, CapacidadMaximaConcurrente = 1, HorariosOperacion = new() { ["Martes"] = "09:00-18:00" } });
        var service = new TurnoService(db);
        var request = new TurnoService.SnapshotRequest("t", "c", "v", "Web", "create", new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc), false, [new("s", null)]);
        Assert.True(await service.Reservar(request));
        Assert.True(await service.Reservar(request with { Id = "occupied", ClaveIdempotencia = "occupied", InicioUtc = request.InicioUtc.AddHours(1) }));
        await db.Collection("turno_cupones").Document("coupon").SetAsync(new { Owner = "c", Percentage = 20d, ExpiresUtc = DateTime.UtcNow.AddDays(1), Consumed = false });
        Assert.True(await service.Reservar(request with { Id = "coupon-turn", ClaveIdempotencia = "coupon-create", InicioUtc = request.InicioUtc.AddHours(2), CuponId = "coupon" }));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(TurnosController).Assembly.GetName().Name, EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews();
        builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, EditAuth>("Test", _ => { });
        builder.Services.AddAuthorization(); builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db); builder.Services.AddSingleton(Mock.Of<ITurnoConsultaService>()); builder.Services.AddScoped<TurnoService>();
        await using var app = builder.Build();
        app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
        app.MapGet("/csrf", (HttpContext c, IAntiforgery anti) => anti.GetAndStoreTokens(c).RequestToken!).RequireAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"); await app.StartAsync();
        try
        {
            var address = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
            using var client = new HttpClient(new HttpClientHandler { UseCookies = true }) { BaseAddress = address };
            using var anonymous = new HttpClient { BaseAddress = address };
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/Turnos/EditarToken?id=t")).StatusCode);
            client.DefaultRequestHeaders.Add("X-User", "operator"); client.DefaultRequestHeaders.Add("X-Permission", "Turnos.Consultar");
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/Turnos/EditarToken?id=t")).StatusCode);
            client.DefaultRequestHeaders.Remove("X-Permission"); client.DefaultRequestHeaders.Add("X-Permission", "Turnos.Consultar,Turnos.Modificar");
            var issued = await client.GetFromJsonAsync<Token>("/Turnos/EditarToken?id=t");
            var values = new Dictionary<string, string> { ["Id"] = "t", ["FormIdentity"] = issued!.Identity, ["Fecha"] = "2030-01-01", ["Hora"] = "09:15", ["AceptaAdelanto"] = "true", ["ClienteId"] = "attacker", ["Precio"] = "1" };
            async Task<string[]> State()
            {
                var state = new List<string>();
                foreach (var name in new[] { "turnos", "turno_ediciones", "turno_solicitudes", "turno_cupones", "coordinacion" })
                {
                    var docs = await db.Collection(name).GetSnapshotAsync();
                    state.AddRange(docs.Documents.Select(d => $"{name}/{d.Id}:{d.UpdateTime}:{System.Text.Json.JsonSerializer.Serialize(d.ToDictionary())}"));
                }
                return state.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            }
            var before = await db.Collection("turnos").Document("t").GetSnapshotAsync();
            values["VersionEsperada"] = before.UpdateTime!.Value.ToDateTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Turnos/Editar", new FormUrlEncodedContent(values))).StatusCode);
            Assert.Equal(before.UpdateTime, (await before.Reference.GetSnapshotAsync()).UpdateTime);
            client.DefaultRequestHeaders.Add("RequestVerificationToken", await client.GetStringAsync("/csrf"));
            var wrong = new Dictionary<string, string>(values) { ["Id"] = "other" };
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Turnos/Editar", new FormUrlEncodedContent(wrong))).StatusCode);
            Assert.Equal(before.UpdateTime, (await before.Reference.GetSnapshotAsync()).UpdateTime);
            using var other = new HttpClient(new HttpClientHandler { UseCookies = true }) { BaseAddress = address };
            other.DefaultRequestHeaders.Add("X-User", "other"); other.DefaultRequestHeaders.Add("X-Permission", "Turnos.Consultar,Turnos.Modificar");
            other.DefaultRequestHeaders.Add("RequestVerificationToken", await other.GetStringAsync("/csrf"));
            var unchanged = await State();
            Assert.Equal(HttpStatusCode.BadRequest, (await other.PostAsync("/Turnos/Editar", new FormUrlEncodedContent(values))).StatusCode);
            Assert.Equal(unchanged, await State());
            var missingVersion = new Dictionary<string, string>(values);
            missingVersion.Remove("VersionEsperada");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Turnos/Editar", new FormUrlEncodedContent(missingVersion))).StatusCode);
            var invalidVersion = new Dictionary<string, string>(values) { ["VersionEsperada"] = "not-a-version" };
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Turnos/Editar", new FormUrlEncodedContent(invalidVersion))).StatusCode);
            Assert.Equal(unchanged, await State());
            var occupied = new Dictionary<string, string>(values) { ["Hora"] = "10:00" };
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/Turnos/Editar", new FormUrlEncodedContent(occupied))).StatusCode);
            Assert.Equal(unchanged, await State());
            var couponIdentity = await client.GetFromJsonAsync<Token>("/Turnos/EditarToken?id=coupon-turn");
            var couponValues = new Dictionary<string, string>(values) { ["Id"] = "coupon-turn", ["FormIdentity"] = couponIdentity!.Identity, ["Hora"] = "11:15" };
            var couponBefore = await db.Collection("turnos").Document("coupon-turn").GetSnapshotAsync();
            couponValues["VersionEsperada"] = couponBefore.UpdateTime!.Value.ToDateTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture);
            var couponDoc = await db.Collection("turno_cupones").Document("coupon").GetSnapshotAsync();
            var couponState = await State();
            var couponDiscount = new Dictionary<string, string>(couponValues) { ["Descuento"] = "10" };
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/Turnos/Editar", new FormUrlEncodedContent(couponDiscount))).StatusCode);
            Assert.Equal(couponState, await State());
            var couponRemoval = new Dictionary<string, string>(couponValues) { ["CuponId"] = "" };
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/Turnos/Editar", new FormUrlEncodedContent(couponRemoval))).StatusCode);
            Assert.Equal(couponState, await State());
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/Turnos/Editar", new FormUrlEncodedContent(couponValues))).StatusCode);
            var couponAfter = (await couponBefore.Reference.GetSnapshotAsync()).ConvertTo<Turno>();
            Assert.Equal(couponBefore.ConvertTo<Turno>().PrecioAcordado, couponAfter.PrecioAcordado);
            Assert.Equal("coupon", couponAfter.CuponId); Assert.Equal(couponDoc.UpdateTime, (await couponDoc.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/Turnos/Editar", new FormUrlEncodedContent(values))).StatusCode);
            var after = await before.Reference.GetSnapshotAsync(); var turn = after.ConvertTo<Turno>();
            Assert.Equal("c", turn.ClienteId); Assert.Equal(123m, turn.PrecioAcordado); Assert.True(turn.AceptaAdelanto);
            Assert.Equal(new DateTime(2030, 1, 1, 12, 15, 0, DateTimeKind.Utc), turn.InicioUtc);
            var afterEdit = await State();
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/Turnos/Editar", new FormUrlEncodedContent(values))).StatusCode);
            Assert.Equal(afterEdit, await State());
            Assert.Equal(after.UpdateTime, (await before.Reference.GetSnapshotAsync()).UpdateTime);
            var freshIdentity = await client.GetFromJsonAsync<Token>("/Turnos/EditarToken?id=t");
            var staleValues = new Dictionary<string, string>(values) { ["FormIdentity"] = freshIdentity!.Identity, ["Hora"] = "09:30" };
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/Turnos/Editar", new FormUrlEncodedContent(staleValues))).StatusCode);
            Assert.Equal(afterEdit, await State());
            var stale = await before.Reference.GetSnapshotAsync();
            await before.Reference.UpdateAsync("Notas", "concurrent");
            var interleaved = await State();
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.Editar(new(request with { InicioUtc = request.InicioUtc.AddMinutes(30) }, "stale-key", stale.UpdateTime)));
            Assert.Equal(interleaved, await State());
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/Turnos/Editar", new FormUrlEncodedContent(values))).StatusCode);
            Assert.Equal(interleaved, await State());
        }
        finally { await app.StopAsync(); }
    }
    [Fact]
    public async Task EditSelectorsRespectPermissionsAndRestrictSearchWithoutConsultPermission()
    {
        var selector = new Mock<ITurnoSelectorService>();
        selector.Setup(s => s.BuscarVehiculos(It.IsAny<string?>(), It.IsAny<bool>())).ReturnsAsync(new[] { new { id = "v" } });
        selector.Setup(s => s.Servicios(It.IsAny<string?>())).ReturnsAsync(new[] { new { id = "s" } });
        selector.Setup(s => s.Paquetes(It.IsAny<string?>())).ReturnsAsync(new[] { new { id = "p" } });
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(TurnosController).Assembly.GetName().Name, EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews();
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TurnoBookingHttpIntegrationTests.Auth>("Test", _ => { });
        builder.Services.AddAuthorization(); builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(Mock.Of<ITurnoConsultaService>()); builder.Services.AddSingleton(selector.Object);
        await using var app = builder.Build(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"); await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            var paths = new[] { "/Turnos/BuscarVehiculosPorPatente?search=ABC123", "/Turnos/ObtenerServiciosPorTipoVehiculo?tipoVehiculo=Auto", "/Turnos/ObtenerPaquetesPorTipoVehiculo?tipoVehiculo=Auto" };
            foreach (var path in paths) Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
            client.DefaultRequestHeaders.Add("X-User", "operator"); client.DefaultRequestHeaders.Add("X-Permission", "Lavados.Consultar");
            foreach (var path in paths) Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
            foreach (var permission in new[] { "Turnos.Crear", "Turnos.Consultar", "Turnos.Modificar" })
            {
                client.DefaultRequestHeaders.Remove("X-Permission"); client.DefaultRequestHeaders.Add("X-Permission", permission);
                foreach (var path in paths) Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
            }
            selector.Verify(s => s.BuscarVehiculos("ABC123", false), Times.Exactly(2));
            selector.Verify(s => s.BuscarVehiculos("ABC123", true), Times.Once);
        }
        finally { await app.StopAsync(); }
    }

    [LocalFirestoreFact]
    public async Task CustomerSelectorDeniesModifyOnlyAndCreateRequiresExactPlate()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-customer-selector-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("clientes").Document("c").SetAsync(new Cliente { Id = "c", Nombre = "Ana", Apellido = "Perez", TipoDocumento = "DNI", NumeroDocumento = "123", Email = "test@example.test", Telefono = "1234567890", Estado = "Activo" });
        await db.Collection("vehiculos").Document("v").SetAsync(new Vehiculo { Id = "v", Patente = "ABC123", Marca = "Ford", Modelo = "Focus", Color = "Rojo", TipoVehiculo = "Auto", Estado = "Activo", ClienteId = "c", ClientesIds = new() { "c" } });
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(TurnosController).Assembly.GetName().Name, EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews();
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TurnoBookingHttpIntegrationTests.Auth>("Test", _ => { });
        builder.Services.AddAuthorization(); builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db); builder.Services.AddMemoryCache(); builder.Services.AddScoped<ClienteService>(); builder.Services.AddScoped<VehiculoService>(); builder.Services.AddScoped<ServicioService>(); builder.Services.AddScoped<PaqueteServicioService>(); builder.Services.AddScoped<ITurnoSelectorService, TurnoSelectorService>(); builder.Services.AddSingleton(Mock.Of<ITurnoConsultaService>());
        await using var app = builder.Build(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"); await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            const string path = "/Turnos/ObtenerClientesVehiculo?vehiculoId=v";
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path + "&patente=ABC123")).StatusCode);
            client.DefaultRequestHeaders.Add("X-User", "operator"); client.DefaultRequestHeaders.Add("X-Permission", "Turnos.Modificar");
            foreach (var suffix in new[] { "", "&patente=ABC", "&patente=ABC123" }) Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path + suffix)).StatusCode);
            client.DefaultRequestHeaders.Remove("X-Permission"); client.DefaultRequestHeaders.Add("X-Permission", "Turnos.Crear");
            foreach (var suffix in new[] { "", "&patente=ABC", "&patente=ABC999" }) Assert.Empty((await client.GetFromJsonAsync<System.Text.Json.JsonElement[]>(path + suffix))!);
            var clients = await client.GetFromJsonAsync<System.Text.Json.JsonElement[]>(path + "&patente=abc123");
            Assert.Equal("c", Assert.Single(clients!).GetProperty("id").GetString());
            client.DefaultRequestHeaders.Remove("X-Permission"); client.DefaultRequestHeaders.Add("X-Permission", "Turnos.Consultar");
            Assert.Equal("c", Assert.Single((await client.GetFromJsonAsync<System.Text.Json.JsonElement[]>(path))!).GetProperty("id").GetString());
        }
        finally { await app.StopAsync(); }
    }

    [LocalFirestoreFact]
    public async Task DetailShowsAdvanceValueAndLinksToSharedEditForm()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-checkbox-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion());
        var consulta = new Mock<ITurnoConsultaService>();
        var inicio = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        consulta.Setup(s => s.Obtener(It.IsAny<string>())).ReturnsAsync((string id) => new TurnoConsultaViewModel(id, "Cliente", "Pendiente", inicio, inicio.AddMinutes(30), id == "yes", 100m, Array.Empty<VehiculoConsultaViewModel>(), "version"));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(TurnosController).Assembly.GetName().Name, EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews();
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, InsumosPermisosHttpIntegrationTests.HeaderAuth>("Test", _ => { });
        builder.Services.AddAuthorization(); builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>(); builder.Services.AddSingleton(consulta.Object);
        builder.Services.AddSingleton(db); builder.Services.AddMemoryCache(); builder.Services.AddScoped<ConfiguracionService>(); builder.Services.AddScoped<InsumoService>();
        await using var app = builder.Build(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"); await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            client.DefaultRequestHeaders.Add("X-Test-Permissions", "Turnos.Consultar,Turnos.Modificar");
            foreach (var (id, expected) in new[] { ("no", false), ("yes", true) })
            {
                using var response = await client.GetAsync("/Turnos/Detalle/" + id);
                var html = await response.Content.ReadAsStringAsync();
                Assert.True(response.StatusCode == HttpStatusCode.OK, html);
                Assert.Contains(expected ? "Acepta adelanto</dt><dd class=\"font-medium text-gray-900 dark:text-white\">Sí</dd>" : "Acepta adelanto</dt><dd class=\"font-medium text-gray-900 dark:text-white\">No</dd>", WebUtility.HtmlDecode(html));
                Assert.Contains("/Turnos?editar=" + id, html);
                Assert.DoesNotContain("id=\"reprogramacion-adelanto\"", html);
            }
        }
        finally { await app.StopAsync(); }
    }

    [Fact]
    public async Task EditDataRequiresBothPermissionsAndReturnsVersionedPendingAppointment()
    {
        var consulta = new Mock<ITurnoConsultaService>();
        var start = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        consulta.Setup(s => s.ObtenerDatosEdicion("t")).ReturnsAsync(new TurnoDatosEdicion("t", "version", start, false, "c", "c", "c", "nota", 0, null, 100, Array.Empty<VehiculoEdicionViewModel>()));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(TurnosController).Assembly.GetName().Name, EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews();
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, InsumosPermisosHttpIntegrationTests.HeaderAuth>("Test", _ => { });
        builder.Services.AddAuthorization(); builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>(); builder.Services.AddSingleton(consulta.Object);
        await using var app = builder.Build(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"); await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/Turnos/DatosEdicion?id=t")).StatusCode);
            client.DefaultRequestHeaders.Add("X-Test-Permissions", "Turnos.Consultar");
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/Turnos/DatosEdicion?id=t")).StatusCode);
            client.DefaultRequestHeaders.Remove("X-Test-Permissions"); client.DefaultRequestHeaders.Add("X-Test-Permissions", "Turnos.Consultar,Turnos.Modificar");
            var response = await client.GetAsync("/Turnos/DatosEdicion?id=t");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            Assert.Equal("version", body.GetProperty("versionEsperada").GetString());
            Assert.Equal("t", body.GetProperty("id").GetString());
        }
        finally { await app.StopAsync(); }
    }

    [Fact]
    public async Task EditBindingDistinguishesMissingAndEmptyNotes()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllers().AddApplicationPart(typeof(TurnoEditBindingProbeController).Assembly);
        await using var app = builder.Build(); app.MapControllers(); await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            foreach (var (values, expected) in new[] { (new Dictionary<string, string>(), false), (new Dictionary<string, string> { ["Notas"] = "" }, true) })
            {
                var response = await client.PostAsync("/turno-edit-binding-probe", new FormUrlEncodedContent(values));
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                Assert.Equal(expected, body.GetProperty("enviada").GetBoolean());
                Assert.Equal(System.Text.Json.JsonValueKind.Null, body.GetProperty("notas").ValueKind);
            }
        }
        finally { await app.StopAsync(); }
    }

    public sealed class EditAuth(Microsoft.Extensions.Options.IOptionsMonitor<AuthenticationSchemeOptions> options, Microsoft.Extensions.Logging.ILoggerFactory logger, System.Text.Encodings.Web.UrlEncoder encoder) : Microsoft.AspNetCore.Authentication.AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-User", out var user)) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<System.Security.Claims.Claim> { new(System.Security.Claims.ClaimTypes.NameIdentifier, user.ToString()) };
            claims.AddRange(Request.Headers["X-Permission"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries).Select(p => new System.Security.Claims.Claim(PermisoClaim.Tipo, p)));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }

    private sealed record Token(string Identity);
}

[Microsoft.AspNetCore.Mvc.ApiController]
public sealed class TurnoEditBindingProbeController : Microsoft.AspNetCore.Mvc.ControllerBase
{
    [Microsoft.AspNetCore.Mvc.HttpPost("/turno-edit-binding-probe")]
    public object Bind([Microsoft.AspNetCore.Mvc.FromForm] TurnoEditWebForm form) => new { enviada = form.NotasEnviadas, notas = form.Notas };
}

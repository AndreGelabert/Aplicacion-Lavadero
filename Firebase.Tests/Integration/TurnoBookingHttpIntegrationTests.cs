using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Firebase.Tests.Integration;

public class TurnoBookingHttpIntegrationTests
{
    [Fact]
    public void BookingAdapterUsesSharedArgentinaZone()
    {
        var source = File.ReadAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(SourcePath())!, "../../Services/TurnoWebBookingAdapter.cs")));
        Assert.Contains("TimeZoneHelper.ArgentinaZone", source);
        Assert.DoesNotContain("FindSystemTimeZoneById", source);
    }

    private static string SourcePath([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;

    public sealed class Auth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-User", out var user)) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, user.ToString()), new Claim(PermisoClaim.Tipo, Request.Headers["X-Permission"].ToString()) };
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }

    [LocalFirestoreFact]
    public async Task ProtectedBookingIsIdempotentAndRejectsInvalidRequests()
    {
        Assert.Equal("127.0.0.1:8190", Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST"));
        var db = new FirestoreDbBuilder { ProjectId = "demo-http-book-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("empleados").Document("e").SetAsync(new { Estado = "Activo" });
        await db.Collection("clientes").Document("c").SetAsync(new { Id = "c" });
        await db.Collection("vehiculos").Document("v").SetAsync(new Vehiculo { Id = "v", ClienteId = "c", TipoVehiculo = "Auto", Patente = "ABC123", Marca = "a", Modelo = "b", Color = "c" });
        await db.Collection("tiposVehiculos").Document("t").SetAsync(new TipoVehiculo { Id = "t", Nombre = "Auto", FormatoPatente = "lllnnn", CantidadEmpleadosRequeridos = 1 });
        await db.Collection("servicios").Document("s").SetAsync(new Servicio { Id = "s", Nombre = "Lavado", Tipo = "Lavado", TipoVehiculo = "Auto", Descripcion = "x", Estado = "Activo", Precio = 123, TiempoEstimado = 30 });
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion { CapacidadMaximaConcurrente = 1, HorariosOperacion = new() { ["Martes"] = "09:00-18:00" } });
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(TurnosController).Assembly.GetName().Name, EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews();
        builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, Auth>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db);
        builder.Services.AddSingleton(Mock.Of<ITurnoConsultaService>());
        builder.Services.AddSingleton<TurnoFormIdentity>();
        builder.Services.AddScoped<TurnoService>();
        builder.Services.AddScoped<TurnoWebBookingAdapter>();
        await using var app = builder.Build();
        app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
        app.MapGet("/test-form", (HttpContext context, IAntiforgery anti, TurnoFormIdentity identity) => Results.Json(new { Token = anti.GetAndStoreTokens(context).RequestToken, Identity = identity.Issue(context.User) })).RequireAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();
        try
        {
            var address = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
            using var a = new HttpClient(new HttpClientHandler { UseCookies = true }) { BaseAddress = address };
            a.DefaultRequestHeaders.Add("X-User", "a"); a.DefaultRequestHeaders.Add("X-Permission", "Turnos.Crear");
            var issued = await a.GetFromJsonAsync<FormTokens>("/test-form");
            var values = new Dictionary<string, string> { ["__RequestVerificationToken"] = issued!.Token, ["FormIdentity"] = issued.Identity, ["ClienteId"] = "c", ["VehiculoId"] = "v", ["ClienteTrajoId"] = "c", ["ClienteRetiraId"] = "c", ["Fecha"] = "2030-01-01", ["Hora"] = "09:00", ["Servicios[0].ServicioId"] = "s", ["AceptaAdelanto"] = "true", ["Precio"] = "1", ["Duracion"] = "1" };
            using var b = new HttpClient(new HttpClientHandler { UseCookies = true }) { BaseAddress = address };
            b.DefaultRequestHeaders.Add("X-User", "b"); b.DefaultRequestHeaders.Add("X-Permission", "Turnos.Crear");
            var other = await b.GetFromJsonAsync<FormTokens>("/test-form");
            var stolen = new Dictionary<string, string>(values) { ["__RequestVerificationToken"] = other!.Token };
            async Task<string> State()
            {
                var turns = await db.Collection("turnos").GetSnapshotAsync();
                var markers = await db.Collection("turno_solicitudes").GetSnapshotAsync();
                var capacity = await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
                var employees = await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync();
                return string.Join("|", turns.Documents.Select(d => $"{d.Id}:{d.UpdateTime}").OrderBy(x => x)) + ";" +
                    string.Join("|", markers.Documents.Select(d => $"{d.Id}:{d.UpdateTime}").OrderBy(x => x)) + $";{capacity.Exists}:{capacity.UpdateTime};{employees.Exists}:{employees.UpdateTime}";
            }
            var empty = await State();
            using var anonymous = new HttpClient { BaseAddress = address };
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/Turnos/Crear", new FormUrlEncodedContent(values))).StatusCode);
            Assert.Equal(empty, await State());
            b.DefaultRequestHeaders.Remove("X-Permission"); b.DefaultRequestHeaders.Add("X-Permission", "Turnos.Consultar");
            Assert.Equal(HttpStatusCode.Forbidden, (await b.PostAsync("/Turnos/Crear", new FormUrlEncodedContent(stolen))).StatusCode);
            Assert.Equal(empty, await State());
            b.DefaultRequestHeaders.Remove("X-Permission"); b.DefaultRequestHeaders.Add("X-Permission", "Turnos.Crear");
            var missingToken = new Dictionary<string, string>(values); missingToken.Remove("__RequestVerificationToken");
            Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsync("/Turnos/Crear", new FormUrlEncodedContent(missingToken))).StatusCode);
            Assert.Equal(empty, await State());
            var wrongToken = new Dictionary<string, string>(values) { ["__RequestVerificationToken"] = other.Token };
            Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsync("/Turnos/Crear", new FormUrlEncodedContent(wrongToken))).StatusCode);
            Assert.Equal(empty, await State());
            Assert.Equal(HttpStatusCode.BadRequest, (await b.PostAsync("/Turnos/Crear", new FormUrlEncodedContent(stolen))).StatusCode);
            Assert.Equal(empty, await State());
            var malformed = new Dictionary<string, string>(values) { ["Hora"] = "nonsense" };
            using var malformedResponse = await a.PostAsync("/Turnos/Crear", new FormUrlEncodedContent(malformed));
            Assert.Equal(HttpStatusCode.BadRequest, malformedResponse.StatusCode);
            var malformedResult = await malformedResponse.Content.ReadFromJsonAsync<ApiResult>();
            Assert.False(malformedResult!.Success); Assert.True(malformedResult.Editable);
            Assert.Equal("Datos de turno inválidos. Corrija los datos e intente nuevamente.", malformedResult.Message);
            Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
            Assert.Empty((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
            Assert.False((await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).Exists);
            Assert.Equal(empty, await State());
            foreach (var (date, time) in new[] { ("2008-10-19", "00:30"), ("2009-03-14", "23:30") })
            {
                var invalidLocal = new Dictionary<string, string>(values) { ["Fecha"] = date, ["Hora"] = time };
                using var invalidResponse = await a.PostAsync("/Turnos/Crear", new FormUrlEncodedContent(invalidLocal));
                Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
                Assert.Equal(empty, await State());
            }
            var oversized = new Dictionary<string, string>(values) { ["Notas"] = new string('x', 2001) };
            using var tooLong = await a.PostAsync("/Turnos/Crear", new FormUrlEncodedContent(oversized));
            Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
            var invalidJson = await tooLong.Content.ReadFromJsonAsync<ApiResult>();
            Assert.False(invalidJson!.Success); Assert.False(string.IsNullOrWhiteSpace(invalidJson.Message));
            Assert.Equal(empty, await State());
            values["Notas"] = new string('x', 2000);
            using var success = await a.PostAsync("/Turnos/Crear", new FormUrlEncodedContent(values));
            Assert.Equal(HttpStatusCode.OK, success.StatusCode);
            var result = await success.Content.ReadFromJsonAsync<ApiResult>();
            Assert.True(result!.Success); Assert.False(string.IsNullOrWhiteSpace(result.Message));
            var booked = await State();
            Assert.Equal(HttpStatusCode.OK, (await a.PostAsync("/Turnos/Crear", new FormUrlEncodedContent(values))).StatusCode);
            Assert.Equal(booked, await State());
            foreach (var (field, value) in new[] { ("Notas", "different"), ("Hora", "09:15"), ("AceptaAdelanto", "false") })
            {
                var changed = new Dictionary<string, string>(values) { [field] = value };
                using var response = await a.PostAsync("/Turnos/Crear", new FormUrlEncodedContent(changed));
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                var error = await response.Content.ReadFromJsonAsync<ApiResult>();
                Assert.False(error!.Success); Assert.False(string.IsNullOrWhiteSpace(error.Message));
                Assert.Equal(booked, await State());
            }
            var turno = Assert.Single((await db.Collection("turnos").GetSnapshotAsync()).Documents).ConvertTo<Turno>();
            Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
            Assert.Equal(new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc), turno.InicioUtc);
            Assert.Equal(123m, turno.PrecioAcordado);
            Assert.Equal(30, turno.Vehiculos.Single().Servicios.Single().TiempoEstimado);
        }
        finally { await app.StopAsync(); }
    }
    private sealed record FormTokens(string Token, string Identity);
    private sealed record ApiResult(bool Success, string Message, bool Editable);
}

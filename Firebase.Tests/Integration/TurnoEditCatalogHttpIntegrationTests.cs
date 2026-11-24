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

public class TurnoEditCatalogHttpIntegrationTests
{
    [LocalFirestoreFact]
    public async Task CatalogEditUsesTrustedPricesAndRejectsStaleAndForgedRequestsWithoutWrites()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-edit-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("empleados").Document("e").SetAsync(new { Estado = "Activo" });
        await db.Collection("clientes").Document("c").SetAsync(new { Id = "c" });
        foreach (var id in new[] { "v", "v2" })
            await db.Collection("vehiculos").Document(id).SetAsync(new Vehiculo { Id = id, ClienteId = "c", TipoVehiculo = "Auto", Patente = "ABC123", Marca = "a", Modelo = "b", Color = "c" });
        await db.Collection("tiposVehiculos").Document("t").SetAsync(new TipoVehiculo { Id = "t", Nombre = "Auto", FormatoPatente = "lllnnn", CantidadEmpleadosRequeridos = 1 });
        foreach (var (id, price, duration) in new[] { ("s", 100, 30), ("s2", 200, 45) })
            await db.Collection("servicios").Document(id).SetAsync(new Servicio { Id = id, Nombre = id, Tipo = "Lavado", TipoVehiculo = "Auto", Descripcion = "x", Estado = "Activo", Precio = price, TiempoEstimado = duration });
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion { ToleranciaLlegadaTurnosMinutos = 15, CapacidadMaximaConcurrente = 1, HorariosOperacion = new() { ["Martes"] = "09:00-18:00" } });
        var service = new TurnoService(db);
        var start = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(await service.Reservar(new("t", "c", "v", "Web", "create", start, false, [new("s", null)])));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(TurnosController).Assembly.GetName().Name, EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews();
        builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TurnoBookingHttpIntegrationTests.Auth>("Test", _ => { });
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
            client.DefaultRequestHeaders.Add("X-User", "operator"); client.DefaultRequestHeaders.Add("X-Permission", "Turnos.Modificar");
            client.DefaultRequestHeaders.Add("RequestVerificationToken", await client.GetStringAsync("/csrf"));
            var token = await client.GetFromJsonAsync<Token>("/Turnos/EditarToken?id=t");
            var before = await db.Collection("turnos").Document("t").GetSnapshotAsync();
            var values = new Dictionary<string, string> { ["Id"] = "t", ["FormIdentity"] = token!.Identity, ["VersionEsperada"] = before.UpdateTime!.Value.ToDateTime().ToString("O"), ["Fecha"] = "2030-01-01", ["Hora"] = "09:00", ["VehiculoId"] = "v2", ["Servicios[0].ServicioId"] = "s2", ["Servicios[0].Precio"] = "1", ["Precio"] = "1", ["ClienteId"] = "attacker" };
            async Task<string[]> State()
            {
                var result = new List<string>();
                foreach (var name in new[] { "turnos", "turno_ediciones", "turno_solicitudes", "turno_cupones", "coordinacion" })
                    result.AddRange((await db.Collection(name).GetSnapshotAsync()).Documents.Select(d => $"{name}/{d.Id}:{d.UpdateTime}:{System.Text.Json.JsonSerializer.Serialize(d.ToDictionary())}"));
                return result.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            }
            var original = await State();
            var forged = new Dictionary<string, string>(values) { ["Id"] = "other" };
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Turnos/Editar", new FormUrlEncodedContent(forged))).StatusCode);
            Assert.Equal(original, await State());
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/Turnos/Editar", new FormUrlEncodedContent(values))).StatusCode);
            var edited = (await before.Reference.GetSnapshotAsync()).ConvertTo<Turno>();
            Assert.Equal("c", edited.ClienteId); Assert.Equal("v2", edited.Vehiculos[0].VehiculoId);
            Assert.Equal("s2", Assert.Single(edited.Vehiculos[0].Servicios).ServicioId);
            Assert.Equal(200m, edited.PrecioAcordado); Assert.Equal(start.AddMinutes(45), edited.FinUtc);
            var after = await State();
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/Turnos/Editar", new FormUrlEncodedContent(values))).StatusCode);
            Assert.Equal(after, await State());
            var fresh = await client.GetFromJsonAsync<Token>("/Turnos/EditarToken?id=t");
            var stale = new Dictionary<string, string>(values) { ["FormIdentity"] = fresh!.Identity, ["Servicios[0].ServicioId"] = "s" };
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/Turnos/Editar", new FormUrlEncodedContent(stale))).StatusCode);
            Assert.Equal(after, await State());
            var couponChange = new Dictionary<string, string>(values) { ["FormIdentity"] = fresh.Identity, ["CuponId"] = "forged" };
            couponChange["VersionEsperada"] = (await before.Reference.GetSnapshotAsync()).UpdateTime!.Value.ToDateTime().ToString("O");
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/Turnos/Editar", new FormUrlEncodedContent(couponChange))).StatusCode);
            Assert.Equal(after, await State());
            var discount = new Dictionary<string, string>(couponChange) { ["Descuento"] = "25" };
            discount.Remove("CuponId");
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/Turnos/Editar", new FormUrlEncodedContent(discount))).StatusCode);
            var discounted = (await before.Reference.GetSnapshotAsync()).ConvertTo<Turno>();
            Assert.Equal(25m, discounted.Descuento); Assert.Equal(150m, discounted.PrecioAcordado);
            Assert.Equal("c", discounted.ClienteId); Assert.Null(discounted.CuponId);
            Assert.Equal("s2", Assert.Single(discounted.Vehiculos[0].Servicios).ServicioId);
            Assert.Equal(start.AddMinutes(45), discounted.FinUtc);
            var discountedState = await State();
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/Turnos/Editar", new FormUrlEncodedContent(discount))).StatusCode);
            Assert.Equal(discountedState, await State());
        }
        finally { await app.StopAsync(); }
    }
    private sealed record Token(string Identity);
}

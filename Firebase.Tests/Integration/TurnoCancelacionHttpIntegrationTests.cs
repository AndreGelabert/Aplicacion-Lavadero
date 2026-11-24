using System.Net;
using Firebase.Security;
using Firebase.Services;
using FirebaseLoginCustom.Controllers;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Firebase.Tests.Integration;

public class TurnoCancelacionHttpIntegrationTests
{
    [LocalFirestoreFact]
    public async Task CancellationProtectsEndpointAndPersistsOwnerCascadeAndReplayAtomically()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-cancel-http-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var start = DateTime.UtcNow.AddDays(10);
        await db.Collection("turnos").Document("t").SetAsync(new { Estado = "Pendiente", ClienteId = "real-owner", InicioUtc = start, FinUtc = start.AddMinutes(30) });
        await db.Collection("coordinacion").Document("turnos").SetAsync(new { Version = 7L });
        await db.Collection("configuracion").Document("system_config").SetAsync(new { CancelacionAnticipadaHorasMinimas = 24, CancelacionAnticipadaDescuento = 10d, CancelacionAnticipadaValidezDias = 30 });
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(TurnosController).Assembly.GetName().Name, EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews();
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, InsumosPermisosHttpIntegrationTests.HeaderAuth>("Test", _ => { });
        builder.Services.AddAuthorization(); builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db); builder.Services.AddSingleton(Mock.Of<ITurnoConsultaService>()); builder.Services.AddScoped<TurnoService>();
        await using var app = builder.Build();
        app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
        app.MapGet("/token", (HttpContext c, IAntiforgery a) => a.GetAndStoreTokens(c).RequestToken!).RequireAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"); await app.StartAsync();
        try
        {
            using var client = new HttpClient(new HttpClientHandler { UseCookies = true }) { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            async Task<string[]> State()
            {
                var result = new List<string>();
                foreach (var n in new[] { "turnos", "coordinacion", "turno_cupones", "turno_cascadas" })
                {
                    var docs = await db.Collection(n).GetSnapshotAsync();
                    result.Add($"{n}:{docs.Count}");
                    result.AddRange(docs.Documents.Select(d => $"{n}/{d.Id}:{d.UpdateTime}:{System.Text.Json.JsonSerializer.Serialize(d.ToDictionary())}"));
                }
                return result.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            }
            static FormUrlEncodedContent Body(string id, string motivo = "Motivo válido") => new(new Dictionary<string, string> { ["id"] = id, ["motivo"] = motivo, ["owner"] = "attacker", ["now"] = "1900-01-01" });
            var before = await State();
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/Turnos/Cancelar", Body("t"))).StatusCode);
            client.DefaultRequestHeaders.Add("X-Test-Permissions", "Turnos.Consultar");
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/Turnos/Cancelar", Body("t"))).StatusCode);
            client.DefaultRequestHeaders.Remove("X-Test-Permissions"); client.DefaultRequestHeaders.Add("X-Test-Permissions", "Turnos.Modificar");
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/Turnos/Cancelar", Body("t"))).StatusCode);
            Assert.Equal(before, await State());
            client.DefaultRequestHeaders.Remove("X-Test-Permissions"); client.DefaultRequestHeaders.Add("X-Test-Permissions", "Turnos.Cancelar");
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/Turnos/Cancelar", Body("t"))).StatusCode);
            client.DefaultRequestHeaders.Remove("X-Test-Permissions"); client.DefaultRequestHeaders.Add("X-Test-Permissions", "Turnos.Consultar,Turnos.Cancelar");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Turnos/Cancelar", Body("t"))).StatusCode);
            client.DefaultRequestHeaders.Add("RequestVerificationToken", "invalid");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Turnos/Cancelar", Body("t"))).StatusCode);
            Assert.Equal(before, await State());
            client.DefaultRequestHeaders.Remove("RequestVerificationToken"); client.DefaultRequestHeaders.Add("RequestVerificationToken", await client.GetStringAsync("/token"));
            using (var invalid = await client.PostAsync("/Turnos/Cancelar", Body("t", "   ")))
            {
                Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
                Assert.Equal(before, await State());
            }
            using (var missing = await client.PostAsync("/Turnos/Cancelar", Body("missing")))
            {
                Assert.Equal(HttpStatusCode.Conflict, missing.StatusCode);
                Assert.Equal("{\"success\":false,\"message\":\"No se pudo cancelar el turno.\"}", await missing.Content.ReadAsStringAsync());
                Assert.Equal(before, await State());
            }
            using (var response = await client.PostAsync("/Turnos/Cancelar", Body("t")))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal("{\"success\":true,\"message\":\"Turno cancelado correctamente.\"}", await response.Content.ReadAsStringAsync());
            }
            var cancelled = await db.Collection("turnos").Document("t").GetSnapshotAsync();
            Assert.Equal("Cancelado", cancelled.GetValue<string>("Estado"));
            Assert.Equal("Motivo válido", cancelled.GetValue<string>("MotivoCancelacion"));
            var coupon = Assert.Single((await db.Collection("turno_cupones").GetSnapshotAsync()).Documents);
            Assert.Equal("real-owner", coupon.GetValue<string>("Owner")); Assert.Equal(10d, coupon.GetValue<double>("Percentage"));
            Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);
            Assert.Equal(8L, (await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).GetValue<long>("Version"));
            var after = await State();
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/Turnos/Cancelar", Body("t"))).StatusCode);
            Assert.Equal(after, await State());
        }
        finally { await app.StopAsync(); }
    }
}

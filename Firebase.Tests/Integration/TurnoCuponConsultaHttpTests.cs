using System.Net;
using System.Net.Http.Json;
using Firebase.Security;
using FirebaseLoginCustom.Controllers;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Firebase.Tests.Integration;

public class TurnoCuponConsultaHttpTests
{
    [LocalFirestoreFact]
    public async Task DiscoveryRequiresPermissionAndReturnsOnlyValidSelectedClientCouponsWithoutWrites()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-coupon-query-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("clientes").Document("selected").SetAsync(new { Id = "selected" });
        await db.Collection("clientes").Document("other").SetAsync(new { Id = "other" });
        var coupons = db.Collection("turno_cupones");
        var expiry = DateTime.UtcNow.AddDays(2);
        await coupons.Document("valid").SetAsync(new { Owner = "selected", Percentage = 20d, ExpiresUtc = expiry, Consumed = false, Private = "secret" });
        await coupons.Document("other").SetAsync(new { Owner = "other", Percentage = 40d, ExpiresUtc = expiry, Consumed = false });
        await coupons.Document("expired").SetAsync(new { Owner = "selected", Percentage = 20d, ExpiresUtc = DateTime.UtcNow.AddDays(-1), Consumed = false });
        await coupons.Document("consumed").SetAsync(new { Owner = "selected", Percentage = 20d, ExpiresUtc = expiry, Consumed = true });
        await coupons.Document("wrong-type").SetAsync(new { Owner = "selected", Percentage = 20L, ExpiresUtc = expiry, Consumed = false });
        await coupons.Document("missing").SetAsync(new { Owner = "selected", Percentage = 20d, ExpiresUtc = expiry });
        await coupons.Document("range").SetAsync(new { Owner = "selected", Percentage = 101d, ExpiresUtc = expiry, Consumed = false });
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(TurnosController).Assembly.GetName().Name, EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews();
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TurnoBookingHttpIntegrationTests.Auth>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db);
        await using var app = builder.Build();
        app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();
        try
        {
            var address = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
            using var client = new HttpClient { BaseAddress = address };
            async Task<string> State()
            {
                var cs = await coupons.GetSnapshotAsync();
                var clients = await db.Collection("clientes").GetSnapshotAsync();
                var turns = await db.Collection("turnos").GetSnapshotAsync();
                var markers = await db.Collection("turno_solicitudes").GetSnapshotAsync();
                var capacity = await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
                return string.Join("|", cs.Documents.Concat(clients.Documents).Concat(turns.Documents).Concat(markers.Documents).Select(x => $"{x.Reference.Path}:{x.UpdateTime}").OrderBy(x => x)) + $";{capacity.Exists}:{capacity.UpdateTime}";
            }
            var before = await State();
            const string path = "/TurnoCupones/Consultar?clienteId=selected";
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
            Assert.Equal(before, await State());
            client.DefaultRequestHeaders.Add("X-User", "staff-not-customer");
            client.DefaultRequestHeaders.Add("X-Permission", "Turnos.Consultar");
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
            Assert.Equal(before, await State());
            client.DefaultRequestHeaders.Remove("X-Permission"); client.DefaultRequestHeaders.Add("X-Permission", "Turnos.Crear");
            foreach (var invalid in new[] { "", "%20", "a%2Fb", ".", "..", "missing-client" })
            {
                var response = await client.GetAsync("/TurnoCupones/Consultar?clienteId=" + invalid);
                Assert.Equal(invalid == "missing-client" ? HttpStatusCode.NotFound : HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Equal(before, await State());
            }
            using var success = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, success.StatusCode);
            var data = await success.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            var item = Assert.Single(data.EnumerateArray());
            Assert.Equal(new[] { "expiresUtc", "id", "percentage" }, item.EnumerateObject().Select(p => p.Name).OrderBy(x => x).ToArray());
            Assert.Equal("valid", item.GetProperty("id").GetString());
            Assert.Equal(20d, item.GetProperty("percentage").GetDouble());
            Assert.Equal(expiry, item.GetProperty("expiresUtc").GetDateTime(), TimeSpan.FromMilliseconds(1));
            Assert.Equal(before, await State());
            var other = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/TurnoCupones/Consultar?clienteId=other");
            Assert.Equal("other", Assert.Single(other.EnumerateArray()).GetProperty("id").GetString());
            Assert.Equal(before, await State());
        }
        finally { await app.StopAsync(); }
    }
}

using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Firebase.Controllers;
using Firebase.Models;
using Firebase.Models.Dtos;
using Firebase.Security;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Firebase.Tests.Integration;

public class VehicleCatalogueReviewHttpSecurityTests
{
    [LocalFirestoreFact]
    public async Task AuthorizedReviewPageSupportsApproveLinkAndDiscardPosts()
    {
        var db = new FirestoreDbBuilder
        {
            ProjectId = "demo-lava-" + Guid.NewGuid().ToString("N")[..16],
            EmulatorDetection = EmulatorDetection.EmulatorOnly
        }.Build();
        var vehicleService = new VehiculoService(db);
        var approvedVehicle = await CreatePendingVehicle(vehicleService, "Approve");
        var linkedVehicle = await CreatePendingVehicle(vehicleService, "Link");
        var discardedVehicle = await CreatePendingVehicle(vehicleService, "Discard");

        var upstream = new Mock<ICarQueryService>();
        upstream.Setup(service => service.GetMarcasAsync()).ReturnsAsync(new List<MarcaSimpleDto> { new("1", "Toyota") });
        upstream.Setup(service => service.GetModelosAsync("1", null)).ReturnsAsync(new List<ModeloSimpleDto> { new("Corolla", "1") });

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(VehicleCatalogueReviewController).Assembly);
        builder.Services.AddMemoryCache();
        builder.Services.AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, PermissionTestAuthenticationHandler>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db);
        builder.Services.AddScoped<ConfiguracionService>();
        builder.Services.AddSingleton(upstream.Object);
        builder.Services.AddScoped<VehicleCatalogueReviewService>();

        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features
                .Get<IServerAddressesFeature>()!.Addresses.Single();
            var cookies = new CookieContainer();
            using var handler = new HttpClientHandler { CookieContainer = cookies, AllowAutoRedirect = false };
            using var client = new HttpClient(handler) { BaseAddress = new Uri(address) };
            client.DefaultRequestHeaders.Add("X-Test-Authenticated", "yes");
            client.DefaultRequestHeaders.Add("X-Test-Permission", "Vehiculos.Modificar");

            var pageResponse = await client.GetAsync("/VehicleCatalogueReview/Index");
            var page = await pageResponse.Content.ReadAsStringAsync();
            Assert.True(pageResponse.StatusCode == HttpStatusCode.OK,
                $"Review page returned {(int)pageResponse.StatusCode}: {page}");
            Assert.Contains(approvedVehicle.Id, page);
            Assert.Contains(linkedVehicle.Id, page);
            Assert.Contains(discardedVehicle.Id, page);
            var tokenMatch = Regex.Match(page,
                "<input[^>]*name=\\\"__RequestVerificationToken\\\"[^>]*value=\\\"([^\\\"]+)\\\"",
                RegexOptions.IgnoreCase);
            Assert.True(tokenMatch.Success, "The review page must render an antiforgery form token.");
            var token = tokenMatch.Groups[1].Value;

            await PostReview("approve", approvedVehicle.Id, "Marca Aprobada", "Modelo Aprobado", token);
            await PostReview("link", linkedVehicle.Id, "Toyota", "Corolla", token);
            await PostReview("discard", discardedVehicle.Id, "", "", token);

            await AssertReviewed(approvedVehicle.Id, "Revisada", "Marca Aprobada", "Modelo Aprobado");
            await AssertReviewed(linkedVehicle.Id, "Revisada", "Toyota", "Corolla");
            await AssertReviewed(discardedVehicle.Id, "Descartada", "Marca Discard", "Modelo Discard");

            var approvedBrands = await db.Collection("approvedVehicleBrands").GetSnapshotAsync();
            Assert.Contains(approvedBrands.Documents, doc => doc.GetValue<string>("Nombre") == "Marca Aprobada");
            Assert.Contains(approvedBrands.Documents, doc => doc.GetValue<string>("Nombre") == "Toyota");
            var approvedModels = await db.Collection("approvedVehicleModels").GetSnapshotAsync();
            Assert.Contains(approvedModels.Documents, doc => doc.GetValue<string>("Nombre") == "Modelo Aprobado");
            Assert.DoesNotContain(approvedModels.Documents, doc => doc.GetValue<string>("Nombre") == "Corolla");
            var audits = await db.Collection("registros_auditoria").GetSnapshotAsync();
            Assert.Equal(3, audits.Count);
            Assert.Contains(audits.Documents, doc => doc.GetValue<string>("Action") == "Revision de catalogo: approve");
            Assert.Contains(audits.Documents, doc => doc.GetValue<string>("Action") == "Revision de catalogo: link");
            Assert.Contains(audits.Documents, doc => doc.GetValue<string>("Action") == "Revision de catalogo: discard");

            async Task PostReview(string decision, string id, string brand, string model, string antiforgeryToken)
            {
                using var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["id"] = id, ["decision"] = decision, ["brand"] = brand, ["model"] = model,
                    ["__RequestVerificationToken"] = antiforgeryToken
                });
                var response = await client.PostAsync("/VehicleCatalogueReview/Resolve", content);
                Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            }

            async Task AssertReviewed(string id, string state, string brand, string model)
            {
                var proposal = await db.Collection("vehicleCatalogueProposals").Document(id).GetSnapshotAsync();
                var vehicle = await db.Collection("vehiculos").Document(id).GetSnapshotAsync();
                Assert.Equal(state, proposal.GetValue<string>("Estado"));
                Assert.Equal(brand, vehicle.GetValue<string>("Marca"));
                Assert.Equal(model, vehicle.GetValue<string>("Modelo"));
                Assert.Equal("integration-test", proposal.GetValue<string>("RevisadaPor"));
            }
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [LocalFirestoreFact]
    public async Task ReviewRoutesEnforceAuthenticationPermissionAndAntiforgeryBeforeMutation()
    {
        var db = new FirestoreDbBuilder
        {
            ProjectId = "demo-lava-" + Guid.NewGuid().ToString("N")[..16],
            EmulatorDetection = EmulatorDetection.EmulatorOnly
        }.Build();
        var vehicle = new Vehiculo { Id = "", Patente = "AB123CD", TipoVehiculo = "Automóvil", Marca = "Toyota", Modelo = "Corolla", Color = "Blanco", ClienteId = "client" };
        var proposal = new VehicleCatalogueProposal { TipoVehiculo = "Automóvil", Marca = "Toyota", Modelo = "Corolla" };
        await new VehiculoService(db).CrearVehiculoConPropuesta(vehicle, proposal);
        var initialVehicle = await db.Collection("vehiculos").Document(vehicle.Id).GetSnapshotAsync();
        var initialProposal = await db.Collection("vehicleCatalogueProposals").Document(vehicle.Id).GetSnapshotAsync();

        var upstream = new Mock<ICarQueryService>();
        upstream.Setup(service => service.GetMarcasAsync()).ReturnsAsync(new List<MarcaSimpleDto> { new("1", "Toyota") });
        upstream.Setup(service => service.GetModelosAsync("1", null)).ReturnsAsync(new List<ModeloSimpleDto> { new("Corolla", "1") });

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(VehicleCatalogueReviewController).Assembly);
        builder.Services.AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, PermissionTestAuthenticationHandler>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db);
        builder.Services.AddSingleton(upstream.Object);
        builder.Services.AddScoped<VehicleCatalogueReviewService>();

        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features
                .Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };

            var anonymous = await client.GetAsync("/VehicleCatalogueReview/Index");
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

            using var noPermissionRequest = new HttpRequestMessage(HttpMethod.Get, "/VehicleCatalogueReview/Index");
            noPermissionRequest.Headers.Add("X-Test-Authenticated", "yes");
            var noPermission = await client.SendAsync(noPermissionRequest);
            Assert.Equal(HttpStatusCode.Forbidden, noPermission.StatusCode);

            using var forgedPost = new HttpRequestMessage(HttpMethod.Post, "/VehicleCatalogueReview/Resolve")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["id"] = vehicle.Id, ["decision"] = "approve", ["brand"] = "Toyota", ["model"] = "Corolla"
                })
            };
            forgedPost.Headers.Add("X-Test-Authenticated", "yes");
            forgedPost.Headers.Add("X-Test-Permission", "Vehiculos.Modificar");
            var csrfDenied = await client.SendAsync(forgedPost);
            Assert.Equal(HttpStatusCode.BadRequest, csrfDenied.StatusCode);

            var finalProposal = await db.Collection("vehicleCatalogueProposals").Document(vehicle.Id).GetSnapshotAsync();
            var finalVehicle = await db.Collection("vehiculos").Document(vehicle.Id).GetSnapshotAsync();
            Assert.Equal(initialProposal.UpdateTime, finalProposal.UpdateTime);
            Assert.Equal("Pendiente", finalProposal.GetValue<string>("Estado"));
            Assert.Equal(initialVehicle.UpdateTime, finalVehicle.UpdateTime);
            Assert.Equal("Toyota", finalVehicle.GetValue<string>("Marca"));
            Assert.Equal("Corolla", finalVehicle.GetValue<string>("Modelo"));
            Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
            Assert.Empty((await db.Collection("approvedVehicleBrands").GetSnapshotAsync()).Documents);
            Assert.Empty((await db.Collection("approvedVehicleModels").GetSnapshotAsync()).Documents);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static async Task<Vehiculo> CreatePendingVehicle(VehiculoService service, string suffix)
    {
        var vehicle = new Vehiculo
        {
            Id = "", Patente = suffix == "Approve" ? "AP123CD" : suffix == "Link" ? "LI123CD" : "DI123CD",
            TipoVehiculo = "Automóvil", Marca = "Marca " + suffix, Modelo = "Modelo " + suffix,
            Color = "Blanco", ClienteId = "client"
        };
        await service.CrearVehiculoConPropuesta(vehicle, new VehicleCatalogueProposal
        {
            TipoVehiculo = vehicle.TipoVehiculo, Marca = vehicle.Marca, Modelo = vehicle.Modelo,
            MarcaManual = true, ModeloManual = true
        });
        return vehicle;
    }

    private sealed class PermissionTestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public PermissionTestAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder) : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Authenticated", out var authenticated) || authenticated != "yes")
                return Task.FromResult(AuthenticateResult.NoResult());

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "integration-test"),
                new(ClaimTypes.Email, "integration@example.invalid")
            };
            if (Request.Headers.TryGetValue("X-Test-Permission", out var permission))
                claims.Add(new Claim("permiso", permission.ToString()));
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}

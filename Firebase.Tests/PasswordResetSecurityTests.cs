using System.Net;
using System.Text.Json;
using Firebase.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;

namespace Firebase.Tests;

public class PasswordResetSecurityTests
{
    private const string Accepted = "Si existe una cuenta asociada a ese correo, recibirás instrucciones para restablecer tu contraseña.";
    private const string Invalid = "Por favor, ingrese un correo electrónico válido.";
    private const string Failure = "Error al enviar el correo de recuperación. Por favor, intente de nuevo.";

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"email\":null}")]
    [InlineData("{\"email\":\"\"}")]
    [InlineData("{\"email\":\"   \"}")]
    [InlineData("{\"email\":\"not-an-email\"}")]
    [InlineData("{\"email\":\"user@@example.com\"}")]
    [InlineData("{\"email\":\"user@example\"}")]
    public async Task InvalidInputReturnsBadRequestWithoutProviderCall(string body)
    {
        var handler = new FakeHandler(HttpStatusCode.OK, "{}");
        var controller = Controller(handler);
        var request = JsonConvert.DeserializeObject<ForgotPasswordRequest>(body);

        var result = await controller.ForgotPassword(request!);

        AssertResponse(result, 400, "error", Invalid);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"email\":null}")]
    [InlineData("{\"email\":\"bad\"}")]
    public async Task HttpModelBindingRejectsInvalidInputWithoutProviderCall(string body)
    {
        var handler = new FakeHandler(HttpStatusCode.OK, "{}");
        using var host = new HostBuilder().ConfigureWebHost(web => web.UseKestrel().UseUrls("http://127.0.0.1:0")
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddControllersWithViews().AddApplicationPart(typeof(LoginController).Assembly);
                services.Replace(ServiceDescriptor.Transient<IControllerActivator, ServiceBasedControllerActivator>());
                services.AddTransient(_ => Controller(handler));
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapControllerRoute("default", "{controller}/{action}"));
            })).Build();
        await host.StartAsync();
        var address = host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };
        using var response = await client.PostAsync("/Login/ForgotPassword", new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, json.RootElement.EnumerateObject().Count());
        Assert.Equal(Invalid, json.RootElement.GetProperty("error").GetString());
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ExistingAndMissingAccountsHaveIdenticalPublicResponseAndProviderTrace()
    {
        var existing = new FakeHandler(HttpStatusCode.OK, "{}");
        var missing = new FakeHandler(HttpStatusCode.BadRequest, "{\"error\":{\"message\":\"EMAIL_NOT_FOUND\"}}");
        var request = new ForgotPasswordRequest { Email = "  person@example.com  " };

        var existingResult = await Controller(existing).ForgotPassword(request);
        var missingResult = await Controller(missing).ForgotPassword(request);

        AssertResponse(existingResult, 200, "message", Accepted);
        AssertResponse(missingResult, 200, "message", Accepted);
        Assert.Equal(Assert.Single(existing.Requests), Assert.Single(missing.Requests));
        using var payload = JsonDocument.Parse(existing.Requests[0]);
        Assert.Equal("PASSWORD_RESET", payload.RootElement.GetProperty("requestType").GetString());
        Assert.Equal("person@example.com", payload.RootElement.GetProperty("email").GetString());
    }

    [Theory]
    [InlineData(429, "{\"error\":{\"message\":\"TOO_MANY_ATTEMPTS_TRY_LATER\"}}")]
    [InlineData(500, "{\"error\":{\"message\":\"EMAIL_NOT_FOUND\"}}")]
    [InlineData(503, "provider account details")]
    [InlineData(400, "{\"error\":{\"message\":\"USER_DISABLED\"}}")]
    public async Task ProviderFailuresAreGeneric(int status, string body)
    {
        var handler = new FakeHandler((HttpStatusCode)status, body);
        var result = await Controller(handler).ForgotPassword(new ForgotPasswordRequest { Email = "person@example.com" });
        AssertResponse(result, 400, "error", Failure);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task NetworkFailureIsGeneric()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, "{}") { Throw = true };
        var result = await Controller(handler).ForgotPassword(new ForgotPasswordRequest { Email = "person@example.com" });
        AssertResponse(result, 400, "error", Failure);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task LoginStillMapsInvalidPasswordAsBefore()
    {
        var handler = new FakeHandler(HttpStatusCode.BadRequest, "{\"error\":{\"message\":\"INVALID_PASSWORD\"}}") { Endpoint = ":signInWithPassword" };
        var result = await Service(handler).AuthenticateWithEmailAsync("person@example.com", "incorrect");
        Assert.False(result.IsSuccess);
        Assert.Equal("La contraseña es incorrecta.", result.ErrorMessage);
        Assert.Single(handler.Requests);
    }

    private static LoginController Controller(FakeHandler handler) =>
        new(Service(handler), null!, null!, null!, null!, NullLogger<LoginController>.Instance);

    private static AuthenticationService Service(FakeHandler handler)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["LocalEmulators:Enabled"] = "true"
        }).Build();
        return new AuthenticationService(config, null!, null!, new HttpClient(handler));
    }

    private static void AssertResponse(IActionResult result, int status, string property, string message)
    {
        var response = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(status, response.StatusCode);
        using var json = JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(response.Value));
        Assert.Equal(1, json.RootElement.EnumerateObject().Count());
        Assert.Equal(message, json.RootElement.GetProperty(property).GetString());
    }

    private sealed class FakeHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<string> Requests { get; } = new();
        public bool Throw { get; init; }
        public string Endpoint { get; init; } = ":sendOobCode";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.EndsWith(Endpoint, request.RequestUri!.AbsolutePath, StringComparison.Ordinal);
            Requests.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            if (Throw) throw new HttpRequestException("synthetic provider failure");
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }
}

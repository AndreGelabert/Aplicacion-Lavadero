using System.Net;
using System.Text;
using Firebase.Services;
using FirebaseLoginCustom.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Firebase.Tests.Integration;

public class TurnoOfertaEmailResponseHttpTests
{
    private const string Path = "/api/turno-oferta-email/responder";
    private const string Valid = "{\"cascadeId\":\"cascade\",\"offerId\":\"offer\",\"clientId\":\"client\",\"token\":\"synthetic-secret\",\"accept\":true}";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AnonymousJsonPostRoutesAndDelegates(bool accept)
    {
        (string, string, string, string, DateTime, bool)? received = null;
        using var host = CreateHost((c, o, i, t, now, a) => { received = (c, o, i, t, now, a); return Task.CompletedTask; });
        await host.StartAsync();
        using var client = Client(host);
        var before = DateTime.UtcNow;
        using var response = await client.PostAsync(Path, Json(Valid.Replace("true", accept ? "true" : "false")));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("", await response.Content.ReadAsStringAsync());
        Assert.NotNull(received);
        Assert.Equal(("cascade", "offer", "client", "synthetic-secret"), (received.Value.Item1, received.Value.Item2, received.Value.Item3, received.Value.Item4));
        Assert.Equal(accept, received.Value.Item6);
        Assert.Equal(DateTimeKind.Utc, received.Value.Item5.Kind);
        Assert.InRange(received.Value.Item5, before, DateTime.UtcNow);
        Assert.Equal(0, received.Value.Item5.Ticks % 10);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"cascadeId\":\" \",\"offerId\":\"offer\",\"clientId\":\"client\",\"token\":\"synthetic-secret\",\"accept\":true}")]
    [InlineData("{\"cascadeId\":\"cascade\",\"offerId\":\"offer\",\"clientId\":\"client\",\"token\":\"synthetic-secret\",\"accept\":\"wrong\"}")]
    [InlineData("{\"token\":\"synthetic-secret\",")]
    public async Task InvalidOrMalformedBodyIsRejectedWithoutDelegateOrLeak(string body)
    {
        var calls = 0;
        using var host = CreateHost((_, _, _, _, _, _) => { calls++; return Task.CompletedTask; });
        await host.StartAsync();
        using var client = Client(host);
        using var response = await client.PostAsync(Path, Json(body));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("", await response.Content.ReadAsStringAsync());
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DelegateErrorsReturnEmptyStatusWithoutLeaking(bool unavailable)
    {
        var calls = 0;
        using var host = CreateHost((_, _, _, _, _, _) => { calls++; throw unavailable ? new Exception("synthetic-secret") : new TurnoOfertaEmailInvalidException(); });
        await host.StartAsync();
        using var client = Client(host);
        using var response = await client.PostAsync(Path, Json(Valid));
        Assert.Equal(unavailable ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("", await response.Content.ReadAsStringAsync());
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task GetCannotInvokeMutation()
    {
        var calls = 0;
        using var host = CreateHost((_, _, _, _, _, _) => { calls++; return Task.CompletedTask; });
        await host.StartAsync();
        using var client = Client(host);
        using var response = await client.GetAsync(Path);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal(0, calls);
    }

    private static StringContent Json(string value) => new(value, Encoding.UTF8, "application/json");
    private static HttpClient Client(IHost host) => new() { BaseAddress = new Uri(host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
    private static IHost CreateHost(Func<string, string, string, string, DateTime, bool, Task> respond) =>
        new HostBuilder().ConfigureWebHost(web => web.UseKestrel().UseUrls("http://127.0.0.1:0")
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddControllers().AddApplicationPart(typeof(TurnoOfertaEmailController).Assembly);
                services.Replace(ServiceDescriptor.Transient<IControllerActivator, ServiceBasedControllerActivator>());
                services.AddTransient(_ => new TurnoOfertaEmailController(respond));
            })
            .Configure(app => { app.UseRouting(); app.UseEndpoints(endpoints => endpoints.MapControllers()); })).Build();
}

using System.Net;
using System.Security.Cryptography;
using System.Text;
using Firebase.Controllers;
using Firebase.Models;
using Firebase.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Firebase.Tests.Integration;

public class WhatsAppWebhookHttpTests
{
    private const string Secret = "synthetic-secret";
    private const string Body = "{ \"entry\":[{\"changes\":[{\"field\":\"messages\",\"value\":{\"messaging_product\":\"whatsapp\",\"messages\":[{\"from\":\"123\",\"id\":\"id\",\"timestamp\":\"123\",\"type\":\"text\",\"text\":{\"body\":\"hello\"}}]}}]}]}";
    private static string Sign(string body) => "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();

    [Theory]
    [InlineData("missing")]
    [InlineData("wrong")]
    [InlineData("tampered")]
    [InlineData("no-secret")]
    [InlineData("valid")]
    [InlineData("chunked-oversized")]
    public async Task PublicWebhook_AuthenticatesRawBodyBeforeMessageEffects(string scenario)
    {
        var handler = new ReceiptHandler();
        var flowLogger = new FlowLogger();
        var settings = Options.Create(new WhatsAppSettings { AccessToken = "synthetic", PhoneNumberId = "synthetic", VerifyToken = "synthetic", AppSecret = scenario == "no-secret" ? null : Secret });
        var meta = new MetaWhatsAppService(new HttpClient(handler), settings, Microsoft.Extensions.Logging.Abstractions.NullLogger<MetaWhatsAppService>.Instance);
        var flow = new WhatsAppFlowService(null!, meta, null!, null!, null!, null!, null!, null!, flowLogger);
        using var host = new HostBuilder().ConfigureWebHost(web => web.UseKestrel().UseUrls("http://127.0.0.1:0")
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddControllers().AddApplicationPart(typeof(WhatsAppWebhookController).Assembly);
                services.Replace(ServiceDescriptor.Transient<IControllerActivator, ServiceBasedControllerActivator>());
                services.AddTransient(provider => new WhatsAppWebhookController(settings, flow, meta, provider.GetRequiredService<ILogger<WhatsAppWebhookController>>()));
            })
            .Configure(app => { app.UseRouting(); app.UseEndpoints(endpoints => endpoints.MapControllers()); })).Build();
        await host.StartAsync();
        try
        {
            var address = host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            var payload = scenario == "tampered" ? Body + " " : scenario == "chunked-oversized" ? Body + new string(' ', 1024 * 1024) : Body;
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/whatsapp/webhook") { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
            if (scenario == "chunked-oversized")
            {
                request.Content.Headers.ContentLength = null;
                request.Headers.TransferEncodingChunked = true;
            }
            if (scenario != "missing") request.Headers.TryAddWithoutValidation("X-Hub-Signature-256", scenario == "wrong" ? "sha256=" + new string('0', 64) : Sign(payload));
            if (scenario == "tampered") request.Headers.Remove("X-Hub-Signature-256");
            if (scenario == "tampered") request.Headers.TryAddWithoutValidation("X-Hub-Signature-256", Sign(Body));
            using var response = await client.SendAsync(request);
            Assert.Equal(scenario == "valid" ? HttpStatusCode.InternalServerError : scenario == "chunked-oversized" ? HttpStatusCode.RequestEntityTooLarge : HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(scenario == "valid" ? 1 : 0, handler.Calls);
            Assert.Equal(scenario == "valid" ? 1 : 0, flowLogger.Entries);
        }
        finally { await host.StopAsync(); }
    }

    private sealed class ReceiptHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class FlowLogger : ILogger<WhatsAppFlowService>
    {
        public int Entries { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (formatter(state, exception).Contains("Procesando mensaje entrante", StringComparison.Ordinal)) Entries++;
        }
    }
}

using System.Net;
using System.Text.Json;
using Firebase.Controllers;
using Firebase.Models;
using Firebase.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Firebase.Tests.Services;

public class WhatsAppOperationTests
{
    private sealed class ScopeProvider : ILoggerProvider, ISupportExternalScope
    {
        private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();
        public List<(string Category, Dictionary<string, object> Scopes)> Entries { get; } = new();
        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;
        public ILogger CreateLogger(string categoryName) => new RecordingLogger(this, categoryName);
        public void Dispose() { }

        private sealed class RecordingLogger(ScopeProvider provider, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => provider._scopes.Push(state);
            public bool IsEnabled(LogLevel level) => true;
            public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var values = new Dictionary<string, object>();
                provider._scopes.ForEachScope((scope, target) =>
                {
                    if (scope is IEnumerable<KeyValuePair<string, object>> items)
                        foreach (var item in items) target[item.Key] = item.Value;
                }, values);
                provider.Entries.Add((category, values));
            }
        }
    }

    // Injects a typed failure at the public flow boundary; this is not a real reply or persistence test.
    private sealed class InjectingFlowLogger : ILogger<WhatsAppFlowService>
    {
        public int Errors { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (level == LogLevel.Information) throw new WhatsAppOperationException("text", "auth", 401, 190, 463);
            if (level == LogLevel.Error) Errors++;
        }
    }

    private sealed class Handler(HttpStatusCode status) : HttpMessageHandler
    {
        public List<string> Requests { get; } = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(status) { Content = new StringContent("private-response") };
        }
    }

    private static readonly IOptions<WhatsAppSettings> Settings = Options.Create(new WhatsAppSettings
    {
        AccessToken = "private-token", VerifyToken = "private-verify", PhoneNumberId = "123"
    });

    private static WhatsAppFlowService Flow(MetaWhatsAppService meta, ILogger<WhatsAppFlowService> logger) =>
        new(null!, meta, null!, null!, null!, null!, null!, null!, logger);

    private static void AssertSafeLogs<T>(Mock<ILogger<T>> logger)
    {
        foreach (var entry in logger.Invocations.Where(i => i.Method.Name == "Log"))
        {
            var text = entry.Arguments[2].ToString()!;
            Assert.DoesNotContain("private-", text);
            Assert.Null(entry.Arguments[3]);
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.OK)]
    public async Task ReadFailureOrSuccess_ContinuesToFlow_AndFlowFailureReturns500WithoutFallback(HttpStatusCode status)
    {
        var handler = new Handler(status);
        var metaLog = new Mock<ILogger<MetaWhatsAppService>>();
        var flowLog = new Mock<ILogger<WhatsAppFlowService>>();
        var controllerLog = new Mock<ILogger<WhatsAppWebhookController>>();
        var meta = new MetaWhatsAppService(new HttpClient(handler), Settings, metaLog.Object);
        var controller = new WhatsAppWebhookController(Settings, Flow(meta, flowLog.Object), meta, controllerLog.Object);
        using var body = JsonDocument.Parse("""
            {"entry":[{"changes":[{"field":"messages","value":{"messaging_product":"whatsapp",
            "messages":[{"from":"private-phone","id":"private-id","timestamp":"123","type":"text",
            "text":{"body":"private-body"}}]}}]}]}
            """);

        var result = await controller.ReceiveMessage(body.RootElement);

        Assert.Equal(500, Assert.IsType<StatusCodeResult>(result).StatusCode);
        Assert.Single(handler.Requests);
        Assert.Equal("read", JsonDocument.Parse(handler.Requests[0]).RootElement.GetProperty("status").GetString());
        Assert.Contains(flowLog.Invocations, i => i.Method.Name == "Log" && i.Arguments[2].ToString()!.Contains("Procesando mensaje"));
        Assert.Contains(flowLog.Invocations, i => i.Method.Name == "Log" && i.Arguments[2].ToString()!.Contains("WhatsApp flow failed"));
        if (status != HttpStatusCode.OK)
            Assert.Contains(controllerLog.Invocations, i => i.Method.Name == "Log" && i.Arguments[2].ToString()!.Contains("read receipt failed"));
        var scopes = controllerLog.Invocations.Where(i => i.Method.Name == "BeginScope").Select(i => i.Arguments[0]).OfType<Dictionary<string, object>>().ToArray();
        Assert.Contains(scopes, s => s.ContainsKey("WhatsAppRequestId"));
        Assert.Contains(scopes, s => s.ContainsKey("WhatsAppMessageId"));
        Assert.DoesNotContain(scopes.SelectMany(s => s.Values), v => v.ToString()!.Contains("private-"));
        AssertSafeLogs(controllerLog);
        AssertSafeLogs(flowLog);
        AssertSafeLogs(metaLog);
    }

    [Fact]
    public async Task InjectedTypedFlowFailure_ReturnsSafeStructured500_AndOneFatalLog()
    {
        var handler = new Handler(HttpStatusCode.OK);
        var meta = new MetaWhatsAppService(new HttpClient(handler), Settings, new Mock<ILogger<MetaWhatsAppService>>().Object);
        var flowLog = new InjectingFlowLogger();
        var controllerLog = new Mock<ILogger<WhatsAppWebhookController>>();
        var controller = new WhatsAppWebhookController(Settings, Flow(meta, flowLog), meta, controllerLog.Object);
        using var body = JsonDocument.Parse("""
            {"entry":[{"changes":[{"field":"messages","value":{"messaging_product":"whatsapp",
            "messages":[{"from":"private-phone","id":"private-id","timestamp":"123","type":"text",
            "text":{"body":"private-body"}}]}}]}]}
            """);

        var result = Assert.IsType<ObjectResult>(await controller.ReceiveMessage(body.RootElement));
        Assert.Equal(500, result.StatusCode);
        var json = JsonSerializer.Serialize(result.Value);
        Assert.DoesNotContain("private-", json);
        using var response = JsonDocument.Parse(json);
        var root = response.RootElement;
        Assert.Equal(6, root.EnumerateObject().Count());
        Assert.Equal("WhatsApp processing failed", root.GetProperty("message").GetString());
        Assert.Equal("text", root.GetProperty("operation").GetString());
        Assert.Equal("auth", root.GetProperty("classification").GetString());
        Assert.Equal(401, root.GetProperty("upstreamStatus").GetInt32());
        Assert.Equal(190, root.GetProperty("metaErrorCode").GetInt32());
        Assert.Equal(463, root.GetProperty("metaErrorSubcode").GetInt32());
        Assert.Equal(0, flowLog.Errors);
        var fatal = Assert.Single(controllerLog.Invocations.Where(i => i.Method.Name == "Log" && Equals(i.Arguments[0], LogLevel.Error)));
        Assert.Contains("190", fatal.Arguments[2].ToString());
        Assert.Contains("463", fatal.Arguments[2].ToString());
        AssertSafeLogs(controllerLog);
        Assert.Single(handler.Requests); // read receipt only; no reply was attempted
    }

    [Fact]
    public async Task IncomingMessageId_IsStableAndCorrelatedAcrossDownstreamCategories()
    {
        var provider = new ScopeProvider();
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(provider));
        var handler = new Handler(HttpStatusCode.OK);
        var meta = new MetaWhatsAppService(new HttpClient(handler), Settings, factory.CreateLogger<MetaWhatsAppService>());
        var controller = new WhatsAppWebhookController(Settings, Flow(meta, factory.CreateLogger<WhatsAppFlowService>()), meta, factory.CreateLogger<WhatsAppWebhookController>());
        using var body = JsonDocument.Parse("""
            {"entry":[{"changes":[{"field":"messages","value":{"messaging_product":"whatsapp",
            "messages":[{"from":"private-phone","id":"wamid.example","timestamp":"123","type":"text",
            "text":{"body":"private-body"}}]}}]}]}
            """);
        await controller.ReceiveMessage(body.RootElement);
        await controller.ReceiveMessage(body.RootElement);

        var expected = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("wamid.example")));
        foreach (var category in new[] { typeof(MetaWhatsAppService).FullName!, typeof(WhatsAppFlowService).FullName! })
        {
            var entries = provider.Entries.Where(e => e.Category == category).ToArray();
            Assert.True(entries.Length >= 2);
            Assert.All(entries, e =>
            {
                Assert.Equal(expected, e.Scopes["WhatsAppMessageId"]);
                Assert.True(e.Scopes.ContainsKey("WhatsAppRequestId"));
            });
        }
        var requests = provider.Entries.Where(e => e.Category == typeof(MetaWhatsAppService).FullName).Select(e => e.Scopes["WhatsAppRequestId"]).Distinct().ToArray();
        Assert.Equal(2, requests.Length);
    }

    [Fact]
    public async Task FlowDependencyFailure_PropagatesWithoutSendingFallback()
    {
        var handler = new Handler(HttpStatusCode.OK);
        var logger = new Mock<ILogger<WhatsAppFlowService>>();
        var meta = new MetaWhatsAppService(new HttpClient(handler), Settings, new Mock<ILogger<MetaWhatsAppService>>().Object);
        await Assert.ThrowsAsync<NullReferenceException>(() => Flow(meta, logger.Object).ProcessMessage("private-phone", "private-body"));
        Assert.Empty(handler.Requests);
        AssertSafeLogs(logger);
    }
}

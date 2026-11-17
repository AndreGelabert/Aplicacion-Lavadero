using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Firebase.Models;
using Firebase.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Firebase.Tests.Services;

public class MetaWhatsAppServiceTests
{
    private sealed class Handler(HttpStatusCode status, bool transport = false, string responseBody = "secret response") : HttpMessageHandler
    {
        public string Body = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (transport) throw new HttpRequestException("secret token secret message");
            return new HttpResponseMessage(status) { Content = new StringContent(responseBody) };
        }
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            throw exception;
        }
    }

    private static MetaWhatsAppService Create(HttpMessageHandler handler, Mock<ILogger<MetaWhatsAppService>> logger) => new(
        new HttpClient(handler), Options.Create(new WhatsAppSettings { AccessToken = "secret token", PhoneNumberId = "123", VerifyToken = "verify" }), logger.Object);

    private static Task<bool> Invoke(MetaWhatsAppService service, string operation) => operation switch
    {
        "text" => service.SendTextMessage("5491111111111", "secret message"),
        "buttons" => service.SendButtonMessage("5491111111111", "secret message", Enumerable.Range(0, 4).Select(i => (i.ToString(), new string('b', 25))).ToList()),
        "list" => service.SendListMessage("5491111111111", "secret message", "button", "section", Enumerable.Range(0, 11).Select(i => (i.ToString(), new string('t', 30), new string('d', 80))).ToList()),
        _ => service.MarkMessageAsRead("secret-id")
    };

    [Theory]
    [InlineData("text")][InlineData("buttons")][InlineData("list")][InlineData("read")]
    public async Task Success_ReturnsTrueAndPreservesPayloadLimits(string operation)
    {
        var handler = new Handler(HttpStatusCode.OK);
        Assert.True(await Invoke(Create(handler, new()), operation));
        using var payload = JsonDocument.Parse(handler.Body);
        if (operation == "buttons")
        {
            var buttons = payload.RootElement.GetProperty("interactive").GetProperty("action").GetProperty("buttons");
            Assert.Equal(3, buttons.GetArrayLength());
            Assert.Equal(20, buttons[0].GetProperty("reply").GetProperty("title").GetString()!.Length);
        }
        if (operation == "list")
        {
            var rows = payload.RootElement.GetProperty("interactive").GetProperty("action").GetProperty("sections")[0].GetProperty("rows");
            Assert.Equal(10, rows.GetArrayLength());
            Assert.Equal(24, rows[0].GetProperty("title").GetString()!.Length);
            Assert.Equal(72, rows[0].GetProperty("description").GetString()!.Length);
        }
        if (operation == "text") Assert.Equal("541111111111", payload.RootElement.GetProperty("to").GetString());
    }

    [Theory]
    [InlineData("text", 401, "auth")][InlineData("buttons", 403, "auth")]
    [InlineData("list", 400, "permanent")][InlineData("read", 429, "transient")]
    [InlineData("text", 503, "transient")]
    public async Task HttpFailure_IsClassifiedAndDoesNotLeak(string operation, int status, string classification)
    {
        var logger = new Mock<ILogger<MetaWhatsAppService>>();
        var exception = await Assert.ThrowsAsync<WhatsAppOperationException>(() => Invoke(Create(new Handler((HttpStatusCode)status), logger), operation));
        Assert.Equal(operation, exception.Operation);
        Assert.Equal(classification, exception.Classification);
        Assert.Equal(status, exception.StatusCode);
        Assert.DoesNotContain("secret", exception.ToString());
        Assert.DoesNotContain(logger.Invocations, i => i.Method.Name == "Log" && i.Arguments[2].ToString()!.Contains("secret"));
    }

    [Fact]
    public async Task HttpFailure_ExtractsOnlyNumericMetadata()
    {
        var logger = new Mock<ILogger<MetaWhatsAppService>>();
        var handler = new Handler(HttpStatusCode.Unauthorized, responseBody: "{\"error\":{\"code\":190,\"error_subcode\":463,\"message\":\"secret token\"}}");
        var ex = await Assert.ThrowsAsync<WhatsAppOperationException>(() => Invoke(Create(handler, logger), "text"));
        Assert.Equal(190, ex.MetaErrorCode);
        Assert.Equal(463, ex.MetaErrorSubcode);
        Assert.DoesNotContain("secret", ex.ToString());
        Assert.DoesNotContain(logger.Invocations, i => i.Method.Name == "Log" && i.Arguments[2].ToString()!.Contains("secret"));
    }

    [Theory]
    [InlineData("")][InlineData("not json")][InlineData("{\"error\":{\"code\":\"190\"}}")]
    public async Task InvalidDiagnostics_PreserveHttpFailure(string body)
    {
        var ex = await Assert.ThrowsAsync<WhatsAppOperationException>(() => Invoke(Create(new Handler(HttpStatusCode.Unauthorized, responseBody: body), new()), "text"));
        Assert.Equal(401, ex.StatusCode);
        Assert.Equal("auth", ex.Classification);
        Assert.Null(ex.MetaErrorCode);
        Assert.Null(ex.MetaErrorSubcode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TransportDiagnostics_AreStructuredAndNeverLeak(bool canceled)
    {
        const string secret = "injected-secret-https://private.example/5491111111111";
        Exception failure = canceled
            ? new TaskCanceledException(secret, new Exception(secret))
            : new HttpRequestException(HttpRequestError.ConnectionError, secret, new SocketException((int)SocketError.ConnectionRefused));
        var handler = new ThrowingHandler(failure);
        var logger = new Mock<ILogger<MetaWhatsAppService>>();
        var result = await Assert.ThrowsAsync<WhatsAppOperationException>(() => Invoke(Create(handler, logger), "text"));
        Assert.Equal("uncertain transport", result.Classification);
        Assert.Null(result.StatusCode);
        Assert.Equal(1, handler.Calls);
        Assert.DoesNotContain(secret, result.ToString());
        var log = Assert.Single(logger.Invocations.Where(i => i.Method.Name == "Log"));
        Assert.Equal(LogLevel.Warning, log.Arguments[0]);
        Assert.Null(log.Arguments[3]);
        var fields = ((IEnumerable<KeyValuePair<string, object?>>)log.Arguments[2]).ToDictionary(x => x.Key, x => x.Value);
        Assert.Equal("text", fields["Operation"]);
        Assert.Equal(canceled ? "TaskCanceledException" : "HttpRequestException", fields["ExceptionType"]);
        if (!canceled)
        {
            Assert.Equal(HttpRequestError.ConnectionError, fields["HttpRequestError"]);
            Assert.Equal(SocketError.ConnectionRefused, fields["SocketErrorCode"]);
        }
        var allLogText = string.Join(" ", fields.Select(x => $"{x.Key}={x.Value}")) + log.Arguments[2];
        foreach (var forbidden in new[] { secret, "private.example", "5491111111111", "secret token", "secret message" })
            Assert.DoesNotContain(forbidden, allLogText);
    }

    [Theory]
    [InlineData("text")][InlineData("buttons")][InlineData("list")][InlineData("read")]
    public async Task TransportFailure_IsUncertainAndSafe(string operation)
    {
        var logger = new Mock<ILogger<MetaWhatsAppService>>();
        var exception = await Assert.ThrowsAsync<WhatsAppOperationException>(() => Invoke(Create(new Handler(HttpStatusCode.OK, true), logger), operation));
        Assert.Equal("uncertain transport", exception.Classification);
        Assert.Null(exception.StatusCode);
        Assert.DoesNotContain("secret", exception.ToString());
        Assert.DoesNotContain(logger.Invocations, i => i.Method.Name == "Log" && i.Arguments[2].ToString()!.Contains("secret"));
    }
}

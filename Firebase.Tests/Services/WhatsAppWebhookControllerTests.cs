using System.Text.Json;
using System.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Firebase.Controllers;
using Firebase.Models;
using Firebase.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Firebase.Tests.Services;

public class WhatsAppWebhookControllerTests
{
    private readonly Mock<ILogger<WhatsAppWebhookController>> _logger = new();

    private const string Secret = "synthetic-app-secret";
    private WhatsAppWebhookController CreateController(string? secret = Secret) => new(
        Options.Create(new WhatsAppSettings
        {
            AccessToken = "test-access", PhoneNumberId = "test-phone", VerifyToken = "test-verify", AppSecret = secret
        }), null!, null!, _logger.Object);

    private static string Sign(string json) => "sha256=" + Convert.ToHexString(
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(json))).ToLowerInvariant();

    private static void SetRequest(WhatsAppWebhookController controller, string json, string? signature)
    {
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        if (signature != null) controller.Request.Headers["X-Hub-Signature-256"] = signature;
    }

    private async Task Receive(string json, int expectedStatus = 200)
    {
        var controller = CreateController();
        SetRequest(controller, json, Sign(json));
        var result = await controller.ReceiveMessage();
        Assert.Equal(expectedStatus, Assert.IsAssignableFrom<StatusCodeResult>(result).StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("sha256=xyz")]
    [InlineData("sha1=0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
    [InlineData("sha256=0000000000000000000000000000000000000000000000000000000000000000")]
    public async Task UnauthenticatedMessage_IsRejectedBeforeProcessing(string signature)
    {
        const string json = "{\"entry\":[{\"changes\":[{\"field\":\"messages\",\"value\":{\"messaging_product\":\"whatsapp\",\"messages\":[{\"from\":\"test\",\"id\":\"id\",\"timestamp\":\"123\",\"type\":\"text\",\"text\":{\"body\":\"hello\"}}]}}]}]}";
        var controller = CreateController();
        SetRequest(controller, json, signature.Length > 0 ? signature : null);
        var result = await controller.ReceiveMessage();
        Assert.Equal(401, Assert.IsAssignableFrom<StatusCodeResult>(result).StatusCode);
        Assert.False(Logged(LogLevel.Information, "WhatsApp message received"));
    }

    [Fact]
    public async Task MissingSecret_RejectsCorrectSignature()
    {
        var controller = CreateController(null);
        SetRequest(controller, "{}", Sign("{}"));
        Assert.IsType<UnauthorizedResult>(await controller.ReceiveMessage());
    }

    [Fact]
    public async Task TamperedBody_AndDuplicateSignatures_AreRejected()
    {
        var controller = CreateController();
        SetRequest(controller, "{ }", Sign("{}"));
        Assert.IsType<UnauthorizedResult>(await controller.ReceiveMessage());
        SetRequest(controller, "{}", Sign("{}"));
        controller.Request.Headers.Append("X-Hub-Signature-256", Sign("{}"));
        Assert.IsType<UnauthorizedResult>(await controller.ReceiveMessage());
        Assert.False(Logged(LogLevel.Information, "WhatsApp message received"));
    }

    [Theory]
    [InlineData("absent")]
    [InlineData("understated")]
    [InlineData("chunked")]
    [InlineData("oversized")]
    public async Task OversizedBody_IsRejectedBeforeProcessing(string framing)
    {
        const int limit = 1024 * 1024;
        var json = "{\"entry\":[]}" + new string(' ', limit);
        var controller = CreateController();
        SetRequest(controller, json, Sign(json));
        if (framing == "understated") controller.Request.ContentLength = 1;
        if (framing == "oversized") controller.Request.ContentLength = json.Length;
        if (framing == "chunked") controller.Request.Headers.TransferEncoding = "chunked";
        var result = await controller.ReceiveMessage();
        Assert.Equal(413, Assert.IsAssignableFrom<StatusCodeResult>(result).StatusCode);
        Assert.False(Logged(LogLevel.Information, "WhatsApp message received"));
        Assert.False(Logged(LogLevel.Information, "status event received"));
    }

    [Fact]
    public async Task SignedBody_AtLimit_IsAccepted()
    {
        const int limit = 1024 * 1024;
        const string envelope = "{\"entry\":[]}";
        await Receive(envelope + new string(' ', limit - envelope.Length));
    }

    [Fact]
    public async Task SignedMalformedJson_ReturnsBadRequest()
    {
        await Receive("{", 400);
    }

    private bool Logged(LogLevel level, string text) => _logger.Invocations.Any(call =>
        call.Method.Name == "Log" && Equals(call.Arguments[0], level) &&
        call.Arguments[2].ToString()!.Contains(text, StringComparison.Ordinal));

    [Fact]
    public async Task AccountUpdate_IsIgnoredWithoutError()
    {
        await Receive("""
            {"object":"whatsapp_business_account","entry":[{"id":"account","changes":[
              {"field":"account_update","value":{"event":"WEBHOOK_CONFIG_UPDATED","details":{"change_action":"subscribe"}}}
            ]}]}
            """);
        Assert.False(_logger.Invocations.Any(call => call.Method.Name == "Log" &&
            Equals(call.Arguments[0], LogLevel.Error)));
    }

    [Fact]
    public async Task MixedBatch_AccountEventDoesNotPreventMessageStatusProcessing()
    {
        await Receive("""
            {"object":"whatsapp_business_account","entry":[{"id":"account","changes":[
              {"field":"account_update","value":{"event":"WEBHOOK_CONFIG_UPDATED"}},
              {"field":"messages","value":{"messaging_product":"whatsapp","statuses":[
                {"id":"status-after-account","status":"delivered","timestamp":"123","recipient_id":"test"}
              ]}}
            ]}]}
            """);
        Assert.True(Logged(LogLevel.Information, "status event received"));
    }

    [Fact]
    public async Task MalformedMessageChange_Returns400BeforeProcessingBatch()
    {
        await Receive("""
            {"object":"whatsapp_business_account","entry":[{"id":"account","changes":[
              {"field":"messages","value":{"statuses":[]}},
              {"field":"messages","value":{"messaging_product":"whatsapp","statuses":[
                {"id":"status-after-invalid","status":"read","timestamp":"123","recipient_id":"test"}
              ]}}
            ]}]}
            """, 400);
        Assert.True(Logged(LogLevel.Warning, "Invalid"));
        Assert.False(Logged(LogLevel.Information, "status-after-invalid"));
    }

    [Fact]
    public async Task MessageStatus_StillLogsStatusAndAcknowledges()
    {
        await Receive("""
            {"object":"whatsapp_business_account","entry":[{"id":"account","changes":[
              {"field":"messages","value":{"messaging_product":"whatsapp","statuses":[
                {"id":"existing-status","status":"delivered","timestamp":"123","recipient_id":"test"}
              ]}}
            ]}]}
            """);
        Assert.True(Logged(LogLevel.Information, "status event received"));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"entry\":[{}]}")]
    [InlineData("{\"entry\":[{\"changes\":[{}]}]}")]
    [InlineData("{\"entry\":[{\"changes\":[{\"field\":\"messages\",\"value\":null}]}]}")]
    public async Task InvalidEnvelopeOrChange_Returns400(string json)
    {
        await Receive(json, 400);
    }

    [Fact]
    public async Task ProcessingException_Returns500AndLogsError()
    {
        // Dependencies are absent deliberately: a supported message reaches processing
        // and fails without contacting Meta or persistent storage.
        await Receive("""
            {"entry":[{"changes":[{"field":"messages","value":{
              "messaging_product":"whatsapp","messages":[{
                "from":"test","id":"test-message","timestamp":"123","type":"text","text":{"body":"hello"}
              }]
            }}]}]}
            """, 500);
        Assert.True(Logged(LogLevel.Error, "webhook"));
    }

    [Fact]
    public async Task InvalidLaterChange_DoesNotProcessEarlierValidChange()
    {
        await Receive("""
            {"entry":[{"changes":[
              {"field":"messages","value":{"messaging_product":"whatsapp","statuses":[
                {"id":"must-not-process","status":"read","timestamp":"123","recipient_id":"test"}
              ]}},
              {"field":"messages","value":{}}
            ]}]}
            """, 400);
        Assert.False(Logged(LogLevel.Information, "must-not-process"));
    }

    [Fact]
    public async Task UnsupportedMessage_IsIntentionallyAcknowledged()
    {
        await Receive("""
            {"entry":[{"changes":[{"field":"messages","value":{
              "messaging_product":"whatsapp","messages":[{
                "from":"test","id":"test-message","timestamp":"123","type":"image"
              }]
            }}]}]}
            """);
    }

    [Fact]
    public void Verification_PreservesChallengeAndRejectsWrongToken()
    {
        var controller = CreateController();
        Assert.Equal("challenge", Assert.IsType<OkObjectResult>(
            controller.VerifyWebhook("subscribe", "test-verify", "challenge")).Value);
        Assert.IsType<ForbidResult>(controller.VerifyWebhook("subscribe", "wrong", "challenge"));
        Assert.DoesNotContain(_logger.Invocations, call => call.Method.Name == "Log" &&
            (call.Arguments[2].ToString()!.Contains("test-verify") || call.Arguments[2].ToString()!.Contains("challenge")));
    }

    [Fact]
    public async Task UnsupportedMessage_UsesRequestAndMessageScopes()
    {
        await Receive("""
            {"entry":[{"changes":[{"field":"messages","value":{
              "messaging_product":"whatsapp","messages":[{
                "from":"private-phone","id":"private-id","timestamp":"123","type":"image"
              }]
            }}]}]}
            """);
        var scopes = _logger.Invocations.Where(i => i.Method.Name == "BeginScope").Select(i => i.Arguments[0]).OfType<Dictionary<string, object>>().ToList();
        Assert.Contains(scopes, s => s.ContainsKey("WhatsAppRequestId"));
        Assert.Contains(scopes, s => s.ContainsKey("WhatsAppMessageId"));
        Assert.DoesNotContain(scopes.SelectMany(s => s.Values), v => v.ToString()!.Contains("private"));
    }
}

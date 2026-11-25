using System.Text.Json;
using Firebase.Services;

namespace Firebase.Tests.Services;

public class WhatsAppInboxBatchAdapterTests
{
    private static JsonDocument Batch(string messages) => JsonDocument.Parse("{\"entry\":[{\"changes\":[{\"field\":\"messages\",\"value\":{\"messages\":[" + messages + "]}}]}]}");

    [Fact]
    public async Task PreservesExactMessageAndAcceptsDuplicatesAcrossBatches()
    {
        var stored = new Dictionary<string, string>();
        var adapter = new WhatsAppInboxBatchAdapter((id, payload) =>
        {
            if (stored.TryGetValue(id, out var existing)) return Task.FromResult(existing == payload ? WhatsAppWebhookInbox.Result.Duplicate : WhatsAppWebhookInbox.Result.Conflict);
            stored.Add(id, payload); return Task.FromResult(WhatsAppWebhookInbox.Result.Created);
        });
        const string message = "{ \"id\": \"wamid.1\", \"from\": \"private\", \"unknown\": 1 }";
        using var batch = Batch(message);
        Assert.True(await adapter.Register(batch.RootElement));
        Assert.True(await adapter.Register(batch.RootElement));
        Assert.Equal(message, Assert.Single(stored).Value);
        Assert.True(stored.ContainsKey("wamid.1"));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task InboxRejectionFailsBatchAndStops(int rejectedResult)
    {
        var rejection = (WhatsAppWebhookInbox.Result)rejectedResult;
        var calls = 0;
        var adapter = new WhatsAppInboxBatchAdapter((id, payload) => { calls++; return Task.FromResult(rejection); });
        using var batch = Batch("{\"id\":\"a\"},{\"id\":\"b\"}");
        Assert.False(await adapter.Register(batch.RootElement));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task StorageExceptionFailsClosedWithoutExposingException()
    {
        var calls = 0;
        var adapter = new WhatsAppInboxBatchAdapter((id, payload) => { calls++; throw new InvalidOperationException("private"); });
        using var batch = Batch("{\"id\":\"a\"},{\"id\":\"b\"}");
        Assert.False(await adapter.Register(batch.RootElement));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task MalformedLaterMessageRejectsEntireBatchBeforeWrites()
    {
        var calls = 0;
        var adapter = new WhatsAppInboxBatchAdapter((id, payload) => { calls++; return Task.FromResult(WhatsAppWebhookInbox.Result.Created); });
        using var batch = Batch("{\"id\":\"a\"},{\"from\":\"private\"}");
        Assert.False(await adapter.Register(batch.RootElement));
        Assert.Equal(0, calls);
    }
}

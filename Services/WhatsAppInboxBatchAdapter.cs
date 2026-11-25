using System.Text.Json;
using Google.Cloud.Firestore;

namespace Firebase.Services;

/// <summary>
/// Isolated adapter for a caller-authenticated batch; it does not authenticate or process
/// messages. Payload identity is the exact raw JSON of each message, including whitespace.
/// Registrations are individually transactional, not atomic across the batch: a failed
/// batch may leave earlier messages registered and must be retried unchanged.
/// </summary>
internal sealed class WhatsAppInboxBatchAdapter
{
    private readonly Func<string, string, Task<WhatsAppWebhookInbox.Result>> register;

    internal WhatsAppInboxBatchAdapter(FirestoreDb db)
        : this((id, payload) => WhatsAppWebhookInbox.Register(db, id, payload))
    {
        ArgumentNullException.ThrowIfNull(db);
    }

    internal WhatsAppInboxBatchAdapter(Func<string, string, Task<WhatsAppWebhookInbox.Result>> register) =>
        this.register = register ?? throw new ArgumentNullException(nameof(register));

    internal async Task<bool> Register(JsonElement batch)
    {
        var messages = new List<(string Id, string Payload)>();
        if (batch.ValueKind != JsonValueKind.Object || !batch.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array) return false;
        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array) return false;
            foreach (var change in changes.EnumerateArray())
            {
                if (change.ValueKind != JsonValueKind.Object || !change.TryGetProperty("field", out var field) || field.ValueKind != JsonValueKind.String) return false;
                if (field.GetString() != "messages") continue;
                if (!change.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Object) return false;
                if (!value.TryGetProperty("messages", out var items)) continue;
                if (items.ValueKind != JsonValueKind.Array) return false;
                foreach (var item in items.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(id.GetString())) return false;
                    messages.Add((id.GetString()!, item.GetRawText()));
                }
            }
        }
        foreach (var (id, payload) in messages)
        {
            try
            {
                var result = await register(id, payload);
                if (result != WhatsAppWebhookInbox.Result.Created && result != WhatsAppWebhookInbox.Result.Duplicate) return false;
            }
            catch (Exception) { return false; }
        }
        return true;
    }
}

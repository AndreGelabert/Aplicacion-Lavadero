using System.Text.Json.Serialization;

namespace Firebase.Models.WhatsApp;

/// <summary>
/// Payload que Meta envía al webhook
/// </summary>
public class WhatsAppWebhookPayload
{
    [JsonPropertyName("object")]
    public required string Object { get; set; }

    [JsonPropertyName("entry")]
    public required List<WhatsAppEntry> Entry { get; set; }
}

public class WhatsAppEntry
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("changes")]
    public required List<WhatsAppChange> Changes { get; set; }
}

public class WhatsAppChange
{
    [JsonPropertyName("value")]
    public required WhatsAppValue Value { get; set; }

    [JsonPropertyName("field")]
    public required string Field { get; set; }
}

public class WhatsAppValue
{
    [JsonPropertyName("messaging_product")]
    public required string MessagingProduct { get; set; }

    [JsonPropertyName("metadata")]
    public WhatsAppMetadata? Metadata { get; set; }

    [JsonPropertyName("contacts")]
    public List<WhatsAppContact>? Contacts { get; set; }

    [JsonPropertyName("messages")]
    public List<WhatsAppIncomingMessage>? Messages { get; set; }

    [JsonPropertyName("statuses")]
    public List<WhatsAppStatus>? Statuses { get; set; }
}

public class WhatsAppMetadata
{
    [JsonPropertyName("display_phone_number")]
    public required string DisplayPhoneNumber { get; set; }

    [JsonPropertyName("phone_number_id")]
    public required string PhoneNumberId { get; set; }
}

public class WhatsAppContact
{
    [JsonPropertyName("profile")]
    public WhatsAppProfile? Profile { get; set; }

    [JsonPropertyName("wa_id")]
    public required string WaId { get; set; }
}

public class WhatsAppProfile
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }
}

public class WhatsAppIncomingMessage
{
    [JsonPropertyName("from")]
    public required string From { get; set; }

    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("timestamp")]
    public required string Timestamp { get; set; }

    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("text")]
    public WhatsAppTextMessage? Text { get; set; }

    [JsonPropertyName("image")]
    public WhatsAppImageMessage? Image { get; set; }

    [JsonPropertyName("button")]
    public WhatsAppButtonReply? Button { get; set; }

    [JsonPropertyName("interactive")]
    public WhatsAppInteractiveReply? Interactive { get; set; }
}

public class WhatsAppTextMessage
{
    [JsonPropertyName("body")]
    public required string Body { get; set; }
}

public class WhatsAppImageMessage
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("mime_type")]
    public string? MimeType { get; set; }

    [JsonPropertyName("caption")]
    public string? Caption { get; set; }
}

public class WhatsAppButtonReply
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("title")]
    public required string Title { get; set; }
    
    // Campos opcionales para compatibilidad
    [JsonPropertyName("payload")]
    public string? Payload { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

public class WhatsAppInteractiveReply
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("button_reply")]
    public WhatsAppButtonReply? ButtonReply { get; set; }

    [JsonPropertyName("list_reply")]
    public WhatsAppListReply? ListReply { get; set; }
}

public class WhatsAppListReply
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("title")]
    public required string Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public class WhatsAppStatus
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("status")]
    public required string Status { get; set; }

    [JsonPropertyName("timestamp")]
    public required string Timestamp { get; set; }

    [JsonPropertyName("recipient_id")]
    public required string RecipientId { get; set; }
}

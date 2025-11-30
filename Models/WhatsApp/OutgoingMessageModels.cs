using System.Text.Json.Serialization;

namespace Firebase.Models.WhatsApp;

/// <summary>
/// Mensaje de texto saliente
/// </summary>
public class WhatsAppTextOutgoingMessage
{
    [JsonPropertyName("messaging_product")]
    public string MessagingProduct { get; set; } = "whatsapp";

    [JsonPropertyName("recipient_type")]
    public string RecipientType { get; set; } = "individual";

    [JsonPropertyName("to")]
    public required string To { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    public required WhatsAppText Text { get; set; }
}

public class WhatsAppText
{
    [JsonPropertyName("body")]
    public required string Body { get; set; }

    [JsonPropertyName("preview_url")]
    public bool? PreviewUrl { get; set; }
}

/// <summary>
/// Mensaje interactivo con botones
/// </summary>
public class WhatsAppInteractiveMessage
{
    [JsonPropertyName("messaging_product")]
    public string MessagingProduct { get; set; } = "whatsapp";

    [JsonPropertyName("to")]
    public required string To { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "interactive";

    [JsonPropertyName("interactive")]
    public required WhatsAppInteractive Interactive { get; set; }
}

public class WhatsAppInteractive
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("header")]
    public WhatsAppInteractiveHeader? Header { get; set; }

    [JsonPropertyName("body")]
    public required WhatsAppInteractiveBody Body { get; set; }

    [JsonPropertyName("footer")]
    public WhatsAppInteractiveFooter? Footer { get; set; }

    [JsonPropertyName("action")]
    public WhatsAppInteractiveAction? Action { get; set; }
}

public class WhatsAppInteractiveHeader
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

public class WhatsAppInteractiveBody
{
    [JsonPropertyName("text")]
    public required string Text { get; set; }
}

public class WhatsAppInteractiveFooter
{
    [JsonPropertyName("text")]
    public required string Text { get; set; }
}

public class WhatsAppInteractiveAction
{
    [JsonPropertyName("buttons")]
    public List<WhatsAppButton>? Buttons { get; set; }

    [JsonPropertyName("button")]
    public string? Button { get; set; }

    [JsonPropertyName("sections")]
    public List<WhatsAppSection>? Sections { get; set; }
}

public class WhatsAppButton
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "reply";

    [JsonPropertyName("reply")]
    public required WhatsAppReply Reply { get; set; }
}

public class WhatsAppReply
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("title")]
    public required string Title { get; set; }
}

public class WhatsAppSection
{
    [JsonPropertyName("title")]
    public required string Title { get; set; }

    [JsonPropertyName("rows")]
    public required List<WhatsAppRow> Rows { get; set; }
}

public class WhatsAppRow
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("title")]
    public required string Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

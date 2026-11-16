namespace Firebase.Models;

public sealed class EmailSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string From { get; set; } = "onboarding@resend.dev";
    public string ReplyTo { get; set; } = string.Empty;
    public string DevelopmentAllowedRecipient { get; set; } = string.Empty;
}

public sealed record EmailMessage(
    string To,
    string Subject,
    string Html,
    string? ReplyTo = null);

namespace Firebase.Services;

public sealed class WhatsAppOperationException : Exception
{
    public string Operation { get; }
    public string Classification { get; }
    public int? StatusCode { get; }

    public WhatsAppOperationException(string operation, string classification, int? statusCode = null)
        : base($"WhatsApp operation {operation} failed ({classification}, status {statusCode?.ToString() ?? "none"}).")
    {
        Operation = operation;
        Classification = classification;
        StatusCode = statusCode;
    }
}

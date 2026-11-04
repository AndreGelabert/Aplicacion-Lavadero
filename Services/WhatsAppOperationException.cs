namespace Firebase.Services;

public sealed class WhatsAppOperationException : Exception
{
    public string Operation { get; }
    public string Classification { get; }
    public int? StatusCode { get; }
    public int? MetaErrorCode { get; }
    public int? MetaErrorSubcode { get; }

    public WhatsAppOperationException(string operation, string classification, int? statusCode = null, int? metaErrorCode = null, int? metaErrorSubcode = null)
        : base($"WhatsApp operation {operation} failed ({classification}, status {statusCode?.ToString() ?? "none"}).")
    {
        Operation = operation;
        Classification = classification;
        StatusCode = statusCode;
        MetaErrorCode = metaErrorCode;
        MetaErrorSubcode = metaErrorSubcode;
    }
}

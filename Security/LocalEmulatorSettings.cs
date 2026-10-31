namespace Firebase.Security;

/// <summary>Validates the explicitly configured local Firebase emulators before SDK initialization.</summary>
public static class LocalEmulatorSettings
{
    public const string ProjectId = "demo-local";
    public const string FirestoreHost = "127.0.0.1:8189";
    public const string AuthHost = "127.0.0.1:9099";

    public static bool Validate(bool development, string? firestore, string? auth)
    {
        if (string.IsNullOrWhiteSpace(firestore) && string.IsNullOrWhiteSpace(auth)) return false;
        if (!development || firestore != FirestoreHost || auth != AuthHost)
            throw new InvalidOperationException("Local Firebase emulators require Development and both configured loopback endpoints.");
        return true;
    }

    public static string IdentityToolkitUrl(bool local, string action, string apiKey)
    {
        var origin = local ? $"http://{AuthHost}/identitytoolkit.googleapis.com" : "https://identitytoolkit.googleapis.com";
        return $"{origin}/v1/accounts:{action}?key={apiKey}";
    }
}

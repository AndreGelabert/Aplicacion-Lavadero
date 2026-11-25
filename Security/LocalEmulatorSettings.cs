namespace Firebase.Security;

/// <summary>Validates the explicitly configured local Firebase emulators before SDK initialization.</summary>
public static class LocalEmulatorSettings
{
    public const string ProjectId = "demo-local";
    public const string FirestoreHost = "127.0.0.1:8189";
    public const string AuthHost = "127.0.0.1:9099";
    public const string SuiteFirestoreHost = "127.0.0.1:8190";
    public const string SuiteAuthHost = "127.0.0.1:9199";

    public static bool Validate(bool development, string? firestore, string? auth)
    {
        if (string.IsNullOrWhiteSpace(firestore) && string.IsNullOrWhiteSpace(auth)) return false;
        var seedEndpoints = firestore == FirestoreHost && auth == AuthHost;
        var suiteEndpoints = firestore == SuiteFirestoreHost && auth == SuiteAuthHost;
        if (!development || (!seedEndpoints && !suiteEndpoints))
            throw new InvalidOperationException("Local Firebase emulators require Development and both configured loopback endpoints.");
        return true;
    }

    // The caller must supply the startup-validated local flag; this method rechecks
    // endpoint pairs, but cannot independently establish the host environment.
    public static string IdentityToolkitUrl(bool local, string action, string apiKey)
    {
        var origin = "https://identitytoolkit.googleapis.com";
        if (local)
        {
            var firestore = Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST");
            var auth = Environment.GetEnvironmentVariable("FIREBASE_AUTH_EMULATOR_HOST");
            if (!Validate(true, firestore, auth))
                throw new InvalidOperationException("Local Firebase emulators require Development and both configured loopback endpoints.");
            origin = $"http://{auth}/identitytoolkit.googleapis.com";
        }
        return $"{origin}/v1/accounts:{action}?key={apiKey}";
    }
}

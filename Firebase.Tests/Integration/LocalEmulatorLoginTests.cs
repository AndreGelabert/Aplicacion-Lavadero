using Firebase.Security;
using Firebase.Services;
using FirebaseAdmin;
using Google.Api.Gax;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Firebase.Tests.Integration;

public class LocalEmulatorLoginTests
{
    [LocalSeedFact]
    public async Task PersistedSyntheticUserCanSignInWithoutProductionFirebase()
    {
        var password = Environment.GetEnvironmentVariable("LOCAL_EMULATOR_PASSWORD")!;
        var db = new FirestoreDbBuilder { ProjectId = LocalEmulatorSettings.ProjectId, EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var app = FirebaseApp.Create(new AppOptions { ProjectId = LocalEmulatorSettings.ProjectId, Credential = GoogleCredential.FromAccessToken("owner") });
        try
        {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["LocalEmulators:Enabled"] = "true" }).Build();
        using var client = new HttpClient();
        var service = new AuthenticationService(configuration, db, new AuditService(db), client);
        var clienteRef = db.Collection("clientes").Document("local-seed-cliente");
        var original = await clienteRef.GetSnapshotAsync();
        Assert.True(original.Exists);
        await LocalEmulatorSeed.RunAsync(db, app, development: true);
        Assert.Equal(original.UpdateTime, (await clienteRef.GetSnapshotAsync()).UpdateTime);

        var result = await service.AuthenticateWithEmailAsync(LocalEmulatorSeed.Email, password);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(LocalEmulatorSeed.UserId, result.UserInfo?.Uid);
        Assert.Equal("Pruebas locales", result.UserInfo?.Role);
        Assert.False((await service.AuthenticateWithEmailAsync(LocalEmulatorSeed.Email, "invalid-password")).IsSuccess);
        }
        finally
        {
            app.Delete();
        }
    }
}

public sealed class LocalSeedFactAttribute : FactAttribute
{
    public LocalSeedFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST") != LocalEmulatorSettings.FirestoreHost ||
            Environment.GetEnvironmentVariable("FIREBASE_AUTH_EMULATOR_HOST") != LocalEmulatorSettings.AuthHost ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LOCAL_EMULATOR_PASSWORD")))
            Skip = "Requires both local emulators and the synthetic test password.";
    }
}

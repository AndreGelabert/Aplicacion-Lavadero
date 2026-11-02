using System.Security.Cryptography;
using Firebase.Security;
using Firebase.Services;
using Firebase.Tests.Helpers;
using FirebaseAdmin;
using Google.Api.Gax;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Firebase.Tests.Integration;

[CollectionDefinition("Isolated emulator environment", DisableParallelization = true)]
public class IsolatedEmulatorCollection { }

[Collection("Isolated emulator environment")]
public class LocalEmulatorLoginTests
{
    [LocalSeedFact]
    public async Task DisposableSeedAndEmailLoginPreserveExistingData()
    {
        Assert.Equal("127.0.0.1:8190", Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST"));
        Assert.Equal("127.0.0.1:9199", Environment.GetEnvironmentVariable("FIREBASE_AUTH_EMULATOR_HOST"));
        var names = new[] { "FIRESTORE_EMULATOR_HOST", "FIREBASE_AUTH_EMULATOR_HOST", "LOCAL_EMULATOR_PASSWORD" };
        var previous = names.ToDictionary(n => n, Environment.GetEnvironmentVariable);
        FirebaseApp? app = null;
        try
        {
            // Build while the SDK sees only the disposable endpoint; the guard is changed afterwards.
            var db = new FirestoreDbBuilder { ProjectId = LocalEmulatorSettings.ProjectId, EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
            var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
            Environment.SetEnvironmentVariable("FIRESTORE_EMULATOR_HOST", LocalEmulatorSettings.FirestoreHost);
            Environment.SetEnvironmentVariable("FIREBASE_AUTH_EMULATOR_HOST", LocalEmulatorSettings.AuthHost);
            Environment.SetEnvironmentVariable("LOCAL_EMULATOR_PASSWORD", password);
            app = FirebaseApp.Create(new AppOptions { ProjectId = LocalEmulatorSettings.ProjectId, Credential = GoogleCredential.FromAccessToken("owner"), HttpClientFactory = new LocalAuthClientFactory() });
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["LocalEmulators:Enabled"] = "true" }).Build();
            using var client = new HttpClient(new LocalAuthEmulatorTransport());
            var service = new AuthenticationService(configuration, db, new AuditService(db), client);
            await LocalEmulatorSeed.RunAsync(db, app, development: true);
            var clienteRef = db.Collection("clientes").Document("local-seed-cliente");
            await clienteRef.UpdateAsync("Nombre", "Preservado");
            var original = await clienteRef.GetSnapshotAsync();
            await LocalEmulatorSeed.RunAsync(db, app, development: true);
            var preserved = await clienteRef.GetSnapshotAsync();
            Assert.Equal(original.UpdateTime, preserved.UpdateTime);
            Assert.Equal("Preservado", preserved.GetValue<string>("Nombre"));

            var result = await service.AuthenticateWithEmailAsync(LocalEmulatorSeed.Email, password);
            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Equal(LocalEmulatorSeed.UserId, result.UserInfo?.Uid);
            Assert.Equal("Pruebas locales", result.UserInfo?.Role);
            var rejected = await service.AuthenticateWithEmailAsync(LocalEmulatorSeed.Email, "invalid-password");
            Assert.False(rejected.IsSuccess);
            Assert.Equal("La contraseña es incorrecta.", rejected.ErrorMessage);
        }
        finally
        {
            app?.Delete();
            foreach (var name in names) Environment.SetEnvironmentVariable(name, previous[name]);
        }
    }
}

public sealed class LocalSeedFactAttribute : FactAttribute
{
    public LocalSeedFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("ISOLATED_FIREBASE_TESTS") != "1")
            Skip = "Requires the disposable emulator test runner.";
    }
}

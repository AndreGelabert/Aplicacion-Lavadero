using Firebase.Security;
using Xunit;

namespace Firebase.Tests.Security;

[Collection("Isolated emulator environment")]
public class LocalEmulatorSettingsTests
{
    [Fact]
    public void DisabledWhenNeitherEmulatorIsConfigured()
    {
        Assert.False(LocalEmulatorSettings.Validate(false, null, null));
    }

    [Theory]
    [InlineData("127.0.0.1:8189", "127.0.0.1:9099")]
    [InlineData("127.0.0.1:8190", "127.0.0.1:9199")]
    public void EnablesOnlyLoopbackPairInDevelopment(string firestore, string auth)
    {
        Assert.True(LocalEmulatorSettings.Validate(true, firestore, auth));
    }

    [Theory]
    [InlineData(false, "127.0.0.1:8189", "127.0.0.1:9099")]
    [InlineData(true, "example.com:8189", "127.0.0.1:9099")]
    [InlineData(true, "127.0.0.1:8189", "example.com:9099")]
    [InlineData(true, "127.0.0.1:8189", null)]
    [InlineData(true, null, "127.0.0.1:9099")]
    [InlineData(true, "127.0.0.1:8190", null)]
    [InlineData(true, null, "127.0.0.1:9199")]
    [InlineData(true, "127.0.0.1:8190", "example.com:9199")]
    [InlineData(true, "example.com:8190", "127.0.0.1:9199")]
    [InlineData(true, "127.0.0.1:8190", "127.0.0.1:9099")]
    [InlineData(true, " 127.0.0.1:8190", "127.0.0.1:9199")]
    [InlineData(true, "127.0.0.1:8190", "127.0.0.1:9199 ")]
    [InlineData(true, "localhost:8190", "127.0.0.1:9199")]
    [InlineData(true, "127.0.0.1:8190", "localhost:9199")]
    [InlineData(true, "[::1]:8190", "127.0.0.1:9199")]
    [InlineData(true, "127.0.0.1:8190", "[::1]:9199")]
    [InlineData(true, "http://127.0.0.1:8190", "127.0.0.1:9199")]
    [InlineData(true, "127.0.0.1:8190", "http://127.0.0.1:9199")]
    [InlineData(true, "127.0.0.1:8190/path", "127.0.0.1:9199")]
    [InlineData(true, "127.0.0.1:8190", "127.0.0.1:9199/path")]
    [InlineData(true, " ", "127.0.0.1:9199")]
    public void RejectsPartialRemoteOrProductionConfiguration(bool development, string? firestore, string? auth)
    {
        Assert.Throws<InvalidOperationException>(() => LocalEmulatorSettings.Validate(development, firestore, auth));
    }

    [Fact]
    public void ProductionRouteIsPreserved()
    {
        Assert.Equal("https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=test",
            LocalEmulatorSettings.IdentityToolkitUrl(false, "signInWithPassword", "test"));
    }

    [Theory]
    [InlineData("127.0.0.1:8189", "127.0.0.1:9099")]
    [InlineData("127.0.0.1:8190", "127.0.0.1:9199")]
    [InlineData("127.0.0.1:8190", " 127.0.0.1:9199")]
    [InlineData("127.0.0.1:8190", "localhost:9199")]
    [InlineData("127.0.0.1:8190", "[::1]:9199")]
    [InlineData("127.0.0.1:8190", "http://127.0.0.1:9199")]
    [InlineData("127.0.0.1:8190", "127.0.0.1:9199/path")]
    [InlineData("127.0.0.1:8190", null)]
    [InlineData("127.0.0.1:8190", "example.com:9199")]
    [InlineData(null, null)]
    public void LocalRouteRequiresAnAllowedPair(string? firestore, string? auth)
    {
        var oldFirestore = Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST");
        var oldAuth = Environment.GetEnvironmentVariable("FIREBASE_AUTH_EMULATOR_HOST");
        try
        {
            Environment.SetEnvironmentVariable("FIRESTORE_EMULATOR_HOST", firestore);
            Environment.SetEnvironmentVariable("FIREBASE_AUTH_EMULATOR_HOST", auth);
            Assert.Equal("https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=test",
                LocalEmulatorSettings.IdentityToolkitUrl(false, "signInWithPassword", "test"));
            if ((firestore == "127.0.0.1:8189" && auth == "127.0.0.1:9099") ||
                (firestore == "127.0.0.1:8190" && auth == "127.0.0.1:9199"))
                Assert.Equal($"http://{auth}/identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=test",
                    LocalEmulatorSettings.IdentityToolkitUrl(true, "signInWithPassword", "test"));
            else
                Assert.Throws<InvalidOperationException>(() => LocalEmulatorSettings.IdentityToolkitUrl(true, "signInWithPassword", "test"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("FIRESTORE_EMULATOR_HOST", oldFirestore);
            Environment.SetEnvironmentVariable("FIREBASE_AUTH_EMULATOR_HOST", oldAuth);
        }
    }
}

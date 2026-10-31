using Firebase.Security;
using Xunit;

namespace Firebase.Tests.Security;

public class LocalEmulatorSettingsTests
{
    [Fact]
    public void DisabledWhenNeitherEmulatorIsConfigured()
    {
        Assert.False(LocalEmulatorSettings.Validate(false, null, null));
    }

    [Fact]
    public void EnablesOnlyLoopbackPairInDevelopment()
    {
        Assert.True(LocalEmulatorSettings.Validate(true, "127.0.0.1:8189", "127.0.0.1:9099"));
    }

    [Theory]
    [InlineData(false, "127.0.0.1:8189", "127.0.0.1:9099")]
    [InlineData(true, "example.com:8189", "127.0.0.1:9099")]
    [InlineData(true, "127.0.0.1:8189", "example.com:9099")]
    [InlineData(true, "127.0.0.1:8189", null)]
    [InlineData(true, null, "127.0.0.1:9099")]
    public void RejectsPartialRemoteOrProductionConfiguration(bool development, string? firestore, string? auth)
    {
        Assert.Throws<InvalidOperationException>(() => LocalEmulatorSettings.Validate(development, firestore, auth));
    }

    [Theory]
    [InlineData(true, "http://127.0.0.1:9099/identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=test")]
    [InlineData(false, "https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=test")]
    public void RoutesIdentityToolkitToTheSelectedEnvironment(bool local, string expected)
    {
        Assert.Equal(expected, LocalEmulatorSettings.IdentityToolkitUrl(local, "signInWithPassword", "test"));
    }
}

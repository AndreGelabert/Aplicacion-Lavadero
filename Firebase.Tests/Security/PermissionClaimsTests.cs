using System.Security.Claims;
using Firebase.Security;
using Xunit;

namespace Firebase.Tests.Security;

public class PermissionClaimsTests
{
    [Fact]
    public void Refresh_ReplacesOnlyPermissionClaimsAndPreservesSessionIdentity()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-1"),
            new Claim(SessionTracking.AuthenticationSessionClaimType, "session-1"),
            new Claim(PermisoClaim.Tipo, "Clientes.Consultar")
        }, "Cookies");

        var changed = PermissionClaims.Refresh(identity, new[] { "Vehiculos.Consultar", "Vehiculos.Consultar" });

        Assert.True(changed);
        Assert.Equal("user-1", identity.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("session-1", identity.FindFirst(SessionTracking.AuthenticationSessionClaimType)?.Value);
        Assert.Equal(new[] { "Vehiculos.Consultar" }, PermisoClaim.Obtener(new ClaimsPrincipal(identity)));
    }

    [Fact]
    public void Refresh_DoesNotRenewWhenPermissionsAreUnchanged()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(PermisoClaim.Tipo, "Clientes.Consultar")
        }, "Cookies");

        Assert.False(PermissionClaims.Refresh(identity, new[] { "Clientes.Consultar" }));
        Assert.Single(identity.FindAll(PermisoClaim.Tipo));
    }
}

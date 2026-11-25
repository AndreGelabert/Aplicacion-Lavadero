using System.Security.Claims;
using Firebase.Services;
using Microsoft.AspNetCore.DataProtection;

namespace Firebase.Tests.Services;

public class TurnoFormIdentityTests
{
    private static ClaimsPrincipal User(string id) => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id)], "test"));

    [Fact]
    public void IssuedIdentityIsStableAcrossRetriesAndBoundToPrincipal()
    {
        var service = new TurnoFormIdentity(new EphemeralDataProtectionProvider());
        var token = service.Issue(User("one"));
        Assert.True(service.TryRead(User("one"), token, out var first));
        Assert.True(service.TryRead(User("one"), token, out var retry));
        Assert.Equal(first, retry);
        Assert.False(service.TryRead(User("two"), token, out _));
        Assert.False(service.TryRead(User("one"), token + "x", out _));
        Assert.False(service.TryRead(new ClaimsPrincipal(), token, out _));
        Assert.NotEqual(token, service.Issue(User("one")));
    }
}

using System.Security.Claims;
using System.Text;
using Firebase.Middleware;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Firebase.Tests.Security;

public class SessionActivityFailureTests
{
    [Theory]
    [InlineData("LoginTime", null)]
    [InlineData("LastActivity", null)]
    [InlineData("MaxDuration", null)]
    [InlineData("LoginTime", "not-a-date")]
    [InlineData("LastActivity", "not-a-date")]
    [InlineData("MaxDuration", "not-a-number")]
    [InlineData("MaxDuration", "0")]
    [InlineData("MaxDuration", "-1")]
    public async Task InvalidTrackingSignsOutWithoutContinuing(string key, string? value)
    {
        var (context, session, auth, middleware, wasInvoked) = CreatePipeline(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, key, value);

        await middleware.InvokeAsync(context, null!, null!);

        Assert.False(wasInvoked());
        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/Login/Index?expired=true", context.Response.Headers.Location.ToString());
        auth.Verify(a => a.SignOutAsync(context, It.IsAny<string>(), It.IsAny<AuthenticationProperties>()), Times.Once);
        session.Verify(s => s.Clear(), Times.Once);
        session.Verify(s => s.Set(It.IsAny<string>(), It.IsAny<byte[]>()), Times.Never);
        session.Verify(s => s.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartedResponseRethrowsSessionLoadFailureWithoutContinuing()
    {
        var (context, session, auth, middleware, wasInvoked) = CreatePipeline(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var response = new Mock<IHttpResponseFeature>();
        response.SetupGet(r => r.HasStarted).Returns(true);
        context.Features.Set(response.Object);
        session.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("store unavailable"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context, null!, null!));

        Assert.Equal("store unavailable", exception.Message);
        Assert.False(wasInvoked());
        auth.Verify(a => a.SignOutAsync(context, It.IsAny<string>(), It.IsAny<AuthenticationProperties>()), Times.Never);
        session.Verify(s => s.Clear(), Times.Never);
        session.Verify(s => s.Set(It.IsAny<string>(), It.IsAny<byte[]>()), Times.Never);
    }

    [Theory]
    [InlineData("load")]
    [InlineData("read")]
    [InlineData("write")]
    [InlineData("commit")]
    public async Task TrackingFailureDoesNotReachNextMiddleware(string failure)
    {
        var (context, session, _, middleware, wasInvoked) = CreatePipeline(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        if (failure == "load")
            session.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("store unavailable"));
        if (failure == "read")
        {
            byte[] ignored;
            session.Setup(s => s.TryGetValue(It.IsAny<string>(), out ignored!)).Throws(new InvalidOperationException("store unavailable"));
        }
        if (failure == "write")
            session.Setup(s => s.Set(It.IsAny<string>(), It.IsAny<byte[]>())).Throws(new InvalidOperationException("store unavailable"));
        if (failure == "commit")
            session.Setup(s => s.CommitAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("store unavailable"));

        await middleware.InvokeAsync(context, null!, null!);

        Assert.False(wasInvoked());
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
    }

    [Fact]
    public async Task ValidTrackingContinuesAndUpdatesActivity()
    {
        var (context, session, _, middleware, wasInvoked) = CreatePipeline(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        await middleware.InvokeAsync(context, null!, null!);
        Assert.True(wasInvoked());
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        session.Verify(s => s.Set("LastActivity", It.IsAny<byte[]>()), Times.Once);
        session.Verify(s => s.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StaleAuthenticationCookieDoesNotClearNewerServerSession()
    {
        var (context, session, auth, middleware, wasInvoked) = CreatePipeline(
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, sessionBindingOverride: "newer-session-binding");

        await middleware.InvokeAsync(context, null!, null!);

        Assert.False(wasInvoked());
        Assert.Equal("/Login/Index?expired=true", context.Response.Headers.Location.ToString());
        auth.Verify(a => a.SignOutAsync(context, It.IsAny<string>(), It.IsAny<AuthenticationProperties>()), Times.Once);
        session.Verify(s => s.Clear(), Times.Never);
        session.Verify(s => s.Set(It.IsAny<string>(), It.IsAny<byte[]>()), Times.Never);
        session.Verify(s => s.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExpiredRegistryStillReportsConfiguredDurationExpiration()
    {
        var now = DateTimeOffset.UtcNow;
        var (context, session, auth, middleware, wasInvoked) = CreatePipeline(
            now.AddHours(-9), now, registryLifetime: TimeSpan.FromMilliseconds(1));
        await Task.Delay(20);

        await middleware.InvokeAsync(context, null!, null!);

        Assert.False(wasInvoked());
        Assert.Equal("/Login/Index?expired=duration", context.Response.Headers.Location.ToString());
        auth.Verify(a => a.SignOutAsync(context, It.IsAny<string>(), It.IsAny<AuthenticationProperties>()), Times.Once);
        session.Verify(s => s.Clear(), Times.Once);
        session.Verify(s => s.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartedResponseExpiredSessionRevokesAndStopsPipeline()
    {
        var now = DateTimeOffset.UtcNow;
        var (context, session, auth, middleware, wasInvoked) = CreatePipeline(now.AddHours(-9), now);
        var response = new Mock<IHttpResponseFeature>();
        response.SetupGet(r => r.HasStarted).Returns(true);
        context.Features.Set(response.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context, null!, null!));

        Assert.False(wasInvoked());
        session.Verify(s => s.Clear(), Times.Once);
        session.Verify(s => s.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        auth.Verify(a => a.SignOutAsync(context, It.IsAny<string>(), It.IsAny<AuthenticationProperties>()), Times.Never);
    }

    [Theory]
    [InlineData(true, "/Login/Index?expired=duration")]
    [InlineData(false, "/Login/Index?expired=inactivity")]
    public async Task ExpiredTrackingStillSignsOut(bool duration, string location)
    {
        var now = DateTimeOffset.UtcNow;
        var (context, session, auth, middleware, wasInvoked) = CreatePipeline(duration ? now.AddHours(-9) : now, now.AddMinutes(-20));
        await middleware.InvokeAsync(context, null!, null!);
        Assert.False(wasInvoked());
        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal(location, context.Response.Headers.Location.ToString());
        auth.Verify(a => a.SignOutAsync(context, It.IsAny<string>(), It.IsAny<AuthenticationProperties>()), Times.Once);
        session.Verify(s => s.Clear(), Times.Once);
        session.Verify(s => s.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static (DefaultHttpContext, Mock<ISession>, Mock<IAuthenticationService>, SessionActivityMiddleware, Func<bool>) CreatePipeline(DateTimeOffset login, DateTimeOffset activity, string? overrideKey = null, string? overrideValue = null, string? sessionBindingOverride = null, TimeSpan? registryLifetime = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/Home/Index";
        var authenticationSessionId = "valid-session-binding";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "test"),
            new Claim(Firebase.Security.SessionTracking.AuthenticationSessionClaimType, authenticationSessionId)
        }, "cookie"));
        var values = new Dictionary<string, byte[]> {
            ["LoginTime"] = Encoding.UTF8.GetBytes(login.ToString("O")),
            ["LastActivity"] = Encoding.UTF8.GetBytes(activity.ToString("O")),
            ["MaxDuration"] = Encoding.UTF8.GetBytes("480"),
            [Firebase.Security.SessionTracking.AuthenticationSessionKey] = Encoding.UTF8.GetBytes(sessionBindingOverride ?? authenticationSessionId)
        };
        if (overrideKey != null)
        {
            if (overrideValue == null)
                values.Remove(overrideKey);
            else
                values[overrideKey] = Encoding.UTF8.GetBytes(overrideValue);
        }
        var session = new Mock<ISession>();
        session.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        session.Setup(s => s.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        session.Setup(s => s.TryGetValue(It.IsAny<string>(), out It.Ref<byte[]>.IsAny))
            .Returns((string key, out byte[] value) => values.TryGetValue(key, out value!));
        context.Session = session.Object;
        var auth = new Mock<IAuthenticationService>();
        auth.Setup(a => a.SignOutAsync(context, It.IsAny<string>(), It.IsAny<AuthenticationProperties>())).Returns(Task.CompletedTask);
        context.RequestServices = new ServiceCollection().AddSingleton(auth.Object).BuildServiceProvider();
        var invoked = false;
        var registry = new Firebase.Security.AuthenticationSessionRegistry(new MemoryCache(new MemoryCacheOptions()));
        registry.Register(authenticationSessionId, registryLifetime ?? TimeSpan.FromHours(8));
        var middleware = new SessionActivityMiddleware(_ => { invoked = true; return Task.CompletedTask; }, NullLogger<SessionActivityMiddleware>.Instance, registry);
        return (context, session, auth, middleware, () => invoked);
    }
}

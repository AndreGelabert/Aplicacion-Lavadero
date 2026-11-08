using System.Security.Claims;
using System.Text;
using Firebase.Middleware;
using Firebase.Security;
using Firebase.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Firebase.Tests.Security;

public class SessionExpirationRefactorTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task ExpirationAuditsThenRevokesAndAwaitsCommitBeforeSignOut(bool duration, bool auditFails)
    {
        var pipeline = new Pipeline(duration, auditFails);
        var invocation = pipeline.Invoke();
        await pipeline.CommitEntered.Task;

        Assert.False(invocation.IsCompleted);
        Assert.Equal(new[] { "audit", "clear", "commit" }, pipeline.Events);
        Assert.Equal(StatusCodes.Status200OK, pipeline.Context.Response.StatusCode);
        Assert.Empty(pipeline.Context.Response.Headers.Location.ToString());
        pipeline.CommitCompletion.SetResult();
        await invocation;

        pipeline.AssertCommon(duration);
        Assert.Equal(new[] { "audit", "clear", "commit", "signout" }, pipeline.Events);
        Assert.Equal(StatusCodes.Status302Found, pipeline.Context.Response.StatusCode);
        Assert.Equal(duration ? "/Login/Index?expired=duration" : "/Login/Index?expired=inactivity", pipeline.Context.Response.Headers.Location.ToString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CommitFailureStopsBeforeSignOutOrRedirect(bool duration)
    {
        var pipeline = new Pipeline(duration);
        var invocation = pipeline.Invoke();
        await pipeline.CommitEntered.Task;
        pipeline.CommitCompletion.SetException(new InvalidOperationException("commit failed"));
        await invocation;

        pipeline.AssertCommon(duration);
        Assert.Equal(new[] { "audit", "clear", "commit" }, pipeline.Events);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, pipeline.Context.Response.StatusCode);
        Assert.Empty(pipeline.Context.Response.Headers.Location.ToString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StartedResponseThrowsAfterCommitWithoutSignOut(bool duration)
    {
        var pipeline = new Pipeline(duration);
        var response = new Mock<IHttpResponseFeature>();
        response.SetupGet(r => r.HasStarted).Returns(true);
        pipeline.Context.Features.Set(response.Object);
        var invocation = pipeline.Invoke();
        await pipeline.CommitEntered.Task;
        pipeline.CommitCompletion.SetResult();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => invocation);
        Assert.Equal("Cannot complete session expiration after the response has started.", exception.Message);
        pipeline.AssertCommon(duration);
        Assert.Equal(new[] { "audit", "clear", "commit" }, pipeline.Events);
        response.Verify(r => r.Headers, Times.Never);
    }

    private sealed class Pipeline
    {
        private const string SessionId = "session-binding";
        private const string UserId = "user-123";
        private const string Email = "user@example.test";
        private readonly AuthenticationSessionRegistry registry = new(new MemoryCache(new MemoryCacheOptions()));
        private readonly Mock<ISession> session = new();
        private readonly Mock<IAuthenticationService> auth = new();
        private readonly Mock<IAuditWriter> audit = new();
        private readonly SessionActivityMiddleware middleware;
        private bool nextInvoked;
        private bool activeAtAudit;
        private bool trackingPresentAtAudit;
        private bool revokedAtClear;
        private string[]? auditArguments;

        public DefaultHttpContext Context { get; } = new();
        public List<string> Events { get; } = new();
        public TaskCompletionSource CommitEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CommitCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Pipeline(bool duration, bool auditFails = false)
        {
            var now = DateTimeOffset.UtcNow;
            var values = new Dictionary<string, byte[]>
            {
                ["LoginTime"] = Encoding.UTF8.GetBytes((duration ? now.AddHours(-9) : now).ToString("O")),
                ["LastActivity"] = Encoding.UTF8.GetBytes(now.AddMinutes(-20).ToString("O")),
                ["MaxDuration"] = Encoding.UTF8.GetBytes("480"),
                [SessionTracking.AuthenticationSessionKey] = Encoding.UTF8.GetBytes(SessionId)
            };
            registry.Register(SessionId, TimeSpan.FromHours(8));
            Context.Request.Path = "/Home/Index";
            Context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, "test"),
                new Claim(ClaimTypes.NameIdentifier, UserId),
                new Claim(ClaimTypes.Email, Email),
                new Claim(SessionTracking.AuthenticationSessionClaimType, SessionId)
            }, "cookie"));
            session.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            session.Setup(s => s.TryGetValue(It.IsAny<string>(), out It.Ref<byte[]>.IsAny))
                .Returns((string key, out byte[] value) => values.TryGetValue(key, out value!));
            session.Setup(s => s.Clear()).Callback(() =>
            {
                Events.Add("clear");
                revokedAtClear = !registry.IsActive(SessionId);
                values.Clear();
            });
            session.Setup(s => s.CommitAsync(It.IsAny<CancellationToken>())).Returns(() =>
            {
                Events.Add("commit");
                CommitEntered.SetResult();
                return CommitCompletion.Task;
            });
            Context.Session = session.Object;
            audit.Setup(a => a.LogSessionEvent(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string userId, string email, string action, string targetId, string targetType) =>
                {
                    Events.Add("audit");
                    activeAtAudit = registry.IsActive(SessionId);
                    trackingPresentAtAudit = values.ContainsKey(SessionTracking.AuthenticationSessionKey);
                    auditArguments = new[] { userId, email, action, targetId, targetType };
                    return auditFails ? Task.FromException(new InvalidOperationException("audit failed")) : Task.CompletedTask;
                });
            auth.Setup(a => a.SignOutAsync(Context, It.IsAny<string>(), It.IsAny<AuthenticationProperties>()))
                .Returns(() => { Events.Add("signout"); return Task.CompletedTask; });
            Context.RequestServices = new ServiceCollection().AddSingleton(auth.Object).BuildServiceProvider();
            middleware = new SessionActivityMiddleware(_ => { nextInvoked = true; return Task.CompletedTask; },
                NullLogger<SessionActivityMiddleware>.Instance, registry);
        }

        public Task Invoke() => middleware.InvokeAsync(Context, null!, audit.Object);

        public void AssertCommon(bool duration)
        {
            Assert.Equal(new[] { UserId, Email, duration ? "Cerrar Sesión por Duración Máxima" : "Cerrar Sesión por Inactividad", UserId, "Empleado" }, auditArguments);
            Assert.True(activeAtAudit);
            Assert.True(trackingPresentAtAudit);
            Assert.True(revokedAtClear);
            Assert.False(registry.IsActive(SessionId));
            Assert.False(nextInvoked);
            session.Verify(s => s.Clear(), Times.Once);
            session.Verify(s => s.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
            session.Verify(s => s.Set(It.IsAny<string>(), It.IsAny<byte[]>()), Times.Never);
            audit.Verify(a => a.LogSessionEvent(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            auth.Verify(a => a.SignOutAsync(Context, CookieAuthenticationDefaults.AuthenticationScheme, It.IsAny<AuthenticationProperties>()),
                Events.Contains("signout") ? Times.Once : Times.Never);
        }
    }
}

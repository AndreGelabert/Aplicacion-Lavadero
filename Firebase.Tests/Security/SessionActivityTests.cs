using System.Security.Claims;
using Firebase.Middleware;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Firebase.Tests.Security;

public class SessionActivityTests
{
    [Fact]
    public async Task MissingTrackingSignsOutInsteadOfRestartingSession()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/Home/Index";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "test") }, "cookie"));
        var session = new Mock<ISession>();
        session.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        context.Session = session.Object;
        var auth = new Mock<IAuthenticationService>();
        auth.Setup(a => a.SignOutAsync(context, It.IsAny<string>(), It.IsAny<AuthenticationProperties>())).Returns(Task.CompletedTask);
        context.RequestServices = new ServiceCollection().AddSingleton(auth.Object).BuildServiceProvider();
        var invoked = false;
        var registry = new Firebase.Security.AuthenticationSessionRegistry(new MemoryCache(new MemoryCacheOptions()));
        var middleware = new SessionActivityMiddleware(_ => { invoked = true; return Task.CompletedTask; }, NullLogger<SessionActivityMiddleware>.Instance, registry);

        await middleware.InvokeAsync(context, null!, null!);

        Assert.False(invoked);
        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/Login/Index?expired=true", context.Response.Headers.Location.ToString());
        auth.Verify(a => a.SignOutAsync(context, It.IsAny<string>(), It.IsAny<AuthenticationProperties>()), Times.Once);
        session.Verify(s => s.Clear(), Times.Once);
        session.Verify(s => s.Set(It.IsAny<string>(), It.IsAny<byte[]>()), Times.Never);
        session.Verify(s => s.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using Moq;

namespace Firebase.Tests.Security;

public class LogoutResponseTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LogoutReturnsExplicitFetchSuccessOrNormalRedirect(bool ajax)
    {
        var context = new DefaultHttpContext();
        if (ajax) context.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        var session = new Mock<ISession>();
        session.Setup(s => s.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        context.Session = session.Object;
        var registry = RegistryForLogout(context, session);
        var auth = new Mock<IAuthenticationService>();
        auth.Setup(a => a.SignOutAsync(context, It.IsAny<string>(), It.IsAny<AuthenticationProperties>())).Returns(Task.CompletedTask);
        context.RequestServices = new ServiceCollection().AddSingleton(auth.Object).BuildServiceProvider();
        var controller = new LoginController(null!, null!, null!, null!, registry, NullLogger<LoginController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = context },
            Url = Mock.Of<IUrlHelper>()
        };

        var result = await controller.Logout();

        if (ajax) Assert.IsType<NoContentResult>(result);
        else Assert.IsType<RedirectToActionResult>(result);
        auth.Verify(a => a.SignOutAsync(context, It.IsAny<string>(), It.IsAny<AuthenticationProperties>()), Times.Once);
        session.Verify(s => s.Clear(), Times.Once);
        session.Verify(s => s.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(registry.IsActive("logout-session"));
    }

    [Fact]
    public async Task LogoutReturnsFailureWhenSessionClearCannotBeCommitted()
    {
        var context = new DefaultHttpContext();
        var session = new Mock<ISession>();
        session.Setup(s => s.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("synthetic cache failure"));
        context.Session = session.Object;
        var registry = RegistryForLogout(context, session);
        var auth = new Mock<IAuthenticationService>();
        auth.Setup(a => a.SignOutAsync(context, It.IsAny<string>(), It.IsAny<AuthenticationProperties>()))
            .Returns(Task.CompletedTask);
        context.RequestServices = new ServiceCollection().AddSingleton(auth.Object).BuildServiceProvider();
        var controller = new LoginController(null!, null!, null!, null!, registry, NullLogger<LoginController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = context },
            Url = Mock.Of<IUrlHelper>()
        };

        var result = await controller.Logout();

        var response = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, response.StatusCode);
        auth.Verify(a => a.SignOutAsync(context, It.IsAny<string>(), It.IsAny<AuthenticationProperties>()), Times.Never);
        session.Verify(s => s.Clear(), Times.Once);
        session.Verify(s => s.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(registry.IsActive("logout-session"));
    }

    [Fact]
    public async Task LogoutRejectsStaleCookieWithoutClearingNewerSession()
    {
        var context = new DefaultHttpContext();
        var session = new Mock<ISession>();
        context.Session = session.Object;
        var registry = RegistryForLogout(context, session, "newer-session");
        var auth = new Mock<IAuthenticationService>();
        context.RequestServices = new ServiceCollection().AddSingleton(auth.Object).BuildServiceProvider();
        var controller = new LoginController(null!, null!, null!, null!, registry, NullLogger<LoginController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = context },
            Url = Mock.Of<IUrlHelper>()
        };

        var result = await controller.Logout();

        Assert.IsType<ConflictResult>(result);
        Assert.True(registry.IsActive("logout-session"));
        session.Verify(s => s.Clear(), Times.Never);
        session.Verify(s => s.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        auth.Verify(a => a.SignOutAsync(context, It.IsAny<string>(), It.IsAny<AuthenticationProperties>()), Times.Never);
    }

    private static Firebase.Security.AuthenticationSessionRegistry RegistryForLogout(DefaultHttpContext context, Mock<ISession> session, string sessionBinding = "logout-session")
    {
        const string sessionId = "logout-session";
        var registry = new Firebase.Security.AuthenticationSessionRegistry(new MemoryCache(new MemoryCacheOptions()));
        registry.Register(sessionId, TimeSpan.FromHours(1));
        var bindingBytes = Encoding.UTF8.GetBytes(sessionBinding);
        session.Setup(s => s.TryGetValue(Firebase.Security.SessionTracking.AuthenticationSessionKey, out It.Ref<byte[]>.IsAny))
            .Returns((string key, out byte[] value) =>
            {
                value = bindingBytes;
                return key == Firebase.Security.SessionTracking.AuthenticationSessionKey;
            });
        context.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]
        {
            new System.Security.Claims.Claim(Firebase.Security.SessionTracking.AuthenticationSessionClaimType, sessionId)
        }, "cookie"));
        return registry;
    }
}

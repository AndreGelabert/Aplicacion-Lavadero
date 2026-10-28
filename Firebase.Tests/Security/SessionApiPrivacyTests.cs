using System.Security.Claims;
using Firebase.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Firebase.Tests.Security;

public class SessionApiPrivacyTests
{
    [Fact]
    public async Task SessionConfigFailureDoesNotLogExceptionDetails()
    {
        const string secret = "private exception detail";
        var session = new Mock<ISession>();
        session.Setup(s => s.TryGetValue("LoginTime", out It.Ref<byte[]>.IsAny))
            .Throws(new InvalidOperationException(secret));
        var logger = new Mock<ILogger<SessionApiController>>();
        var messages = new List<string>();
        logger.Setup(l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(new InvocationAction(invocation => messages.Add(invocation.Arguments[2].ToString()!)));
        var controller = new SessionApiController(null!, logger.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { Session = session.Object } }
        };

        var result = await controller.GetSessionConfig();

        Assert.Equal(StatusCodes.Status500InternalServerError, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.DoesNotContain(messages, message => message.Contains(secret, StringComparison.Ordinal));
    }

    [Fact]
    public void SessionPingFailureDoesNotLogExceptionDetails()
    {
        const string secret = "private exception detail";
        var session = new Mock<ISession>();
        session.Setup(s => s.Set("LastActivity", It.IsAny<byte[]>())).Throws(new InvalidOperationException(secret));
        var logger = new Mock<ILogger<SessionApiController>>();
        var messages = new List<string>();
        logger.Setup(l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(new InvocationAction(invocation => messages.Add(invocation.Arguments[2].ToString()!)));
        var controller = new SessionApiController(null!, logger.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { Session = session.Object } }
        };

        var result = controller.PingSession();

        Assert.Equal(StatusCodes.Status500InternalServerError, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.DoesNotContain(messages, message => message.Contains(secret, StringComparison.Ordinal));
    }

    [Fact]
    public void PingUpdatesActivityWithoutLoggingEmailOrSessionId()
    {
        const string email = "private@example.test";
        const string sessionId = "private-session-id";
        var session = new Mock<ISession>();
        session.SetupGet(s => s.Id).Returns(sessionId);
        var context = new DefaultHttpContext
        {
            Session = session.Object,
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Email, email) }, "test"))
        };
        var logger = new Mock<ILogger<SessionApiController>>();
        var messages = new List<string>();
        logger.Setup(l => l.Log(LogLevel.Information, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(new InvocationAction(invocation => messages.Add(invocation.Arguments[2].ToString()!)));
        var controller = new SessionApiController(null!, logger.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };

        var result = controller.PingSession();

        Assert.IsType<OkObjectResult>(result);
        session.Verify(s => s.Set("LastActivity", It.IsAny<byte[]>()), Times.Once);
        var message = Assert.Single(messages);
        Assert.DoesNotContain(email, message);
        Assert.DoesNotContain(sessionId, message);
    }
}

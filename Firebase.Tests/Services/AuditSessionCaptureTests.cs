using Xunit;

namespace Firebase.Tests.Services;

public class AuditSessionCaptureTests
{
    [Fact]
    public void SessionEventsHaveExplicitPublicWriter()
    {
        var method = typeof(AuditService).GetMethod("LogSessionEvent", new[] { typeof(string), typeof(string), typeof(string), typeof(string), typeof(string) });
        Assert.NotNull(method);
    }
}

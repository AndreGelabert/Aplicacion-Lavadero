using System.Reflection;
using FirebaseLoginCustom.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Firebase.Services;

namespace Firebase.Tests.Controllers;

public class TurnoOfertaEmailResponseControllerTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RespondDelegatesBodyWithServerUtcAndNeverReturnsToken(bool accept)
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        (string, string, string, string, DateTime, bool)? received = null;
        var controller = new TurnoOfertaEmailController((cascade, offer, client, token, now, answer) =>
        {
            received = (cascade, offer, client, token, now, answer);
            return Task.CompletedTask;
        });
        var result = await controller.Responder(new() { CascadeId = "cascade", OfferId = "offer", ClientId = "client", Token = "secret", Accept = accept });
        var ok = Assert.IsType<OkResult>(result);
        Assert.Equal(200, ok.StatusCode);
        Assert.NotNull(received);
        Assert.Equal(("cascade", "offer", "client", "secret"), (received.Value.Item1, received.Value.Item2, received.Value.Item3, received.Value.Item4));
        Assert.Equal(accept, received.Value.Item6);
        Assert.Equal(DateTimeKind.Utc, received.Value.Item5.Kind);
        Assert.InRange(received.Value.Item5, before, DateTime.UtcNow.AddSeconds(1));
        Assert.Equal(0, received.Value.Item5.Ticks % 10);
    }

    [Fact]
    public async Task InvalidAndExceptionsHaveIdenticalEmptyResults()
    {
        var calls = 0;
        var controller = new TurnoOfertaEmailController((_, _, _, _, _, _) => { calls++; throw new TurnoOfertaEmailInvalidException(); });
        var invalid = Assert.IsType<BadRequestResult>(await controller.Responder(null));
        var missing = Assert.IsType<BadRequestResult>(await controller.Responder(new() { CascadeId = "c", OfferId = "o", ClientId = "i", Token = "secret" }));
        var failed = Assert.IsType<BadRequestResult>(await controller.Responder(new() { CascadeId = "c", OfferId = "o", ClientId = "i", Token = "secret", Accept = true }));
        Assert.Equal(invalid.StatusCode, missing.StatusCode);
        Assert.Equal(invalid.StatusCode, failed.StatusCode);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task UnavailableReturns503AndLogsWithoutSecrets()
    {
        var logger = new RecordingLogger();
        var controller = new TurnoOfertaEmailController((_, _, _, _, _, _) => throw new Exception("secret"), logger);
        var result = Assert.IsType<StatusCodeResult>(await controller.Responder(new() { CascadeId = "c", OfferId = "o", ClientId = "i", Token = "secret", Accept = true }));
        Assert.Equal(503, result.StatusCode);
        Assert.Single(logger.Entries);
        Assert.DoesNotContain("secret", logger.Entries[0]);
    }

    [Fact]
    public async Task CorruptedDurableReceiptReturnsEmpty503WithoutLeakingSecrets()
    {
        const string token = "token-secret";
        const string receiptSecret = "receipt-secret";
        var logger = new RecordingLogger();
        var calls = 0;
        var controller = new TurnoOfertaEmailController((_, _, _, _, _, _) =>
        {
            calls++;
            throw new InvalidOperationException($"Corrupted durable receipt: {receiptSecret}; token: {token}");
        }, logger);

        var result = Assert.IsType<StatusCodeResult>(await controller.Responder(new()
        {
            CascadeId = "cascade", OfferId = "offer", ClientId = "client", Token = token, Accept = true
        }));

        Assert.Equal(503, result.StatusCode);
        Assert.Equal(1, calls);
        var entry = Assert.Single(logger.Entries);
        Assert.DoesNotContain(token, entry);
        Assert.DoesNotContain(receiptSecret, entry);
    }

    [Fact]
    public async Task RequestCancellationReachesService()
    {
        using var source = new CancellationTokenSource();
        var controller = new TurnoOfertaEmailController((string c, string o, string i, string t, DateTime now, bool a, CancellationToken cancellation) =>
        {
            Assert.Equal(source.Token, cancellation);
            source.Cancel();
            cancellation.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        });
        controller.ControllerContext.HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { RequestAborted = source.Token };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => controller.Responder(new() { CascadeId = "c", OfferId = "o", ClientId = "i", Token = "secret", Accept = true }));
    }

    [Fact]
    public async Task CancellationPropagates()
    {
        var controller = new TurnoOfertaEmailController((_, _, _, _, _, _) => throw new OperationCanceledException());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => controller.Responder(new() { CascadeId = "c", OfferId = "o", ClientId = "i", Token = "secret", Accept = true }));
    }

    private sealed class RecordingLogger : ILogger<TurnoOfertaEmailController>
    {
        public List<string> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter) => Entries.Add(formatter(state, error) + error);
    }

    [Fact]
    public void OnlyPostBodyRouteCanMutate()
    {
        var methods = typeof(TurnoOfertaEmailController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        var method = Assert.Single(methods);
        Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
        Assert.Null(method.GetCustomAttribute<HttpGetAttribute>());
        Assert.NotNull(method.GetParameters()[0].GetCustomAttribute<FromBodyAttribute>());
        Assert.DoesNotContain("token", method.GetCustomAttribute<HttpPostAttribute>()!.Template ?? "", StringComparison.OrdinalIgnoreCase);
    }
}

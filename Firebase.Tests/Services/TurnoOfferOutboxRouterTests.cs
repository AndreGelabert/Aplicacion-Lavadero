using Firebase.Services;
using Xunit;

namespace Firebase.Tests.Services;

public class TurnoOfferOutboxRouterTests
{
    [Fact]
    public async Task RoutesBothChannelsWithFreshTokensAndNoFallback()
    {
        var calls = new List<(string Channel, string Id, string Token)>();
        var router = new TurnoOfferOutboxRouter(
            (id, token, ct) => { calls.Add(("WhatsApp", id, token)); return Task.CompletedTask; },
            (id, token, ct) => { calls.Add(("Email", id, token)); return Task.CompletedTask; });
        await router.Route("wa-id", "WhatsApp", CancellationToken.None);
        await router.Route("email-id", "Email", CancellationToken.None);
        await router.Route("wa-id", "WhatsApp", CancellationToken.None);
        await Assert.ThrowsAsync<ArgumentException>(() => router.Route("id", "Other", CancellationToken.None));
        Assert.Equal(new[] { "WhatsApp", "Email", "WhatsApp" }, calls.Select(c => c.Channel));
        Assert.Equal(new[] { "wa-id", "email-id", "wa-id" }, calls.Select(c => c.Id));
        Assert.All(calls, c => Assert.Matches("^[A-F0-9]{64}$", c.Token));
        Assert.Equal(3, calls.Select(c => c.Token).Distinct().Count());
    }

    [Fact]
    public async Task CancellationPreventsDispatchAndIsForwarded()
    {
        var seen = CancellationToken.None;
        var router = new TurnoOfferOutboxRouter((id, token, ct) => { seen = ct; return Task.CompletedTask; },
            (id, token, ct) => throw new Exception("wrong channel"));
        using var cts = new CancellationTokenSource();
        await router.Route("id", "WhatsApp", cts.Token);
        Assert.Equal(cts.Token, seen);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => router.Route("id", "WhatsApp", cts.Token));
    }
}

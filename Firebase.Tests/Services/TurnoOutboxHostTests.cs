using Firebase.BackgroundServices;
using Firebase.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Firebase.Tests.Services;

public class TurnoOutboxHostTests
{
    [Fact]
    public async Task PagesRouteOnlyEligibleOffersAndWrap()
    {
        var cursors = new List<string?>();
        var routed = new List<string>();
        using var stop = new CancellationTokenSource();
        var host = new TurnoOutboxHost((size, cursor, token) =>
        {
            Assert.Equal(100, size);
            cursors.Add(cursor);
            return Task.FromResult(cursors.Count == 1
                ? new TurnoOutboxHost.Page(new[] {
                    new TurnoOutboxHost.Item("a", "Offer", "Pending", "WhatsApp"),
                    new TurnoOutboxHost.Item("b", "Offer", "Indeterminate", "Email"),
                    new TurnoOutboxHost.Item("c", "Booking", "Pending", "Email") }, "c")
                : new TurnoOutboxHost.Page(new[] { new TurnoOutboxHost.Item("d", "Offer", "Released", "Email") }, null));
        }, (id, channel, token) => { routed.Add(id + ":" + channel); return Task.CompletedTask; },
        (delay, token) => { if (cursors.Count == 3) stop.Cancel(); return Task.CompletedTask; }, NullLogger<TurnoOutboxHost>.Instance);
        await host.RunAsync(stop.Token);
        Assert.Equal(new string?[] { null, "c", null }, cursors);
        Assert.Equal(new[] { "a:WhatsApp", "d:Email", "d:Email" }, routed);
    }

    [Fact]
    public async Task ExpiredClaimsAreRoutedButLiveAndIndeterminateClaimsAreNot()
    {
        var now = DateTime.UtcNow;
        var routed = new List<string>();
        using var stop = new CancellationTokenSource();
        var host = new TurnoOutboxHost((size, cursor, token) =>
        {
            Assert.Equal(100, size);
            Assert.Null(cursor);
            return Task.FromResult(new TurnoOutboxHost.Page(new[] {
                new TurnoOutboxHost.Item("expired", "Offer", "Claimed", "WhatsApp", now.AddMinutes(-1)),
                new TurnoOutboxHost.Item("live", "Offer", "Claimed", "Email", now.AddHours(1)),
                new TurnoOutboxHost.Item("missing", "Offer", "Claimed", "Email"),
                new TurnoOutboxHost.Item("uncertain", "Offer", "Indeterminate", "Email", now.AddMinutes(-1))
            }, null));
        }, (id, channel, token) => { routed.Add(id + ":" + channel); return Task.CompletedTask; },
        (delay, token) => { stop.Cancel(); return Task.CompletedTask; }, NullLogger<TurnoOutboxHost>.Instance);

        await host.RunAsync(stop.Token);
        Assert.Equal(new[] { "expired:WhatsApp" }, routed);
    }

    [Fact]
    public async Task FailureDoesNotStarveLaterItemsAndCancellationStops()
    {
        var routed = new List<string>();
        using var stop = new CancellationTokenSource();
        var host = new TurnoOutboxHost((size, cursor, token) => Task.FromResult(new TurnoOutboxHost.Page(
            new[] { new TurnoOutboxHost.Item("a", "Offer", "Pending", "WhatsApp"), new TurnoOutboxHost.Item("b", "Offer", "Pending", "Email") }, null)),
            (id, channel, token) => { routed.Add(id); if (id == "a") throw new InvalidOperationException(); stop.Cancel(); return Task.CompletedTask; },
            (delay, token) => throw new Exception("unexpected delay"), NullLogger<TurnoOutboxHost>.Instance);
        await host.RunAsync(stop.Token);
        Assert.Equal(new[] { "a", "b" }, routed);
    }
}

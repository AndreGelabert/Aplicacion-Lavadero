using Firebase.BackgroundServices;
using Firebase.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Firebase.Tests.Services;

public class TurnoOfertaHostTests
{
    [Fact]
    public async Task RetriesSamePageAfterFailureAndStartsNextScanAtBeginning()
    {
        var cursors = new List<string?>();
        using var stop = new CancellationTokenSource();
        var host = new TurnoOfertaHost((size, cursor, token) =>
        {
            Assert.Equal(100, size);
            cursors.Add(cursor);
            return cursors.Count switch
            {
                1 => Task.FromResult<string?>("page-1"),
                2 => Task.FromException<string?>(new InvalidOperationException()),
                3 => Task.FromResult<string?>("page-2"),
                4 => Task.FromResult<string?>(null),
                _ => Task.FromResult<string?>(null)
            };
        }, (delay, token) =>
        {
            Assert.Equal(TimeSpan.FromMinutes(1), delay);
            if (cursors.Count == 5) stop.Cancel();
            return Task.CompletedTask;
        }, NullLogger<TurnoOfertaHost>.Instance);

        await host.RunAsync(stop.Token);
        Assert.Equal(new string?[] { null, "page-1", "page-1", "page-2", null }, cursors);
    }

    [Fact]
    public async Task FailedPagesAdvanceAndAreRevisitedAfterWrapWithBackoff()
    {
        var cursors = new List<string?>();
        var delays = 0;
        using var stop = new CancellationTokenSource();
        var host = new TurnoOfertaHost((size, cursor, token) =>
        {
            Assert.Equal(100, size);
            cursors.Add(cursor);
            return cursors.Count switch
            {
                1 => Task.FromException<string?>(new TurnoOfertaSweep.BatchFailedException(
                    new TurnoOfertaSweep.BatchResult("page-1", new[] { "failed-1" }))),
                2 => Task.FromResult<string?>("page-2"),
                3 => Task.FromException<string?>(new TurnoOfertaSweep.BatchFailedException(
                    new TurnoOfertaSweep.BatchResult(null, new[] { "failed-last" }))),
                _ => Task.FromResult<string?>(null)
            };
        }, (delay, token) =>
        {
            Assert.Equal(TimeSpan.FromMinutes(1), delay);
            if (++delays == 4) stop.Cancel();
            return Task.CompletedTask;
        }, NullLogger<TurnoOfertaHost>.Instance);

        await host.RunAsync(stop.Token);
        Assert.Equal(new string?[] { null, "page-1", "page-2", null }, cursors);
        Assert.Equal(4, delays);
    }

    [Fact]
    public async Task CancellationDuringBackoffStopsWithoutAnotherBatch()
    {
        var calls = 0;
        using var stop = new CancellationTokenSource();
        var host = new TurnoOfertaHost((size, cursor, token) =>
        {
            calls++;
            return Task.FromResult<string?>(null);
        }, async (delay, token) =>
        {
            stop.Cancel();
            await Task.Delay(delay, token);
        }, NullLogger<TurnoOfertaHost>.Instance);

        await host.RunAsync(stop.Token);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task CancellationDuringBatchStopsWithoutBackoff()
    {
        using var stop = new CancellationTokenSource();
        var delays = 0;
        var host = new TurnoOfertaHost((size, cursor, token) =>
        {
            stop.Cancel();
            return Task.FromCanceled<string?>(token);
        }, (delay, token) => { delays++; return Task.CompletedTask; }, NullLogger<TurnoOfertaHost>.Instance);

        await host.RunAsync(stop.Token);
        Assert.Equal(0, delays);
    }
}

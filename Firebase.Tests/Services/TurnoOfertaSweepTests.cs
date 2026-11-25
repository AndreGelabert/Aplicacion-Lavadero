using Firebase.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Firebase.Tests.Services;

public class TurnoOfertaSweepClassificationTests
{
    [Fact]
    public async Task PendingRecordsReachResumeAndFailuresAreReported()
    {
        var failures = new List<string>();
        var visited = new List<string>();
        await TurnoOfertaSweep.ProcessPendingAsync("partial", id => { visited.Add(id); throw new InvalidOperationException(); }, failures, NullLogger<TurnoOfertaSweep>.Instance, CancellationToken.None);
        await TurnoOfertaSweep.ProcessPendingAsync("valid", id => { visited.Add(id); return Task.CompletedTask; }, failures, NullLogger<TurnoOfertaSweep>.Instance, CancellationToken.None);
        Assert.Equal(new[] { "partial", "valid" }, visited);
        Assert.Equal(new[] { "partial" }, failures);
    }
}

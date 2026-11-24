using Firebase.Services;

namespace Firebase.Tests.Services;

public class TurnoOfertaClockTests
{
    public static TheoryData<DateTime> InvalidClocks => new()
    {
        new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Local),
        new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Unspecified),
        default(DateTime),
        DateTime.SpecifyKind(default, DateTimeKind.Utc),
        new DateTime(1, DateTimeKind.Utc),
        new DateTime(9, DateTimeKind.Utc)
    };

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(9)]
    public void UtcClockPreservesInstantAtMicrosecondPrecision(int extraTicks)
    {
        var expected = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(120);
        var result = TurnoOfertaOrquestacion.NormalizeClock(expected.AddTicks(extraTicks));
        Assert.Equal(expected, result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Fact]
    public async Task ValidUtcClockStillReachesFirestoreForBothOperations()
    {
        var orchestration = new TurnoOfertaOrquestacion(null!, () => new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(9));
        await Assert.ThrowsAsync<NullReferenceException>(() => orchestration.Resume("cascade"));
        await Assert.ThrowsAsync<NullReferenceException>(() => orchestration.SelectAndCreate("cascade"));
    }

    [Theory]
    [MemberData(nameof(InvalidClocks))]
    public async Task ResumeRejectsInvalidClockBeforeAccessingFirestore(DateTime now)
    {
        var orchestration = new TurnoOfertaOrquestacion(null!, () => now);

        await Assert.ThrowsAsync<InvalidOperationException>(() => orchestration.Resume("cascade"));
    }

    [Theory]
    [MemberData(nameof(InvalidClocks))]
    public async Task SelectAndCreateRejectsInvalidClockBeforeAccessingFirestore(DateTime now)
    {
        var orchestration = new TurnoOfertaOrquestacion(null!, () => now);

        await Assert.ThrowsAsync<InvalidOperationException>(() => orchestration.SelectAndCreate("cascade"));
    }
}

using Firebase.Services;

namespace Firebase.Tests.Services;

public class TurnoAdelantoEligibilityPolicyTests
{
    private static readonly DateTime Day = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static DateTime At(int minute) => Day.AddMinutes(minute);

    [Theory]
    [InlineData(479, true)]
    [InlineData(480, false)]
    [InlineData(481, false)]
    public void AdvanceRequiresStrictlyEarlierStart(int proposed, bool expected)
    {
        Assert.Equal(expected, TurnoAdelantoEligibilityPolicy.IsEarlier(At(proposed), At(480)));
    }

    [Theory]
    [InlineData(DateTimeKind.Local, true)]
    [InlineData(DateTimeKind.Unspecified, true)]
    [InlineData(DateTimeKind.Local, false)]
    [InlineData(DateTimeKind.Unspecified, false)]
    public void AdvanceRejectsNonUtcInstants(DateTimeKind kind, bool proposedIsNonUtc)
    {
        var nonUtc = DateTime.SpecifyKind(At(479), kind);
        var error = Assert.Throws<ArgumentException>(() =>
            TurnoAdelantoEligibilityPolicy.IsEarlier(
                proposedIsNonUtc ? nonUtc : At(479),
                proposedIsNonUtc ? At(480) : nonUtc));

        Assert.Equal("Instants must be UTC.", error.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ExcludingReservationsRejectsNullOrEmptyOriginalId(string? originalId)
    {
        var candidates = new[] { new TurnoCapacityCandidate("other", new(At(480), At(540), 1)) };

        var error = Assert.ThrowsAny<ArgumentException>(() =>
            TurnoAdelantoEligibilityPolicy.OtherReservations(originalId!, candidates));

        Assert.Equal(originalId is null ? typeof(ArgumentNullException) : typeof(ArgumentException), error.GetType());
        Assert.Equal("originalId", error.ParamName);
        Assert.Single(candidates);
        Assert.Equal("other", candidates[0].Id);
    }

    [Fact]
    public void ExcludesOnlyOriginalByIdentityWithoutMutatingInput()
    {
        var original = new TurnoCapacityCandidate("original", new(At(480), At(540), 1));
        var sameInterval = new TurnoCapacityCandidate("other", original.Interval);
        var later = new TurnoCapacityCandidate("later", new(At(540), At(600), 2));
        TurnoCapacityCandidate[] candidates = [original, sameInterval, later];

        var result = TurnoAdelantoEligibilityPolicy.OtherReservations("original", candidates);

        Assert.Equal(new[] { sameInterval.Interval, later.Interval }, result);
        Assert.Equal(new[] { original, sameInterval, later }, candidates);
        Assert.Equal(candidates.Select(c => c.Interval), TurnoAdelantoEligibilityPolicy.OtherReservations("missing", candidates));
    }

    [Fact]
    public void OverlapWithOriginalIsReplacedButOtherReservationsStillConsumeBothCapacities()
    {
        TurnoCapacityCandidate[] candidates =
        [
            new("original", new(At(510), At(570), 1)),
            new("other", new(At(495), At(525), 2))
        ];
        var others = TurnoAdelantoEligibilityPolicy.OtherReservations("original", candidates);
        bool Available(int slots, int staff, IEnumerable<TurnoInterval> existing) =>
            TurnoAvailabilityPolicy.IsAvailable(At(495), [60], 1, slots, staff,
                [new(At(480), At(600))], existing, At(0), At(0));

        Assert.True(Available(2, 3, others));
        Assert.False(Available(1, 3, others));
        Assert.False(Available(2, 2, others));
        Assert.False(Available(2, 3, candidates.Select(c => c.Interval)));
    }
}

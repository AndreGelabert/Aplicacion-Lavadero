using Firebase.Services;

namespace Firebase.Tests.Services;

public class TurnoAdelantoCandidateSelectorTests
{
    private static readonly DateTime Day = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static TurnoAdelantoCandidate Candidate(string id, int start, int booked, bool optedIn = true, bool eligible = true) =>
        new(id, Day.AddMinutes(start), Day.AddMinutes(booked), optedIn, eligible);

    [Fact]
    public void OrdersNearestThenOldestThenOrdinalIdWithoutMutatingInput()
    {
        TurnoAdelantoCandidate[] candidates =
        [
            Candidate("far", 120, 1), Candidate("z", 60, 20), Candidate("b", 60, 10),
            Candidate("a", 60, 10), Candidate("out", 30, 1, optedIn: false),
            Candidate("ineligible", 30, 1, eligible: false), Candidate("same", 0, 1),
            Candidate("before", -1, 1)
        ];
        var original = candidates.ToArray();

        var result = TurnoAdelantoCandidateSelector.Order(Day, candidates);

        Assert.Equal(new[] { "a", "b", "z", "far" }, result.Select(c => c.Id));
        Assert.Equal(original, candidates);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void RejectsMissingOrNonUtcBookingMetadataWithoutMutatingInput(DateTimeKind? kind)
    {
        DateTime? booked = kind.HasValue ? DateTime.SpecifyKind(Day, kind.Value) : null;
        TurnoAdelantoCandidate[] candidates = [Candidate("valid", 30, 1), new("invalid", Day.AddMinutes(60), booked, true, true)];
        var original = candidates.ToArray();

        Assert.Throws<ArgumentException>(() => TurnoAdelantoCandidateSelector.Order(Day, candidates));
        Assert.Equal(original, candidates);
    }

    [Fact]
    public void ReturnsEmptyWhenNoCandidatesQualify()
    {
        Assert.Empty(TurnoAdelantoCandidateSelector.Order(Day, [Candidate("out", 30, 1, false)]));
    }
}

using Firebase.Services;

namespace Firebase.Tests.Services;

public class TurnoAvailabilityPreviewTests
{
    private static readonly DateTime Day = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static DateTime At(int minutes) => Day.AddMinutes(minutes);
    private static TurnoAvailabilityPreviewResult[] Preview(DateTime[] starts, int[] durations,
        int staff = 1, int slots = 2, int active = 2, TurnoOpening[]? openings = null,
        TurnoInterval[]? reservations = null, int grid = 15) =>
        TurnoAvailabilityPreview.Calculate(starts, durations, staff, slots, active,
            openings ?? [new(At(480), At(720))], reservations ?? [], At(480), At(0), grid).ToArray();

    [Fact]
    public void PreservesOrderDuplicatesAndExactSummedDuration()
    {
        var results = Preview([At(510), At(480), At(510)], [7, 13], staff: 2);
        Assert.Equal(new[] { At(510), At(480), At(510) }, results.Select(x => x.Start));
        Assert.Equal(new[] { At(530), At(500), At(530) }, results.Select(x => x.End));
        Assert.All(results, x => Assert.True(x.Available));
    }

    [Fact]
    public void ReportsCapacityOpeningGridAndPastBoundaries()
    {
        var results = Preview([At(480), At(495), At(496), At(465), At(705)], [16], slots: 1,
            reservations: [new(At(500), At(510), 1)]);
        Assert.Equal(new[] { true, false, false, false, false }, results.Select(x => x.Available));
        Assert.False(Preview([At(480)], [15], staff: 2, active: 1)[0].Available);
        Assert.False(Preview([At(480)], [15], slots: 2, active: 2,
            reservations: [new(At(480), At(495), 2)])[0].Available);
        Assert.True(Preview([At(510)], [15], slots: 1,
            reservations: [new(At(495), At(510), 1)])[0].Available);
    }

    [Fact]
    public void EmptyCandidatesReturnEmptyAndInputsAreNotMutated()
    {
        Assert.Empty(Preview([], [15]));
        var starts = new[] { At(495), At(480) };
        var durations = new[] { 10, 5 };
        var openings = new[] { new TurnoOpening(At(480), At(720)) };
        var reservations = new[] { new TurnoInterval(At(500), At(515), 1) };
        var startsCopy = starts.ToArray(); var durationsCopy = durations.ToArray();
        var openingsCopy = openings.ToArray(); var reservationsCopy = reservations.ToArray();
        Preview(starts, durations, openings: openings, reservations: reservations);
        Assert.Equal(startsCopy, starts); Assert.Equal(durationsCopy, durations);
        Assert.Equal(openingsCopy, openings); Assert.Equal(reservationsCopy, reservations);
    }

    [Fact]
    public void InvalidInputsAreRejectedByPolicy()
    {
        Assert.Throws<ArgumentException>(() => Preview([At(480)], []));
        Assert.Throws<ArgumentException>(() => Preview([At(480)], [0]));
        Assert.Throws<ArgumentOutOfRangeException>(() => Preview([At(480)], [15], grid: 0));
        Assert.Throws<ArgumentException>(() => Preview([DateTime.SpecifyKind(At(480), DateTimeKind.Unspecified)], [15]));
        Assert.Throws<ArgumentException>(() => Preview([At(480)], [15], openings: [new(At(500), At(500))]));
        Assert.Throws<ArgumentException>(() => Preview([At(480)], [15], reservations: [new(At(500), At(510), 0)]));
    }
}

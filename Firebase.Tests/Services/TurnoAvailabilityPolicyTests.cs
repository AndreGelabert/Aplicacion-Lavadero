using Firebase.Services;

namespace Firebase.Tests.Services;

public class TurnoAvailabilityPolicyTests
{
    private static readonly DateTime Day = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static DateTime At(int minutes) => Day.AddMinutes(minutes);
    private static TurnoInterval Interval(int start, int end, int staff = 1) => new(At(start), At(end), staff);
    private static bool Available(int start, int[] durations, int staff = 1, int slots = 2, int active = 2,
        TurnoInterval[]? existing = null, TurnoOpening[]? openings = null, int grid = 15, int anchor = 0, int now = 0) =>
        TurnoAvailabilityPolicy.IsAvailable(At(start), durations, staff, slots, active,
            openings ?? [new(At(480), At(720)), new(At(780), At(1020))],
            existing ?? [], At(now), At(anchor), grid);

    [Fact]
    public void DurationIsSumWithoutStaffScalingOrGridRounding()
    {
        Assert.True(Available(690, [10, 20], staff: 2));
        Assert.False(Available(690, [10, 21], staff: 2));
        Assert.True(Available(495, [7], existing: [Interval(502, 510, 2)]));
    }

    [Fact]
    public void WholeIntervalMustFitOneOpeningAndRespectGridAndNow()
    {
        Assert.True(Available(705, [15], now: 705));
        Assert.False(Available(705, [16]));
        Assert.False(Available(720, [60]));
        Assert.False(Available(1005, [16]));
        Assert.False(Available(496, [15]));
        Assert.False(Available(495, [15], now: 496));
        Assert.True(Available(497, [15], grid: 10, anchor: 487));
        Assert.False(Available(495, [15], grid: 10, anchor: 487));
    }

    [Fact]
    public void AdjacentIntervalsDoNotOverlap()
    {
        Assert.True(Available(510, [15], slots: 1, active: 1,
            existing: [Interval(495, 510), Interval(525, 540)]));
        Assert.False(Available(510, [16], slots: 1, active: 1,
            existing: [Interval(525, 540)]));
    }

    [Fact]
    public void CapacityIsCheckedAtActualBoundariesNotAcrossWholeProposal()
    {
        Assert.True(Available(480, [60], slots: 2, active: 3,
            existing: [Interval(480, 510, 2), Interval(510, 540, 2)]));
        Assert.False(Available(480, [60], slots: 2, active: 4,
            existing: [Interval(480, 520), Interval(510, 540)]));
        Assert.False(Available(480, [60], slots: 3, active: 3,
            existing: [Interval(480, 520, 2), Interval(510, 540)]));
    }

    [Fact]
    public void RejectsInvalidInputsAndNonUtcInstants()
    {
        Assert.Throws<ArgumentException>(() => Available(480, []));
        Assert.Throws<ArgumentException>(() => Available(480, [0]));
        Assert.Throws<ArgumentException>(() => Available(480, [10, -1]));
        Assert.Throws<ArgumentOutOfRangeException>(() => Available(480, [15], staff: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Available(480, [15], slots: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Available(480, [15], active: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Available(480, [15], grid: 0));
        Assert.Throws<ArgumentException>(() => Available(480, [15], existing: [Interval(500, 500)]));
        Assert.Throws<ArgumentException>(() => Available(480, [15], existing: [Interval(500, 510, 0)]));
        Assert.Throws<ArgumentException>(() => Available(480, [15], openings: [new(At(500), At(500))]));
        Assert.Throws<ArgumentException>(() => TurnoAvailabilityPolicy.IsAvailable(DateTime.SpecifyKind(At(480), DateTimeKind.Unspecified),
            [15], 1, 1, 1, [new(At(480), At(600))], [], At(0), At(0)));
    }

    [Fact]
    public void DefaultGridIsFifteenMinutesAndExcessRequestedStaffIsUnavailable()
    {
        Assert.True(TurnoAvailabilityPolicy.IsAvailable(At(495), [15], 1, 1, 1,
            [new(At(480), At(600))], [], At(0), At(0)));
        Assert.False(Available(495, [15], staff: 3, active: 2));
    }

    [Fact]
    public void CheckAvailabilityReturnsDescriptiveReasons()
    {
        // Past time
        var past = TurnoAvailabilityPolicy.CheckAvailability(At(500), [15], 1, 2, 2, [new(At(480), At(720))], [], At(501), At(0), 15);
        Assert.False(past.IsAvailable);
        Assert.Equal(TurnoAvailabilityFailureReason.PastTime, past.Reason);
        Assert.Contains("pasado", past.Message);

        // Invalid grid
        var grid = TurnoAvailabilityPolicy.CheckAvailability(At(505), [15], 1, 2, 2, [new(At(480), At(720))], [], At(0), At(0), 15);
        Assert.False(grid.IsAvailable);
        Assert.Equal(TurnoAvailabilityFailureReason.InvalidGridInterval, grid.Reason);
        Assert.Contains("15 minutos", grid.Message);

        // Outside business hours
        var closed = TurnoAvailabilityPolicy.CheckAvailability(At(450), [15], 1, 2, 2, [new(At(480), At(720))], [], At(0), At(0), 15);
        Assert.False(closed.IsAvailable);
        Assert.Equal(TurnoAvailabilityFailureReason.OutsideBusinessHours, closed.Reason);
        Assert.Contains("cerrado", closed.Message);

        // Exceeds closing time
        var exceeds = TurnoAvailabilityPolicy.CheckAvailability(At(705), [30], 1, 2, 2, [new(At(480), At(720))], [], At(0), At(0), 15);
        Assert.False(exceeds.IsAvailable);
        Assert.Equal(TurnoAvailabilityFailureReason.ExceedsClosingTime, exceeds.Reason);
        Assert.Contains("cierre", exceeds.Message);

        // Insufficient active staff
        var staff = TurnoAvailabilityPolicy.CheckAvailability(At(495), [15], 3, 2, 2, [new(At(480), At(720))], [], At(0), At(0), 15);
        Assert.False(staff.IsAvailable);
        Assert.Equal(TurnoAvailabilityFailureReason.InsufficientActiveStaff, staff.Reason);
        Assert.Contains("requiere 3 empleados", staff.Message);
    }

    [Fact]
    public void ParallelTurnosAllowedWhenCapacityAndStaffAreSufficient()
    {
        // Existing turno at 11:00 (660 min) for 60 min (660-720) with 2 staff
        var existing = new TurnoInterval[] { Interval(660, 720, staff: 2) };

        // Candidate turno at 10:30 (630 min) for 45 min (630-675) with 2 staff
        // Overlap occurs between 660 and 675 (15 min) -> Slots = 2, Staff = 4
        var resultSuccess = TurnoAvailabilityPolicy.CheckAvailability(At(630), [45], requiredStaff: 2,
            maxSlots: 2, activeStaff: 4, openings: [new(At(480), At(780))], reservations: existing, now: At(0), gridAnchor: At(0), gridMinutes: 15);
        Assert.True(resultSuccess.IsAvailable);
        Assert.Equal(TurnoAvailabilityFailureReason.None, resultSuccess.Reason);

        // If slots capacity is only 1, concurrent booking fails with ConcurrentSlotsExceeded
        var resultSlotsFail = TurnoAvailabilityPolicy.CheckAvailability(At(630), [45], requiredStaff: 2,
            maxSlots: 1, activeStaff: 4, openings: [new(At(480), At(780))], reservations: existing, now: At(0), gridAnchor: At(0), gridMinutes: 15);
        Assert.False(resultSlotsFail.IsAvailable);
        Assert.Equal(TurnoAvailabilityFailureReason.ConcurrentSlotsExceeded, resultSlotsFail.Reason);
        Assert.Contains("capacidad máxima concurrente", resultSlotsFail.Message);

        // If active staff is 3 (less than 2+2=4), concurrent booking fails with ConcurrentStaffExceeded
        var resultStaffFail = TurnoAvailabilityPolicy.CheckAvailability(At(630), [45], requiredStaff: 2,
            maxSlots: 5, activeStaff: 3, openings: [new(At(480), At(780))], reservations: existing, now: At(0), gridAnchor: At(0), gridMinutes: 15);
        Assert.False(resultStaffFail.IsAvailable);
        Assert.Equal(TurnoAvailabilityFailureReason.ConcurrentStaffExceeded, resultStaffFail.Reason);
        Assert.Contains("personal disponible", resultStaffFail.Message);
    }
}

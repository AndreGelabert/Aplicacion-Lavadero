namespace Firebase.Services;

public readonly record struct TurnoAvailabilityPreviewResult(DateTime Start, DateTime End, bool Available);

/// <summary>Advisory only: booking must revalidate availability transactionally.</summary>
public static class TurnoAvailabilityPreview
{
    public static IReadOnlyList<TurnoAvailabilityPreviewResult> Calculate(
        IEnumerable<DateTime> starts, IEnumerable<int> serviceMinutes, int requiredStaff,
        int maxSlots, int activeStaff, IEnumerable<TurnoOpening> openings,
        IEnumerable<TurnoInterval> reservations, DateTime now, DateTime gridAnchor, int gridMinutes = 15)
    {
        ArgumentNullException.ThrowIfNull(starts);
        ArgumentNullException.ThrowIfNull(serviceMinutes);
        ArgumentNullException.ThrowIfNull(openings);
        ArgumentNullException.ThrowIfNull(reservations);
        var durations = serviceMinutes.ToArray();
        var openingList = openings.ToArray();
        var reservationList = reservations.ToArray();
        var results = new List<TurnoAvailabilityPreviewResult>();
        foreach (var start in starts)
        {
            var available = TurnoAvailabilityPolicy.IsAvailable(start, durations, requiredStaff,
                maxSlots, activeStaff, openingList, reservationList, now, gridAnchor, gridMinutes);
            long minutes = 0;
            foreach (var duration in durations) minutes = checked(minutes + duration);
            var end = start.AddTicks(checked(minutes * TimeSpan.TicksPerMinute));
            results.Add(new(start, end, available));
        }
        return results;
    }
}

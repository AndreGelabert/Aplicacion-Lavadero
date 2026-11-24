namespace Firebase.Services;

/// <summary>A reservation identity and its capacity demand.</summary>
public readonly record struct TurnoCapacityCandidate(string Id, TurnoInterval Interval);

/// <summary>Pure preparation helpers; capacity must be revalidated transactionally on acceptance.</summary>
public static class TurnoAdelantoEligibilityPolicy
{
    /// <summary>Compares UTC appointment instants without deciding any other eligibility rule.</summary>
    public static bool IsEarlier(DateTime proposedStart, DateTime originalStart)
    {
        if (proposedStart.Kind != DateTimeKind.Utc || originalStart.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Instants must be UTC.");
        return proposedStart < originalStart;
    }

    /// <summary>Excludes the replaced appointment by identity, retaining all other capacity demand.</summary>
    public static TurnoInterval[] OtherReservations(string originalId, IEnumerable<TurnoCapacityCandidate> candidates)
    {
        ArgumentException.ThrowIfNullOrEmpty(originalId);
        ArgumentNullException.ThrowIfNull(candidates);
        return candidates.Where(candidate => !string.Equals(candidate.Id, originalId, StringComparison.Ordinal))
            .Select(candidate => candidate.Interval).ToArray();
    }
}

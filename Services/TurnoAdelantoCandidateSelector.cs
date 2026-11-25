namespace Firebase.Services;

/// <summary>Caller-supplied appointment data; booking creation must come from the server, not inferred legacy age.</summary>
internal readonly record struct TurnoAdelantoCandidate(
    string Id, DateTime StartUtc, DateTime? BookingCreatedUtc, bool OptedIn, bool Eligible);

/// <summary>Pure ordering only. The caller supplies authoritative metadata and independently revalidates capacity on acceptance.</summary>
internal static class TurnoAdelantoCandidateSelector
{
    internal static TurnoAdelantoCandidate[] Order(DateTime vacancyStartUtc, IEnumerable<TurnoAdelantoCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (vacancyStartUtc == default || vacancyStartUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Vacancy start must be UTC.", nameof(vacancyStartUtc));

        var snapshot = candidates.ToArray();
        foreach (var candidate in snapshot)
        {
            if (string.IsNullOrWhiteSpace(candidate.Id) || candidate.StartUtc == default || candidate.StartUtc.Kind != DateTimeKind.Utc ||
                !candidate.BookingCreatedUtc.HasValue || candidate.BookingCreatedUtc.Value == default || candidate.BookingCreatedUtc.Value.Kind != DateTimeKind.Utc)
                throw new ArgumentException("Candidate identity and UTC appointment and booking creation times are required.", nameof(candidates));
        }

        return snapshot.Where(c => c.OptedIn && c.Eligible && c.StartUtc > vacancyStartUtc)
            .OrderBy(c => c.StartUtc)
            .ThenBy(c => c.BookingCreatedUtc!.Value)
            .ThenBy(c => c.Id, StringComparer.Ordinal)
            .ToArray();
    }
}

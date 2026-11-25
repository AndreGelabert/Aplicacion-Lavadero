using Google.Cloud.Firestore;

namespace Firebase.Services;

/// <summary>Read-only candidate discovery; results are stale immediately and never authorize an offer.</summary>
internal sealed class TurnoAdelantoCandidateQuery(FirestoreDb db)
{
    internal async Task<TurnoAdelantoCandidate[]> Find(DateTime vacancyStartUtc)
    {
        if (vacancyStartUtc == default || vacancyStartUtc.Kind != DateTimeKind.Utc ||
            vacancyStartUtc.Ticks > DateTime.MaxValue.Ticks - TimeSpan.FromDays(7).Ticks)
            throw new ArgumentException("Vacancy start must be UTC and permit a seven-day window.", nameof(vacancyStartUtc));

        var limit = vacancyStartUtc.AddDays(7);
        var snapshot = await db.Collection("turnos")
            .WhereGreaterThan("InicioUtc", vacancyStartUtc)
            .WhereLessThanOrEqualTo("InicioUtc", limit)
            .Select("InicioUtc", "Estado", "AceptaAdelanto")
            .GetSnapshotAsync();
        var candidates = new List<TurnoAdelantoCandidate>();
        foreach (var doc in snapshot.Documents)
        {
            if (doc.GetValue<string>("Estado") != "Pendiente" || !doc.GetValue<bool>("AceptaAdelanto")) continue;
            var start = doc.GetValue<DateTime>("InicioUtc");
            var anchor = await db.Collection("turno_adelanto_anclas").Document(doc.Id).GetSnapshotAsync();
            var original = anchor.Exists ? anchor.GetValue<DateTime>("AnchorUtc") : start;
            if (original.Kind != DateTimeKind.Utc || original < start)
                throw new InvalidOperationException("Invalid appointment anchor.");
            if (original.Ticks - vacancyStartUtc.Ticks > TimeSpan.FromDays(7).Ticks) continue;
            if (!doc.CreateTime.HasValue)
                throw new InvalidOperationException("Appointment creation metadata is missing.");
            candidates.Add(new(doc.Id, start, doc.CreateTime.Value.ToDateTime(), true, true));
        }
        return TurnoAdelantoCandidateSelector.Order(vacancyStartUtc, candidates);
    }
}

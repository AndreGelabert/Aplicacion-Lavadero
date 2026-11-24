using Google.Cloud.Firestore;

namespace Firebase.Services;

/// <summary>Internal selection and explicit continuation; does not schedule or deliver offers.</summary>
internal sealed class TurnoOfertaOrquestacion(FirestoreDb db, Func<DateTime>? clock = null)
{
    internal Func<Task>? AfterPendingRead { get; init; }

    internal static DateTime NormalizeClock(DateTime now)
    {
        var ticks = now.Ticks - now.Ticks % 10;
        if (now.Kind != DateTimeKind.Utc || ticks == 0)
            throw new InvalidOperationException("Clock must return a non-default UTC instant.");
        return new DateTime(ticks, DateTimeKind.Utc);
    }

    internal async Task<string?> Resume(string cascadeId)
    {
        var now = NormalizeClock(clock?.Invoke() ?? DateTime.UtcNow);
        var cascade = await db.Collection("turno_cascadas").Document(cascadeId).GetSnapshotAsync();
        if (cascade.Exists && cascade.TryGetValue<string>("PendingId", out var pending) && !string.IsNullOrEmpty(pending))
        {
            var offer = await db.Collection("turno_ofertas").Document(pending).GetSnapshotAsync();
            if (AfterPendingRead != null) await AfterPendingRead();
            if (offer.Exists && offer.TryGetValue<DateTime>("ExpiresUtc", out var deadline) && now >= deadline)
            {
                var persistence = new TurnoOfertaPersistence(db);
                try { await persistence.Expire(cascadeId, pending, now); }
                catch (InvalidOperationException)
                {
                    // Only a verified terminal transition can explain a stale observation.
                    var current = await offer.Reference.GetSnapshotAsync();
                    if (!current.Exists || !current.TryGetValue<string>("Estado", out var state)) throw;
                    if (state == "Rechazada") await persistence.Reject(cascadeId, pending, now);
                    else if (state == "Aceptada") await persistence.Accept(cascadeId, pending, now);
                    else throw;
                }
            }
        }
        return await new TurnoOfertaPersistence(db).SelectAndCreateConfigured(cascadeId, Guid.NewGuid().ToString("N"), now);
    }

    internal Task<string?> SelectAndCreate(string cascadeId)
    {
        var now = NormalizeClock(clock?.Invoke() ?? DateTime.UtcNow);
        return new TurnoOfertaPersistence(db).SelectAndCreateConfigured(cascadeId, Guid.NewGuid().ToString("N"), now);
    }
}

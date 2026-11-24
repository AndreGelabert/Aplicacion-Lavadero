using Google.Cloud.Firestore;

namespace Firebase.Services;

// Discovery is advisory: MarcarAusencia rechecks state and configuration in its transaction.
internal sealed class TurnoNoShowSweep(FirestoreDb db, TurnoService turnos, ILogger<TurnoNoShowSweep> logger)
{
    // Cursor is retained by the host across batches, then wraps to revisit failures and new records.
    internal async Task<string?> RunBatchAsync(Func<DateTime> utcNow, int pageSize, string? cursor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(utcNow);
        if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));
        cancellationToken.ThrowIfCancellationRequested();
        var config = await db.Collection("configuracion").Document("system_config").GetSnapshotAsync(cancellationToken);
        long grace = 5;
        if (config.Exists && config.ToDictionary().TryGetValue("ToleranciaLlegadaTurnosMinutos", out var raw) && raw is long g && g >= 5)
            grace = g;
        if (grace < 5 || grace > 525600)
            throw new InvalidOperationException("Tolerancia de llegada no disponible.");

        // Equality uses the automatic Estado index, whose document-name tie breaker
        // supports this stable cursor; changes behind it are revisited on wrap.
        Query query = db.Collection("turnos").WhereEqualTo("Estado", "Pendiente")
            .OrderBy(FieldPath.DocumentId).Limit(pageSize);
        if (cursor != null) query = query.StartAfter(db.Collection("turnos").Document(cursor));
        var page = await query.GetSnapshotAsync(cancellationToken);
        foreach (var doc in page.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!doc.TryGetValue<string>("Estado", out var state) || state != "Pendiente" ||
                !doc.TryGetValue<DateTime>("InicioUtc", out var start)) continue;
            var now = utcNow();
            if (now == default || now.Kind != DateTimeKind.Utc) throw new ArgumentException("Se requiere hora UTC.", nameof(utcNow));
            if (start >= now) continue;
            try { await turnos.MarcarAusencia(doc.Id, now); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex) { logger.LogError(ex, "No se pudo procesar ausencia del turno {TurnoId}", doc.Id); }
        }
        return page.Count < pageSize ? null : page.Documents[^1].Id;
    }
}

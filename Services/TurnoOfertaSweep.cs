using Google.Cloud.Firestore;

namespace Firebase.Services;

// Explicitly invoked discovery only. The caller retains the cursor and must keep
// invoking batches; restart from null is safe. Resume owns transactional validation.
internal sealed class TurnoOfertaSweep(FirestoreDb db, ILogger<TurnoOfertaSweep> logger, Func<DateTime>? clock = null)
{
    // The failure list is per batch; callers must retain failures across pages and
    // arrange subsequent scans. A null cursor alone does not mean a clean scan.
    internal sealed record BatchResult(string? Cursor, IReadOnlyList<string> FailedCascadeIds);

    internal sealed class BatchFailedException(BatchResult result)
        : InvalidOperationException($"No se pudieron continuar las cascadas: {string.Join(", ", result.FailedCascadeIds)}")
    {
        internal string? Cursor { get; } = result.Cursor;
        internal IReadOnlyList<string> FailedCascadeIds { get; } = result.FailedCascadeIds;
    }

    internal async Task<string?> RunBatchAsync(int pageSize, string? cursor, CancellationToken cancellationToken)
    {
        var result = await RunBatchWithFailuresAsync(pageSize, cursor, cancellationToken);
        if (result.FailedCascadeIds.Count != 0) throw new BatchFailedException(result);
        return result.Cursor;
    }

    internal async Task<BatchResult> RunBatchWithFailuresAsync(int pageSize, string? cursor, CancellationToken cancellationToken)
    {
        if (pageSize <= 0 || pageSize > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));
        cancellationToken.ThrowIfCancellationRequested();
        Query query = db.Collection("turno_cascadas").WhereEqualTo("ContinuationState", "Pendiente")
            .OrderBy(FieldPath.DocumentId).Limit(pageSize);
        if (cursor != null) query = query.StartAfter(db.Collection("turno_cascadas").Document(cursor));
        var page = await query.GetSnapshotAsync(cancellationToken);
        var orchestration = new TurnoOfertaOrquestacion(db, clock);
        var failures = new List<string>();
        foreach (var cascade in page.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // The query excludes legacy offer-only records without ContinuationState.
            // Every returned record must reach transactional validation, even if partial.
            await ProcessPendingAsync(cascade.Id, id => orchestration.Resume(id), failures, logger, cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new BatchResult(page.Count < pageSize ? null : page.Documents[^1].Id, failures.AsReadOnly());
    }

    internal static async Task ProcessPendingAsync(string id, Func<string, Task> resume, List<string> failures,
        ILogger logger, CancellationToken cancellationToken)
    {
        try { await resume(id); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo continuar la cascada {CascadeId}", id);
            failures.Add(id);
        }
    }
}

using Google.Cloud.Firestore;

namespace Firebase.BackgroundServices;

// Isolated: callers supply channel routing; never registered by this type.
internal sealed class TurnoOutboxHost : BackgroundService
{
    internal sealed record Item(string Id, string Event, string State, string Channel, DateTime? LeaseUntil = null);
    internal sealed record Page(IReadOnlyList<Item> Items, string? Cursor);
    private const int PageSize = 100;
    private static readonly TimeSpan Backoff = TimeSpan.FromMinutes(1);
    private readonly Func<int, string?, CancellationToken, Task<Page>> discover;
    private readonly Func<string, string, CancellationToken, Task> route;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;
    private readonly ILogger<TurnoOutboxHost> logger;
    private readonly Func<DateTime> clock;

    internal TurnoOutboxHost(FirestoreDb db, Func<string, string, CancellationToken, Task> route,
        ILogger<TurnoOutboxHost> logger, Func<DateTime>? clock = null)
        : this(async (size, cursor, token) =>
        {
            Query query = db.Collection("turno_outbox").OrderBy(FieldPath.DocumentId).Limit(size);
            if (cursor != null) query = query.StartAfter(cursor);
            var snapshot = await query.GetSnapshotAsync(token);
            var items = snapshot.Documents.Select(doc =>
            {
                var data = doc.ToDictionary();
                string Field(string key) => data.TryGetValue(key, out var value) && value is string s ? s : "";
                var lease = data.TryGetValue("LeaseUntil", out var rawLease) && rawLease is Timestamp timestamp
                    ? timestamp.ToDateTime() : (DateTime?)null;
                return new Item(doc.Id, Field("Event"), Field("State"), Field("Channel"), lease);
            }).ToArray();
            return new Page(items, items.Length == size ? items[^1].Id : null);
        }, route, Task.Delay, logger, clock) { }

    internal TurnoOutboxHost(Func<int, string?, CancellationToken, Task<Page>> discover,
        Func<string, string, CancellationToken, Task> route, Func<TimeSpan, CancellationToken, Task> delay,
        ILogger<TurnoOutboxHost> logger, Func<DateTime>? clock = null)
    {
        this.clock = clock ?? (() => DateTime.UtcNow);
        this.discover = discover ?? throw new ArgumentNullException(nameof(discover));
        this.route = route ?? throw new ArgumentNullException(nameof(route));
        this.delay = delay ?? throw new ArgumentNullException(nameof(delay));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => RunAsync(stoppingToken);

    internal async Task RunAsync(CancellationToken stoppingToken)
    {
        string? cursor = null;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var page = await discover(PageSize, cursor, stoppingToken);
                foreach (var item in page.Items)
                {
                    stoppingToken.ThrowIfCancellationRequested();
                    // Discovery is advisory: the dispatcher must claim transactionally before sending.
                    var eligible = item.State is "Pending" or "Released" ||
                        (item.State == "Claimed" && item.LeaseUntil is DateTime lease &&
                         lease.Kind == DateTimeKind.Utc && lease <= clock());
                    if (item.Event != "Offer" || !eligible ||
                        item.Channel is not ("WhatsApp" or "Email")) continue;
                    try { await route(item.Id, item.Channel, stoppingToken); }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception ex) { logger.LogError(ex, "Error al procesar notificación de oferta"); }
                }
                cursor = page.Cursor;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Error al buscar notificaciones de ofertas"); }
            if (stoppingToken.IsCancellationRequested) break;
            try { await delay(Backoff, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}

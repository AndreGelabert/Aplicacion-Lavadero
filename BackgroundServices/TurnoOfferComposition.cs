using Firebase.Services;
using Google.Cloud.Firestore;

namespace Firebase.BackgroundServices;

// Deliberately unregistered. Constructing this graph does not start either host.
internal sealed record TurnoOfferComposition(TurnoOfertaHost OfferHost, TurnoOutboxHost OutboxHost)
{
    internal static TurnoOfferComposition Create(FirestoreDb db, IServiceScopeFactory scopes,
        string? origin, TimeSpan lease, Func<DateTime> serverUtcClock, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(serverUtcClock);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        if (lease <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(lease));
        DateTime Clock()
        {
            var now = serverUtcClock();
            if (now == default || now.Kind != DateTimeKind.Utc) throw new ArgumentException("Clock must return UTC.", nameof(serverUtcClock));
            return now.AddTicks(-(now.Ticks % 10));
        }
        _ = Clock();
        var sweep = new TurnoOfertaSweep(db, loggerFactory.CreateLogger<TurnoOfertaSweep>(), Clock);
        return new TurnoOfferComposition(
            new TurnoOfertaHost(sweep.RunBatchAsync, Task.Delay, loggerFactory.CreateLogger<TurnoOfertaHost>()),
            new TurnoOutboxHost(db, (id, channel, ct) => Route(scopes, db, origin, lease, Clock, id, channel, ct), loggerFactory.CreateLogger<TurnoOutboxHost>(), Clock));
    }

    internal static async Task Route(IServiceScopeFactory scopes, FirestoreDb db, string? origin, TimeSpan lease,
        Func<DateTime> clock, string id, string channel, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var scope = scopes.CreateScope();
        var services = scope.ServiceProvider;
        var router = new TurnoOfferOutboxRouter(db,
            () => services.GetRequiredService<MetaWhatsAppService>(), new TurnoOfertaNotificationResolver(db, clock),
            () => services.GetRequiredService<IEmailSender>(), lease, origin, clock);
        await router.Route(id, channel, ct);
    }

    internal static TurnoOfferComposition Create(FirestoreDb db, MetaWhatsAppService whatsapp,
        IEmailSender email, string origin, TimeSpan lease, Func<DateTime> serverUtcClock, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(whatsapp);
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(serverUtcClock);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        if (lease <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(lease));
        if (!new TurnoOfertaEmail(email, origin).HasOrigin) throw new ArgumentException("Invalid trusted HTTPS origin.", nameof(origin));
        DateTime Clock()
        {
            var now = serverUtcClock();
            if (now == default || now.Kind != DateTimeKind.Utc) throw new ArgumentException("Clock must return UTC.", nameof(serverUtcClock));
            return now.AddTicks(-(now.Ticks % 10));
        }
        _ = Clock();
        var sweep = new TurnoOfertaSweep(db, loggerFactory.CreateLogger<TurnoOfertaSweep>(), Clock);
        var resolver = new TurnoOfertaNotificationResolver(db, Clock);
        var router = new TurnoOfferOutboxRouter(db, whatsapp, resolver, email, lease, origin, Clock);
        return new TurnoOfferComposition(
            new TurnoOfertaHost(sweep.RunBatchAsync, Task.Delay, loggerFactory.CreateLogger<TurnoOfertaHost>()),
            new TurnoOutboxHost(db, router.Route, loggerFactory.CreateLogger<TurnoOutboxHost>(), Clock));
    }
}

using System.Security.Cryptography;
using Google.Cloud.Firestore;

namespace Firebase.Services;

// Isolated adapter for the offer host; production registration is deliberately absent.
internal sealed class TurnoOfferOutboxRouter
{
    private readonly Func<string, string, CancellationToken, Task> whatsapp;
    private readonly Func<string, string, CancellationToken, Task> email;

    internal TurnoOfferOutboxRouter(FirestoreDb db, MetaWhatsAppService whatsappSender,
        TurnoOfertaNotificationResolver resolver, IEmailSender emailSender, TimeSpan lease)
        : this(db, whatsappSender, resolver, emailSender, lease, null) { }

    internal TurnoOfferOutboxRouter(FirestoreDb db, MetaWhatsAppService whatsappSender,
        TurnoOfertaNotificationResolver resolver, IEmailSender emailSender, TimeSpan lease, string? origin)
        : this(db, whatsappSender, resolver, emailSender, lease, origin,
            () => { var now = DateTime.UtcNow; return now.AddTicks(-(now.Ticks % 10)); }) { }

    internal TurnoOfferOutboxRouter(FirestoreDb db, MetaWhatsAppService whatsappSender,
        TurnoOfertaNotificationResolver resolver, IEmailSender emailSender, TimeSpan lease, string? origin, Func<DateTime> clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(whatsappSender);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(emailSender);
        ArgumentNullException.ThrowIfNull(clock);
        var wa = new TurnoWhatsAppOutboxDispatcher(db, whatsappSender.SendTextMessage, resolver, clock, lease);
        var offerEmail = new TurnoOfertaEmail(emailSender, origin);
        var mail = new TurnoEmailOutboxDispatcher(
            (id, token, now, duration) => TurnoOutboxPersistence.ClaimBound(db, id, token, now, duration),
            (id, token, binding, now) => TurnoOutboxPersistence.BeginEmailOffer(db, id, token, binding, now, offerEmail),
            (id, token, binding, now) => TurnoOutboxPersistence.ReleaseBound(db, id, token, binding, now),
            (id, token, generation) => TurnoOutboxPersistence.Complete(db, id, token, generation),
            (message, ct) => emailSender.EnviarAsync(message, ct),
            clock, lease);
        whatsapp = async (id, token, ct) => { await wa.Dispatch(id, token, ct); };
        email = async (id, token, ct) =>
        {
            // Never claim or complete an email intent without a trusted response origin.
            if (!offerEmail.HasOrigin) throw new InvalidOperationException("Invalid trusted HTTPS origin for email offer.");
            await mail.Dispatch(id, token, ct);
        };
    }

    internal TurnoOfferOutboxRouter(FirestoreDb db, Func<MetaWhatsAppService> whatsappSender,
        TurnoOfertaNotificationResolver resolver, Func<IEmailSender> emailSender, TimeSpan lease, string? origin, Func<DateTime> clock)
    {
        whatsapp = async (id, token, ct) =>
        {
            var sender = whatsappSender();
            var dispatcher = new TurnoWhatsAppOutboxDispatcher(db, sender.SendTextMessage, resolver, clock, lease);
            await dispatcher.Dispatch(id, token, ct);
        };
        email = async (id, token, ct) =>
        {
            var sender = emailSender();
            var offerEmail = new TurnoOfertaEmail(sender, origin);
            if (!offerEmail.HasOrigin) throw new InvalidOperationException("Invalid trusted HTTPS origin for email offer.");
            var dispatcher = new TurnoEmailOutboxDispatcher(
                (item, claim, now, duration) => TurnoOutboxPersistence.ClaimBound(db, item, claim, now, duration),
                (item, claim, binding, now) => TurnoOutboxPersistence.BeginEmailOffer(db, item, claim, binding, now, offerEmail),
                (item, claim, binding, now) => TurnoOutboxPersistence.ReleaseBound(db, item, claim, binding, now),
                (item, claim, generation) => TurnoOutboxPersistence.Complete(db, item, claim, generation),
                (message, cancellation) => sender.EnviarAsync(message, cancellation), clock, lease);
            await dispatcher.Dispatch(id, token, ct);
        };
    }

    internal TurnoOfferOutboxRouter(Func<string, string, CancellationToken, Task> whatsapp,
        Func<string, string, CancellationToken, Task> email)
    {
        this.whatsapp = whatsapp ?? throw new ArgumentNullException(nameof(whatsapp));
        this.email = email ?? throw new ArgumentNullException(nameof(email));
    }

    internal Task Route(string id, string channel, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dispatch = channel switch
        {
            "WhatsApp" => whatsapp,
            "Email" => email,
            _ => throw new ArgumentException("Invalid outbox channel.", nameof(channel))
        };
        return dispatch(id, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), cancellationToken);
    }
}

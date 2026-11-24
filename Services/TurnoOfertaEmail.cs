using System.Net;
using Firebase.Models;
using Firebase.Utils;
using Google.Cloud.Firestore;

namespace Firebase.Services;

/// <summary>Unregistered email resolver. Resolution is a read-only snapshot, not a delivery guarantee.</summary>
internal sealed class TurnoOfertaEmail
{
    // Retain the existing constructor for the dispatcher; the resolver cannot send.
    private readonly string? origin;
    internal TurnoOfertaEmail(IEmailSender sender, string? origin = null)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps &&
            string.IsNullOrEmpty(uri.UserInfo) && uri.AbsolutePath == "/" && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment))
            this.origin = uri.GetLeftPart(UriPartial.Authority);
    }

    internal bool HasOrigin => origin != null;
    internal EmailMessage AddLink(EmailMessage message, string cascade, string offer, string client, string token)
    {
        if (origin == null) throw new InvalidOperationException("Origen de email inválido.");
        var url = origin + "/turno-oferta-email/responder#CascadeId=" + Uri.EscapeDataString(cascade) + "&OfferId=" + Uri.EscapeDataString(offer) + "&ClientId=" + Uri.EscapeDataString(client) + "&Token=" + token;
        return message with { Html = message.Html + "<p><a href=\"" + WebUtility.HtmlEncode(url) + "\">Responder oferta</a></p>" };
    }

    // Domain state can change between resolution and the external send.
    internal Task<EmailMessage?> Resolve(FirestoreDb db, TurnoOutboxPersistence.BoundClaim? binding, DateTime now) =>
        db.RunTransactionAsync(tx => ResolveInTransaction(db, tx, binding, now));

    internal async Task<EmailMessage?> ResolveInTransaction(FirestoreDb db, Transaction tx, TurnoOutboxPersistence.BoundClaim? binding, DateTime now)
    {
        static bool Id(string? s) => !string.IsNullOrWhiteSpace(s) && !s.Contains('/') && s is not "." and not ".." && !(s.StartsWith("__", StringComparison.Ordinal) && s.EndsWith("__", StringComparison.Ordinal)) && System.Text.Encoding.UTF8.GetByteCount(s) <= 1500;
        static string Text(Dictionary<string, object> d, string key) => d.TryGetValue(key, out var v) && v is string s ? s : "";
        static DateTime Time(Dictionary<string, object> d, string key) => d.TryGetValue(key, out var v) && v is Timestamp t ? t.ToDateTime() : default;
        if (binding == null || binding.Generation <= 0 || binding.EventType != TurnoOutboxPersistence.Event.Offer || binding.DeliveryChannel != TurnoOutboxPersistence.Channel.Email || !Id(binding.ReferenceId) || now == default || now.Kind != DateTimeKind.Utc) return null;
        now = now.AddTicks(-(now.Ticks % 10));
            var offer = await tx.GetSnapshotAsync(db.Collection("turno_ofertas").Document(binding.ReferenceId));
            if (!offer.Exists) return null;
            var o = offer.ToDictionary();
            var cascadeId = Text(o, "CascadeId"); var appointmentId = Text(o, "AppointmentId"); var clientId = Text(o, "ClienteId");
            var created = Time(o, "CreatedUtc"); var expires = Time(o, "ExpiresUtc"); var original = Time(o, "OriginalUtc"); var anchor = Time(o, "AnchorUtc"); var proposed = Time(o, "ProposedUtc"); var proposedEnd = Time(o, "ProposedEndUtc");
            if (Text(o, "Estado") != "Pendiente" || !Id(cascadeId) || !Id(appointmentId) || !Id(clientId) || created == default || expires == default || original == default || anchor == default || proposed == default || proposedEnd == default || now < created || now >= expires || expires <= created || proposed >= original || proposed >= anchor || anchor < original || anchor.Ticks - proposed.Ticks > TimeSpan.FromDays(7).Ticks || proposedEnd <= proposed) return null;
            var claim = await tx.GetSnapshotAsync(db.Collection("turno_ofertas_pendientes").Document(appointmentId));
            var cascade = await tx.GetSnapshotAsync(db.Collection("turno_cascadas").Document(cascadeId));
            var appointment = await tx.GetSnapshotAsync(db.Collection("turnos").Document(appointmentId));
            var client = await tx.GetSnapshotAsync(db.Collection("clientes").Document(clientId));
            if (!claim.Exists || !cascade.Exists || !appointment.Exists || !client.Exists) return null;
            var c = claim.ToDictionary(); var chain = cascade.ToDictionary(); var a = appointment.ToDictionary(); var customer = client.ToDictionary();
            var start = Time(a, "InicioUtc"); var end = Time(a, "FinUtc");
            if (Text(c, "OfferId") != binding.ReferenceId || Text(c, "CascadeId") != cascadeId || Text(chain, "PendingId") != binding.ReferenceId || Text(a, "Id") != appointmentId || Text(a, "ClienteId") != clientId || Text(a, "Estado") != "Pendiente" || !a.TryGetValue("AceptaAdelanto", out var advance) || advance is not true || start != original || end <= start || end.Ticks - start.Ticks != proposedEnd.Ticks - proposed.Ticks || Text(customer, "Id") != clientId || Text(customer, "Estado") != "Activo") return null;
            var emailPreference = customer.TryGetValue("NotificarPorEmail", out var ep) ? ep : null;
            var whatsappPreference = customer.TryGetValue("NotificarPorWhatsApp", out var wp) ? wp : null;
            if (emailPreference is not null and not bool || whatsappPreference is not null and not bool) return null;
            // Explicit false always rejects. A missing email preference defaults to email
            // only when WhatsApp is also absent or false (legacy records).
            if (emailPreference is false || (emailPreference is null && whatsappPreference is true)) return null;
            var email = Text(customer, "Email");
            if (string.IsNullOrWhiteSpace(email) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email) || email.Contains('\r') || email.Contains('\n')) return null;
            var name = Text(customer, "Nombre");
            return new EmailMessage(email, "Oferta para adelantar su turno", $"<p>Hola {WebUtility.HtmlEncode(name)}.</p><p>Hay una oferta para adelantar su turno al {TimeZoneHelper.FormatearFechaHoraApp(proposed, "yyyy-MM-dd HH:mm")} (hora de Buenos Aires). Responda antes de {TimeZoneHelper.FormatearFechaHoraApp(expires, "yyyy-MM-dd HH:mm")} (hora de Buenos Aires).</p>");
    }
}

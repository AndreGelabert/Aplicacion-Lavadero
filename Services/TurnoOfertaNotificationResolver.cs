using Google.Cloud.Firestore;
using Firebase.Utils;
using System.Text;

namespace Firebase.Services;

/// <summary>Unregistered read-only resolver. Its snapshot may become stale before any send;
/// resolving an offer is not atomic with delivery or a promise of delivery.</summary>
internal sealed class TurnoOfertaNotificationResolver(FirestoreDb db, Func<DateTime> clock)
{
    private static bool Id(string? value) => !string.IsNullOrWhiteSpace(value) && !value.Contains('/') &&
        value is not "." and not ".." && !(value.StartsWith("__", StringComparison.Ordinal) && value.EndsWith("__", StringComparison.Ordinal)) && Encoding.UTF8.GetByteCount(value) <= 1500;
    private static bool Text(Dictionary<string, object> data, string key, out string value)
    {
        value = data.TryGetValue(key, out var raw) && raw is string s ? s : "";
        return value.Length > 0;
    }
    private static bool Time(Dictionary<string, object> data, string key, out DateTime value)
    {
        value = data.TryGetValue(key, out var raw) && raw is Timestamp t ? t.ToDateTime() : default;
        return value != default;
    }

    internal static string FormatMessage(DateTime proposed, DateTime expires) =>
        $"Hay una oferta para adelantar su turno al {TimeZoneHelper.FormatearFechaHoraApp(proposed, "yyyy-MM-dd HH:mm")} (hora de Buenos Aires). Responda antes de {TimeZoneHelper.FormatearFechaHoraApp(expires, "yyyy-MM-dd HH:mm")} (hora de Buenos Aires).";

    internal static string FormatMessage(DateTime proposed, DateTime expires, string? lavadero, string? patente)
    {
        if (string.IsNullOrWhiteSpace(lavadero) && string.IsNullOrWhiteSpace(patente))
            return FormatMessage(proposed, expires);

        var nombre = !string.IsNullOrWhiteSpace(lavadero) ? lavadero : "Lavadero AutoClean";
        var detalleVehiculo = !string.IsNullOrWhiteSpace(patente) ? $" para tu vehículo ({patente})" : "";
        return $"🏢 *{nombre}*\n\nHay una oferta para adelantar tu turno{detalleVehiculo} al *{TimeZoneHelper.FormatearFechaHoraApp(proposed, "yyyy-MM-dd HH:mm")}* (hora de Buenos Aires).\n\nPodés responder antes de las *{TimeZoneHelper.FormatearFechaHoraApp(expires, "yyyy-MM-dd HH:mm")}* eligiendo una opción:";
    }

    internal async Task<TurnoWhatsAppOutboxDispatcher.Delivery?> Resolve(TurnoOutboxPersistence.BoundClaim? binding)
    {
        if (binding == null || binding.Generation <= 0 || binding.EventType != TurnoOutboxPersistence.Event.Offer ||
            binding.DeliveryChannel != TurnoOutboxPersistence.Channel.WhatsApp || !Id(binding.ReferenceId)) return null;
        return await db.RunTransactionAsync<TurnoWhatsAppOutboxDispatcher.Delivery?>(tx => ResolveInTransaction(tx, binding));
    }

    internal Task<TurnoWhatsAppOutboxDispatcher.Delivery?> ResolveInTransaction(Transaction tx, TurnoOutboxPersistence.BoundClaim? binding) =>
        binding == null || binding.Generation <= 0 || binding.EventType != TurnoOutboxPersistence.Event.Offer ||
        binding.DeliveryChannel != TurnoOutboxPersistence.Channel.WhatsApp || !Id(binding.ReferenceId)
            ? Task.FromResult<TurnoWhatsAppOutboxDispatcher.Delivery?>(null)
            : ResolveInTransaction(tx, binding, clock());

    internal async Task<TurnoWhatsAppOutboxDispatcher.Delivery?> ResolveInTransaction(Transaction tx, TurnoOutboxPersistence.BoundClaim? binding, DateTime now)
    {
            if (binding == null || binding.Generation <= 0 || binding.EventType != TurnoOutboxPersistence.Event.Offer ||
                binding.DeliveryChannel != TurnoOutboxPersistence.Channel.WhatsApp || !Id(binding.ReferenceId)) return null;
            if (now == default || now.Kind != DateTimeKind.Utc) return null;
            now = now.AddTicks(-(now.Ticks % 10));
            var offer = await tx.GetSnapshotAsync(db.Collection("turno_ofertas").Document(binding.ReferenceId));
            if (!offer.Exists) return null;
            var o = offer.ToDictionary();
            if (!Text(o, "Estado", out var state) || state != "Pendiente" ||
                !Text(o, "CascadeId", out var cascadeId) || !Id(cascadeId) ||
                !Text(o, "AppointmentId", out var appointmentId) || !Id(appointmentId) ||
                !Text(o, "ClienteId", out var clientId) || !Id(clientId) ||
                !Time(o, "CreatedUtc", out var created) || !Time(o, "ExpiresUtc", out var expires) ||
                now < created || now >= expires || expires <= created ||
                !Time(o, "OriginalUtc", out var original) || !Time(o, "AnchorUtc", out var anchor) ||
                !Time(o, "ProposedUtc", out var proposed) || !Time(o, "ProposedEndUtc", out var proposedEnd) ||
                proposed >= original || proposed >= anchor || anchor < original ||
                anchor.Ticks - proposed.Ticks > TimeSpan.FromDays(7).Ticks || proposedEnd <= proposed) return null;
            var claim = await tx.GetSnapshotAsync(db.Collection("turno_ofertas_pendientes").Document(appointmentId));
            var cascade = await tx.GetSnapshotAsync(db.Collection("turno_cascadas").Document(cascadeId));
            var appointment = await tx.GetSnapshotAsync(db.Collection("turnos").Document(appointmentId));
            var client = await tx.GetSnapshotAsync(db.Collection("clientes").Document(clientId));
            if (!claim.Exists || !cascade.Exists || !appointment.Exists || !client.Exists) return null;
            var c = claim.ToDictionary(); var chain = cascade.ToDictionary(); var a = appointment.ToDictionary(); var customer = client.ToDictionary();
            if (!Text(c, "OfferId", out var claimedOffer) || claimedOffer != binding.ReferenceId ||
                !Text(c, "CascadeId", out var claimedCascade) || claimedCascade != cascadeId ||
                !Text(chain, "PendingId", out var pending) || pending != binding.ReferenceId ||
                !Text(a, "Id", out var appointmentKey) || appointmentKey != appointmentId ||
                !Text(a, "ClienteId", out var owner) || owner != clientId ||
                !Text(a, "Estado", out var appointmentState) || appointmentState != "Pendiente" ||
                !a.TryGetValue("AceptaAdelanto", out var advance) || advance is not true ||
                !Time(a, "InicioUtc", out var start) || start != original || !Time(a, "FinUtc", out var end) || end <= start ||
                end.Ticks - start.Ticks != proposedEnd.Ticks - proposed.Ticks ||
                !Text(customer, "Id", out var customerKey) || customerKey != clientId ||
                !Text(customer, "Estado", out var customerState) || customerState != "Activo" ||
                !customer.TryGetValue("NotificarPorWhatsApp", out var preference) || preference is not true ||
                !Text(customer, "Telefono", out var phone) || phone.Length != 10 || phone.Any(ch => ch is < '0' or > '9')) return null;
            var recipient = PhoneNumberHelper.ToWhatsAppFormat(phone);
            if (!PhoneNumberHelper.IsValidPhoneNumber(recipient)) return null;

            var config = await tx.GetSnapshotAsync(db.Collection("configuracion").Document("system_config"));
            var lavadero = config.Exists && config.TryGetValue<string>("NombreLavadero", out var nl) && !string.IsNullOrWhiteSpace(nl) ? nl : (a.TryGetValue("VehiculoPatente", out _) ? "Lavadero AutoClean" : null);
            var patente = a.TryGetValue("VehiculoPatente", out var vp) && vp is string sp && !string.IsNullOrWhiteSpace(sp) ? sp : "";
            if (string.IsNullOrEmpty(patente) && a.TryGetValue("Vehiculos", out var listObj) && listObj is IEnumerable<object> list)
            {
                foreach (var item in list)
                {
                    if (item is Dictionary<string, object> map && map.TryGetValue("Patente", out var p) && p is string plate && !string.IsNullOrWhiteSpace(plate))
                    {
                        patente = plate;
                        break;
                    }
                }
            }

            var buttons = new List<(string id, string title)>
            {
                ("oferta_si_" + binding.ReferenceId, "Sí, adelantar"),
                ("oferta_no_" + binding.ReferenceId, "No, gracias")
            };

            return new TurnoWhatsAppOutboxDispatcher.Delivery(recipient,
                FormatMessage(proposed, expires, lavadero, patente),
                buttons);
    }
}

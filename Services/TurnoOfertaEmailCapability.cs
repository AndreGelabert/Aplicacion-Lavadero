using System.Security.Cryptography;
using System.Text;
using Google.Cloud.Firestore;

namespace Firebase.Services;

internal sealed class TurnoOfertaEmailInvalidException() : InvalidOperationException("Capacidad inválida.");

/// <summary>Internal bearer capability; callers must use a server-controlled clock and protect the returned secret.</summary>
internal sealed class TurnoOfertaEmailCapability(FirestoreDb db)
{
    internal async Task<string> Issue(string cascadeId, string offerId, string clientId, DateTime now, DateTime expiry)
    {
        if (!Id(cascadeId) || !Id(offerId) || !Id(clientId) || !Utc(now) || !Utc(expiry) || expiry <= now)
            throw new InvalidOperationException("Capacidad inválida.");
        var token = GenerateSecret();
        await TurnoService.RetryAborted(() => db.RunTransactionAsync(async tx =>
        {
            await IssueInTransaction(tx, cascadeId, offerId, clientId, now, expiry, token);
            return true;
        }));
        return token;
    }

    internal static string GenerateSecret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    // The caller generates the secret once before entering any retryable transaction callback.
    internal async Task IssueInTransaction(Transaction tx, string cascadeId, string offerId, string clientId, DateTime now, DateTime expiry, string token)
    {
        if (!Id(cascadeId) || !Id(offerId) || !Id(clientId) || !Utc(now) || !Utc(expiry) || expiry <= now ||
            token == null || token.Length != 64 || !token.All(Uri.IsHexDigit))
            throw new InvalidOperationException("Capacidad inválida.");
        var hash = Hash(token);
            var offer = await tx.GetSnapshotAsync(db.Collection("turno_ofertas").Document(offerId));
            var capability = await tx.GetSnapshotAsync(db.Collection("turno_ofertas_email_capabilities").Document(offerId));
            if (!offer.Exists || capability.Exists || !offer.TryGetValue<string>("CascadeId", out var cascade) || cascade != cascadeId ||
                !offer.TryGetValue<string>("ClienteId", out var owner) || owner != clientId ||
                !offer.TryGetValue<string>("Estado", out var state) || state != "Pendiente" ||
                !offer.TryGetValue<DateTime>("CreatedUtc", out var created) || now < created ||
                !offer.TryGetValue<DateTime>("ExpiresUtc", out var offerExpiry) || expiry > offerExpiry || now >= offerExpiry ||
                !offer.TryGetValue<string>("AppointmentId", out var appointmentId) || !Id(appointmentId))
                throw new InvalidOperationException("Capacidad inválida.");
            var appointment = await tx.GetSnapshotAsync(db.Collection("turnos").Document(appointmentId));
            var chain = await tx.GetSnapshotAsync(db.Collection("turno_cascadas").Document(cascadeId));
            var claim = await tx.GetSnapshotAsync(db.Collection("turno_ofertas_pendientes").Document(appointmentId));
            if (!appointment.Exists || !appointment.TryGetValue<string>("Id", out var id) || id != appointmentId ||
                !appointment.TryGetValue<string>("ClienteId", out var appointmentOwner) || appointmentOwner != clientId ||
                !chain.Exists || !chain.TryGetValue<string>("PendingId", out var pending) || pending != offerId ||
                !claim.Exists || !claim.TryGetValue<string>("OfferId", out var claimedOffer) || claimedOffer != offerId ||
                !claim.TryGetValue<string>("CascadeId", out var claimedCascade) || claimedCascade != cascadeId)
                throw new InvalidOperationException("Capacidad inválida.");
            tx.Create(capability.Reference, new Dictionary<string, object> { ["Hash"] = hash, ["CascadeId"] = cascadeId, ["ClienteId"] = clientId, ["ExpiresUtc"] = expiry, ["Consumed"] = false });
    }

    internal Task Respond(string cascadeId, string offerId, string clientId, string token, DateTime now, bool accept)
    {
        if (!Id(cascadeId) || !Id(offerId) || !Id(clientId) || !Utc(now) || token == null || token.Length != 64 || !token.All(Uri.IsHexDigit))
            throw new InvalidOperationException("Capacidad inválida.");
        var cap = new TurnoOfertaPersistence.EmailCapability(clientId, Hash(token));
        var offers = new TurnoOfertaPersistence(db);
        return accept ? offers.AcceptEmail(cascadeId, offerId, now, cap) : offers.RejectEmail(cascadeId, offerId, now, cap);
    }
    internal Task RespondHttp(string cascadeId, string offerId, string clientId, string token, DateTime now, bool accept, CancellationToken cancellationToken)
    {
        if (!Id(cascadeId) || !Id(offerId) || !Id(clientId) || !Utc(now) || token == null || token.Length != 64 || !token.All(Uri.IsHexDigit))
            throw new TurnoOfertaEmailInvalidException();
        var cap = new TurnoOfertaPersistence.EmailCapability(clientId, Hash(token), true);
        var offers = new TurnoOfertaPersistence(db);
        return accept ? offers.AcceptEmail(cascadeId, offerId, now, cap, cancellationToken) : offers.RejectEmail(cascadeId, offerId, now, cap, cancellationToken);
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static bool Utc(DateTime value) => value != default && value.Kind == DateTimeKind.Utc && value.Ticks % 10 == 0;
    private static bool Id(string? id) => !string.IsNullOrWhiteSpace(id) && !id.Contains('/') && id is not "." and not ".." && !(id.StartsWith("__", StringComparison.Ordinal) && id.EndsWith("__", StringComparison.Ordinal)) && Encoding.UTF8.GetByteCount(id) <= 1500;
}

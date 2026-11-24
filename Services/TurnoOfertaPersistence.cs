using System.Text;
using System.Security.Cryptography;
using Google.Cloud.Firestore;
using Firebase.Models;

namespace Firebase.Services;

/// <summary>Internal storage only. Pending offers do not reserve capacity; acceptance must revalidate it.
/// Times are trusted internal inputs; exposed integrations must use a server-controlled clock.</summary>
internal sealed class TurnoOfertaPersistence(FirestoreDb db)
{
    internal record Request(string CascadeId, string OfferId, string AppointmentId, DateTime CreatedUtc,
        DateTime ExpiresUtc, DateTime AnchorUtc, DateTime ProposedUtc)
    {
        internal long? ExpectedVacancyVersion { get; init; }
        internal DateTime? ExpectedVacancyStartUtc { get; init; }
        internal DateTime? ExpectedVacancyEndUtc { get; init; }
    }

    private static bool ValidId(string? id) => !string.IsNullOrWhiteSpace(id) && !id.Contains('/') &&
        id is not "." and not ".." && !(id.StartsWith("__", StringComparison.Ordinal) && id.EndsWith("__", StringComparison.Ordinal)) &&
        Encoding.UTF8.GetByteCount(id) <= 1500;
    // Firestore stores microseconds; reject finer inputs rather than silently rounding them.
    private static bool Utc(DateTime t) => t != default && t.Kind == DateTimeKind.Utc && t.Ticks % 10 == 0;

    private DocumentReference Ledger(string cascade, string cliente, DateTime start, DateTime end)
    {
        if (!ValidId(cascade) || !ValidId(cliente) || !Utc(start) || !Utc(end) || end <= start)
            throw new InvalidOperationException("Oferta inválida.");
        var key = $"{Encoding.UTF8.GetByteCount(cascade)}:{cascade}{Encoding.UTF8.GetByteCount(cliente)}:{cliente}:{start.Ticks}:{end.Ticks}";
        return db.Collection("turno_ofertas_terminales").Document(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))));
    }

    private async Task<DocumentSnapshot> ReadClaim(Transaction tx, string appointmentId)
    {
        var claim = await tx.GetSnapshotAsync(db.Collection("turno_ofertas_pendientes").Document(appointmentId));
        if (!claim.Exists) return claim;
        var fields = claim.ToDictionary();
        if (!fields.TryGetValue("OfferId", out var offerValue) || offerValue is not string offerId || !ValidId(offerId) ||
            !fields.TryGetValue("CascadeId", out var cascadeValue) || cascadeValue is not string cascadeId || !ValidId(cascadeId))
            throw new InvalidOperationException("Reserva de oferta incompatible.");
        var offer = await tx.GetSnapshotAsync(db.Collection("turno_ofertas").Document(offerId));
        var cascade = await tx.GetSnapshotAsync(db.Collection("turno_cascadas").Document(cascadeId));
        if (!offer.Exists || !offer.TryGetValue<string>("AppointmentId", out var id) || id != appointmentId ||
            !offer.TryGetValue<string>("CascadeId", out var owner) || owner != cascadeId ||
            !offer.TryGetValue<string>("Estado", out var state) || state != "Pendiente" ||
            !cascade.Exists || !cascade.TryGetValue<string>("PendingId", out var pending) || pending != offerId)
            throw new InvalidOperationException("Reserva de oferta incompatible.");
        return claim;
    }

    private static void RequireClaim(DocumentSnapshot claim, string cascadeId, string offerId)
    {
        if (!claim.Exists || claim.GetValue<string>("CascadeId") != cascadeId || claim.GetValue<string>("OfferId") != offerId)
            throw new InvalidOperationException("Reserva de oferta incompatible.");
    }

    private static void ValidatePendingBooking(TurnoOferta item, Turno turno)
    {
        if (turno.Id != item.AppointmentId || turno.Validar().Count != 0 || turno.Estado != "Pendiente" ||
            !ValidId(turno.ClienteId) || turno.ClienteId != item.ClienteId || turno.InicioUtc != item.OriginalUtc ||
            !Utc(item.ProposedUtc) || !Utc(item.ProposedEndUtc) || item.ProposedEndUtc <= item.ProposedUtc ||
            item.ProposedEndUtc.Ticks - item.ProposedUtc.Ticks != turno.FinUtc.Ticks - turno.InicioUtc.Ticks)
            throw new InvalidOperationException("Oferta incompatible con el turno.");
    }

    // Legacy offer-only cascades have none of these fields. A partial continuation is corrupt;
    // do not fabricate the missing vacancy or overwrite it while attaching an offer.
    private static void ValidateContinuation(DocumentSnapshot cascade)
    {
        if (!cascade.Exists) return;
        var data = cascade.ToDictionary();
        string[] fields = ["CancelledAppointmentId", "VacancyStartUtc", "VacancyEndUtc", "ContinuationState"];
        if (!fields.Any(data.ContainsKey) && !data.ContainsKey("VacancySourceOfferId") && !data.ContainsKey("VacancyVersion")) return;
        if (!data.TryGetValue(fields[0], out var id) || id is not string cancelledId || !ValidId(cancelledId) ||
            !data.TryGetValue(fields[1], out var from) || from is not Timestamp start ||
            !data.TryGetValue(fields[2], out var to) || to is not Timestamp end ||
            start.ToDateTime() == default || end.ToDateTime() <= start.ToDateTime() ||
            !data.TryGetValue(fields[3], out var state) || state is not string text || text != "Pendiente")
            throw new InvalidOperationException("Cascada incompatible.");
    }

    private async Task ValidateCancellation(Transaction tx, DocumentSnapshot cascade)
    {
        ValidateContinuation(cascade);
        if (!cascade.Exists || !cascade.ContainsField("CancelledAppointmentId")) return;
        var cancelledId = cascade.GetValue<string>("CancelledAppointmentId");
        if (cascade.Id != TurnoService.CancellationCascadeId(cancelledId))
            throw new InvalidOperationException("Cascada incompatible.");
        var cancelled = await tx.GetSnapshotAsync(db.Collection("turnos").Document(cancelledId));
        if (!cancelled.Exists || !cancelled.TryGetValue<string>("Estado", out var status) || status != "Cancelado" ||
            !cancelled.TryGetValue<DateTime>("InicioUtc", out var start) ||
            !cancelled.TryGetValue<DateTime>("FinUtc", out var end))
            throw new InvalidOperationException("Cascada incompatible.");
        if (!cascade.ContainsField("VacancySourceOfferId"))
        {
            if (cascade.ContainsField("VacancyVersion") || start != cascade.GetValue<DateTime>("VacancyStartUtc") || end != cascade.GetValue<DateTime>("VacancyEndUtc"))
                throw new InvalidOperationException("Cascada incompatible.");
            return;
        }
        if (!cascade.TryGetValue<string>("VacancySourceOfferId", out var sourceId) || !ValidId(sourceId) ||
            !cascade.TryGetValue<long>("VacancyVersion", out var version) || version < 1)
            throw new InvalidOperationException("Cascada incompatible.");
        var source = await tx.GetSnapshotAsync(db.Collection("turno_ofertas").Document(sourceId));
        var receipt = await tx.GetSnapshotAsync(db.Collection("turno_aceptaciones").Document(sourceId));
        if (!source.Exists || !source.TryGetValue<string>("CascadeId", out var sourceCascade) || sourceCascade != cascade.Id ||
            !source.TryGetValue<string>("Estado", out var sourceState) || sourceState != "Aceptada" ||
            !source.TryGetValue<long>("VacancyVersion", out var sourceVersion) || sourceVersion != version ||
            !source.TryGetValue<DateTime>("OriginalUtc", out var sourceStart) ||
            !source.TryGetValue<DateTime>("OriginalEndUtc", out var sourceEnd) || sourceEnd <= sourceStart ||
            sourceStart != cascade.GetValue<DateTime>("VacancyStartUtc") || sourceEnd != cascade.GetValue<DateTime>("VacancyEndUtc") ||
            !source.TryGetValue<string>("AppointmentId", out var sourceAppointment) || !ValidId(sourceAppointment) ||
            !source.TryGetValue<DateTime>("ProposedUtc", out var sourceProposed) || !Utc(sourceProposed) ||
            !source.TryGetValue<DateTime>("AnchorUtc", out var sourceAnchor) || !Utc(sourceAnchor) ||
            !receipt.Exists || !receipt.TryGetValue<string>("CascadeId", out var proofCascade) || proofCascade != sourceCascade ||
            !receipt.TryGetValue<string>("AppointmentId", out var proofAppointment) || proofAppointment != sourceAppointment ||
            !receipt.TryGetValue<DateTime>("OriginalUtc", out var proofStart) || proofStart != sourceStart ||
            !receipt.TryGetValue<DateTime>("OriginalEndUtc", out var proofEnd) || proofEnd != sourceEnd ||
            !receipt.TryGetValue<DateTime>("ProposedUtc", out var proofProposed) || proofProposed != sourceProposed ||
            !receipt.TryGetValue<DateTime>("AnchorUtc", out var proofAnchor) || proofAnchor != sourceAnchor)
            throw new InvalidOperationException("Cascada incompatible.");
    }

    internal Task<string?> SelectAndCreate(string cascadeId, string offerId, DateTime createdUtc, DateTime expiresUtc) =>
        SelectAndCreateCore(cascadeId, offerId, createdUtc, expiresUtc, false);

    internal Task<string?> SelectAndCreateConfigured(string cascadeId, string offerId, DateTime createdUtc) =>
        SelectAndCreateCore(cascadeId, offerId, createdUtc, createdUtc.AddMinutes(10), true);

    private async Task<string?> SelectAndCreateCore(string cascadeId, string offerId, DateTime createdUtc, DateTime expiresUtc, bool configured)
    {
        if (!ValidId(cascadeId) || !ValidId(offerId) || !Utc(createdUtc) || !Utc(expiresUtc) || expiresUtc <= createdUtc)
            throw new ArgumentException("Oferta inválida.");
        return await TurnoService.RetryAborted(() => db.RunTransactionAsync(async tx =>
        {
            await tx.GetSnapshotAsync(db.Collection("coordinacion").Document("turnos"));
            var cascade = await tx.GetSnapshotAsync(db.Collection("turno_cascadas").Document(cascadeId));
            await ValidateCancellation(tx, cascade);
            if (!cascade.Exists || !cascade.ContainsField("CancelledAppointmentId"))
                throw new InvalidOperationException("Cascada incompatible o con oferta pendiente.");
            if (configured)
            {
                var pending = cascade.GetValue<string>("PendingId");
                if (pending != "")
                {
                    if (!ValidId(pending)) throw new InvalidOperationException("Cascada incompatible.");
                    var pendingDoc = await tx.GetSnapshotAsync(db.Collection("turno_ofertas").Document(pending));
                    if (!pendingDoc.Exists) throw new InvalidOperationException("Cascada incompatible.");
                    var item = pendingDoc.ConvertTo<TurnoOferta>();
                    if (item.CascadeId != cascadeId || item.Estado != "Pendiente" || !Utc(item.CreatedUtc) || !Utc(item.ExpiresUtc) || item.ExpiresUtc <= item.CreatedUtc ||
                        !pendingDoc.TryGetValue<long>("ExpectedVacancyVersion", out var pv) ||
                        !pendingDoc.TryGetValue<DateTime>("ExpectedVacancyStartUtc", out var ps) ||
                        !pendingDoc.TryGetValue<DateTime>("ExpectedVacancyEndUtc", out var pe))
                        throw new InvalidOperationException("Cascada incompatible.");
                    await CreateInTransaction(tx, new Request(cascadeId, pending, item.AppointmentId, item.CreatedUtc, item.ExpiresUtc, item.AnchorUtc, item.ProposedUtc)
                    { ExpectedVacancyVersion = pv, ExpectedVacancyStartUtc = ps, ExpectedVacancyEndUtc = pe });
                    return item.AppointmentId;
                }
                var config = await tx.GetSnapshotAsync(db.Collection("configuracion").Document("system_config"));
                if (!config.Exists) throw new InvalidOperationException("Configuración no encontrada.");
                var minutes = config.ContainsField("RespuestaOfertaAdelantoMinutos") ? config.GetValue<int>("RespuestaOfertaAdelantoMinutos") : 10;
                if (minutes <= 0) throw new InvalidOperationException("Configuración inválida.");
                try { expiresUtc = createdUtc.AddMinutes(minutes); }
                catch (ArgumentOutOfRangeException) { throw new InvalidOperationException("Configuración inválida."); }
            }
            var existing = await tx.GetSnapshotAsync(db.Collection("turno_ofertas").Document(offerId));
            if (existing.Exists)
            {
                var stored = existing.ConvertTo<TurnoOferta>();
                if (stored.CascadeId != cascadeId || stored.CreatedUtc != createdUtc || stored.ExpiresUtc != expiresUtc ||
                    !existing.TryGetValue<long>("ExpectedVacancyVersion", out var expectedVersion) ||
                    !existing.TryGetValue<DateTime>("ExpectedVacancyStartUtc", out var expectedStart) ||
                    !existing.TryGetValue<DateTime>("ExpectedVacancyEndUtc", out var expectedEnd))
                    throw new InvalidOperationException("Oferta existente incompatible.");
                await CreateInTransaction(tx, new Request(cascadeId, offerId, stored.AppointmentId, createdUtc, expiresUtc, stored.AnchorUtc, stored.ProposedUtc)
                { ExpectedVacancyVersion = expectedVersion, ExpectedVacancyStartUtc = expectedStart, ExpectedVacancyEndUtc = expectedEnd });
                return stored.AppointmentId;
            }
            if (cascade.GetValue<string>("PendingId") != "")
                throw new InvalidOperationException("Cascada incompatible o con oferta pendiente.");
            var start = cascade.GetValue<DateTime>("VacancyStartUtc");
            var end = cascade.GetValue<DateTime>("VacancyEndUtc");
            if (start.Ticks > DateTime.MaxValue.Ticks - TimeSpan.FromDays(7).Ticks)
                throw new InvalidOperationException("Cascada incompatible.");
            var candidates = await tx.GetSnapshotAsync(db.Collection("turnos")
                .WhereGreaterThan("InicioUtc", start).WhereLessThanOrEqualTo("InicioUtc", start.AddDays(7)));
            var eligible = new List<TurnoAdelantoCandidate>();
            var anchors = new Dictionary<string, DateTime>();
            foreach (var doc in candidates.Documents)
            {
                var booking = doc.ConvertTo<Turno>();
                if (booking.Estado != "Pendiente" || !booking.AceptaAdelanto) continue;
                if (booking.Id != doc.Id || booking.Validar().Count != 0 || !ValidId(booking.ClienteId) || !doc.CreateTime.HasValue)
                    throw new InvalidOperationException("Turno persistido inválido.");
                var claim = await ReadClaim(tx, doc.Id);
                if (claim.Exists) continue;
                var anchorDoc = await tx.GetSnapshotAsync(db.Collection("turno_adelanto_anclas").Document(doc.Id));
                var anchor = anchorDoc.Exists ? anchorDoc.GetValue<DateTime>("AnchorUtc") : booking.InicioUtc;
                if (!Utc(anchor) || anchor < booking.InicioUtc) throw new InvalidOperationException("Ancla incompatible.");
                if (anchor.Ticks - start.Ticks > TimeSpan.FromDays(7).Ticks) continue;
                DateTime proposedEnd;
                try { proposedEnd = start.AddTicks(booking.FinUtc.Ticks - booking.InicioUtc.Ticks); }
                catch (ArgumentOutOfRangeException) { throw new InvalidOperationException("Oferta inválida."); }
                var ledger = await tx.GetSnapshotAsync(Ledger(cascadeId, booking.ClienteId, start, proposedEnd));
                if (ledger.Exists)
                {
                    if (!ledger.TryGetValue<string>("OfferId", out var terminalId) || !ValidId(terminalId) ||
                        !ledger.TryGetValue<string>("Estado", out var terminalState) || terminalState is not ("Rechazada" or "Expirada"))
                        throw new InvalidOperationException("Marcador incompatible.");
                    var terminalOffer = await tx.GetSnapshotAsync(db.Collection("turno_ofertas").Document(terminalId));
                    if (!terminalOffer.Exists || !terminalOffer.TryGetValue<string>("Estado", out var offerState) || offerState != terminalState ||
                        !terminalOffer.TryGetValue<string>("CascadeId", out var offerCascade) || offerCascade != cascadeId ||
                        !terminalOffer.TryGetValue<string>("ClienteId", out var offerClient) || offerClient != booking.ClienteId ||
                        !terminalOffer.TryGetValue<DateTime>("ProposedUtc", out var offerStart) || offerStart != start ||
                        !terminalOffer.TryGetValue<DateTime>("ProposedEndUtc", out var offerEnd) || offerEnd != proposedEnd)
                        throw new InvalidOperationException("Marcador incompatible.");
                    continue;
                }
                anchors.Add(doc.Id, anchor);
                eligible.Add(new(doc.Id, booking.InicioUtc, doc.CreateTime.Value.ToDateTime(), true, true));
            }
            var chosen = TurnoAdelantoCandidateSelector.Order(start, eligible).FirstOrDefault();
            if (chosen.Id == null) return null;
            var request = new Request(cascadeId, offerId, chosen.Id, createdUtc, expiresUtc, anchors[chosen.Id], start)
            {
                ExpectedVacancyVersion = cascade.ContainsField("VacancyVersion") ? cascade.GetValue<long>("VacancyVersion") : 0L,
                ExpectedVacancyStartUtc = start, ExpectedVacancyEndUtc = end
            };
            await CreateInTransaction(tx, request);
            return chosen.Id;
        }));
    }

    internal async Task Create(Request r)
    {
        ArgumentNullException.ThrowIfNull(r);
        if (!ValidId(r.CascadeId) || !ValidId(r.OfferId) || !ValidId(r.AppointmentId) ||
            !Utc(r.CreatedUtc) || !Utc(r.ExpiresUtc) || !Utc(r.AnchorUtc) || !Utc(r.ProposedUtc) ||
            r.ExpiresUtc <= r.CreatedUtc || r.ProposedUtc >= r.AnchorUtc || r.AnchorUtc.Ticks - r.ProposedUtc.Ticks > TimeSpan.FromDays(7).Ticks)
            throw new ArgumentException("Oferta inválida.", nameof(r));
        await TurnoService.RetryAborted(() => db.RunTransactionAsync(async tx =>
        {
            await tx.GetSnapshotAsync(db.Collection("coordinacion").Document("turnos"));
            return await CreateInTransaction(tx, r);
        }));
    }

    private async Task<bool> CreateInTransaction(Transaction tx, Request r)
    {
            var cascadeRef = db.Collection("turno_cascadas").Document(r.CascadeId);
            var offerRef = db.Collection("turno_ofertas").Document(r.OfferId);
            var cascade = await tx.GetSnapshotAsync(cascadeRef);
            await ValidateCancellation(tx, cascade);
            var offer = await tx.GetSnapshotAsync(offerRef);
            var rooted = cascade.Exists && cascade.ContainsField("CancelledAppointmentId");
            if (rooted)
            {
                if (r.ProposedUtc != r.ExpectedVacancyStartUtc) throw new InvalidOperationException("Cascada incompatible.");
                if (r.ExpectedVacancyVersion is not long expectedVersion || expectedVersion < 0 ||
                    r.ExpectedVacancyStartUtc is not DateTime expectedStart || !Utc(expectedStart) ||
                    r.ExpectedVacancyEndUtc is not DateTime expectedEnd || !Utc(expectedEnd) || expectedEnd <= expectedStart)
                    throw new InvalidOperationException("Cascada incompatible.");
                var evidence = offer.Exists ? offer : cascade;
                var versionField = offer.Exists ? "ExpectedVacancyVersion" : "VacancyVersion";
                var startField = offer.Exists ? "ExpectedVacancyStartUtc" : "VacancyStartUtc";
                var endField = offer.Exists ? "ExpectedVacancyEndUtc" : "VacancyEndUtc";
                var version = !offer.Exists && !evidence.ContainsField(versionField) ? 0L :
                    evidence.TryGetValue<long>(versionField, out var v) ? v : -1L;
                if (version != expectedVersion || !evidence.TryGetValue<DateTime>(startField, out var start) || start != expectedStart ||
                    !evidence.TryGetValue<DateTime>(endField, out var end) || end != expectedEnd)
                    throw new InvalidOperationException("Cascada incompatible.");
            }
            else if (r.ExpectedVacancyVersion.HasValue || r.ExpectedVacancyStartUtc.HasValue || r.ExpectedVacancyEndUtc.HasValue)
                throw new InvalidOperationException("Cascada incompatible.");
            var claim = await ReadClaim(tx, r.AppointmentId);
            var appointment = await tx.GetSnapshotAsync(db.Collection("turnos").Document(r.AppointmentId));
            var anchorRef = db.Collection("turno_adelanto_anclas").Document(r.AppointmentId);
            var anchorDoc = await tx.GetSnapshotAsync(anchorRef);
            if (!appointment.Exists) throw new InvalidOperationException("Turno inexistente.");
            var turno = appointment.ConvertTo<Turno>();
            if (turno.Id != r.AppointmentId || turno.Validar().Count != 0 || turno.Estado != "Pendiente" || !turno.AceptaAdelanto)
                throw new InvalidOperationException("Turno no elegible.");
            if (!ValidId(turno.ClienteId)) throw new InvalidOperationException("Turno no elegible.");
            DateTime proposedEnd;
            try { proposedEnd = r.ProposedUtc.AddTicks(checked(turno.FinUtc.Ticks - turno.InicioUtc.Ticks)); }
            catch (ArgumentOutOfRangeException) { throw new InvalidOperationException("Oferta inválida."); }
            var ledger = await tx.GetSnapshotAsync(Ledger(r.CascadeId, turno.ClienteId, r.ProposedUtc, proposedEnd));
            var originalAnchor = anchorDoc.Exists ? anchorDoc.GetValue<DateTime>("AnchorUtc") : turno.InicioUtc;
            if (originalAnchor != r.AnchorUtc || r.ProposedUtc >= turno.InicioUtc || originalAnchor.Ticks - r.ProposedUtc.Ticks > TimeSpan.FromDays(7).Ticks)
                throw new InvalidOperationException("Ancla o propuesta incompatible.");
            if (offer.Exists)
            {
                var stored = offer.ConvertTo<Firebase.Models.TurnoOferta>();
                if (stored.CascadeId != r.CascadeId || stored.AppointmentId != r.AppointmentId || stored.CreatedUtc != r.CreatedUtc ||
                    stored.ExpiresUtc != r.ExpiresUtc || stored.AnchorUtc != r.AnchorUtc || stored.ProposedUtc != r.ProposedUtc || !cascade.Exists ||
                    (stored.Estado == "Pendiente" ? cascade.GetValue<string>("PendingId") != r.OfferId || turno.InicioUtc != stored.OriginalUtc || stored.ProposedEndUtc != proposedEnd || stored.ClienteId != turno.ClienteId :
                        (stored.Estado != "Rechazada" && stored.Estado != "Expirada") || cascade.GetValue<string>("PendingId") == r.OfferId))
                    throw new InvalidOperationException("Oferta existente incompatible.");
                if (stored.Estado == "Pendiente")
                {
                    RequireClaim(claim, r.CascadeId, r.OfferId);
                    if (offer.ContainsField("OutboxChannelsV1"))
                    {
                        var data = offer.ToDictionary();
                        if (data["OutboxChannelsV1"] is not long channels || channels is < 1 or > 3)
                            throw new InvalidOperationException("Notificación existente incompatible.");
                        foreach (var (bit, channel) in new[] { (1L, TurnoOutboxPersistence.Channel.WhatsApp), (2L, TurnoOutboxPersistence.Channel.Email) })
                        {
                            if ((channels & bit) == 0) continue;
                            var intent = await tx.GetSnapshotAsync(db.Collection("turno_outbox").Document(TurnoOutboxPersistence.OfferIntentId(r.OfferId, channel)));
                            if (!intent.Exists) throw new InvalidOperationException("Notificación existente incompatible.");
                            var fields = intent.ToDictionary();
                            if (!TurnoOutboxPersistence.ValidState(fields) || !Equals(fields["Event"], "Offer") ||
                                !Equals(fields["Channel"], channel.ToString()) || !Equals(fields["ReferenceId"], r.OfferId))
                                throw new InvalidOperationException("Notificación existente incompatible.");
                        }
                    }
                }
                if (stored.Estado is "Rechazada" or "Expirada")
                {
                    var terminal = await tx.GetSnapshotAsync(Ledger(r.CascadeId, stored.ClienteId, stored.ProposedUtc, stored.ProposedEndUtc));
                    if (!terminal.Exists || !terminal.TryGetValue<string>("OfferId", out var terminalOffer) || terminalOffer != r.OfferId ||
                        !terminal.TryGetValue<string>("Estado", out var terminalState) || terminalState != stored.Estado)
                        throw new InvalidOperationException("Marcador incompatible.");
                }
                return true;
            }
            if (claim.Exists) throw new InvalidOperationException("Turno con oferta pendiente.");
            if (ledger.Exists) throw new InvalidOperationException("Horario ya ofrecido.");
            if (cascade.Exists && cascade.GetValue<string>("PendingId") != "")
                throw new InvalidOperationException("Cascada incompatible o con oferta pendiente.");
            var client = await tx.GetSnapshotAsync(db.Collection("clientes").Document(turno.ClienteId));
            if (!client.Exists || !client.TryGetValue<string>("Id", out var clientId) || clientId != turno.ClienteId)
                throw new InvalidOperationException("Cliente incompatible.");
            var clientData = client.ToDictionary();
            static bool? Preference(Dictionary<string, object> data, string field)
            {
                if (!data.TryGetValue(field, out var value) || value == null) return null;
                if (value is bool selected) return selected;
                throw new InvalidOperationException("Preferencias de notificación inválidas.");
            }
            var whatsapp = Preference(clientData, "NotificarPorWhatsApp");
            var email = Preference(clientData, "NotificarPorEmail");
            // Explicit email opt-out is never overridden; missing email defaults only
            // when WhatsApp is not selected, matching the delivery resolver.
            var sendEmail = email == true || (email == null && whatsapp != true);
            if (!sendEmail && whatsapp != true ||
                !client.TryGetValue<string>("Estado", out var state) || state != "Activo")
                throw new InvalidOperationException("Cliente incompatible.");
            if (sendEmail && (!client.TryGetValue<string>("Email", out var address) ||
                string.IsNullOrWhiteSpace(address) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(address) ||
                address.Contains('\r') || address.Contains('\n')))
                throw new InvalidOperationException("Cliente incompatible.");
            if (whatsapp == true && (!client.TryGetValue<string>("Telefono", out var phone) || phone.Length != 10 ||
                phone.Any(c => c is < '0' or > '9') || !PhoneNumberHelper.IsValidPhoneNumber(PhoneNumberHelper.ToWhatsAppFormat(phone))))
                throw new InvalidOperationException("Cliente incompatible.");
            var intents = new List<TurnoOutboxPersistence.Request>();
            if (whatsapp == true) intents.Add(new(TurnoOutboxPersistence.OfferIntentId(r.OfferId, TurnoOutboxPersistence.Channel.WhatsApp), TurnoOutboxPersistence.Event.Offer, TurnoOutboxPersistence.Channel.WhatsApp, r.OfferId));
            if (sendEmail) intents.Add(new(TurnoOutboxPersistence.OfferIntentId(r.OfferId, TurnoOutboxPersistence.Channel.Email), TurnoOutboxPersistence.Event.Offer, TurnoOutboxPersistence.Channel.Email, r.OfferId));
            foreach (var intent in intents)
            {
                var existingIntent = await tx.GetSnapshotAsync(db.Collection("turno_outbox").Document(intent.Id));
                if (existingIntent.Exists) throw new InvalidOperationException("Notificación existente incompatible.");
            }
            foreach (var intent in intents) TurnoOutboxPersistence.Enqueue(db, tx, intent);
            if (!anchorDoc.Exists) tx.Create(anchorRef, new Dictionary<string, object> { ["AnchorUtc"] = originalAnchor });
            var offerData = new Dictionary<string, object> { ["CascadeId"] = r.CascadeId, ["AppointmentId"] = r.AppointmentId,
                ["CreatedUtc"] = r.CreatedUtc, ["ExpiresUtc"] = r.ExpiresUtc, ["AnchorUtc"] = r.AnchorUtc, ["OriginalUtc"] = turno.InicioUtc,
                ["ProposedUtc"] = r.ProposedUtc, ["ProposedEndUtc"] = proposedEnd, ["ClienteId"] = turno.ClienteId, ["Estado"] = "Pendiente",
                ["OutboxChannelsV1"] = (whatsapp == true ? 1L : 0L) | (sendEmail ? 2L : 0L) };
            if (rooted)
            {
                offerData["ExpectedVacancyVersion"] = r.ExpectedVacancyVersion!.Value;
                offerData["ExpectedVacancyStartUtc"] = r.ExpectedVacancyStartUtc!.Value;
                offerData["ExpectedVacancyEndUtc"] = r.ExpectedVacancyEndUtc!.Value;
            }
            tx.Create(offerRef, offerData);
            tx.Create(claim.Reference, new Dictionary<string, object> { ["OfferId"] = r.OfferId, ["CascadeId"] = r.CascadeId });
            if (cascade.Exists) tx.Update(cascadeRef, "PendingId", r.OfferId);
            else tx.Create(cascadeRef, new Dictionary<string, object> { ["PendingId"] = r.OfferId });
            return true;
    }

    // Bounded owner-scoped lookup. The appointment is checked independently; stale or
    // corrupt offers are never displayed as belonging to the current client.
    internal async Task<List<(string Id, TurnoOferta Offer)>> PendingForOwner(string owner, DateTime now)
    {
        if (!ValidId(owner) || now.Kind != DateTimeKind.Utc) return [];
        var query = db.Collection("turno_ofertas").WhereEqualTo("ClienteId", owner).OrderBy(FieldPath.DocumentId);
        var result = new List<(string, TurnoOferta)>();
        const int budget = 1000;
        var scanned = 0;
        DocumentSnapshot? cursor = null;
        while (true)
        {
            // Read one extra document at the boundary so a complete budget-sized result succeeds.
            var page = await (cursor == null ? query : query.StartAfter(cursor)).Limit(Math.Min(100, budget - scanned + 1)).GetSnapshotAsync();
            if (scanned + page.Count > budget)
                throw new InvalidOperationException("Demasiadas ofertas para consultar.");
            foreach (var doc in page.Documents)
            {
                var data = doc.ToDictionary();
                if (!ValidId(doc.Id) || !data.TryGetValue("Estado", out var state) || !Equals(state, "Pendiente") ||
                    !data.TryGetValue("ClienteId", out var clientId) || !Equals(clientId, owner) ||
                    !data.TryGetValue("AppointmentId", out var appointmentValue) || appointmentValue is not string appointmentId || !ValidId(appointmentId) ||
                    !data.TryGetValue("CascadeId", out var cascadeValue) || cascadeValue is not string cascadeId || !ValidId(cascadeId) ||
                    !data.TryGetValue("CreatedUtc", out var createdValue) || createdValue is not Timestamp createdTimestamp ||
                    !data.TryGetValue("ExpiresUtc", out var expiryValue) || expiryValue is not Timestamp expiryTimestamp ||
                    !data.TryGetValue("AnchorUtc", out var anchorValue) || anchorValue is not Timestamp anchorTimestamp ||
                    !data.TryGetValue("OriginalUtc", out var originalValue) || originalValue is not Timestamp originalTimestamp ||
                    !data.TryGetValue("ProposedUtc", out var proposedValue) || proposedValue is not Timestamp proposedTimestamp ||
                    !data.TryGetValue("ProposedEndUtc", out var endValue) || endValue is not Timestamp endTimestamp) continue;
                var created = createdTimestamp.ToDateTime();
                var expiry = expiryTimestamp.ToDateTime();
                var anchor = anchorTimestamp.ToDateTime();
                var original = originalTimestamp.ToDateTime();
                var proposed = proposedTimestamp.ToDateTime();
                var end = endTimestamp.ToDateTime();
                if (!Utc(created) || !Utc(expiry) || !Utc(anchor) || !Utc(original) || !Utc(proposed) || !Utc(end) ||
                    created > now || expiry <= created || expiry <= now || proposed <= now || end <= proposed ||
                    proposed >= original || original > anchor || anchor.Ticks - proposed.Ticks > TimeSpan.FromDays(7).Ticks) continue;
                var appointment = await db.Collection("turnos").Document(appointmentId).GetSnapshotAsync();
                var claim = await db.Collection("turno_ofertas_pendientes").Document(appointmentId).GetSnapshotAsync();
                var cascade = await db.Collection("turno_cascadas").Document(cascadeId).GetSnapshotAsync();
                if (!appointment.Exists || !appointment.TryGetValue<string>("Id", out var id) || id != appointmentId ||
                    !appointment.TryGetValue<string>("ClienteId", out var client) || client != owner ||
                    !appointment.TryGetValue<string>("Estado", out var appointmentState) || appointmentState != "Pendiente" ||
                    !claim.Exists || !claim.TryGetValue<string>("OfferId", out var offerId) || offerId != doc.Id ||
                    !claim.TryGetValue<string>("CascadeId", out var claimCascade) || claimCascade != cascadeId ||
                    !cascade.Exists || !cascade.TryGetValue<string>("PendingId", out var pending) || pending != doc.Id) continue;
                try
                {
                    var offer = doc.ConvertTo<TurnoOferta>();
                    var booking = appointment.ConvertTo<Turno>();
                    ValidatePendingBooking(offer, booking);
                    if (offer.ClienteId == owner) result.Add((doc.Id, offer));
                }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or FormatException or OverflowException)
                {
                    // Malformed persisted snapshots must not become actionable offers.
                }
            }
            scanned += page.Count;
            if (page.Count < 100 || scanned == budget && page.Count == 0) break;
            cursor = page.Documents[^1];
        }
        return result;
    }

    internal record WhatsAppCapability(string Phone, string Owner, string Incarnation, long Revision, string Selection);

    private async Task<DocumentReference> AuthorizeWhatsApp(Transaction tx, WhatsAppCapability capability, string offerId, TurnoOferta item)
    {
        if (capability == null || !ValidId(capability.Phone) || !PhoneNumberHelper.IsValidPhoneNumber(capability.Phone) ||
            capability.Phone.Any(c => c is < '0' or > '9') || !ValidId(capability.Owner) ||
            string.IsNullOrWhiteSpace(capability.Incarnation) || capability.Revision < 0 || capability.Revision == long.MaxValue ||
            capability.Selection != offerId || !ValidId(item.AppointmentId))
            throw new InvalidOperationException("Sesión incompatible.");
        var sessionRef = db.Collection("whatsapp_sessions").Document(capability.Phone);
        var session = await tx.GetSnapshotAsync(sessionRef);
        var appointment = await tx.GetSnapshotAsync(db.Collection("turnos").Document(item.AppointmentId));
        if (!session.Exists || !session.TryGetValue<string>("Id", out var id) || id != capability.Phone ||
            !session.TryGetValue<string>("ClienteId", out var owner) || owner != capability.Owner ||
            !session.TryGetValue<string>("IncarnationId", out var incarnation) || incarnation != capability.Incarnation ||
            !session.TryGetValue<long>("Revision", out var revision) || revision != capability.Revision ||
            !session.TryGetValue<string>("CurrentState", out var state) || state != "TURNO_OFERTA_RESPUESTA" ||
            !session.TryGetValue<string>("SelectionId", out var selection) || selection != offerId ||
            item.Estado != "Pendiente" || item.ClienteId != owner || !appointment.Exists ||
            !appointment.TryGetValue<string>("Id", out var appointmentId) || appointmentId != item.AppointmentId ||
            !appointment.TryGetValue<string>("ClienteId", out var appointmentOwner) || appointmentOwner != owner)
            throw new InvalidOperationException("Sesión incompatible.");
        return sessionRef;
    }

    private static void ConsumeWhatsApp(Transaction tx, DocumentReference session, WhatsAppCapability capability) =>
        tx.Update(session, new Dictionary<string, object> { ["Revision"] = capability.Revision + 1,
            ["CurrentState"] = "MENU_CLIENTE_AUTENTICADO", ["SelectionId"] = FieldValue.Delete, ["LastInteraction"] = DateTime.UtcNow });

    internal record EmailCapability(string ClientId, string Hash, bool HttpErrors = false);

    private async Task<(DocumentReference Reference, bool Replay)> AuthorizeEmail(Transaction tx, EmailCapability capability, string cascadeId, string offerId, TurnoOferta item, DateTime time, string action)
    {
        var doc = await tx.GetSnapshotAsync(db.Collection("turno_ofertas_email_capabilities").Document(offerId));
        var appointment = ValidId(item.AppointmentId) ? await tx.GetSnapshotAsync(db.Collection("turnos").Document(item.AppointmentId)) : null;
        if (!doc.Exists || !doc.TryGetValue<string>("Hash", out var hash) || hash.Length != 64 ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(hash), Encoding.UTF8.GetBytes(capability.Hash)) ||
            !doc.TryGetValue<string>("CascadeId", out var cascade) || cascade != cascadeId ||
            !doc.TryGetValue<string>("ClienteId", out var owner) || owner != capability.ClientId || owner != item.ClienteId ||
            !doc.TryGetValue<bool>("Consumed", out var consumed) ||
            !doc.TryGetValue<DateTime>("ExpiresUtc", out var expiry) || expiry > item.ExpiresUtc || time >= expiry || time < item.CreatedUtc ||
            item.CascadeId != cascadeId || appointment == null || !appointment.Exists ||
            !appointment.TryGetValue<string>("Id", out var id) || id != item.AppointmentId ||
            !appointment.TryGetValue<string>("ClienteId", out var appointmentOwner) || appointmentOwner != owner)
        {
            if (capability.HttpErrors) throw new TurnoOfertaEmailInvalidException();
            throw new InvalidOperationException("Capacidad inválida.");
        }
        if (consumed)
        {
            if (!doc.TryGetValue<string>("ResponseActionV1", out var storedAction) || storedAction != action ||
                !doc.TryGetValue<string>("ResponseOfferV1", out var storedOffer) || storedOffer != offerId ||
                !doc.TryGetValue<string>("ResponseAppointmentV1", out var storedAppointment) || storedAppointment != item.AppointmentId ||
                item.Estado != (action == "Accept" ? "Aceptada" : "Rechazada"))
            {
                if (capability.HttpErrors) throw new TurnoOfertaEmailInvalidException();
                throw new InvalidOperationException("Capacidad inválida.");
            }
            return (doc.Reference, true);
        }
        if (item.Estado != "Pendiente" || doc.ContainsField("ResponseActionV1") || doc.ContainsField("ResponseOfferV1") || doc.ContainsField("ResponseAppointmentV1"))
        {
            if (capability.HttpErrors) throw new TurnoOfertaEmailInvalidException();
            throw new InvalidOperationException("Capacidad inválida.");
        }
        return (doc.Reference, false);
    }

    private static void ConsumeEmail(Transaction tx, DocumentReference doc, string action, string offerId, string appointmentId) =>
        tx.Update(doc, new Dictionary<string, object> { ["Consumed"] = true, ["ResponseActionV1"] = action,
            ["ResponseOfferV1"] = offerId, ["ResponseAppointmentV1"] = appointmentId });

    internal Task AcceptEmail(string cascadeId, string offerId, DateTime time, EmailCapability capability, CancellationToken cancellationToken = default) =>
        AcceptCore(cascadeId, offerId, time, null, capability, cancellationToken);
    internal Task RejectEmail(string cascadeId, string offerId, DateTime time, EmailCapability capability, CancellationToken cancellationToken = default) =>
        Transition(cascadeId, offerId, time, false, null, capability, cancellationToken);

    internal Task Accept(string cascadeId, string offerId, DateTime time) => AcceptCore(cascadeId, offerId, time, null);
    internal Task AcceptWhatsApp(string cascadeId, string offerId, DateTime time, WhatsAppCapability capability) =>
        AcceptCore(cascadeId, offerId, time, capability ?? throw new ArgumentNullException(nameof(capability)));

    private async Task AcceptCore(string cascadeId, string offerId, DateTime time, WhatsAppCapability? capability, EmailCapability? email = null, CancellationToken cancellationToken = default)
    {
        if (!ValidId(cascadeId) || !ValidId(offerId) || !Utc(time)) throw new ArgumentException("Transición inválida.");
        var lockRef = db.Collection("coordinacion").Document("turnos");
        var cascadeRef = db.Collection("turno_cascadas").Document(cascadeId);
        var offerRef = db.Collection("turno_ofertas").Document(offerId);
        Task<bool> Operation() => db.RunTransactionAsync(async tx =>
        {
            var capacity = await tx.GetSnapshotAsync(lockRef);
            var cascade = await tx.GetSnapshotAsync(cascadeRef);
            var offer = await tx.GetSnapshotAsync(offerRef);
            var receiptRef = db.Collection("turno_aceptaciones").Document(offerId);
            var receipt = await tx.GetSnapshotAsync(receiptRef);
            if (!cascade.Exists || !offer.Exists)
            {
                if (email?.HttpErrors == true) throw new TurnoOfertaEmailInvalidException();
                throw new InvalidOperationException("Oferta inexistente.");
            }
            var item = offer.ConvertTo<TurnoOferta>();
            var session = capability == null ? null : await AuthorizeWhatsApp(tx, capability, offerId, item);
            var emailAuth = email == null ? ((DocumentReference Reference, bool Replay)?)null : await AuthorizeEmail(tx, email, cascadeId, offerId, item, time, "Accept");
            await ValidateCancellation(tx, cascade);
            if (item.CascadeId != cascadeId || !ValidId(item.AppointmentId))
                throw new InvalidOperationException("Cascada incompatible.");
            var claim = await ReadClaim(tx, item.AppointmentId);
            var appointmentRef = db.Collection("turnos").Document(item.AppointmentId);
            var appointment = await tx.GetSnapshotAsync(appointmentRef);
            var anchorDoc = await tx.GetSnapshotAsync(db.Collection("turno_adelanto_anclas").Document(item.AppointmentId));
            if (!appointment.Exists || !anchorDoc.Exists || anchorDoc.GetValue<DateTime>("AnchorUtc") != item.AnchorUtc)
            {
                if (email?.HttpErrors == true) throw new TurnoOfertaEmailInvalidException();
                throw new InvalidOperationException("Turno o ancla inexistente.");
            }
            var turno = appointment.ConvertTo<Turno>();
            if (turno.Id != item.AppointmentId)
                throw new InvalidOperationException("Turno no elegible.");
            if (item.Estado == "Aceptada")
            {
                // Legacy accepted offers have no durable proof; never manufacture it on replay.
                if (!Utc(turno.InicioUtc) || !Utc(turno.FinUtc) || turno.FinUtc <= turno.InicioUtc ||
                    !ValidId(item.ClienteId) || turno.ClienteId != item.ClienteId ||
                    (offer.ContainsField("ExpectedVacancyVersion") &&
                        (!offer.TryGetValue<DateTime>("OriginalEndUtc", out var originalEnd) ||
                         !receipt.TryGetValue<DateTime>("OriginalEndUtc", out var receiptEnd) || originalEnd != receiptEnd)) ||
                    cascade.GetValue<string>("PendingId") == offerId || !receipt.Exists ||
                    !receipt.TryGetValue<string>("CascadeId", out var proofCascade) || proofCascade != cascadeId ||
                    !receipt.TryGetValue<string>("AppointmentId", out var proofAppointment) || proofAppointment != item.AppointmentId ||
                    !receipt.TryGetValue<DateTime>("OriginalUtc", out var proofStart) || proofStart != item.OriginalUtc ||
                    !receipt.TryGetValue<DateTime>("OriginalEndUtc", out var proofEnd) || proofEnd <= proofStart ||
                    !Utc(item.ProposedUtc) || !Utc(item.ProposedEndUtc) || item.ProposedEndUtc <= item.ProposedUtc ||
                    item.ProposedEndUtc.Ticks - item.ProposedUtc.Ticks != proofEnd.Ticks - proofStart.Ticks ||
                    !receipt.TryGetValue<DateTime>("ProposedUtc", out var proofProposed) || proofProposed != item.ProposedUtc ||
                    !receipt.TryGetValue<DateTime>("AnchorUtc", out var proofAnchor) || proofAnchor != item.AnchorUtc)
                    throw new InvalidOperationException("Aceptación incompatible.");
                return true;
            }
            if (email?.HttpErrors == true && (!claim.Exists || !claim.TryGetValue<string>("CascadeId", out var claimCascade) || claimCascade != cascadeId ||
                !claim.TryGetValue<string>("OfferId", out var claimedOffer) || claimedOffer != offerId))
                throw new TurnoOfertaEmailInvalidException();
            RequireClaim(claim, cascadeId, offerId);
            if (receipt.Exists) throw new InvalidOperationException("Aceptación incompatible.");
            if (turno.Validar().Count != 0 || turno.Estado != "Pendiente" || !turno.AceptaAdelanto)
            {
                if (email?.HttpErrors == true) throw new TurnoOfertaEmailInvalidException();
                throw new InvalidOperationException("Turno no elegible.");
            }
            ValidatePendingBooking(item, turno);
            if (item.Estado != "Pendiente" || cascade.GetValue<string>("PendingId") != offerId ||
                time < item.CreatedUtc || time >= item.ExpiresUtc || turno.InicioUtc != item.OriginalUtc ||
                item.ProposedUtc >= item.OriginalUtc || item.ProposedUtc >= item.AnchorUtc ||
                item.AnchorUtc.Ticks - item.ProposedUtc.Ticks > TimeSpan.FromDays(7).Ticks)
            {
                if (email?.HttpErrors == true) throw new TurnoOfertaEmailInvalidException();
                throw new InvalidOperationException("Transición no permitida.");
            }
            var membership = await EmployeeMembershipVersion.ReadAsync(db, tx);
            var employees = await tx.GetSnapshotAsync(db.Collection("empleados").WhereEqualTo("Estado", "Activo"));
            var reservations = await tx.GetSnapshotAsync(db.Collection("turnos").WhereEqualTo("Estado", "Pendiente"));
            var configDoc = await tx.GetSnapshotAsync(db.Collection("configuracion").Document("system_config"));
            if (!configDoc.Exists) throw new InvalidOperationException("Configuración no encontrada.");
            var config = configDoc.ConvertTo<Configuracion>();
            if (config.CapacidadMaximaConcurrente <= 0 || config.IntervaloInicioTurnosMinutos <= 0)
                throw new InvalidOperationException("Configuración inválida.");
            var openings = TurnoService.ObtenerAperturas(config, item.ProposedUtc, out var gridAnchor);
            var intervals = reservations.Documents.Select(doc =>
            {
                var reservation = doc.ConvertTo<Turno>();
                if (reservation.Id != doc.Id || reservation.Validar().Count != 0) throw new InvalidOperationException("Turno persistido inválido.");
                return new TurnoCapacityCandidate(doc.Id, new TurnoInterval(reservation.InicioUtc, reservation.FinUtc, reservation.Vehiculos[0].CantidadEmpleadosRequeridos));
            }).ToArray();
            if (employees.Count == 0 || !TurnoAvailabilityPolicy.IsAvailable(item.ProposedUtc,
                turno.Vehiculos[0].Servicios.Select(s => s.TiempoEstimado), turno.Vehiculos[0].CantidadEmpleadosRequeridos,
                config.CapacidadMaximaConcurrente, employees.Count, openings,
                TurnoAdelantoEligibilityPolicy.OtherReservations(item.AppointmentId, intervals), time, gridAnchor, config.IntervaloInicioTurnosMinutos))
                throw new InvalidOperationException("Capacidad no disponible.");
            var end = item.ProposedUtc.AddTicks(turno.FinUtc.Ticks - turno.InicioUtc.Ticks);
            if (session != null) ConsumeWhatsApp(tx, session, capability!);
            if (emailAuth != null) ConsumeEmail(tx, emailAuth.Value.Reference, "Accept", offerId, item.AppointmentId);
            tx.Delete(claim.Reference);
            tx.Create(receiptRef, new Dictionary<string, object> { ["CascadeId"] = cascadeId, ["AppointmentId"] = item.AppointmentId,
                ["OriginalUtc"] = turno.InicioUtc, ["OriginalEndUtc"] = turno.FinUtc, ["ProposedUtc"] = item.ProposedUtc, ["AnchorUtc"] = item.AnchorUtc });
            tx.Update(appointmentRef, new Dictionary<string, object> { ["InicioUtc"] = item.ProposedUtc, ["FinUtc"] = end });
            if (cascade.ContainsField("CancelledAppointmentId"))
            {
                var nextVersion = cascade.ContainsField("VacancyVersion") ? checked(cascade.GetValue<long>("VacancyVersion") + 1) : 1L;
                tx.Update(offerRef, new Dictionary<string, object> { ["Estado"] = "Aceptada", ["OriginalEndUtc"] = turno.FinUtc, ["VacancyVersion"] = nextVersion });
                tx.Update(cascadeRef, new Dictionary<string, object> { ["PendingId"] = "", ["VacancyStartUtc"] = turno.InicioUtc,
                    ["VacancyEndUtc"] = turno.FinUtc, ["VacancyVersion"] = nextVersion, ["VacancySourceOfferId"] = offerId,
                    ["ContinuationState"] = "Pendiente" });
            }
            else
            {
                tx.Update(offerRef, "Estado", "Aceptada");
                tx.Update(cascadeRef, "PendingId", "");
            }
            var version = capacity.Exists ? capacity.GetValue<long>("Version") : 0;
            tx.Set(lockRef, new Dictionary<string, object> { ["Version"] = checked(version + 1) });
            EmployeeMembershipVersion.Advance(tx, membership);
            return true;
        }, cancellationToken: cancellationToken);
        if (email != null) await TurnoService.RetryAborted(Operation, cancellationToken);
        else await TurnoService.RetryAborted(Operation);
    }

    internal Task Reject(string cascadeId, string offerId, DateTime time) => Transition(cascadeId, offerId, time, false);
    internal Task Expire(string cascadeId, string offerId, DateTime time) => Transition(cascadeId, offerId, time, true);
    internal Task RejectWhatsApp(string cascadeId, string offerId, DateTime time, WhatsAppCapability capability) =>
        Transition(cascadeId, offerId, time, false, capability ?? throw new ArgumentNullException(nameof(capability)));

    private async Task Transition(string cascadeId, string offerId, DateTime time, bool expire, WhatsAppCapability? capability = null, EmailCapability? email = null, CancellationToken cancellationToken = default)
    {
        if (!ValidId(cascadeId) || !ValidId(offerId) || !Utc(time)) throw new ArgumentException("Transición inválida.");
        var cascadeRef = db.Collection("turno_cascadas").Document(cascadeId);
        var offerRef = db.Collection("turno_ofertas").Document(offerId);
        Task<bool> Operation() => db.RunTransactionAsync(async tx =>
        {
            var cascade = await tx.GetSnapshotAsync(cascadeRef);
            var offer = await tx.GetSnapshotAsync(offerRef);
            if (!cascade.Exists || !offer.Exists)
            {
                if (email?.HttpErrors == true) throw new TurnoOfertaEmailInvalidException();
                throw new InvalidOperationException("Oferta inexistente.");
            }
            var item = offer.ConvertTo<Firebase.Models.TurnoOferta>();
            var session = capability == null ? null : await AuthorizeWhatsApp(tx, capability, offerId, item);
            var emailAuth = email == null ? ((DocumentReference Reference, bool Replay)?)null : await AuthorizeEmail(tx, email, cascadeId, offerId, item, time, "Reject");
            await ValidateCancellation(tx, cascade);
            if (item.CascadeId != cascadeId || !ValidId(item.AppointmentId) ||
                !Utc(item.CreatedUtc) || !Utc(item.ExpiresUtc) || item.ExpiresUtc <= item.CreatedUtc)
                throw new InvalidOperationException("Cascada incompatible.");
            var ledgerRef = Ledger(cascadeId, item.ClienteId, item.ProposedUtc, item.ProposedEndUtc);
            var ledger = await tx.GetSnapshotAsync(ledgerRef);
            var target = expire ? "Expirada" : "Rechazada";
            if (item.Estado == target)
            {
                if (cascade.GetValue<string>("PendingId") == offerId || !ledger.Exists ||
                    !ledger.TryGetValue<string>("OfferId", out var terminalOffer) || terminalOffer != offerId ||
                    !ledger.TryGetValue<string>("Estado", out var terminalState) || terminalState != target)
                    throw new InvalidOperationException("Marcador incompatible.");
                return true;
            }
            if (item.Estado != "Pendiente" || cascade.GetValue<string>("PendingId") != offerId || time < item.CreatedUtc ||
                (expire ? time < item.ExpiresUtc : time >= item.ExpiresUtc))
            {
                if (email?.HttpErrors == true) throw new TurnoOfertaEmailInvalidException();
                throw new InvalidOperationException("Transición no permitida.");
            }
            if (!ValidId(item.AppointmentId)) throw new InvalidOperationException("Turno inexistente.");
            var claim = await ReadClaim(tx, item.AppointmentId);
            if (email?.HttpErrors == true && (!claim.Exists || !claim.TryGetValue<string>("CascadeId", out var claimCascade) || claimCascade != cascadeId ||
                !claim.TryGetValue<string>("OfferId", out var claimedOffer) || claimedOffer != offerId))
                throw new TurnoOfertaEmailInvalidException();
            RequireClaim(claim, cascadeId, offerId);
            var appointment = await tx.GetSnapshotAsync(db.Collection("turnos").Document(item.AppointmentId));
            if (!appointment.Exists)
            {
                if (email?.HttpErrors == true) throw new TurnoOfertaEmailInvalidException();
                throw new InvalidOperationException("Turno inexistente.");
            }
            var turno = appointment.ConvertTo<Turno>();
            if (turno.Id != item.AppointmentId || !ValidId(turno.ClienteId) || turno.ClienteId != item.ClienteId)
                throw new InvalidOperationException("Oferta incompatible con el turno.");
            if (ledger.Exists) throw new InvalidOperationException("Horario ya ofrecido.");
            if (session != null) ConsumeWhatsApp(tx, session, capability!);
            if (emailAuth != null) ConsumeEmail(tx, emailAuth.Value.Reference, "Reject", offerId, item.AppointmentId);
            tx.Delete(claim.Reference);
            tx.Create(ledgerRef, new Dictionary<string, object> { ["OfferId"] = offerId, ["Estado"] = target });
            tx.Update(offerRef, "Estado", target);
            if (cascade.ContainsField("CancelledAppointmentId"))
                tx.Update(cascadeRef, new Dictionary<string, object> { ["PendingId"] = "", ["ContinuationState"] = "Pendiente", ["LastTerminalOfferId"] = offerId });
            else
                tx.Update(cascadeRef, "PendingId", "");
            return true;
        }, cancellationToken: cancellationToken);
        if (email != null) await TurnoService.RetryAborted(Operation, cancellationToken);
        else await TurnoService.RetryAborted(Operation);
    }
}

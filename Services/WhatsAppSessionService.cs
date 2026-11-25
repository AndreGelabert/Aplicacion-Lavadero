using Firebase.Models.WhatsApp;
using Google.Cloud.Firestore;

namespace Firebase.Services;

/// <summary>
/// Servicio para gestionar sesiones de conversación de WhatsApp en Firestore
/// </summary>
public class WhatsAppSessionService
{
    private readonly FirestoreDb _firestore;
    private readonly ILogger<WhatsAppSessionService> _logger;
    private const string SESSIONS_COLLECTION = "whatsapp_sessions";
    internal Func<Task>? AfterMissingSessionRead { get; set; }
    internal Func<Task>? AfterCleanupSnapshot { get; set; }
    internal Func<DateTime> CleanupUtcNow { get; set; } = () => DateTime.UtcNow;

    public WhatsAppSessionService(
        FirestoreDb firestore,
        ILogger<WhatsAppSessionService> logger)
    {
        _firestore = firestore;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene o crea una sesión para un número de teléfono
    /// </summary>
    public async Task<WhatsAppSession> GetOrCreateSession(string phoneNumber)
    {
        try
        {
            var docRef = _firestore.Collection(SESSIONS_COLLECTION).Document(phoneNumber);
            var snapshot = await docRef.GetSnapshotAsync();

            if (snapshot.Exists)
            {
                var session = snapshot.ConvertTo<WhatsAppSession>();
                _logger.LogInformation("WhatsApp session found");
                return session;
            }

            if (AfterMissingSessionRead != null) await AfterMissingSessionRead();
            // Recheck inside the transaction: another writer may have created the session.
            return await _firestore.RunTransactionAsync(async transaction =>
            {
                var current = await transaction.GetSnapshotAsync(docRef);
                if (current.Exists)
                    return current.ConvertTo<WhatsAppSession>();

                var newSession = CreateSession(phoneNumber);
                transaction.Create(docRef, newSession);
                return newSession;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError("WhatsApp session lookup failed: {ExceptionType}", ex.GetType().Name);
            throw;
        }
    }

    private static WhatsAppSession CreateSession(string phoneNumber) => new()
    {
        Id = phoneNumber,
        ClienteId = null,
        CurrentState = WhatsAppFlowStates.INICIO,
        TemporaryData = new Dictionary<string, string>(),
        LastInteraction = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
        IncarnationId = Guid.NewGuid().ToString("N")
    };

    // These primitives are isolated; legacy writers and flow callers remain unsafe until migrated.
    internal Task<bool> TryUpdateState(string phone, string incarnation, long revision, string state) =>
        TrySessionMutation(phone, incarnation, revision, session => new() { ["CurrentState"] = state });

    internal Task<bool> TryAssociateClient(string phone, string incarnation, long revision, string client) =>
        TrySessionMutation(phone, incarnation, revision, session => new() {
            ["ClienteId"] = client, ["CurrentState"] = WhatsAppFlowStates.MENU_CLIENTE_AUTENTICADO });

    internal Task<bool> TrySaveTemporaryData(string phone, string incarnation, long revision, string key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        return TrySessionMutation(phone, incarnation, revision, session => {
            session.TemporaryData[key] = value;
            return new() { ["TemporaryData"] = session.TemporaryData };
        });
    }

    internal Task<bool> TryDeleteSession(string phone, string incarnation, long revision) =>
        TrySessionMutation(phone, incarnation, revision, null);

    private async Task<bool> TrySessionMutation(string phone, string incarnation, long revision,
        Func<WhatsAppSession, Dictionary<string, object>>? update)
    {
        if (string.IsNullOrWhiteSpace(incarnation) || revision < 0 || revision == long.MaxValue) return false;
        var reference = _firestore.Collection(SESSIONS_COLLECTION).Document(phone);
        return await _firestore.RunTransactionAsync(async tx => {
            var snapshot = await tx.GetSnapshotAsync(reference);
            if (!snapshot.Exists) return false;
            var data = snapshot.ToDictionary();
            if (!data.TryGetValue("Id", out var id) || !Equals(id, phone) ||
                !data.TryGetValue("IncarnationId", out var storedIncarnation) || !Equals(storedIncarnation, incarnation) ||
                !data.TryGetValue("Revision", out var storedRevision) || !Equals(storedRevision, revision)) return false;
            if (update == null) tx.Delete(reference);
            else {
                var changes = update(snapshot.ConvertTo<WhatsAppSession>());
                changes["Revision"] = revision + 1;
                changes["LastInteraction"] = DateTime.UtcNow;
                tx.Update(reference, changes);
            }
            return true;
        });
    }

    // The selection mapping and confirmation are written only after comparing the entire
    // conversation identity, including its incarnation, in the same transaction.
    internal async Task<bool> TryCancellationTransition(string phone, WhatsAppSession expected, string state,
        string? selection, string? appointment, Dictionary<string, string>? choices, Timestamp? appointmentVersion = null)
    {
        if (string.IsNullOrEmpty(expected.IncarnationId) || expected.Revision < 0 || expected.Revision == long.MaxValue ||
            string.IsNullOrWhiteSpace(expected.ClienteId)) return false;
        var reference = _firestore.Collection(SESSIONS_COLLECTION).Document(phone);
        return await _firestore.RunTransactionAsync(async tx =>
        {
            var snapshot = await tx.GetSnapshotAsync(reference);
            if (!snapshot.Exists) return false;
            var data = snapshot.ToDictionary();
            if (!data.TryGetValue("Id", out var id) || !Equals(id, phone) ||
                !data.TryGetValue("ClienteId", out var client) || !Equals(client, expected.ClienteId) ||
                !data.TryGetValue("IncarnationId", out var incarnation) || !Equals(incarnation, expected.IncarnationId) ||
                !data.TryGetValue("Revision", out var revision) || !Equals(revision, expected.Revision) ||
                !data.TryGetValue("CurrentState", out var current) || !Equals(current, expected.CurrentState) ||
                (data.TryGetValue("SelectionId", out var prior) ? !Equals(prior, expected.SelectionId) : expected.SelectionId != null)) return false;
            tx.Update(reference, new Dictionary<string, object> {
                ["Revision"] = expected.Revision + 1, ["CurrentState"] = state,
                ["SelectionId"] = selection == null ? FieldValue.Delete : selection,
                ["CancellationAppointmentId"] = appointment == null ? FieldValue.Delete : appointment,
                ["CancellationAppointmentVersion"] = appointmentVersion == null ? FieldValue.Delete : appointmentVersion,
                ["CancellationChoices"] = choices == null ? FieldValue.Delete : choices,
                ["LastInteraction"] = DateTime.UtcNow });
            return true;
        });
    }

    // Internal capability transition; it does not reserve an appointment.
    internal async Task<bool> TryBookingTransition(string phone, WhatsAppSession expected, string state,
        string? selection, string? draft, int? tolerance, Dictionary<string, string>? choices)
    {
        if (phone is null || !PhoneNumberHelper.IsValidPhoneNumber(phone) || phone.Any(c => c is < '0' or > '9') ||
            expected == null || !ValidTransitionToken(expected.IncarnationId) || !ValidTransitionToken(expected.CurrentState) ||
            !ValidTransitionToken(state) || string.IsNullOrWhiteSpace(expected.ClienteId) || expected.Revision < 0 ||
            expected.Revision == long.MaxValue || (expected.SelectionId != null && !ValidTransitionToken(expected.SelectionId)) ||
            (selection != null && !ValidTransitionToken(selection)) || tolerance is < 0 or > 525600 ||
            !ValidBookingString(draft) ||
            (choices != null && choices.Any(x => !ValidTransitionToken(x.Key) || string.IsNullOrWhiteSpace(x.Value) || !ValidBookingString(x.Value)))) return false;
        var reference = _firestore.Collection(SESSIONS_COLLECTION).Document(phone);
        return await _firestore.RunTransactionAsync(async tx =>
        {
            var snapshot = await tx.GetSnapshotAsync(reference);
            if (!snapshot.Exists) return false;
            var data = snapshot.ToDictionary();
            if (!data.TryGetValue("Id", out var id) || !Equals(id, phone) ||
                !data.TryGetValue("ClienteId", out var client) || !Equals(client, expected.ClienteId) ||
                !data.TryGetValue("IncarnationId", out var incarnation) || !Equals(incarnation, expected.IncarnationId) ||
                !data.TryGetValue("Revision", out var revision) || !Equals(revision, expected.Revision) ||
                !data.TryGetValue("CurrentState", out var current) || !Equals(current, expected.CurrentState) ||
                (data.TryGetValue("SelectionId", out var prior) ? !Equals(prior, expected.SelectionId) : expected.SelectionId != null) ||
                !data.TryGetValue("TemporaryData", out var temp) || temp is not Dictionary<string, object> entries || entries.Values.Any(v => v is not string) ||
                !data.TryGetValue("CreatedAt", out var created) || created is not Timestamp ||
                !data.TryGetValue("LastInteraction", out var last) || last is not Timestamp ||
                (data.TryGetValue("BookingRequest", out var request) && request is not null && (request is not string s || !ValidBookingString(s))) ||
                !Equals(request, expected.BookingRequest) ||
                (data.TryGetValue("BookingTolerance", out var storedTolerance) && storedTolerance is not null && (storedTolerance is not long t || t < 0 || t > 525600)) ||
                (storedTolerance is long storedMinutes ? storedMinutes != expected.BookingTolerance : expected.BookingTolerance != null) ||
                (data.TryGetValue("BookingChoices", out var storedChoices) && storedChoices is not null &&
                    (storedChoices is not Dictionary<string, object> map || map.Any(x => !ValidTransitionToken(x.Key) || x.Value is not string v || string.IsNullOrWhiteSpace(v) || !ValidBookingString(v)))) ||
                (storedChoices is Dictionary<string, object> storedMap
                    ? expected.BookingChoices == null || storedMap.Count != expected.BookingChoices.Count || storedMap.Any(x => !expected.BookingChoices.TryGetValue(x.Key, out var v) || !Equals(x.Value, v))
                    : expected.BookingChoices != null)) return false;
            tx.Update(reference, new Dictionary<string, object> {
                ["Revision"] = expected.Revision + 1, ["CurrentState"] = state,
                ["SelectionId"] = selection == null ? FieldValue.Delete : selection,
                ["BookingRequest"] = draft == null ? FieldValue.Delete : draft,
                ["BookingTolerance"] = tolerance == null ? FieldValue.Delete : tolerance.Value,
                ["BookingChoices"] = choices == null ? FieldValue.Delete : choices,
                ["LastInteraction"] = DateTime.UtcNow });
            return true;
        });
    }

    /// <summary>
    /// Transición aislada: otros escritores anteriores no participan en este control.
    /// La selección es un identificador opaco suministrado por el llamador.
    /// </summary>
    internal async Task<bool> TryTransition(string phoneNumber, string? expectedIncarnation, long expectedRevision, string expectedState,
        string? expectedSelectionId, string newState, string? newSelectionId)
    {
        if (phoneNumber is null || !PhoneNumberHelper.IsValidPhoneNumber(phoneNumber) ||
            phoneNumber.Any(c => c is < '0' or > '9') ||
            expectedRevision < 0 || expectedRevision == long.MaxValue ||
            !ValidTransitionToken(expectedState) || !ValidTransitionToken(newState) ||
            (expectedSelectionId != null && !ValidTransitionToken(expectedSelectionId)) ||
            (newSelectionId != null && !ValidTransitionToken(newSelectionId)))
            throw new ArgumentException("Invalid transition");

        if (string.IsNullOrWhiteSpace(expectedIncarnation)) return false;
        var docRef = _firestore.Collection(SESSIONS_COLLECTION).Document(phoneNumber);
        return await _firestore.RunTransactionAsync(async transaction =>
        {
            var snapshot = await transaction.GetSnapshotAsync(docRef);
            if (!snapshot.Exists) return false;
            var data = snapshot.ToDictionary();
            if (!data.TryGetValue("IncarnationId", out var incarnation) || !Equals(incarnation, expectedIncarnation) ||
                !data.TryGetValue("Id", out var id) || id is not string storedId || storedId != phoneNumber ||
                !data.TryGetValue("CurrentState", out var state) || state is not string storedState || !ValidTransitionToken(storedState) ||
                !data.TryGetValue("TemporaryData", out var temp) || temp is not Dictionary<string, object> tempData || tempData.Values.Any(v => v is not string) ||
                !data.TryGetValue("CreatedAt", out var created) || created is not Timestamp ||
                !data.TryGetValue("LastInteraction", out var last) || last is not Timestamp ||
                (data.TryGetValue("ClienteId", out var client) && client is not null && client is not string) ||
                (data.TryGetValue("Revision", out var revisionValue) && (revisionValue is not long r || r < 0)) ||
                (data.TryGetValue("SelectionId", out var selectionValue) && selectionValue is not null &&
                    (selectionValue is not string s || !ValidTransitionToken(s)))) return false;
            var revision = data.TryGetValue("Revision", out var value) ? (long)value : 0;
            var selection = data.TryGetValue("SelectionId", out var selected) ? (string?)selected : null;
            if (revision != expectedRevision || storedState != expectedState || selection != expectedSelectionId) return false;
            transaction.Update(docRef, new Dictionary<string, object>
            {
                ["Revision"] = expectedRevision + 1,
                ["CurrentState"] = newState,
                ["SelectionId"] = newSelectionId!,
                ["LastInteraction"] = DateTime.UtcNow
            });
            return true;
        });
    }

    // Firestore's maximum UTF-8 string field value is 1 MiB minus 89 bytes.
    private static bool ValidBookingString(string? value)
    {
        if (value == null) return true;
        try { return new System.Text.UTF8Encoding(false, true).GetByteCount(value) <= 1048487; }
        catch (System.Text.EncoderFallbackException) { return false; }
    }

    private static bool ValidTransitionToken(string? value) => value is { Length: > 0 and <= 512 } &&
        value.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_' or '.' or '=');

    /// <summary>
    /// Actualiza el estado y la última interacción en una transacción que incrementa la revisión.
    /// La sesión debe existir; una revisión inválida o agotada provoca un error.
    /// </summary>
    public async Task UpdateSessionState(string phoneNumber, string newState)
    {
        try
        {
            var docRef = _firestore.Collection(SESSIONS_COLLECTION).Document(phoneNumber);
            await UpdateWithRevision(docRef, new Dictionary<string, object>
            {
                { "CurrentState", newState },
                { "LastInteraction", DateTime.UtcNow }
            });

            _logger.LogInformation("WhatsApp session state updated");
        }
        catch (Exception ex)
        {
            _logger.LogError("WhatsApp session update failed: {ExceptionType}", ex.GetType().Name);
            throw;
        }
    }

    /// <summary>
    /// Guarda un dato temporal en la sesión
    /// </summary>
    public async Task SaveTemporaryData(string phoneNumber, string key, string value)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(key);
            var docRef = _firestore.Collection(SESSIONS_COLLECTION).Document(phoneNumber);
            await _firestore.RunTransactionAsync(async transaction =>
            {
                var snapshot = await transaction.GetSnapshotAsync(docRef);
                if (!snapshot.Exists)
                {
                    var session = CreateSession(phoneNumber);
                    session.TemporaryData[key] = value;
                    session.LastInteraction = DateTime.UtcNow;
                    session.Revision = 1;
                    transaction.Create(docRef, session);
                    return;
                }

                var data = snapshot.ToDictionary();
                var revision = 0L;
                if (data.TryGetValue("Revision", out var storedRevision))
                {
                    if (storedRevision is not long r || r < 0 || r == long.MaxValue)
                        throw new InvalidOperationException("Invalid session revision");
                    revision = r;
                }
                var sessionData = snapshot.ConvertTo<WhatsAppSession>().TemporaryData;
                sessionData[key] = value;
                transaction.Update(docRef, new Dictionary<string, object>
                {
                    ["TemporaryData"] = sessionData,
                    ["Revision"] = revision + 1,
                    ["LastInteraction"] = DateTime.UtcNow
                });
            });

            _logger.LogInformation("WhatsApp session data saved");
        }
        catch (Exception ex)
        {
            _logger.LogError("WhatsApp session data save failed: {ExceptionType}", ex.GetType().Name);
            throw;
        }
    }

    /// <summary>
    /// Asocia un ClienteId y actualiza el estado y la última interacción en una transacción que incrementa la revisión.
    /// La sesión debe existir; una revisión inválida o agotada provoca un error.
    /// </summary>
    public async Task AssociateClienteToSession(string phoneNumber, string clienteId)
    {
        try
        {
            var docRef = _firestore.Collection(SESSIONS_COLLECTION).Document(phoneNumber);
            await UpdateWithRevision(docRef, new Dictionary<string, object>
            {
                { "ClienteId", clienteId },
                { "CurrentState", WhatsAppFlowStates.MENU_CLIENTE_AUTENTICADO },
                { "LastInteraction", DateTime.UtcNow }
            });

            _logger.LogInformation("Client associated with WhatsApp session");
        }
        catch (Exception ex)
        {
            _logger.LogError("WhatsApp session association failed: {ExceptionType}", ex.GetType().Name);
            throw;
        }
    }

    private async Task UpdateWithRevision(DocumentReference docRef, Dictionary<string, object> updates)
    {
        await _firestore.RunTransactionAsync(async transaction =>
        {
            var snapshot = await transaction.GetSnapshotAsync(docRef);
            var revision = 0L;
            if (snapshot.Exists && snapshot.ToDictionary().TryGetValue("Revision", out var storedRevision))
            {
                if (storedRevision is not long r || r < 0 || r == long.MaxValue)
                    throw new InvalidOperationException("Invalid session revision");
                revision = r;
            }
            updates["Revision"] = revision + 1;
            transaction.Update(docRef, updates);
        });
    }

    /// <summary>
    /// Limpia la sesión (útil para reiniciar el flujo)
    /// </summary>
    public async Task ClearSession(string phoneNumber)
    {
        try
        {
            var docRef = _firestore.Collection(SESSIONS_COLLECTION).Document(phoneNumber);
            await docRef.DeleteAsync();

            _logger.LogInformation("WhatsApp session deleted");
        }
        catch (Exception ex)
        {
            _logger.LogError("WhatsApp session deletion failed: {ExceptionType}", ex.GetType().Name);
            throw;
        }
    }

    private static bool TryGetCleanupActivity(DocumentSnapshot snapshot, out DateTime lastInteraction)
    {
        lastInteraction = default;
        var data = snapshot.ToDictionary();
        if (!data.TryGetValue("Id", out var id) || id is not string ||
            !data.TryGetValue("CurrentState", out var state) || state is not string ||
            !data.TryGetValue("CreatedAt", out var created) || created is not Timestamp ||
            !data.TryGetValue("LastInteraction", out var last) || last is not Timestamp timestamp ||
            (data.TryGetValue("TemporaryData", out var temp) && (temp is not Dictionary<string, object> entries || entries.Values.Any(v => v is not string))) ||
            (data.TryGetValue("ClienteId", out var client) && client is not null && client is not string) ||
            (data.TryGetValue("Revision", out var revision) && revision is not long) ||
            (data.TryGetValue("SelectionId", out var selection) && selection is not null && selection is not string))
            return false;
        lastInteraction = timestamp.ToDateTime();
        return true;
    }

    /// <summary>
    /// Limpia sesiones inactivas (más de 30 minutos sin actividad)
    /// </summary>
    public async Task CleanupInactiveSessions()
    {
        try
        {
            var thirtyMinutesAgo = CleanupUtcNow().AddMinutes(-30);
            var snapshot = await _firestore.Collection(SESSIONS_COLLECTION).GetSnapshotAsync();
            if (AfterCleanupSnapshot != null) await AfterCleanupSnapshot();

            foreach (var doc in snapshot.Documents)
            {
                if (TryGetCleanupActivity(doc, out var lastInteraction) && lastInteraction < thirtyMinutesAgo)
                {
                    var deleted = await _firestore.RunTransactionAsync(async transaction =>
                    {
                        var current = await transaction.GetSnapshotAsync(doc.Reference);
                        if (!current.Exists || !doc.UpdateTime.HasValue || current.UpdateTime != doc.UpdateTime ||
                            !TryGetCleanupActivity(current, out var currentActivity) || currentActivity >= thirtyMinutesAgo)
                            return false;
                        transaction.Delete(doc.Reference);
                        return true;
                    });
                    if (deleted) _logger.LogInformation("Inactive WhatsApp session deleted");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("WhatsApp session cleanup failed: {ExceptionType}", ex.GetType().Name);
            throw;
        }
    }
}

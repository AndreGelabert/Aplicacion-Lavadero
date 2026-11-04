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

            // Crear nueva sesión
            var newSession = new WhatsAppSession
            {
                Id = phoneNumber,
                ClienteId = null,
                CurrentState = WhatsAppFlowStates.INICIO,
                TemporaryData = new Dictionary<string, string>(),
                LastInteraction = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            await docRef.SetAsync(newSession);
            _logger.LogInformation("WhatsApp session created");
            return newSession;
        }
        catch (Exception ex)
        {
            _logger.LogError("WhatsApp session lookup failed: {ExceptionType}", ex.GetType().Name);
            throw;
        }
    }

    /// <summary>
    /// Actualiza el estado de una sesión
    /// </summary>
    public async Task UpdateSessionState(string phoneNumber, string newState)
    {
        try
        {
            var docRef = _firestore.Collection(SESSIONS_COLLECTION).Document(phoneNumber);
            await docRef.UpdateAsync(new Dictionary<string, object>
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
            var session = await GetOrCreateSession(phoneNumber);
            session.TemporaryData[key] = value;
            session.LastInteraction = DateTime.UtcNow;

            var docRef = _firestore.Collection(SESSIONS_COLLECTION).Document(phoneNumber);
            await docRef.SetAsync(session, SetOptions.Overwrite);

            _logger.LogInformation("WhatsApp session data saved");
        }
        catch (Exception ex)
        {
            _logger.LogError("WhatsApp session data save failed: {ExceptionType}", ex.GetType().Name);
            throw;
        }
    }

    /// <summary>
    /// Asocia un ClienteId a una sesión (login)
    /// </summary>
    public async Task AssociateClienteToSession(string phoneNumber, string clienteId)
    {
        try
        {
            var docRef = _firestore.Collection(SESSIONS_COLLECTION).Document(phoneNumber);
            await docRef.UpdateAsync(new Dictionary<string, object>
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

    /// <summary>
    /// Limpia sesiones inactivas (más de 30 minutos sin actividad)
    /// </summary>
    public async Task CleanupInactiveSessions()
    {
        try
        {
            var thirtyMinutesAgo = DateTime.UtcNow.AddMinutes(-30);
            var snapshot = await _firestore.Collection(SESSIONS_COLLECTION).GetSnapshotAsync();

            foreach (var doc in snapshot.Documents)
            {
                var session = doc.ConvertTo<WhatsAppSession>();
                if (session.LastInteraction < thirtyMinutesAgo)
                {
                    await doc.Reference.DeleteAsync();
                    _logger.LogInformation("Inactive WhatsApp session deleted");
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

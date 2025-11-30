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
                _logger.LogInformation("📱 Sesión existente encontrada para {PhoneNumber}", phoneNumber);
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
            _logger.LogInformation("✨ Nueva sesión creada para {PhoneNumber}", phoneNumber);
            return newSession;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener/crear sesión para {PhoneNumber}", phoneNumber);
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

            _logger.LogInformation("🔄 Estado actualizado para {PhoneNumber}: {NewState}", phoneNumber, newState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar estado de sesión para {PhoneNumber}", phoneNumber);
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

            _logger.LogInformation("💾 Dato guardado en sesión {PhoneNumber}: {Key} = {Value}", phoneNumber, key, value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar dato temporal en sesión para {PhoneNumber}", phoneNumber);
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

            _logger.LogInformation("🔐 Cliente {ClienteId} asociado a sesión {PhoneNumber}", clienteId, phoneNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al asociar cliente a sesión para {PhoneNumber}", phoneNumber);
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

            _logger.LogInformation("🗑️ Sesión eliminada para {PhoneNumber}", phoneNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar sesión para {PhoneNumber}", phoneNumber);
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
                    _logger.LogInformation("🧹 Sesión inactiva eliminada: {PhoneNumber}", session.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al limpiar sesiones inactivas");
        }
    }
}

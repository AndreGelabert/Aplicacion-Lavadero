using Google.Cloud.Firestore;

/// <summary>
/// Modelo para registros de auditoría en Firestore.
/// Se utiliza para rastrear todas las acciones importantes realizadas por los usuarios en el sistema.
/// </summary>
/// <remarks>
/// Este modelo se almacena en la colección "audit_logs" de Firestore.
/// Se utiliza en AuditService.LogEvent() para crear registros de auditoría
/// de acciones como login, logout, creación/edición/eliminación de servicios, etc.
/// 
/// Ejemplos de uso:
/// - Registro de inicio de sesión con Google
/// - Migración de cuentas a Google Auth
/// - Creación, actualización o eliminación de servicios
/// - Cambios en roles de empleados
/// </remarks>
[FirestoreData]
public class AuditLog
{
    /// <summary>
    /// ID único del usuario que realizó la acción.
    /// Corresponde al UID de Firebase Authentication.
    /// </summary>
    [FirestoreProperty]
    public string UserId { get; set; }

    /// <summary>
    /// Correo electrónico del usuario que realizó la acción.
    /// Se incluye para facilitar la identificación en los logs.
    /// </summary>
    [FirestoreProperty]
    public string UserEmail { get; set; }

    /// <summary>
    /// Descripción de la acción realizada.
    /// Ejemplos: "Inicio de sesión con Google", "Servicio creado", "Rol actualizado"
    /// </summary>
    [FirestoreProperty]
    public string Action { get; set; }

    /// <summary>
    /// ID del objeto afectado por la acción (si aplica).
    /// Por ejemplo, el ID de un servicio que fue modificado.
    /// Puede ser null para acciones que no afectan un objeto específico.
    /// </summary>
    [FirestoreProperty]
    public string TargetId { get; set; }

    /// <summary>
    /// Tipo del objeto afectado por la acción (si aplica).
    /// Ejemplos: "Servicio", "Empleado", "TipoServicio"
    /// Puede ser null para acciones que no afectan un objeto específico.
    /// </summary>
    [FirestoreProperty]
    public string TargetType { get; set; }

    /// <summary>
    /// Fecha y hora en que se realizó la acción.
    /// Se establece automáticamente en AuditService.LogEvent().
    /// </summary>
    [FirestoreProperty]
    public DateTime Timestamp { get; set; }
}

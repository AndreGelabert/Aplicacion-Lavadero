using Google.Cloud.Firestore;

/// <summary>
/// Modelo para registros de auditoría en Firestore.
/// Se utiliza para rastrear todas las acciones importantes realizadas por los usuarios en el sistema.
/// </summary>
/// <remarks>
/// Este modelo se almacena en la colección "registros_auditoria" de Firestore.
/// Se utiliza en AuditService.LogEvent() para crear registros de auditoría
/// de acciones como login, logout, creación/edición/eliminación de servicios, etc.
///
/// Campos de comparación (S1-S4):
/// - Antes/Despues: diccionarios clave→valor de los campos auditados antes y después de la operación.
///   null indica registro legado sin historial; diccionarios vacíos pueden representar una captura sin cambios.
/// - NombreHistorico: nombre descriptivo del objeto al momento de la operación (para identificación histórica).
/// - DocId: ID del documento Firestore, mapeado en lectura (no persistido en el payload).
/// </remarks>
[FirestoreData]
public class AuditLog
{
    /// <summary>
    /// ID del documento Firestore. No es un campo persistido; se asigna en el mapeo de lectura.
    /// </summary>
    public string DocId { get; set; }

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
    /// Nombre descriptivo del objeto al momento de la operación.
    /// Permite identificación histórica aunque el objeto sea modificado o eliminado después.
    /// </summary>
    [FirestoreProperty]
    public string NombreHistorico { get; set; }

    /// <summary>
    /// Estado de los campos auditados ANTES de la operación.
    /// null → registro legado sin historial de comparación.
    /// diccionario vacío + Despues vacío → captura sin campos cambiados.
    /// diccionario vacío + Despues con valores → creación (sin estado previo).
    /// </summary>
    [FirestoreProperty]
    public Dictionary<string, string> Antes { get; set; }

    /// <summary>
    /// Estado de los campos auditados DESPUÉS de la operación.
    /// null → registro legado sin historial.
    /// </summary>
    [FirestoreProperty]
    public Dictionary<string, string> Despues { get; set; }

    /// <summary>Indica explícitamente que el evento capturado no admite comparación.</summary>
    [FirestoreProperty]
    public bool ComparacionNoAplica { get; set; }

    /// <summary>
    /// Fecha y hora en que se realizó la acción.
    /// Se establece automáticamente en AuditService.LogEvent().
    /// </summary>
    [FirestoreProperty]
    public DateTime Timestamp { get; set; }
}

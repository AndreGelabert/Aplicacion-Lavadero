namespace Firebase.Models
{
    /// <summary>
    /// Modelo auxiliar para auditoría con nombre de usuario.
    /// Extiende la información básica de AuditLog con nombres legibles
    /// para facilitar la visualización en interfaces de usuario.
    /// </summary>
    /// <remarks>
    /// Este modelo se utiliza para mostrar registros de auditoría con
    /// información adicional que no se almacena directamente en Firestore,
    /// como el nombre del usuario y el nombre del objeto afectado.
    /// </remarks>
    public class AuditLogConNombre
    {
        /// <summary>
        /// ID del documento Firestore. Necesario para la llamada al endpoint de detalle.
        /// </summary>
        public string DocId { get; set; }

        /// <summary>
        /// ID único del usuario que realizó la acción.
        /// Corresponde al UID de Firebase Authentication.
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// Correo electrónico del usuario que realizó la acción.
        /// </summary>
        public string UserEmail { get; set; }

        /// <summary>
        /// Nombre completo del usuario que realizó la acción.
        /// Se obtiene de la colección de empleados.
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// Descripción de la acción realizada.
        /// Ejemplos: "Inicio de sesión con Google", "Servicio creado"
        /// </summary>
        public string Action { get; set; }

        /// <summary>
        /// ID del objeto afectado por la acción (si aplica).
        /// Por ejemplo, el ID de un servicio que fue modificado.
        /// </summary>
        public string TargetId { get; set; }

        /// <summary>
        /// Tipo del objeto afectado por la acción (si aplica).
        /// Ejemplos: "Servicio", "Empleado", "TipoServicio"
        /// </summary>
        public string TargetType { get; set; }

        /// <summary>
        /// Nombre legible del objeto afectado.
        /// Facilita la identificación sin necesidad de buscar por ID.
        /// </summary>
        public string TargetName { get; set; }

        /// <summary>
        /// Nombre histórico del objeto capturado al momento de la operación.
        /// </summary>
        public string NombreHistorico { get; set; }

        /// <summary>
        /// Estado de los campos auditados ANTES de la operación.
        /// null indica registro legado sin historial.
        /// </summary>
        public Dictionary<string, string> Antes { get; set; }

        /// <summary>
        /// Estado de los campos auditados DESPUÉS de la operación.
        /// null indica registro legado sin historial.
        /// </summary>
        public Dictionary<string, string> Despues { get; set; }

        /// <summary>
        /// Fecha y hora en que se realizó la acción.
        /// </summary>
        public DateTime Timestamp { get; set; }
    }
}

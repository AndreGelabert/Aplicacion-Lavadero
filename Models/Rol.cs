using Google.Cloud.Firestore;

namespace Firebase.Models
{
    /// <summary>
    /// Modelo que representa un rol del sistema con su conjunto de permisos.
    /// </summary>
    /// <remarks>
    /// Este modelo se almacena en Firestore en la colección "roles".
    /// Los permisos se guardan como claves planas con el formato "Modulo.Accion"
    /// (por ejemplo, "Clientes.Consultar").
    /// Los roles nuevos inician con Estado "Activo" y la fecha de creación en UTC.
    /// </remarks>
    [FirestoreData]
    public class Rol
    {
        /// <summary>
        /// Identificador único del rol en Firestore.
        /// Los roles base usan IDs fijos: "rol-administrador" y "rol-empleado".
        /// </summary>
        [FirestoreProperty]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Nombre del rol. Es obligatorio, único (sin distinguir mayúsculas)
        /// y no puede superar los 50 caracteres.
        /// </summary>
        [FirestoreProperty]
        public string Nombre { get; set; } = string.Empty;

        /// <summary>
        /// Descripción opcional del rol. No puede superar los 250 caracteres.
        /// </summary>
        [FirestoreProperty]
        public string Descripcion { get; set; } = string.Empty;

        /// <summary>
        /// Permisos asignados al rol como claves "Modulo.Accion".
        /// Debe contener al menos un permiso válido.
        /// </summary>
        [FirestoreProperty]
        public List<string> Permisos { get; set; } = new();

        /// <summary>
        /// Estado actual del rol. Valores: "Activo", "Inactivo".
        /// Los roles nuevos inician en "Activo".
        /// </summary>
        [FirestoreProperty]
        public string Estado { get; set; } = "Activo";

        /// <summary>
        /// Fecha de creación del rol en UTC.
        /// </summary>
        [FirestoreProperty]
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    }
}

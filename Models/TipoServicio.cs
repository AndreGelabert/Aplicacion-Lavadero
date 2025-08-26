using Google.Cloud.Firestore;

namespace Firebase.Models
{
    /// <summary>
    /// Modelo que representa un tipo de servicio en el sistema de lavadero.
    /// Se utiliza para categorizar los servicios disponibles.
    /// </summary>
    /// <remarks>
    /// Este modelo se almacena en Firestore en la colección "tipos_servicio".
    /// Se utiliza en:
    /// - TipoServicioService para operaciones CRUD
    /// - ServicioController para llenar dropdowns y validaciones
    /// - Vistas de servicios para mostrar opciones disponibles
    /// 
    /// Ejemplos de tipos de servicio: "Lavado Básico", "Lavado Premium", "Encerado", etc.
    /// </remarks>
    [FirestoreData]
    public class TipoServicio
    {
        /// <summary>
        /// Identificador único del tipo de servicio en Firestore.
        /// Se genera automáticamente al crear un nuevo tipo de servicio.
        /// </summary>
        [FirestoreProperty]
        public required string Id { get; set; }

        /// <summary>
        /// Nombre descriptivo del tipo de servicio.
        /// Este nombre se muestra en las interfaces de usuario y formularios.
        /// Debe ser único y descriptivo (ej: "Lavado Básico", "Lavado Premium").
        /// </summary>
        [FirestoreProperty]
        public required string Nombre { get; set; }
    }
}

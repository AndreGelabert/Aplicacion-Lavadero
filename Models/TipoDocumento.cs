using Google.Cloud.Firestore;

namespace Firebase.Models
{
    /// <summary>
    /// Modelo que representa un tipo de documento.
    /// </summary>
    [FirestoreData]
    public class TipoDocumento
    {
        /// <summary>
        /// Identificador único del tipo de documento en Firestore.
        /// </summary>
        [FirestoreProperty]
        public required string Id { get; set; }

        /// <summary>
        /// Nombre del tipo de documento (ej: DNI, Pasaporte, Cédula).
        /// </summary>
        [FirestoreProperty]
        public required string Nombre { get; set; }
    }
}

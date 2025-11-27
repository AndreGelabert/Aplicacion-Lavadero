using Google.Cloud.Firestore;

namespace Firebase.Models
{
    /// <summary>
    /// Modelo que representa un tipo de documento de identidad.
    /// </summary>
    [FirestoreData]
    public class TipoDocumento
    {
        /// <summary>
        /// Identificador único del tipo de documento.
        /// </summary>
        [FirestoreProperty]
        public required string Id { get; set; }

        /// <summary>
        /// Nombre del tipo de documento (ej: DNI, Pasaporte, CUIT).
        /// </summary>
        [FirestoreProperty]
        public required string Nombre { get; set; }
    }
}

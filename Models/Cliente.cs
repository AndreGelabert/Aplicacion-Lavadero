using System.ComponentModel.DataAnnotations;
using Google.Cloud.Firestore;

namespace Firebase.Models
{
    /// <summary>
    /// Modelo que representa un cliente del lavadero.
    /// Contiene información personal y relaciones con vehículos.
    /// </summary>
    [FirestoreData]
    public class Cliente
    {
        /// <summary>
        /// Identificador único del cliente en Firestore.
        /// </summary>
        [FirestoreProperty]
        public required string Id { get; set; }

        /// <summary>
        /// Tipo de documento del cliente (ej: DNI, Pasaporte, etc.).
        /// Debe corresponder a un tipo existente en la colección "tipos_documento".
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El tipo de documento es obligatorio")]
        public required string TipoDocumento { get; set; }

        /// <summary>
        /// Número de documento del cliente.
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El número de documento es obligatorio")]
        public required string NumeroDocumento { get; set; }

        /// <summary>
        /// Nombre completo del cliente (FirstName + LastName combinados).
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El nombre completo es obligatorio")]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$", ErrorMessage = "El nombre solo puede contener letras y espacios")]
        public required string NombreCompleto { get; set; }

        /// <summary>
        /// Número de teléfono del cliente.
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El teléfono es obligatorio")]
        public required string Telefono { get; set; }

        /// <summary>
        /// Dirección de correo electrónico del cliente.
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El email es obligatorio")]
        [EmailAddress(ErrorMessage = "El formato del email no es válido")]
        public required string Email { get; set; }

        /// <summary>
        /// Lista de IDs de vehículos que pertenecen a este cliente.
        /// </summary>
        [FirestoreProperty]
        public List<string> VehiculosIds { get; set; } = new List<string>();

        /// <summary>
        /// Estado actual del cliente.
        /// Valores: "Activo", "Inactivo".
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El estado es obligatorio")]
        public required string Estado { get; set; }
    }
}

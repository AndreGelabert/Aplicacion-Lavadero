using System.ComponentModel.DataAnnotations;
using Google.Cloud.Firestore;

namespace Firebase.Models
{
    /// <summary>
    /// Modelo que representa un cliente del lavadero.
    /// </summary>
    [FirestoreData]
    public class Cliente
    {
        /// <summary>
        /// Identificador único del cliente.
        /// </summary>
        [FirestoreProperty]
        public required string Id { get; set; }

        /// <summary>
        /// Nombre del tipo de documento (ej: DNI).
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El tipo de documento es obligatorio")]
        public required string TipoDocumento { get; set; }

        /// <summary>
        /// Número de documento de identidad.
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El número de documento es obligatorio")]
        [RegularExpression(@"^[0-9]+$", ErrorMessage = "El número de documento solo puede contener números")]
        public required string NumeroDocumento { get; set; }

        /// <summary>
        /// Nombre del cliente.
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El nombre es obligatorio")]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$", ErrorMessage = "El nombre solo puede contener letras y espacios")]
        public required string Nombre { get; set; }

        /// <summary>
        /// Apellido del cliente.
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El apellido es obligatorio")]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$", ErrorMessage = "El apellido solo puede contener letras y espacios")]
        public required string Apellido { get; set; }

        /// <summary>
        /// Nombre completo concatenado (Nombre + Apellido) para búsquedas y visualización.
        /// </summary>
        public string NombreCompleto => $"{Nombre} {Apellido}";

        /// <summary>
        /// Teléfono de contacto.
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El teléfono es obligatorio")]
        [Phone(ErrorMessage = "El formato del teléfono no es válido")]
        public required string Telefono { get; set; }

        /// <summary>
        /// Correo electrónico de contacto.
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El email es obligatorio")]
        [EmailAddress(ErrorMessage = "El formato del email no es válido")]
        public required string Email { get; set; }

        /// <summary>
        /// Lista de IDs de los vehículos asociados a este cliente.
        /// </summary>
        [FirestoreProperty]
        public List<string> VehiculosIds { get; set; } = new List<string>();
    }
}

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
        /// Nombre del cliente. Debe contener al menos 3 letras.
        /// Puede contener múltiples nombres separados por espacios.
        /// Ejemplos válidos: "Juan", "María José", "José Luis"
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El nombre es obligatorio")]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]{3,}$", 
            ErrorMessage = "El nombre debe contener al menos 3 letras")]
        [MinLength(3, ErrorMessage = "El nombre debe tener al menos 3 caracteres")]
        public required string Nombre { get; set; }

        /// <summary>
        /// Apellido del cliente. Debe contener al menos 3 letras.
        /// Puede contener múltiples apellidos separados por espacios.
        /// Ejemplos válidos: "Pérez", "García López", "Rodríguez Martínez"
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El apellido es obligatorio")]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]{3,}$", 
            ErrorMessage = "El apellido debe contener al menos 3 letras")]
        [MinLength(3, ErrorMessage = "El apellido debe tener al menos 3 caracteres")]
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
        /// Debe tener un formato válido con al menos 3 caracteres en el dominio después del @.
        /// Ejemplo: usuario@dominio.com
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El email es obligatorio")]
        [EmailAddress(ErrorMessage = "El formato del email no es válido")]
        [RegularExpression(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]{3,}\.[a-zA-Z]{2,}$",
            ErrorMessage = "El email debe tener al menos 3 caracteres después del @")]
      public required string Email { get; set; }

        /// <summary>
        /// Lista de IDs de los vehículos asociados a este cliente.
        /// </summary>
        [FirestoreProperty]
        public List<string> VehiculosIds { get; set; } = new List<string>();

        /// <summary>
        /// Estado del cliente (Activo/Inactivo) para borrado lógico.
        /// </summary>
        [FirestoreProperty]
        public string Estado { get; set; } = "Activo";
    }
}

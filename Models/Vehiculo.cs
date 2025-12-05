using System.ComponentModel.DataAnnotations;
using Google.Cloud.Firestore;

namespace Firebase.Models
{
    /// <summary>
    /// Modelo que representa un vehículo registrado en el lavadero.
    /// Un vehículo puede tener múltiples clientes asociados (dueños compartidos).
    /// </summary>
    [FirestoreData]
    public class Vehiculo
    {
        /// <summary>
        /// Identificador único del vehículo.
        /// </summary>
        [FirestoreProperty]
        public required string Id { get; set; }

        /// <summary>
        /// Patente o matrícula del vehículo.
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "La patente es obligatoria")]
        [RegularExpression(@"^[a-zA-Z0-9\s-]+$", ErrorMessage = "La patente solo puede contener letras, números, espacios y guiones")]
        public required string Patente { get; set; }

        /// <summary>
        /// Tipo de vehículo (ej: Automóvil, SUV).
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El tipo de vehículo es obligatorio")]
        public required string TipoVehiculo { get; set; }

        /// <summary>
        /// Marca del vehículo.
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "La marca es obligatoria")]
        public required string Marca { get; set; }

        /// <summary>
        /// Modelo del vehículo.
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El modelo es obligatorio")]
        public required string Modelo { get; set; }

        /// <summary>
        /// Color del vehículo.
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El color es obligatorio")]
        public required string Color { get; set; }

        /// <summary>
        /// ID del cliente dueño principal del vehículo (mantenido por compatibilidad).
        /// Para nuevos vehículos, representa al primer cliente que registró el vehículo.
        /// </summary>
        [FirestoreProperty]
        public string ClienteId { get; set; } = "";

        /// <summary>
        /// Nombre completo del dueño principal (solo lectura/visualización).
        /// </summary>
        [FirestoreProperty]
        public string? ClienteNombreCompleto { get; set; }

        /// <summary>
        /// Lista de IDs de todos los clientes asociados a este vehículo.
        /// Incluye al cliente principal y a los clientes que se asociaron mediante clave.
        /// </summary>
        [FirestoreProperty]
        public List<string> ClientesIds { get; set; } = new List<string>();

        /// <summary>
        /// Clave de asociación encriptada (hash SHA256) para permitir que otros clientes
        /// se asocien a este vehículo. La clave en texto plano solo se muestra al momento
        /// del registro inicial.
        /// </summary>
        [FirestoreProperty]
        public string? ClaveAsociacionHash { get; set; }

        /// <summary>
        /// Estado del vehículo (Activo/Inactivo) para borrado lógico.
        /// </summary>
        [FirestoreProperty]
        public string Estado { get; set; } = "Activo";
    }
}

using System.ComponentModel.DataAnnotations;
using Google.Cloud.Firestore;

namespace Firebase.Models
{
    /// <summary>
    /// Modelo que representa un vehículo en el sistema.
    /// Un vehículo pertenece a un único cliente.
    /// </summary>
    [FirestoreData]
    public class Vehiculo
    {
        /// <summary>
        /// Identificador único del vehículo en Firestore.
        /// </summary>
        [FirestoreProperty]
        public required string Id { get; set; }

        /// <summary>
        /// Placa o patente del vehículo.
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "La placa es obligatoria")]
        public required string Placa { get; set; }

        /// <summary>
        /// Tipo de vehículo (ej: Automóvil, SUV, Motocicleta).
        /// Debe corresponder a un tipo existente en la colección "tipos_vehiculo".
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El tipo de vehículo es obligatorio")]
        public required string TipoVehiculo { get; set; }

        /// <summary>
        /// Marca del vehículo (ej: Toyota, Ford, Honda).
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "La marca es obligatoria")]
        public required string Marca { get; set; }

        /// <summary>
        /// Modelo del vehículo (ej: Corolla, Focus, Civic).
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
        /// ID del cliente dueño del vehículo.
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El dueño es obligatorio")]
        public required string ClienteId { get; set; }

        /// <summary>
        /// Estado actual del vehículo.
        /// Valores: "Activo", "Inactivo".
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El estado es obligatorio")]
        public required string Estado { get; set; }
    }
}

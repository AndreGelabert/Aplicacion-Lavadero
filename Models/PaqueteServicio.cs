using System.ComponentModel.DataAnnotations;
using Google.Cloud.Firestore;

namespace Firebase.Models
{
    /// <summary>
    /// Modelo que representa un paquete de servicios (combo) en el sistema de lavadero.
    /// Un paquete agrupa 2 o más servicios con un precio diferencial (descuento).
    /// </summary>
    /// <remarks>
    /// Este modelo se almacena en Firestore en la colección "paquetes_servicios".
    /// Reglas de validación:
    /// - Solo puede incluir un servicio de cada tipo de servicio
    /// - Todos los servicios deben ser para el mismo tipo de vehículo
    /// - El precio es la suma de los servicios menos el porcentaje de descuento
    /// - El tiempo estimado es la suma de los tiempos de los servicios (no editable)
    /// </remarks>
    [FirestoreData]
    public class PaqueteServicio
    {
        /// <summary>
        /// Identificador único del paquete en Firestore.
        /// Se genera automáticamente al crear un nuevo paquete.
        /// </summary>
        [FirestoreProperty]
        public required string Id { get; set; }

        /// <summary>
        /// Nombre descriptivo del paquete de servicios.
        /// Solo permite letras, acentos y espacios.
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El nombre del paquete es obligatorio")]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$", ErrorMessage = "El nombre solo puede contener letras y espacios")]
        public required string Nombre { get; set; }

        /// <summary>
        /// Estado actual del paquete.
        /// Valores: "Activo", "Inactivo".
        /// Solo paquetes activos están disponibles para seleccionar.
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El estado es obligatorio")]
        public required string Estado { get; set; }

        /// <summary>
        /// Precio total del paquete después de aplicar el descuento.
        /// Calculado como: suma de precios de servicios - (suma * porcentaje descuento / 100)
        /// </summary>
        [FirestoreProperty]
        [Range(0, double.MaxValue, ErrorMessage = "El precio debe ser igual o mayor a 0")]
        public decimal Precio { get; set; }

        /// <summary>
        /// Porcentaje de descuento aplicado al paquete (0-100).
        /// Se aplica sobre la suma de los precios de los servicios individuales.
        /// </summary>
        [FirestoreProperty]
        [Range(0, 100, ErrorMessage = "El porcentaje de descuento debe estar entre 0 y 100")]
        public decimal PorcentajeDescuento { get; set; }

        /// <summary>
        /// Tiempo estimado total del paquete en minutos.
        /// Es la suma de los tiempos estimados de cada servicio incluido.
        /// Este campo es calculado y no es editable manualmente.
        /// </summary>
        [FirestoreProperty]
        [Range(1, int.MaxValue, ErrorMessage = "El tiempo estimado debe ser mayor a 0")]
        public int TiempoEstimado { get; set; }

        /// <summary>
        /// Tipo de vehículo para el cual está destinado el paquete.
        /// Todos los servicios del paquete deben ser para este tipo de vehículo.
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El tipo de vehículo es obligatorio")]
        public required string TipoVehiculo { get; set; }

        /// <summary>
        /// Lista de IDs de los servicios incluidos en el paquete.
        /// Debe contener al menos 2 servicios.
        /// Solo puede haber un servicio de cada tipo de servicio.
        /// </summary>
        [FirestoreProperty]
        public List<string> ServiciosIds { get; set; } = new List<string>();
    }
}

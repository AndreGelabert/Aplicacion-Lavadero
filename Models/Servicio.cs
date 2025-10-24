using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Google.Cloud.Firestore;

namespace Firebase.Models
{
    /// <summary>
    /// Modelo principal que representa un servicio de lavadero en el sistema.
    /// Contiene toda la información necesaria para gestionar los servicios ofrecidos.
    /// </summary>
    /// <remarks>
    /// Este modelo se almacena en Firestore en la colección "servicios".
    /// Se utiliza extensivamente en:
    /// - ServicioService para operaciones CRUD
    /// - ServicioController para manejar formularios y validaciones
    /// - Vistas de servicios para mostrar y editar información
    /// - Sistema de filtrado y paginación
    /// 
    /// Cada servicio tiene validaciones específicas para garantizar la integridad de los datos.
    /// El servicio está asociado a tipos específicos de servicio y vehículo.
    /// </remarks>
    [FirestoreData]
    public class Servicio
    {
        /// <summary>
        /// Identificador único del servicio en Firestore.
        /// Se genera automáticamente al crear un nuevo servicio.
        /// </summary>
        [FirestoreProperty]
        public required string Id { get; set; }

        /// <summary>
        /// Nombre descriptivo del servicio.
        /// Solo permite letras, acentos y espacios. No permite números ni caracteres especiales.
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El nombre del servicio es obligatorio")]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$", ErrorMessage = "El nombre solo puede contener letras y espacios")]
        public required string Nombre { get; set; }

        /// <summary>
        /// Precio del servicio en la moneda local.
        /// Debe ser mayor o igual a 0. Se permite precio gratuito (0).
        /// </summary>
        [FirestoreProperty]
        [Range(0, double.MaxValue, ErrorMessage = "El precio debe ser igual o mayor a 0")]
        public decimal Precio { get; set; }

        /// <summary>
        /// Tipo de servicio al que pertenece.
        /// Debe corresponder a un tipo existente en la colección "tipos_servicio".
        /// Ejemplos: "Lavado Básico", "Lavado Premium", "Encerado".
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El tipo de servicio es obligatorio")]
        public required string Tipo { get; set; }

        /// <summary>
        /// Tipo de vehículo para el cual está destinado el servicio.
        /// Debe corresponder a un tipo existente en la colección "tipos_vehiculo".
        /// Ejemplos: "Automóvil", "SUV", "Camioneta", "Motocicleta".
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El tipo de vehículo es obligatorio")]
        public required string TipoVehiculo { get; set; }

        /// <summary>
        /// Tiempo estimado para completar el servicio en minutos.
        /// Debe ser mayor a 0. Se utiliza para programación y estimaciones.
        /// </summary>
        [FirestoreProperty]
        [Range(1, int.MaxValue, ErrorMessage = "El tiempo estimado debe ser mayor a 0")]
        public int TiempoEstimado { get; set; }

        /// <summary>
        /// Descripción detallada del servicio.
        /// Contiene información adicional sobre lo que incluye el servicio.
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "La descripción es obligatoria")]
        public required string Descripcion { get; set; }

        /// <summary>
        /// Estado actual del servicio.
        /// Valores comunes: "Activo", "Inactivo".
        /// Solo servicios "Activo" están disponibles para reservar.
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El estado es obligatorio")]
        public required string Estado { get; set; }

        /// <summary>
        /// Lista de etapas que componen el servicio.
        /// Un servicio puede tener cero, una o muchas etapas.
        /// Las etapas permiten dividir el servicio en pasos específicos
        /// que pueden ser completados de manera independiente.
        /// </summary>
        [FirestoreProperty]
        public List<Etapa> Etapas { get; set; } = new List<Etapa>();
    }
}

using System.ComponentModel.DataAnnotations;
using Google.Cloud.Firestore;
using Firebase.Converters;

namespace Firebase.Models
{
    /// <summary>
    /// Modelo que representa un lavado/realización de servicio en el sistema.
    /// </summary>
    [FirestoreData]
    public class Lavado
    {
        /// <summary>
        /// Estados posibles para el retiro del vehículo.
        /// </summary>
        public static class EstadosRetiro
        {
            public const string Pendiente = "Pendiente";
            public const string Retirado = "Retirado";
        }

        /// <summary>
        /// Identificador único del lavado.
        /// </summary>
        [FirestoreProperty]
        public required string Id { get; set; }

        /// <summary>
        /// Estado del lavado: Pendiente, EnProceso, Realizado, RealizadoParcialmente, Cancelado.
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El estado es obligatorio")]
        public required string Estado { get; set; }

        /// <summary>
        /// ID del cliente asociado al lavado.
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El cliente es obligatorio")]
        public required string ClienteId { get; set; }

        /// <summary>
        /// Nombre completo del cliente (para visualización).
        /// </summary>
        [FirestoreProperty]
        public string? ClienteNombre { get; set; }

        /// <summary>
        /// ID del vehículo a lavar.
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El vehículo es obligatorio")]
        public required string VehiculoId { get; set; }

        /// <summary>
        /// Patente del vehículo (para visualización).
        /// </summary>
        [FirestoreProperty]
        public string? VehiculoPatente { get; set; }

        /// <summary>
        /// Tipo del vehículo (para visualización y validación).
        /// </summary>
        [FirestoreProperty]
        public string? TipoVehiculo { get; set; }

        /// <summary>
        /// Lista de servicios a realizar con su estado y orden.
        /// </summary>
        [FirestoreProperty]
        public List<ServicioEnLavado> Servicios { get; set; } = new List<ServicioEnLavado>();

        /// <summary>
        /// Precio total del lavado (suma de servicios menos descuento).
        /// </summary>
        [FirestoreProperty(ConverterType = typeof(DecimalConverter))]
        [Range(0, double.MaxValue, ErrorMessage = "El precio debe ser igual o mayor a 0")]
        public decimal Precio { get; set; }

        /// <summary>
        /// Precio original sin descuento.
        /// </summary>
        [FirestoreProperty(ConverterType = typeof(DecimalConverter))]
        public decimal PrecioOriginal { get; set; }

        /// <summary>
        /// Porcentaje de descuento aplicado (0-100).
        /// </summary>
        [FirestoreProperty(ConverterType = typeof(DecimalConverter))]
        [Range(0, 100, ErrorMessage = "El descuento debe estar entre 0 y 100")]
        public decimal Descuento { get; set; }

        /// <summary>
        /// Información del pago.
        /// </summary>
        [FirestoreProperty]
        public PagoLavado? Pago { get; set; }

        /// <summary>
        /// Cantidad de empleados requeridos para el lavado.
        /// </summary>
        [FirestoreProperty]
        [Range(1, 10, ErrorMessage = "La cantidad de empleados debe estar entre 1 y 10")]
        public int CantidadEmpleadosRequeridos { get; set; } = 1;

        /// <summary>
        /// Lista de IDs de empleados asignados al lavado.
        /// </summary>
        [FirestoreProperty]
        public List<string> EmpleadosAsignadosIds { get; set; } = new List<string>();

        /// <summary>
        /// Nombres de empleados asignados (para visualización).
        /// </summary>
        [FirestoreProperty]
        public List<string> EmpleadosAsignadosNombres { get; set; } = new List<string>();

        /// <summary>
        /// Tiempo estimado total en minutos (suma de servicios).
        /// </summary>
        [FirestoreProperty]
        public int TiempoEstimado { get; set; }

        /// <summary>
        /// Fecha y hora de inicio del lavado.
        /// </summary>
        [FirestoreProperty]
        public DateTime? TiempoInicio { get; set; }

        /// <summary>
        /// Fecha y hora de finalización del lavado.
        /// </summary>
        [FirestoreProperty]
        public DateTime? TiempoFinalizacion { get; set; }

        /// <summary>
        /// Fecha de creación del registro.
        /// </summary>
        [FirestoreProperty]
        public DateTime FechaCreacion { get; set; }

        /// <summary>
        /// Motivo de cancelación (obligatorio si estado es Cancelado).
        /// </summary>
        [FirestoreProperty]
        public string? MotivoCancelacion { get; set; }

        /// <summary>
        /// Notas adicionales sobre el lavado.
        /// </summary>
        [FirestoreProperty]
        public string? Notas { get; set; }

        /// <summary>
        /// Indica si se ha notificado al empleado sobre el tiempo límite.
        /// </summary>
        [FirestoreProperty]
        public bool NotificacionTiempoEnviada { get; set; }

        /// <summary>
        /// Número de veces que se ha preguntado si ya terminó (cada 5 min de tolerancia).
        /// </summary>
        [FirestoreProperty]
        public int PreguntasFinalizacion { get; set; }

        /// <summary>
        /// Estado del retiro del vehículo: Pendiente, Retirado.
        /// </summary>
        [FirestoreProperty]
        public string EstadoRetiro { get; set; } = EstadosRetiro.Pendiente;

        /// <summary>
        /// ID del cliente que trajo el vehículo al lavadero.
        /// </summary>
        [FirestoreProperty]
        public string? ClienteTrajoId { get; set; }

        /// <summary>
        /// Nombre del cliente que trajo el vehículo (para visualización).
        /// </summary>
        /// </summary>
        [FirestoreProperty]
        public string? ClienteTrajoNombre { get; set; }

        /// <summary>
        /// ID del cliente encargado de retirar el vehículo.
        /// </summary>
        [FirestoreProperty]
        public string? ClienteRetiraId { get; set; }

        /// <summary>
        /// Nombre del cliente encargado de retirar el vehículo (para visualización).
        /// </summary>
        [FirestoreProperty]
        public string? ClienteRetiraNombre { get; set; }

        /// <summary>
        /// Fecha y hora en que el vehículo fue retirado.
        /// </summary>
        [FirestoreProperty]
        public DateTime? FechaRetiro { get; set; }
    }

    /// <summary>
    /// Modelo que representa un servicio dentro de un lavado.
    /// </summary>
    [FirestoreData]
    public class ServicioEnLavado
    {
        /// <summary>
        /// ID del servicio.
        /// </summary>
        [FirestoreProperty]
        public required string ServicioId { get; set; }

        /// <summary>
        /// Nombre del servicio (para visualización).
        /// </summary>
        [FirestoreProperty]
        public string? ServicioNombre { get; set; }

        /// <summary>
        /// Tipo de servicio.
        /// </summary>
        [FirestoreProperty]
        public string? TipoServicio { get; set; }

        /// <summary>
        /// Precio del servicio.
        /// </summary>
        [FirestoreProperty(ConverterType = typeof(DecimalConverter))]
        public decimal Precio { get; set; }

        /// <summary>
        /// Tiempo estimado del servicio en minutos.
        /// </summary>
        [FirestoreProperty]
        public int TiempoEstimado { get; set; }

        /// <summary>
        /// Estado del servicio: Pendiente, EnProceso, Realizado, Cancelado.
        /// </summary>
        [FirestoreProperty]
        public string Estado { get; set; } = "Pendiente";

        /// <summary>
        /// Orden de ejecución del servicio.
        /// </summary>
        [FirestoreProperty]
        public int Orden { get; set; }

        /// <summary>
        /// Fecha y hora de inicio del servicio.
        /// </summary>
        [FirestoreProperty]
        public DateTime? TiempoInicio { get; set; }

        /// <summary>
        /// Fecha y hora de finalización del servicio.
        /// </summary>
        [FirestoreProperty]
        public DateTime? TiempoFinalizacion { get; set; }

        /// <summary>
        /// Motivo de cancelación del servicio.
        /// </summary>
        [FirestoreProperty]
        public string? MotivoCancelacion { get; set; }

        /// <summary>
        /// Lista de etapas del servicio (si tiene múltiples etapas).
        /// </summary>
        [FirestoreProperty]
        public List<EtapaEnLavado> Etapas { get; set; } = new List<EtapaEnLavado>();

        /// <summary>
        /// ID del paquete de servicio al que pertenece (si aplica).
        /// </summary>
        [FirestoreProperty]
        public string? PaqueteId { get; set; }

        /// <summary>
        /// Nombre del paquete de servicio (si aplica).
        /// </summary>
        [FirestoreProperty]
        public string? PaqueteNombre { get; set; }
    }

    /// <summary>
    /// Modelo que representa una etapa dentro de un servicio en un lavado.
    /// </summary>
    [FirestoreData]
    public class EtapaEnLavado
    {
        /// <summary>
        /// ID de la etapa.
        /// </summary>
        [FirestoreProperty]
        public required string EtapaId { get; set; }

        /// <summary>
        /// Nombre de la etapa.
        /// </summary>
        [FirestoreProperty]
        public string? Nombre { get; set; }

        /// <summary>
        /// Estado de la etapa: Pendiente, EnProceso, Realizado, Cancelado.
        /// </summary>
        [FirestoreProperty]
        public string Estado { get; set; } = "Pendiente";

        /// <summary>
        /// Fecha y hora de inicio de la etapa.
        /// </summary>
        [FirestoreProperty]
        public DateTime? TiempoInicio { get; set; }

        /// <summary>
        /// Fecha y hora de finalización de la etapa.
        /// </summary>
        [FirestoreProperty]
        public DateTime? TiempoFinalizacion { get; set; }

        /// <summary>
        /// Motivo de cancelación de la etapa.
        /// </summary>
        [FirestoreProperty]
        public string? MotivoCancelacion { get; set; }
    }

    /// <summary>
    /// Modelo que representa el pago de un lavado.
    /// </summary>
    [FirestoreData]
    public class PagoLavado
    {
        /// <summary>
        /// Estado del pago: Pendiente, Parcial, Pagado.
        /// </summary>
        [FirestoreProperty]
        public string Estado { get; set; } = "Pendiente";

        /// <summary>
        /// Monto total pagado.
        /// </summary>
        [FirestoreProperty(ConverterType = typeof(DecimalConverter))]
        public decimal MontoPagado { get; set; }

        /// <summary>
        /// Lista de pagos realizados.
        /// </summary>
        [FirestoreProperty]
        public List<DetallePago> Pagos { get; set; } = new List<DetallePago>();
    }

    /// <summary>
    /// Modelo que representa un pago individual.
    /// </summary>
    [FirestoreData]
    public class DetallePago
    {
        /// <summary>
        /// ID del pago.
        /// </summary>
        [FirestoreProperty]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Monto del pago.
        /// </summary>
        [FirestoreProperty(ConverterType = typeof(DecimalConverter))]
        public decimal Monto { get; set; }

        /// <summary>
        /// Medio de pago: Efectivo, TarjetaDebito, TarjetaCredito, Transferencia, MercadoPago, etc.
        /// </summary>
        [FirestoreProperty]
        public string MedioPago { get; set; } = "Efectivo";

        /// <summary>
        /// Fecha del pago.
        /// </summary>
        [FirestoreProperty]
        public DateTime Fecha { get; set; }

        /// <summary>
        /// Notas del pago.
        /// </summary>
        [FirestoreProperty]
        public string? Notas { get; set; }
    }
}

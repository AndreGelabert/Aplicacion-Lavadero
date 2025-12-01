using Google.Cloud.Firestore;
using Firebase.Converters;
using System.ComponentModel.DataAnnotations;

namespace Firebase.Models
{
    /// <summary>
    /// Modelo que representa la configuración del sistema de lavadero.
    /// Solo debe existir un documento de configuración en Firestore.
    /// </summary>
    [FirestoreData]
    public class Configuracion
    {
        #region Configuración de Descuentos por Cancelación Anticipada
        /// <summary>
        /// Identificador único de la configuración. Usualmente "system_config".
        /// </summary>
        [FirestoreProperty]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Porcentaje o monto de descuento que recibe el usuario por cancelar anticipadamente.
        /// Ejemplo: 10 representa 10% de descuento.
        /// </summary>
        [FirestoreProperty(ConverterType = typeof(DecimalConverter))]
        [Range(0, 100, ErrorMessage = "El porcentaje de descuento debe estar entre 0 y 100")]
        public decimal CancelacionAnticipadaDescuento { get; set; }

        /// <summary>
        /// Tiempo mínimo (en horas) antes de la cita para que se aplique el descuento por cancelación.
        /// Ejemplo: 24 significa que debe cancelar al menos 24 horas antes.
        /// </summary>
        [FirestoreProperty]
        [Range(1, 168, ErrorMessage = "El tiempo mínimo debe estar entre 1 y 168 horas (7 días)")]
        public int CancelacionAnticipadaHorasMinimas { get; set; } = 24;

        /// <summary>
        /// Duración (en días) durante la cual el descuento es válido después de la cancelación.
        /// Ejemplo: 30 significa que el descuento es válido por 30 días.
        /// </summary>
        [FirestoreProperty]
        [Range(1, 365, ErrorMessage = "La duración debe estar entre 1 y 365 días")]
        public int CancelacionAnticipadaValidezDias { get; set; } = 30;

        #endregion

        #region Configuración de Pasos de Descuento

        /// <summary>
        /// Paso/incremento para el campo de descuento de paquetes de servicios.
        /// Ejemplo: 5 significa que los descuentos se pueden configurar en múltiplos de 5% (5%, 10%, 15%, etc.)
        /// </summary>
        [FirestoreProperty]
        [Range(1, 50, ErrorMessage = "El paso de descuento debe estar entre 1 y 50")]
        public int PaquetesDescuentoStep { get; set; } = 5;

        #endregion

        #region Información del Lavadero

        /// <summary>
        /// Nombre del lavadero para mostrar en WhatsApp y otros lugares
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El nombre del lavadero es obligatorio")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 100 caracteres")]
        public string NombreLavadero { get; set; } = "Lavadero AutoClean";

        /// <summary>
        /// Ubicación/Dirección del lavadero
        /// </summary>
        [FirestoreProperty]
        [StringLength(200, ErrorMessage = "La ubicación no puede exceder 200 caracteres")]
        public string Ubicacion { get; set; } = "";

        #endregion

        #region Configuración de Horarios de Operación

        /// <summary>
        /// Horarios de operación del lavadero por día de la semana.
        /// La clave es el día de la semana (Lunes, Martes, etc.) y el valor es el horario.
        /// Formato del horario: "09:00-18:00" para horario continuo, "09:00-13:00,15:00-19:00" para horario dividido,
        /// o "CERRADO" si no opera ese día.
        /// </summary>
        [FirestoreProperty]
        public Dictionary<string, string> HorariosOperacion { get; set; } = new Dictionary<string, string>
        {
            { "Lunes", "09:00-18:00" },
            { "Martes", "09:00-18:00" },
            { "Miércoles", "09:00-18:00" },
            { "Jueves", "09:00-18:00" },
            { "Viernes", "09:00-18:00" },
            { "Sábado", "09:00-13:00" },
            { "Domingo", "CERRADO" }
        };

        #endregion

        #region Configuración de Capacidad

        /// <summary>
        /// Número máximo de lavados/citas que se pueden aceptar simultáneamente.
        /// Este es un límite global inicial. En el futuro se podría expandir para considerar
        /// tipos de vehículos, tipos de servicio, y número de empleados.
        /// </summary>
        [FirestoreProperty]
        [Range(1, 100, ErrorMessage = "La capacidad debe estar entre 1 y 100")]
        public int CapacidadMaximaConcurrente { get; set; } = 5;

        /// <summary>
        /// Indica si se debe considerar el número de empleados activos al calcular la capacidad.
        /// Si es true, la capacidad efectiva será el mínimo entre CapacidadMaximaConcurrente 
        /// y el número de empleados activos.
        /// </summary>
        [FirestoreProperty]
        public bool ConsiderarEmpleadosActivos { get; set; } = true;

        /// <summary>
        /// Número máximo de empleados que se pueden asignar a un solo lavado.
        /// Esto limita la cantidad de empleados que un lavado individual puede requerir.
        /// </summary>
        [FirestoreProperty]
        [Range(1, 20, ErrorMessage = "La cantidad máxima de empleados por lavado debe estar entre 1 y 20")]
        public int EmpleadosMaximosPorLavado { get; set; } = 3;

        #endregion

        #region Configuración de Tiempos de Lavado

        /// <summary>
        /// Minutos de anticipación para notificar al empleado antes de que se cumpla el tiempo estimado.
        /// Ejemplo: 15 significa que se notificará cuando falten 15 minutos.
        /// </summary>
        [FirestoreProperty]
        [Range(1, 60, ErrorMessage = "El tiempo de notificación debe estar entre 1 y 60 minutos")]
        public int TiempoNotificacionMinutos { get; set; } = 15;

        /// <summary>
        /// Minutos de tolerancia máxima que se puede extender un lavado después del tiempo estimado.
        /// Ejemplo: 15 significa que puede excederse hasta 15 minutos del tiempo estimado.
        /// </summary>
        [FirestoreProperty]
        [Range(5, 60, ErrorMessage = "El tiempo de tolerancia debe estar entre 5 y 60 minutos")]
        public int TiempoToleranciaMinutos { get; set; } = 15;

        /// <summary>
        /// Intervalo en minutos para preguntar si ya terminó el servicio cuando se excede el tiempo.
        /// Ejemplo: 5 significa que se preguntará cada 5 minutos.
        /// </summary>
        [FirestoreProperty]
        [Range(1, 15, ErrorMessage = "El intervalo de preguntas debe estar entre 1 y 15 minutos")]
        public int IntervaloPreguntas { get; set; } = 5;

        #endregion

        #region Configuración de Sesiones

        /// <summary>
        /// Duración máxima de la sesión en horas.
        /// La sesión se cerrará automáticamente al alcanzar este tiempo, incluso si el usuario está activo.
        /// Ejemplo: 8 horas
        /// </summary>
        [FirestoreProperty]
        [Range(1, 24, ErrorMessage = "La duración de sesión debe estar entre 1 y 24 horas")]
        public int SesionDuracionHoras { get; set; } = 8; // 8 horas por defecto

        /// <summary>
        /// Tiempo de inactividad en minutos antes de cerrar la sesión automáticamente.
        /// Si el usuario no interactúa con la aplicación durante este tiempo, la sesión se cerrará.
        /// Ejemplo: 15 minutos
        /// </summary>
        [FirestoreProperty]
        [Range(5, 120, ErrorMessage = "El tiempo de inactividad debe estar entre 5 y 120 minutos")]
        public int SesionInactividadMinutos { get; set; } = 15; // 15 minutos por defecto

        #endregion

        #region Metadata

        /// <summary>
        /// Fecha de última actualización de la configuración.
        /// </summary>
        [FirestoreProperty]
        public DateTime? FechaActualizacion { get; set; }

        /// <summary>
        /// Email del usuario que actualizó la configuración por última vez.
        /// </summary>
        [FirestoreProperty]
        public string? ActualizadoPor { get; set; }

        #endregion
    }
}

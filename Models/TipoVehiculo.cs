using Google.Cloud.Firestore;

namespace Firebase.Models
{
    /// <summary>
    /// Modelo que representa un tipo de vehículo en el sistema de lavadero.
    /// Se utiliza para categorizar los vehículos según su tamaño y características.
    /// </summary>
    /// <remarks>
    /// Este modelo se almacena en Firestore en la colección "tipos_vehiculo".
    /// Se utiliza en:
    /// - TipoVehiculoService para operaciones CRUD
    /// - ServicioController para llenar dropdowns y validaciones
    /// - Vistas de servicios para mostrar opciones disponibles
    /// - Cálculo de precios diferenciados según el tipo de vehículo
    /// 
    /// Ejemplos de tipos de vehículo: "Automóvil", "SUV", "Camioneta", "Motocicleta", etc.
    /// </remarks>
    [FirestoreData]
    public class TipoVehiculo
    {
        /// <summary>
        /// Identificador único del tipo de vehículo en Firestore.
        /// Se genera automáticamente al crear un nuevo tipo de vehículo.
        /// </summary>
        [FirestoreProperty]
        public required string Id { get; set; }

        /// <summary>
        /// Nombre descriptivo del tipo de vehículo.
        /// Este nombre se muestra en las interfaces de usuario y formularios.
        /// Debe ser único y descriptivo (ej: "Automóvil", "SUV", "Camioneta").
        /// </summary>
        [FirestoreProperty]
        public required string Nombre { get; set; }
    }
}

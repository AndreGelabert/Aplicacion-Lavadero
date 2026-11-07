namespace FirebaseLoginCustom.Controllers
{
    /// <summary>Modelo para la solicitud de creación de lavado.</summary>
    public class CrearLavadoRequest
    {
        public string ClienteId { get; set; } = string.Empty;
        public List<VehiculoServiciosRequest> VehiculosServicios { get; set; } = new List<VehiculoServiciosRequest>();
        public decimal Descuento { get; set; }
        public string? Notas { get; set; }
        /// <summary>ID del cliente que trajo el vehículo al lavadero. Si no se especifica, se usa el ClienteId.</summary>
        public string? ClienteTrajoId { get; set; }
        /// <summary>ID del cliente encargado de retirar el vehículo. Si no se especifica, se usa el ClienteId.</summary>
        public string? ClienteRetiraId { get; set; }
    }

    /// <summary>Modelo para los servicios de un vehículo.</summary>
    public class VehiculoServiciosRequest
    {
        public string VehiculoId { get; set; } = string.Empty;
        public List<ServicioItemRequest> Servicios { get; set; } = new List<ServicioItemRequest>();
    }

    /// <summary>Modelo para un servicio individual.</summary>
    public class ServicioItemRequest
    {
        public string ServicioId { get; set; } = string.Empty;
        public string? PaqueteId { get; set; }
        public string? PaqueteNombre { get; set; }
    }
}

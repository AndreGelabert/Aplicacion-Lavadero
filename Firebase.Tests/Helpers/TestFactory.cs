using Firebase.Models;

namespace Firebase.Tests.Helpers
{
    /// <summary>
    /// Métodos de fábrica para crear instancias de modelos en tests.
    /// 
    /// Esta clase proporciona métodos helper para crear objetos con todas las propiedades
    /// requeridas, facilitando la escritura de tests unitarios siguiendo el patrón AAA.
    /// 
    /// Patrones utilizados:
    /// - Factory Pattern: Para crear instancias con configuración por defecto
    /// - Builder Pattern: Parámetros opcionales permiten personalizar cada instancia
    /// </summary>
    public static class TestFactory
    {
        #region Empleado Factory

        /// <summary>
        /// Crea una instancia de Empleado para testing.
        /// </summary>
        /// <param name="id">ID del empleado (default: "emp-001")</param>
        /// <param name="nombre">Nombre completo (default: "Test User")</param>
        /// <param name="email">Email (default: "test@example.com")</param>
        /// <param name="rol">Rol (default: "Empleado")</param>
        /// <param name="estado">Estado (default: "Activo")</param>
        /// <returns>Instancia de Empleado configurada</returns>
        public static Empleado CreateEmpleado(
            string id = "emp-001",
            string nombre = "Test User",
            string email = "test@example.com",
            string rol = "Empleado",
            string estado = "Activo")
        {
            return new Empleado
            {
                Id = id,
                NombreCompleto = nombre,
                Email = email,
                Rol = rol,
                Estado = estado
            };
        }

        #endregion

        #region Cliente Factory

        /// <summary>
        /// Crea una instancia de Cliente para testing.
        /// </summary>
        /// <param name="id">ID del cliente (default: "cli-001")</param>
        /// <param name="nombre">Nombre (default: "Juan")</param>
        /// <param name="apellido">Apellido (default: "Pérez")</param>
        /// <param name="tipoDocumento">Tipo de documento (default: "DNI")</param>
        /// <param name="numeroDocumento">Número de documento (default: "12345678")</param>
        /// <param name="telefono">Teléfono (default: "3751590586")</param>
        /// <param name="email">Email (default: "juan@test.com")</param>
        /// <param name="estado">Estado (default: "Activo")</param>
        /// <returns>Instancia de Cliente configurada</returns>
        public static Cliente CreateCliente(
            string id = "cli-001",
            string nombre = "Juan",
            string apellido = "Pérez",
            string tipoDocumento = "DNI",
            string numeroDocumento = "12345678",
            string telefono = "3751590586",
            string email = "juan@test.com",
            string estado = "Activo")
        {
            return new Cliente
            {
                Id = id,
                Nombre = nombre,
                Apellido = apellido,
                TipoDocumento = tipoDocumento,
                NumeroDocumento = numeroDocumento,
                Telefono = telefono,
                Email = email,
                Estado = estado,
                VehiculosIds = new List<string>()
            };
        }

        #endregion

        #region Vehiculo Factory

        /// <summary>
        /// Crea una instancia de Vehiculo para testing.
        /// </summary>
        /// <param name="id">ID del vehículo (default: "veh-001")</param>
        /// <param name="patente">Patente (default: "ABC123")</param>
        /// <param name="tipoVehiculo">Tipo de vehículo (default: "Automóvil")</param>
        /// <param name="marca">Marca (default: "Toyota")</param>
        /// <param name="modelo">Modelo (default: "Corolla")</param>
        /// <param name="color">Color (default: "Blanco")</param>
        /// <param name="clienteId">ID del cliente dueño (default: "")</param>
        /// <param name="estado">Estado (default: "Activo")</param>
        /// <returns>Instancia de Vehiculo configurada</returns>
        public static Vehiculo CreateVehiculo(
            string id = "veh-001",
            string patente = "ABC123",
            string tipoVehiculo = "Automóvil",
            string marca = "Toyota",
            string modelo = "Corolla",
            string color = "Blanco",
            string clienteId = "",
            string estado = "Activo")
        {
            return new Vehiculo
            {
                Id = id,
                Patente = patente,
                TipoVehiculo = tipoVehiculo,
                Marca = marca,
                Modelo = modelo,
                Color = color,
                ClienteId = clienteId,
                Estado = estado,
                ClientesIds = new List<string>()
            };
        }

        #endregion

        #region Lavado Factory

        /// <summary>
        /// Crea una instancia de Lavado para testing.
        /// </summary>
        /// <param name="id">ID del lavado (default: "lav-001")</param>
        /// <param name="clienteId">ID del cliente (default: "cli-001")</param>
        /// <param name="clienteNombre">Nombre del cliente (default: "Juan Pérez")</param>
        /// <param name="vehiculoId">ID del vehículo (default: "veh-001")</param>
        /// <param name="vehiculoPatente">Patente del vehículo (default: "ABC123")</param>
        /// <param name="estado">Estado del lavado (default: "EnProceso")</param>
        /// <param name="precio">Precio total (default: 10000)</param>
        /// <param name="precioOriginal">Precio original sin descuento (default: 10000)</param>
        /// <param name="descuento">Porcentaje de descuento (default: 0)</param>
        /// <param name="fechaCreacion">Fecha de creación (default: DateTime.UtcNow)</param>
        /// <returns>Instancia de Lavado configurada</returns>
        public static Lavado CreateLavado(
            string id = "lav-001",
            string clienteId = "cli-001",
            string clienteNombre = "Juan Pérez",
            string vehiculoId = "veh-001",
            string vehiculoPatente = "ABC123",
            string estado = "EnProceso",
            decimal precio = 10000,
            decimal precioOriginal = 10000,
            decimal descuento = 0,
            DateTime? fechaCreacion = null)
        {
            return new Lavado
            {
                Id = id,
                ClienteId = clienteId,
                ClienteNombre = clienteNombre,
                VehiculoId = vehiculoId,
                VehiculoPatente = vehiculoPatente,
                Estado = estado,
                Precio = precio,
                PrecioOriginal = precioOriginal,
                Descuento = descuento,
                FechaCreacion = fechaCreacion ?? DateTime.UtcNow,
                CantidadEmpleadosRequeridos = 1,
                Servicios = new List<ServicioEnLavado>(),
                EmpleadosAsignadosIds = new List<string>(),
                EmpleadosAsignadosNombres = new List<string>(),
                EstadoRetiro = Lavado.EstadosRetiro.Pendiente
            };
        }

        /// <summary>
        /// Crea un servicio dentro de un lavado para testing.
        /// </summary>
        /// <param name="servicioId">ID del servicio (default: "serv-001")</param>
        /// <param name="servicioNombre">Nombre del servicio (default: "Lavado Básico")</param>
        /// <param name="tipoServicio">Tipo de servicio (default: "Lavado")</param>
        /// <param name="precio">Precio del servicio (default: 5000)</param>
        /// <param name="tiempoEstimado">Tiempo estimado en minutos (default: 30)</param>
        /// <param name="estado">Estado del servicio (default: "Pendiente")</param>
        /// <param name="orden">Orden de ejecución (default: 1)</param>
        /// <returns>Instancia de ServicioEnLavado configurada</returns>
        public static ServicioEnLavado CreateServicioEnLavado(
            string servicioId = "serv-001",
            string servicioNombre = "Lavado Básico",
            string tipoServicio = "Lavado",
            decimal precio = 5000,
            int tiempoEstimado = 30,
            string estado = "Pendiente",
            int orden = 1)
        {
            return new ServicioEnLavado
            {
                ServicioId = servicioId,
                ServicioNombre = servicioNombre,
                TipoServicio = tipoServicio,
                Precio = precio,
                TiempoEstimado = tiempoEstimado,
                Estado = estado,
                Orden = orden,
                Etapas = new List<EtapaEnLavado>()
            };
        }

        #endregion

        #region Servicio Factory

        /// <summary>
        /// Crea una instancia de Servicio para testing.
        /// </summary>
        /// <param name="id">ID del servicio (default: "serv-001")</param>
        /// <param name="nombre">Nombre del servicio (default: "Lavado Basico")</param>
        /// <param name="precio">Precio (default: 10000)</param>
        /// <param name="tipo">Tipo de servicio (default: "Lavado")</param>
        /// <param name="tipoVehiculo">Tipo de vehículo (default: "Automovil")</param>
        /// <param name="tiempoEstimado">Tiempo estimado en minutos (default: 30)</param>
        /// <param name="descripcion">Descripción (default: "Descripcion del servicio")</param>
        /// <param name="estado">Estado (default: "Activo")</param>
        /// <param name="etapas">Lista de etapas (default: lista vacía)</param>
        /// <returns>Instancia de Servicio configurada</returns>
        public static Servicio CreateServicio(
            string id = "serv-001",
            string nombre = "Lavado Basico",
            decimal precio = 10000,
            string tipo = "Lavado",
            string tipoVehiculo = "Automovil",
            int tiempoEstimado = 30,
            string descripcion = "Descripcion del servicio",
            string estado = "Activo",
            List<Etapa>? etapas = null)
        {
            return new Servicio
            {
                Id = id,
                Nombre = nombre,
                Precio = precio,
                Tipo = tipo,
                TipoVehiculo = tipoVehiculo,
                TiempoEstimado = tiempoEstimado,
                Descripcion = descripcion,
                Estado = estado,
                Etapas = etapas ?? new List<Etapa>()
            };
        }

        #endregion

        #region PaqueteServicio Factory

        /// <summary>
        /// Crea una instancia de PaqueteServicio para testing.
        /// </summary>
        /// <param name="id">ID del paquete (default: "pkg-001")</param>
        /// <param name="nombre">Nombre del paquete (default: "Paquete Basico")</param>
        /// <param name="estado">Estado (default: "Activo")</param>
        /// <param name="porcentajeDescuento">Porcentaje de descuento (default: 10)</param>
        /// <param name="tipoVehiculo">Tipo de vehículo (default: "Automovil")</param>
        /// <param name="serviciosIds">Lista de IDs de servicios (default: lista con 2 servicios)</param>
        /// <returns>Instancia de PaqueteServicio configurada</returns>
        public static PaqueteServicio CreatePaquete(
            string id = "pkg-001",
            string nombre = "Paquete Basico",
            string estado = "Activo",
            decimal porcentajeDescuento = 10,
            string tipoVehiculo = "Automovil",
            List<string>? serviciosIds = null)
        {
            return new PaqueteServicio
            {
                Id = id,
                Nombre = nombre,
                Estado = estado,
                PorcentajeDescuento = porcentajeDescuento,
                TipoVehiculo = tipoVehiculo,
                ServiciosIds = serviciosIds ?? new List<string> { "serv-1", "serv-2" }
            };
        }

        #endregion

        #region Configuracion Factory

        /// <summary>
        /// Crea una instancia de Configuracion para testing.
        /// </summary>
        /// <param name="id">ID de configuración (default: "system_config")</param>
        /// <param name="cancelacionDescuento">Descuento por cancelación anticipada (default: 10)</param>
        /// <param name="cancelacionHoras">Horas mínimas para cancelación anticipada (default: 24)</param>
        /// <param name="cancelacionDias">Días de validez para cancelación (default: 30)</param>
        /// <param name="descuentoStep">Step de descuento para paquetes (default: 5)</param>
        /// <param name="capacidad">Capacidad máxima concurrente (default: 5)</param>
        /// <param name="sesionHoras">Duración de sesión en horas (default: 8)</param>
        /// <param name="sesionInactividad">Minutos de inactividad de sesión (default: 15)</param>
        /// <returns>Instancia de Configuracion configurada</returns>
        public static Configuracion CreateConfiguracion(
            string id = "system_config",
            decimal cancelacionDescuento = 10,
            int cancelacionHoras = 24,
            int cancelacionDias = 30,
            int descuentoStep = 5,
            int capacidad = 5,
            int sesionHoras = 8,
            int sesionInactividad = 15)
        {
            return new Configuracion
            {
                Id = id,
                CancelacionAnticipadaDescuento = cancelacionDescuento,
                CancelacionAnticipadaHorasMinimas = cancelacionHoras,
                CancelacionAnticipadaValidezDias = cancelacionDias,
                PaquetesDescuentoStep = descuentoStep,
                CapacidadMaximaConcurrente = capacidad,
                SesionDuracionHoras = sesionHoras,
                SesionInactividadMinutos = sesionInactividad,
                ConsiderarEmpleadosActivos = true,
                HorariosOperacion = new Dictionary<string, string>
                {
                    { "Lunes", "09:00-18:00" },
                    { "Martes", "09:00-18:00" },
                    { "Miércoles", "09:00-18:00" },
                    { "Jueves", "09:00-18:00" },
                    { "Viernes", "09:00-18:00" },
                    { "Sábado", "09:00-13:00" },
                    { "Domingo", "CERRADO" }
                }
            };
        }

        #endregion

        #region AuditLog Factory

        /// <summary>
        /// Crea una instancia de AuditLog para testing.
        /// </summary>
        /// <param name="userId">ID del usuario (default: "user-001")</param>
        /// <param name="userEmail">Email del usuario (default: "user@test.com")</param>
        /// <param name="action">Acción realizada (default: "Test Action")</param>
        /// <param name="targetId">ID del objetivo (default: "target-001")</param>
        /// <param name="targetType">Tipo de objetivo (default: "TestEntity")</param>
        /// <returns>Instancia de AuditLog configurada</returns>
        public static AuditLog CreateAuditLog(
            string userId = "user-001",
            string userEmail = "user@test.com",
            string action = "Test Action",
            string targetId = "target-001",
            string targetType = "TestEntity")
        {
            return new AuditLog
            {
                UserId = userId,
                UserEmail = userEmail,
                Action = action,
                TargetId = targetId,
                TargetType = targetType,
                Timestamp = DateTime.UtcNow
            };
        }

        #endregion
    }
}

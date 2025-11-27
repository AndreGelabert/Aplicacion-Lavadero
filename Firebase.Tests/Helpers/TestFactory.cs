using Firebase.Models;

namespace Firebase.Tests.Helpers
{
    /// <summary>
    /// Test factory methods to create model instances with all required properties.
    /// </summary>
    public static class TestFactory
    {
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
    }
}

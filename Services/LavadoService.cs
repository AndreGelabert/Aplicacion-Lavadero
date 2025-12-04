using Firebase.Models;
using Google.Cloud.Firestore;

namespace Firebase.Services
{
    /// <summary>
    /// Servicio para la gestión de lavados/realizaciones de servicio en Firestore.
    /// Proporciona operaciones CRUD, filtrado, paginación y validación de lavados.
    /// </summary>
    public class LavadoService
    {
        #region Constantes
        private const string COLLECTION_NAME = "lavados";
        private const string ESTADO_PENDIENTE = "Pendiente";
        private const string ESTADO_EN_PROCESO = "EnProceso";
        private const string ESTADO_REALIZADO = "Realizado";
        private const string ESTADO_REALIZADO_PARCIALMENTE = "RealizadoParcialmente";
        private const string ESTADO_CANCELADO = "Cancelado";
        private const string ORDEN_DEFECTO = "FechaCreacion";
        private const string DIRECCION_DEFECTO = "desc";
        #endregion

        #region Dependencias
        private readonly FirestoreDb _firestore;
        private readonly ServicioService _servicioService;
        private readonly PaqueteServicioService _paqueteServicioService;
        private readonly PersonalService _personalService;
        private readonly ConfiguracionService _configuracionService;

        /// <summary>
        /// Inicializa una nueva instancia del servicio de lavados.
        /// </summary>
        public LavadoService(
            FirestoreDb firestore,
            ServicioService servicioService,
            PaqueteServicioService paqueteServicioService,
            PersonalService personalService,
            ConfiguracionService configuracionService)
        {
            _firestore = firestore ?? throw new ArgumentNullException(nameof(firestore));
            _servicioService = servicioService ?? throw new ArgumentNullException(nameof(servicioService));
            _paqueteServicioService = paqueteServicioService ?? throw new ArgumentNullException(nameof(paqueteServicioService));
            _personalService = personalService ?? throw new ArgumentNullException(nameof(personalService));
            _configuracionService = configuracionService ?? throw new ArgumentNullException(nameof(configuracionService));
        }
        #endregion

        #region Operaciones de Consulta

        /// <summary>
        /// Obtiene una lista paginada de lavados aplicando filtros y ordenamiento.
        /// </summary>
        public async Task<List<Lavado>> ObtenerLavados(
            List<string>? estados = null,
            string? clienteId = null,
            string? vehiculoId = null,
            DateTime? fechaDesde = null,
            DateTime? fechaHasta = null,
            decimal? precioDesde = null,
            decimal? precioHasta = null,
            List<string>? estadosPago = null,
            int pageNumber = 1,
            int pageSize = 10,
            string? sortBy = null,
            string? sortOrder = null)
        {
            ValidarParametrosPaginacion(pageNumber, pageSize);

            sortBy ??= ORDEN_DEFECTO;
            sortOrder ??= DIRECCION_DEFECTO;

            var lavados = await ObtenerLavadosFiltrados(estados, clienteId, vehiculoId, fechaDesde, fechaHasta, precioDesde, precioHasta, estadosPago, sortBy, sortOrder);

            return AplicarPaginacion(lavados, pageNumber, pageSize);
        }

        /// <summary>
        /// Calcula el número total de páginas para los lavados filtrados.
        /// </summary>
        public async Task<int> ObtenerTotalPaginas(
            List<string>? estados,
            string? clienteId,
            string? vehiculoId,
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            decimal? precioDesde,
            decimal? precioHasta,
            List<string>? estadosPago,
            int pageSize)
        {
            if (pageSize <= 0)
                throw new ArgumentException("El tamaño de página debe ser mayor a 0", nameof(pageSize));

            var totalLavados = await ObtenerTotalLavados(estados, clienteId, vehiculoId, fechaDesde, fechaHasta, precioDesde, precioHasta, estadosPago);
            return Math.Max((int)Math.Ceiling(totalLavados / (double)pageSize), 1);
        }

        /// <summary>
        /// Obtiene un lavado específico por su ID.
        /// </summary>
        public async Task<Lavado?> ObtenerLavado(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("El ID del lavado no puede estar vacío", nameof(id));

            var docRef = _firestore.Collection(COLLECTION_NAME).Document(id);
            var snapshot = await docRef.GetSnapshotAsync();

            return snapshot.Exists ? MapearDocumentoALavado(snapshot) : null;
        }

        /// <summary>
        /// Busca lavados por término de búsqueda.
        /// </summary>
        public async Task<List<Lavado>> BuscarLavados(
            string searchTerm,
            List<string>? estados = null,
            string? clienteId = null,
            string? vehiculoId = null,
            DateTime? fechaDesde = null,
            DateTime? fechaHasta = null,
            decimal? precioDesde = null,
            decimal? precioHasta = null,
            List<string>? estadosPago = null,
            int pageNumber = 1,
            int pageSize = 10,
            string? sortBy = null,
            string? sortOrder = null)
        {
            var baseFiltrada = await ObtenerLavadosFiltrados(estados, clienteId, vehiculoId, fechaDesde, fechaHasta, precioDesde, precioHasta, estadosPago, sortBy, sortOrder);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                baseFiltrada = AplicarBusqueda(baseFiltrada, searchTerm);
            }

            var ordenados = AplicarOrdenamiento(baseFiltrada, sortBy, sortOrder);
            return AplicarPaginacion(ordenados, Math.Max(pageNumber, 1), Math.Max(pageSize, 1));
        }

        /// <summary>
        /// Obtiene el total de lavados que coinciden con la búsqueda.
        /// </summary>
        public async Task<int> ObtenerTotalLavadosBusqueda(
            string searchTerm,
            List<string>? estados,
            string? clienteId,
            string? vehiculoId,
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            decimal? precioDesde,
            decimal? precioHasta,
            List<string>? estadosPago)
        {
            var baseFiltrada = await ObtenerLavadosFiltrados(estados, clienteId, vehiculoId, fechaDesde, fechaHasta, precioDesde, precioHasta, estadosPago, ORDEN_DEFECTO, DIRECCION_DEFECTO);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                baseFiltrada = AplicarBusqueda(baseFiltrada, searchTerm);
            }

            return baseFiltrada.Count;
        }

        /// <summary>
        /// Obtiene los lavados activos (en proceso) de un empleado.
        /// </summary>
        public async Task<List<Lavado>> ObtenerLavadosActivosPorEmpleado(string empleadoId)
        {
            if (string.IsNullOrWhiteSpace(empleadoId))
                return new List<Lavado>();

            var coleccion = _firestore.Collection(COLLECTION_NAME);
            var querySnapshot = await coleccion
                .WhereArrayContains("EmpleadosAsignadosIds", empleadoId)
                .WhereEqualTo("Estado", ESTADO_EN_PROCESO)
                .GetSnapshotAsync();

            return querySnapshot.Documents
                .Select(MapearDocumentoALavado)
                .ToList();
        }

        /// <summary>
        /// Cuenta el número de lavados activos (en proceso).
        /// </summary>
        public async Task<int> ContarLavadosActivos()
        {
            var coleccion = _firestore.Collection(COLLECTION_NAME);
            var querySnapshot = await coleccion
                .WhereEqualTo("Estado", ESTADO_EN_PROCESO)
                .GetSnapshotAsync();

            return querySnapshot.Count;
        }

        #endregion

        #region Operaciones CRUD

        /// <summary>
        /// Crea un nuevo lavado en la base de datos.
        /// </summary>
        public async Task<Lavado> CrearLavado(Lavado lavado)
        {
            if (lavado == null)
                throw new ArgumentNullException(nameof(lavado));

            // Validar horario de operación
            await ValidarHorarioOperacion();

            // Validar capacidad concurrente
            await ValidarCapacidadConcurrente();

            // Validar datos del lavado
            ValidarLavado(lavado);

            // Generar ID si no tiene
            var lavadoRef = _firestore.Collection(COLLECTION_NAME).Document();
            lavado.Id = lavadoRef.Id;

            // Establecer fecha de creación y estado inicial
            lavado.FechaCreacion = DateTime.UtcNow;
            lavado.Estado = ESTADO_EN_PROCESO; // Inicia inmediatamente
            lavado.TiempoInicio = DateTime.UtcNow;

            // Inicializar pago si no existe
            lavado.Pago ??= new PagoLavado();

            // Guardar en Firestore
            var lavadoData = CrearDiccionarioLavado(lavado);
            await lavadoRef.SetAsync(lavadoData);

            return lavado;
        }

        /// <summary>
        /// Actualiza un lavado existente en la base de datos.
        /// </summary>
        public async Task ActualizarLavado(Lavado lavado)
        {
            if (lavado == null)
                throw new ArgumentNullException(nameof(lavado));

            if (string.IsNullOrWhiteSpace(lavado.Id))
                throw new ArgumentException("El ID del lavado es obligatorio para actualizar", nameof(lavado));

            ValidarLavado(lavado);

            var lavadoRef = _firestore.Collection(COLLECTION_NAME).Document(lavado.Id);
            var lavadoData = CrearDiccionarioLavado(lavado);
            await lavadoRef.SetAsync(lavadoData, SetOptions.Overwrite);
        }

        /// <summary>
        /// Finaliza un lavado.
        /// </summary>
        public async Task FinalizarLavado(string id)
        {
            var lavado = await ObtenerLavado(id);
            if (lavado == null)
                throw new ArgumentException("No se encontró el lavado", nameof(id));

            // Verificar si todos los servicios están finalizados
            var serviciosPendientes = lavado.Servicios.Where(s => s.Estado == "Pendiente" || s.Estado == "EnProceso").ToList();
            
            if (serviciosPendientes.Any())
            {
                // Verificar si hay servicios cancelados
                var serviciosCancelados = lavado.Servicios.Where(s => s.Estado == "Cancelado").ToList();
                if (serviciosCancelados.Any() && serviciosCancelados.Count < lavado.Servicios.Count)
                {
                    lavado.Estado = ESTADO_REALIZADO_PARCIALMENTE;
                }
                else
                {
                    throw new InvalidOperationException("No se puede finalizar el lavado porque hay servicios pendientes o en proceso.");
                }
            }
            else
            {
                // Verificar si algún servicio fue cancelado
                var serviciosCancelados = lavado.Servicios.Where(s => s.Estado == "Cancelado").ToList();
                if (serviciosCancelados.Any())
                {
                    lavado.Estado = ESTADO_REALIZADO_PARCIALMENTE;
                }
                else
                {
                    lavado.Estado = ESTADO_REALIZADO;
                }
            }

            lavado.TiempoFinalizacion = DateTime.UtcNow;
            await ActualizarLavado(lavado);
        }

        /// <summary>
        /// Finaliza un servicio específico dentro de un lavado.
        /// </summary>
        public async Task FinalizarServicio(string lavadoId, string servicioId)
        {
            var lavado = await ObtenerLavado(lavadoId);
            if (lavado == null)
                throw new ArgumentException("No se encontró el lavado", nameof(lavadoId));

            var servicio = lavado.Servicios.FirstOrDefault(s => s.ServicioId == servicioId);
            if (servicio == null)
                throw new ArgumentException("No se encontró el servicio en el lavado", nameof(servicioId));

            // Si tiene etapas, verificar que todas estén finalizadas
            if (servicio.Etapas.Any())
            {
                var etapasPendientes = servicio.Etapas.Where(e => e.Estado == "Pendiente" || e.Estado == "EnProceso").ToList();
                if (etapasPendientes.Any())
                {
                    throw new InvalidOperationException("No se puede finalizar el servicio porque hay etapas pendientes o en proceso.");
                }

                var etapasCanceladas = servicio.Etapas.Where(e => e.Estado == "Cancelado").ToList();
                if (etapasCanceladas.Any())
                {
                    servicio.Estado = "RealizadoParcialmente";
                }
                else
                {
                    servicio.Estado = "Realizado";
                }
            }
            else
            {
                servicio.Estado = "Realizado";
            }

            servicio.TiempoFinalizacion = DateTime.UtcNow;
            await ActualizarLavado(lavado);
            
            // Iniciar el siguiente servicio automáticamente
            await IniciarSiguienteServicioAutomaticamente(lavado);
            
            // Verificar si todos los servicios están finalizados para finalizar el lavado
            await FinalizarLavadoAutomaticamente(lavado);
        }

        /// <summary>
        /// Finaliza una etapa específica dentro de un servicio.
        /// </summary>
        public async Task FinalizarEtapa(string lavadoId, string servicioId, string etapaId)
        {
            var lavado = await ObtenerLavado(lavadoId);
            if (lavado == null)
                throw new ArgumentException("No se encontró el lavado", nameof(lavadoId));

            var servicio = lavado.Servicios.FirstOrDefault(s => s.ServicioId == servicioId);
            if (servicio == null)
                throw new ArgumentException("No se encontró el servicio en el lavado", nameof(servicioId));

            var etapa = servicio.Etapas.FirstOrDefault(e => e.EtapaId == etapaId);
            if (etapa == null)
                throw new ArgumentException("No se encontró la etapa en el servicio", nameof(etapaId));

            etapa.Estado = "Realizado";
            etapa.TiempoFinalizacion = DateTime.UtcNow;

            // Verificar si todas las etapas están finalizadas para finalizar el servicio automáticamente
            var etapasPendientes = servicio.Etapas.Where(e => e.Estado == "Pendiente" || e.Estado == "EnProceso").ToList();
            if (!etapasPendientes.Any())
            {
                var etapasCanceladas = servicio.Etapas.Where(e => e.Estado == "Cancelado").ToList();
                if (etapasCanceladas.Any())
                {
                    servicio.Estado = "RealizadoParcialmente";
                }
                else
                {
                    servicio.Estado = "Realizado";
                }
                servicio.TiempoFinalizacion = DateTime.UtcNow;
                
                // Iniciar el siguiente servicio automáticamente
                await IniciarSiguienteServicioAutomaticamente(lavado);
            }
            else
            {
                // Iniciar la siguiente etapa automáticamente
                var siguienteEtapa = servicio.Etapas
                    .Where(e => e.Estado == "Pendiente")
                    .OrderBy(e => servicio.Etapas.IndexOf(e))
                    .FirstOrDefault();
                    
                if (siguienteEtapa != null)
                {
                    siguienteEtapa.Estado = "EnProceso";
                    siguienteEtapa.TiempoInicio = DateTime.UtcNow;
                }
            }

            await ActualizarLavado(lavado);
            
            // Verificar si todos los servicios están finalizados para finalizar el lavado
            await FinalizarLavadoAutomaticamente(lavado);
        }

        /// <summary>
        /// Cancela un lavado completo.
        /// </summary>
        public async Task CancelarLavado(string id, string motivo)
        {
            if (string.IsNullOrWhiteSpace(motivo))
                throw new ArgumentException("El motivo de cancelación es obligatorio", nameof(motivo));

            var lavado = await ObtenerLavado(id);
            if (lavado == null)
                throw new ArgumentException("No se encontró el lavado", nameof(id));

            lavado.Estado = ESTADO_CANCELADO;
            lavado.MotivoCancelacion = motivo;
            lavado.TiempoFinalizacion = DateTime.UtcNow;

            // Cancelar el pago si existe y no está completamente pagado
            if (lavado.Pago != null)
            {
                if (lavado.Pago.Estado != "Pagado")
                {
                    lavado.Pago.Estado = "Cancelado";
                }
            }
            else
            {
                // Inicializar pago como cancelado si no existía
                lavado.Pago = new PagoLavado
                {
                    Estado = "Cancelado",
                    MontoPagado = 0,
                    Pagos = new List<DetallePago>()
                };
            }

            // Cancelar todos los servicios pendientes
            foreach (var servicio in lavado.Servicios.Where(s => s.Estado == "Pendiente" || s.Estado == "EnProceso"))
            {
                servicio.Estado = "Cancelado";
                servicio.MotivoCancelacion = motivo;
                servicio.TiempoFinalizacion = DateTime.UtcNow;

                foreach (var etapa in servicio.Etapas.Where(e => e.Estado == "Pendiente" || e.Estado == "EnProceso"))
                {
                    etapa.Estado = "Cancelado";
                    etapa.MotivoCancelacion = motivo;
                    etapa.TiempoFinalizacion = DateTime.UtcNow;
                }
            }

            await ActualizarLavado(lavado);
        }

        /// <summary>
        /// Cancela un servicio específico dentro de un lavado.
        /// </summary>
        public async Task CancelarServicio(string lavadoId, string servicioId, string motivo)
        {
            if (string.IsNullOrWhiteSpace(motivo))
                throw new ArgumentException("El motivo de cancelación es obligatorio", nameof(motivo));

            var lavado = await ObtenerLavado(lavadoId);
            if (lavado == null)
                throw new ArgumentException("No se encontró el lavado", nameof(lavadoId));

            var servicio = lavado.Servicios.FirstOrDefault(s => s.ServicioId == servicioId);
            if (servicio == null)
                throw new ArgumentException("No se encontró el servicio en el lavado", nameof(servicioId));

            servicio.Estado = "Cancelado";
            servicio.MotivoCancelacion = motivo;
            servicio.TiempoFinalizacion = DateTime.UtcNow;

            // Cancelar todas las etapas pendientes
            foreach (var etapa in servicio.Etapas.Where(e => e.Estado == "Pendiente" || e.Estado == "EnProceso"))
            {
                etapa.Estado = "Cancelado";
                etapa.MotivoCancelacion = motivo;
                etapa.TiempoFinalizacion = DateTime.UtcNow;
            }

            await ActualizarLavado(lavado);
        }

        /// <summary>
        /// Cancela una etapa específica dentro de un servicio.
        /// </summary>
        public async Task CancelarEtapa(string lavadoId, string servicioId, string etapaId, string motivo)
        {
            if (string.IsNullOrWhiteSpace(motivo))
                throw new ArgumentException("El motivo de cancelación es obligatorio", nameof(motivo));

            var lavado = await ObtenerLavado(lavadoId);
            if (lavado == null)
                throw new ArgumentException("No se encontró el lavado", nameof(lavadoId));

            var servicio = lavado.Servicios.FirstOrDefault(s => s.ServicioId == servicioId);
            if (servicio == null)
                throw new ArgumentException("No se encontró el servicio en el lavado", nameof(servicioId));

            var etapa = servicio.Etapas.FirstOrDefault(e => e.EtapaId == etapaId);
            if (etapa == null)
                throw new ArgumentException("No se encontró la etapa en el servicio", nameof(etapaId));

            etapa.Estado = "Cancelado";
            etapa.MotivoCancelacion = motivo;
            etapa.TiempoFinalizacion = DateTime.UtcNow;

            await ActualizarLavado(lavado);
        }

        /// <summary>
        /// Registra un pago para un lavado.
        /// </summary>
        public async Task RegistrarPago(string lavadoId, decimal monto, string medioPago, string? notas = null)
        {
            var lavado = await ObtenerLavado(lavadoId);
            if (lavado == null)
                throw new ArgumentException("No se encontró el lavado", nameof(lavadoId));

            if (monto <= 0)
                throw new ArgumentException("El monto debe ser mayor a 0", nameof(monto));

            lavado.Pago ??= new PagoLavado();

            var nuevoPago = new DetallePago
            {
                Id = Guid.NewGuid().ToString(),
                Monto = monto,
                MedioPago = medioPago,
                Fecha = DateTime.UtcNow,
                Notas = notas
            };

            lavado.Pago.Pagos.Add(nuevoPago);
            lavado.Pago.MontoPagado = lavado.Pago.Pagos.Sum(p => p.Monto);

            // Actualizar estado del pago
            if (lavado.Pago.MontoPagado >= lavado.Precio)
            {
                lavado.Pago.Estado = "Pagado";
            }
            else if (lavado.Pago.MontoPagado > 0)
            {
                lavado.Pago.Estado = "Parcial";
            }

            await ActualizarLavado(lavado);
        }

        /// <summary>
        /// Inicia un servicio específico dentro de un lavado.
        /// </summary>
        public async Task IniciarServicio(string lavadoId, string servicioId)
        {
            var lavado = await ObtenerLavado(lavadoId);
            if (lavado == null)
                throw new ArgumentException("No se encontró el lavado", nameof(lavadoId));

            var servicio = lavado.Servicios.FirstOrDefault(s => s.ServicioId == servicioId);
            if (servicio == null)
                throw new ArgumentException("No se encontró el servicio en el lavado", nameof(servicioId));

            if (servicio.Estado != "Pendiente")
                throw new InvalidOperationException("El servicio ya fue iniciado o finalizado.");

            servicio.Estado = "EnProceso";
            servicio.TiempoInicio = DateTime.UtcNow;

            await ActualizarLavado(lavado);
        }

        /// <summary>
        /// Inicia una etapa específica dentro de un servicio.
        /// </summary>
        public async Task IniciarEtapa(string lavadoId, string servicioId, string etapaId)
        {
            var lavado = await ObtenerLavado(lavadoId);
            if (lavado == null)
                throw new ArgumentException("No se encontró el lavado", nameof(lavadoId));

            var servicio = lavado.Servicios.FirstOrDefault(s => s.ServicioId == servicioId);
            if (servicio == null)
                throw new ArgumentException("No se encontró el servicio en el lavado", nameof(servicioId));

            var etapa = servicio.Etapas.FirstOrDefault(e => e.EtapaId == etapaId);
            if (etapa == null)
                throw new ArgumentException("No se encontró la etapa en el servicio", nameof(etapaId));

            if (etapa.Estado != "Pendiente")
                throw new InvalidOperationException("La etapa ya fue iniciada o finalizada.");

            // Verificar que no haya otra etapa en proceso en este servicio
            var etapaEnProceso = servicio.Etapas.FirstOrDefault(e => e.Estado == "EnProceso");
            if (etapaEnProceso != null && etapaEnProceso.EtapaId != etapaId)
            {
                throw new InvalidOperationException($"No se puede iniciar esta etapa porque la etapa '{etapaEnProceso.Nombre}' está en proceso. Las etapas deben completarse en orden.");
            }

            etapa.Estado = "EnProceso";
            etapa.TiempoInicio = DateTime.UtcNow;

            // Si es la primera etapa en iniciarse, iniciar el servicio también
            if (servicio.Estado == "Pendiente")
            {
                servicio.Estado = "EnProceso";
                servicio.TiempoInicio = DateTime.UtcNow;
            }

            await ActualizarLavado(lavado);
        }

        #endregion

        #region Asignación de Empleados

        /// <summary>
        /// Asigna empleados aleatoriamente a un lavado basándose en disponibilidad.
        /// </summary>
        public async Task<List<Empleado>> AsignarEmpleadosAleatorios(int cantidad)
        {
            var empleadosActivos = await _personalService.ObtenerEmpleados(
                estados: new List<string> { "Activo" },
                pageNumber: 1,
                pageSize: 100
            );

            // Filtrar empleados que no tienen lavados activos
            var empleadosDisponibles = new List<Empleado>();
            foreach (var empleado in empleadosActivos)
            {
                var lavadosActivos = await ObtenerLavadosActivosPorEmpleado(empleado.Id);
                if (!lavadosActivos.Any())
                {
                    empleadosDisponibles.Add(empleado);
                }
            }

            if (empleadosDisponibles.Count < cantidad)
            {
                throw new InvalidOperationException($"No hay suficientes empleados disponibles. Se requieren {cantidad} pero solo hay {empleadosDisponibles.Count} disponibles.");
            }

            // Seleccionar aleatoriamente
            var random = new Random();
            var empleadosSeleccionados = empleadosDisponibles
                .OrderBy(x => random.Next())
                .Take(cantidad)
                .ToList();

            return empleadosSeleccionados;
        }

        #endregion

        #region Validaciones

        /// <summary>
        /// Valida que el lavadero esté en horario de operación.
        /// </summary>
        public async Task ValidarHorarioOperacion()
        {
            var config = await _configuracionService.ObtenerConfiguracion();
            if (config == null) return; // Si no hay configuración, permitir

            var ahora = DateTime.Now;
            var diaSemana = ahora.DayOfWeek switch
            {
                DayOfWeek.Monday => "Lunes",
                DayOfWeek.Tuesday => "Martes",
                DayOfWeek.Wednesday => "Miércoles",
                DayOfWeek.Thursday => "Jueves",
                DayOfWeek.Friday => "Viernes",
                DayOfWeek.Saturday => "Sábado",
                DayOfWeek.Sunday => "Domingo",
                _ => "Lunes"
            };

            if (!config.HorariosOperacion.TryGetValue(diaSemana, out var horario))
                return; // Si no hay horario definido, permitir

            if (horario.ToUpperInvariant() == "CERRADO")
            {
                throw new InvalidOperationException($"El lavadero está cerrado los días {diaSemana}.");
            }

            // Parsear horario (formato: "09:00-18:00" o "09:00-13:00,15:00-19:00")
            var rangos = horario.Split(',');
            var dentroDeHorario = false;

            foreach (var rango in rangos)
            {
                var partes = rango.Trim().Split('-');
                if (partes.Length != 2) continue;

                if (TimeSpan.TryParse(partes[0].Trim(), out var inicio) &&
                    TimeSpan.TryParse(partes[1].Trim(), out var fin))
                {
                    var horaActual = ahora.TimeOfDay;
                    if (horaActual >= inicio && horaActual <= fin)
                    {
                        dentroDeHorario = true;
                        break;
                    }
                }
            }

            if (!dentroDeHorario)
            {
                throw new InvalidOperationException($"El lavadero está fuera de horario de operación. Horario del día: {horario}");
            }
        }

        /// <summary>
        /// Valida que no se exceda la capacidad de lavados concurrentes.
        /// </summary>
        public async Task ValidarCapacidadConcurrente()
        {
            var config = await _configuracionService.ObtenerConfiguracion();
            if (config == null) return;

            var lavadosActivos = await ContarLavadosActivos();

            if (lavadosActivos >= config.CapacidadMaximaConcurrente)
            {
                throw new InvalidOperationException($"Se ha alcanzado la capacidad máxima de {config.CapacidadMaximaConcurrente} lavados concurrentes.");
            }
        }

        #endregion

        #region Métodos Privados

        private async Task IniciarSiguienteServicioAutomaticamente(Lavado lavado)
        {
            // Buscar el siguiente servicio pendiente en orden
            var siguienteServicio = lavado.Servicios
                .Where(s => s.Estado == "Pendiente")
                .OrderBy(s => s.Orden)
                .FirstOrDefault();
                
            if (siguienteServicio != null)
            {
                siguienteServicio.Estado = "EnProceso";
                siguienteServicio.TiempoInicio = DateTime.UtcNow;
                
                // Si el servicio tiene etapas, iniciar la primera
                if (siguienteServicio.Etapas.Any())
                {
                    var primeraEtapa = siguienteServicio.Etapas.First();
                    primeraEtapa.Estado = "EnProceso";
                    primeraEtapa.TiempoInicio = DateTime.UtcNow;
                }
                
                await ActualizarLavado(lavado);
            }
        }

        private async Task FinalizarLavadoAutomaticamente(Lavado lavado)
        {
            // Recargar el lavado para tener los datos más recientes
            var lavadoActualizado = await ObtenerLavado(lavado.Id);
            if (lavadoActualizado == null) return;
            
            // Verificar si todos los servicios están finalizados o cancelados
            var serviciosPendientes = lavadoActualizado.Servicios
                .Where(s => s.Estado == "Pendiente" || s.Estado == "EnProceso")
                .ToList();
            
            if (!serviciosPendientes.Any())
            {
                // Determinar el estado final del lavado
                var serviciosRealizados = lavadoActualizado.Servicios.Where(s => s.Estado == "Realizado" || s.Estado == "RealizadoParcialmente").ToList();
                var serviciosCancelados = lavadoActualizado.Servicios.Where(s => s.Estado == "Cancelado").ToList();
                
                if (serviciosCancelados.Count == lavadoActualizado.Servicios.Count)
                {
                    // Todos los servicios fueron cancelados - el lavado ya debería estar cancelado
                    return;
                }
                else if (serviciosRealizados.Any() && serviciosCancelados.Any())
                {
                    // Algunos realizados, algunos cancelados
                    lavadoActualizado.Estado = ESTADO_REALIZADO_PARCIALMENTE;
                }
                else if (serviciosRealizados.Count == lavadoActualizado.Servicios.Count)
                {
                    // Todos los servicios realizados completamente
                    lavadoActualizado.Estado = ESTADO_REALIZADO;
                }
                else if (serviciosRealizados.Any())
                {
                    // Hay servicios realizados (algunos parcialmente)
                    var hayParciales = lavadoActualizado.Servicios.Any(s => s.Estado == "RealizadoParcialmente");
                    lavadoActualizado.Estado = hayParciales ? ESTADO_REALIZADO_PARCIALMENTE : ESTADO_REALIZADO;
                }
                
                lavadoActualizado.TiempoFinalizacion = DateTime.UtcNow;
                await ActualizarLavado(lavadoActualizado);
            }
        }

        private async Task<List<Lavado>> ObtenerLavadosFiltrados(
            List<string>? estados,
            string? clienteId,
            string? vehiculoId,
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            decimal? precioDesde,
            decimal? precioHasta,
            List<string>? estadosPago,
            string? sortBy,
            string? sortOrder)
        {
            Query query = _firestore.Collection(COLLECTION_NAME);

            // Aplicar filtros
            if (estados != null && estados.Any())
            {
                query = query.WhereIn("Estado", estados);
            }

            if (!string.IsNullOrWhiteSpace(clienteId))
            {
                query = query.WhereEqualTo("ClienteId", clienteId);
            }

            if (!string.IsNullOrWhiteSpace(vehiculoId))
            {
                query = query.WhereEqualTo("VehiculoId", vehiculoId);
            }

            var snapshot = await query.GetSnapshotAsync();
            var lavados = snapshot.Documents
                .Select(MapearDocumentoALavado)
                .ToList();

            // Filtros de fecha en memoria (Firestore no permite múltiples WhereIn/rangos en una sola consulta)
            if (fechaDesde.HasValue)
            {
                // Incluir desde las 00:00:00 del día especificado
                var fechaDesdeInicio = fechaDesde.Value.Date;
                lavados = lavados.Where(l => l.FechaCreacion >= fechaDesdeInicio).ToList();
            }

            if (fechaHasta.HasValue)
            {
                // Incluir hasta las 23:59:59.999 del día especificado
                var fechaHastaFin = fechaHasta.Value.Date.AddDays(1).AddMilliseconds(-1);
                lavados = lavados.Where(l => l.FechaCreacion <= fechaHastaFin).ToList();
            }

            // Filtros de precio en memoria
            if (precioDesde.HasValue)
            {
                lavados = lavados.Where(l => l.Precio >= precioDesde.Value).ToList();
            }

            if (precioHasta.HasValue)
            {
                lavados = lavados.Where(l => l.Precio <= precioHasta.Value).ToList();
            }

            // Filtro de estados de pago en memoria
            if (estadosPago != null && estadosPago.Any())
            {
                lavados = lavados.Where(l => l.Pago != null && estadosPago.Contains(l.Pago.Estado)).ToList();
            }

            return AplicarOrdenamiento(lavados, sortBy, sortOrder);
        }

        private async Task<int> ObtenerTotalLavados(
            List<string>? estados,
            string? clienteId,
            string? vehiculoId,
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            decimal? precioDesde,
            decimal? precioHasta,
            List<string>? estadosPago)
        {
            var lavados = await ObtenerLavadosFiltrados(estados, clienteId, vehiculoId, fechaDesde, fechaHasta, precioDesde, precioHasta, estadosPago, null, null);
            return lavados.Count;
        }

        private static List<Lavado> AplicarBusqueda(List<Lavado> baseFiltrada, string searchTerm)
        {
            var term = searchTerm?.Trim() ?? string.Empty;
            if (term.Length == 0) return baseFiltrada;

            var termUpper = term.ToUpperInvariant();
            
            // Intentar convertir el término a decimal para búsqueda por precio
            bool esPrecio = decimal.TryParse(term, out decimal precioBuscado);

            return baseFiltrada.Where(l =>
                (l.ClienteNombre?.ToUpperInvariant().Contains(termUpper) ?? false) ||
                (l.VehiculoPatente?.ToUpperInvariant().Contains(termUpper) ?? false) ||
                (l.Estado?.ToUpperInvariant().Contains(termUpper) ?? false) ||
                (l.Id?.ToUpperInvariant().Contains(termUpper) ?? false) ||
                l.EmpleadosAsignadosNombres.Any(n => n.ToUpperInvariant().Contains(termUpper)) ||
                l.Servicios.Any(s => s.ServicioNombre?.ToUpperInvariant().Contains(termUpper) ?? false) ||
                (esPrecio && l.Precio == precioBuscado) ||
                (l.Precio.ToString().Contains(term)) ||
                (l.Pago != null && l.Pago.Estado.ToUpperInvariant().Contains(termUpper))
            ).ToList();
        }

        private static List<Lavado> AplicarOrdenamiento(List<Lavado> lavados, string? sortBy, string? sortOrder)
        {
            sortBy ??= ORDEN_DEFECTO;
            sortOrder = (sortOrder ?? DIRECCION_DEFECTO).Trim().ToLowerInvariant();

            Func<Lavado, object> keySelector = sortBy switch
            {
                "FechaCreacion" => l => l.FechaCreacion,
                "ClienteNombre" => l => l.ClienteNombre ?? string.Empty,
                "VehiculoPatente" => l => l.VehiculoPatente ?? string.Empty,
                "Estado" => l => l.Estado,
                "Precio" => l => l.Precio,
                "TiempoEstimado" => l => l.TiempoEstimado,
                "TiempoInicio" => l => l.TiempoInicio ?? DateTime.MinValue,
                _ => l => l.FechaCreacion
            };

            var ordered = sortOrder == "asc"
                ? lavados.OrderBy(keySelector)
                : lavados.OrderByDescending(keySelector);

            return ordered.ToList();
        }

        private static List<Lavado> AplicarPaginacion(List<Lavado> lista, int pageNumber, int pageSize)
            => lista.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

        private static void ValidarParametrosPaginacion(int pageNumber, int pageSize)
        {
            if (pageNumber <= 0)
                throw new ArgumentException("El número de página debe ser mayor a 0", nameof(pageNumber));

            if (pageSize <= 0)
                throw new ArgumentException("El tamaño de página debe ser mayor a 0", nameof(pageSize));
        }

        private static void ValidarLavado(Lavado lavado)
        {
            var errores = new List<string>();

            if (string.IsNullOrWhiteSpace(lavado.ClienteId))
                errores.Add("El cliente es obligatorio");

            if (string.IsNullOrWhiteSpace(lavado.VehiculoId))
                errores.Add("El vehículo es obligatorio");

            if (!lavado.Servicios.Any())
                errores.Add("Debe seleccionar al menos un servicio");

            if (lavado.CantidadEmpleadosRequeridos <= 0)
                errores.Add("La cantidad de empleados debe ser mayor a 0");

            if (errores.Any())
                throw new ArgumentException($"Errores de validación: {string.Join(", ", errores)}");
        }

        private static Dictionary<string, object> CrearDiccionarioLavado(Lavado lavado)
        {
            var serviciosList = lavado.Servicios.Select(s => new Dictionary<string, object>
            {
                ["ServicioId"] = s.ServicioId,
                ["ServicioNombre"] = s.ServicioNombre ?? string.Empty,
                ["TipoServicio"] = s.TipoServicio ?? string.Empty,
                ["Precio"] = (double)s.Precio,
                ["TiempoEstimado"] = s.TiempoEstimado,
                ["Estado"] = s.Estado,
                ["Orden"] = s.Orden,
                ["TiempoInicio"] = s.TiempoInicio.HasValue ? Timestamp.FromDateTime(s.TiempoInicio.Value.ToUniversalTime()) : (object?)null!,
                ["TiempoFinalizacion"] = s.TiempoFinalizacion.HasValue ? Timestamp.FromDateTime(s.TiempoFinalizacion.Value.ToUniversalTime()) : (object?)null!,
                ["MotivoCancelacion"] = s.MotivoCancelacion ?? string.Empty,
                ["PaqueteId"] = s.PaqueteId ?? string.Empty,
                ["PaqueteNombre"] = s.PaqueteNombre ?? string.Empty,
                ["Etapas"] = s.Etapas.Select(e => new Dictionary<string, object>
                {
                    ["EtapaId"] = e.EtapaId,
                    ["Nombre"] = e.Nombre ?? string.Empty,
                    ["Estado"] = e.Estado,
                    ["TiempoInicio"] = e.TiempoInicio.HasValue ? Timestamp.FromDateTime(e.TiempoInicio.Value.ToUniversalTime()) : (object?)null!,
                    ["TiempoFinalizacion"] = e.TiempoFinalizacion.HasValue ? Timestamp.FromDateTime(e.TiempoFinalizacion.Value.ToUniversalTime()) : (object?)null!,
                    ["MotivoCancelacion"] = e.MotivoCancelacion ?? string.Empty
                }).ToList()
            }).ToList();

            var pagoDict = new Dictionary<string, object>
            {
                ["Estado"] = lavado.Pago?.Estado ?? "Pendiente",
                ["MontoPagado"] = (double)(lavado.Pago?.MontoPagado ?? 0),
                ["Pagos"] = (lavado.Pago?.Pagos ?? new List<DetallePago>()).Select(p => new Dictionary<string, object>
                {
                    ["Id"] = p.Id,
                    ["Monto"] = (double)p.Monto,
                    ["MedioPago"] = p.MedioPago,
                    ["Fecha"] = Timestamp.FromDateTime(p.Fecha.ToUniversalTime()),
                    ["Notas"] = p.Notas ?? string.Empty
                }).ToList()
            };

            return new Dictionary<string, object>
            {
                ["Id"] = lavado.Id,
                ["Estado"] = lavado.Estado,
                ["ClienteId"] = lavado.ClienteId,
                ["ClienteNombre"] = lavado.ClienteNombre ?? string.Empty,
                ["VehiculoId"] = lavado.VehiculoId,
                ["VehiculoPatente"] = lavado.VehiculoPatente ?? string.Empty,
                ["TipoVehiculo"] = lavado.TipoVehiculo ?? string.Empty,
                ["Servicios"] = serviciosList,
                ["Precio"] = (double)lavado.Precio,
                ["PrecioOriginal"] = (double)lavado.PrecioOriginal,
                ["Descuento"] = (double)lavado.Descuento,
                ["Pago"] = pagoDict,
                ["CantidadEmpleadosRequeridos"] = lavado.CantidadEmpleadosRequeridos,
                ["EmpleadosAsignadosIds"] = lavado.EmpleadosAsignadosIds,
                ["EmpleadosAsignadosNombres"] = lavado.EmpleadosAsignadosNombres,
                ["TiempoEstimado"] = lavado.TiempoEstimado,
                ["TiempoInicio"] = lavado.TiempoInicio.HasValue ? Timestamp.FromDateTime(lavado.TiempoInicio.Value.ToUniversalTime()) : (object?)null!,
                ["TiempoFinalizacion"] = lavado.TiempoFinalizacion.HasValue ? Timestamp.FromDateTime(lavado.TiempoFinalizacion.Value.ToUniversalTime()) : (object?)null!,
                ["FechaCreacion"] = Timestamp.FromDateTime(lavado.FechaCreacion.ToUniversalTime()),
                ["MotivoCancelacion"] = lavado.MotivoCancelacion ?? string.Empty,
                ["Notas"] = lavado.Notas ?? string.Empty,
                ["NotificacionTiempoEnviada"] = lavado.NotificacionTiempoEnviada,
                ["PreguntasFinalizacion"] = lavado.PreguntasFinalizacion
            };
        }

        private static Lavado MapearDocumentoALavado(DocumentSnapshot documento)
        {
            var lavado = new Lavado
            {
                Id = documento.Id,
                Estado = documento.GetValue<string>("Estado") ?? "Pendiente",
                ClienteId = documento.GetValue<string>("ClienteId") ?? string.Empty,
                ClienteNombre = documento.ContainsField("ClienteNombre") ? documento.GetValue<string>("ClienteNombre") : null,
                VehiculoId = documento.GetValue<string>("VehiculoId") ?? string.Empty,
                VehiculoPatente = documento.ContainsField("VehiculoPatente") ? documento.GetValue<string>("VehiculoPatente") : null,
                TipoVehiculo = documento.ContainsField("TipoVehiculo") ? documento.GetValue<string>("TipoVehiculo") : null,
                Precio = documento.ContainsField("Precio") ? (decimal)Convert.ToDouble(documento.GetValue<object>("Precio")) : 0m,
                PrecioOriginal = documento.ContainsField("PrecioOriginal") ? (decimal)Convert.ToDouble(documento.GetValue<object>("PrecioOriginal")) : 0m,
                Descuento = documento.ContainsField("Descuento") ? (decimal)Convert.ToDouble(documento.GetValue<object>("Descuento")) : 0m,
                CantidadEmpleadosRequeridos = documento.ContainsField("CantidadEmpleadosRequeridos") ? documento.GetValue<int>("CantidadEmpleadosRequeridos") : 1,
                TiempoEstimado = documento.ContainsField("TiempoEstimado") ? documento.GetValue<int>("TiempoEstimado") : 0,
                MotivoCancelacion = documento.ContainsField("MotivoCancelacion") ? documento.GetValue<string>("MotivoCancelacion") : null,
                Notas = documento.ContainsField("Notas") ? documento.GetValue<string>("Notas") : null,
                NotificacionTiempoEnviada = documento.ContainsField("NotificacionTiempoEnviada") && documento.GetValue<bool>("NotificacionTiempoEnviada"),
                PreguntasFinalizacion = documento.ContainsField("PreguntasFinalizacion") ? documento.GetValue<int>("PreguntasFinalizacion") : 0
            };

            // Mapear fechas
            if (documento.ContainsField("TiempoInicio"))
            {
                var timestamp = documento.GetValue<Timestamp?>("TiempoInicio");
                lavado.TiempoInicio = timestamp?.ToDateTime();
            }

            if (documento.ContainsField("TiempoFinalizacion"))
            {
                var timestamp = documento.GetValue<Timestamp?>("TiempoFinalizacion");
                lavado.TiempoFinalizacion = timestamp?.ToDateTime();
            }

            if (documento.ContainsField("FechaCreacion"))
            {
                var timestamp = documento.GetValue<Timestamp>("FechaCreacion");
                lavado.FechaCreacion = timestamp.ToDateTime();
            }

            // Mapear empleados
            if (documento.ContainsField("EmpleadosAsignadosIds"))
            {
                lavado.EmpleadosAsignadosIds = documento.GetValue<List<object>>("EmpleadosAsignadosIds")?.Select(o => o.ToString() ?? "").ToList() ?? new List<string>();
            }

            if (documento.ContainsField("EmpleadosAsignadosNombres"))
            {
                lavado.EmpleadosAsignadosNombres = documento.GetValue<List<object>>("EmpleadosAsignadosNombres")?.Select(o => o.ToString() ?? "").ToList() ?? new List<string>();
            }

            // Mapear servicios
            if (documento.ContainsField("Servicios"))
            {
                lavado.Servicios = MapearServicios(documento);
            }

            // Mapear pago
            if (documento.ContainsField("Pago"))
            {
                lavado.Pago = MapearPago(documento);
            }

            return lavado;
        }

        private static object? GetDictValue(IDictionary<string, object> dict, string key)
        {
            return dict.TryGetValue(key, out var value) ? value : null;
        }

        private static List<ServicioEnLavado> MapearServicios(DocumentSnapshot documento)
        {
            var servicios = new List<ServicioEnLavado>();
            var raw = documento.GetValue<IList<object>>("Servicios");

            foreach (var item in raw)
            {
                if (item is IDictionary<string, object> map)
                {
                    var servicio = new ServicioEnLavado
                    {
                        ServicioId = GetDictValue(map, "ServicioId")?.ToString() ?? string.Empty,
                        ServicioNombre = GetDictValue(map, "ServicioNombre")?.ToString(),
                        TipoServicio = GetDictValue(map, "TipoServicio")?.ToString(),
                        Precio = map.ContainsKey("Precio") ? (decimal)Convert.ToDouble(map["Precio"]) : 0m,
                        TiempoEstimado = map.ContainsKey("TiempoEstimado") ? Convert.ToInt32(map["TiempoEstimado"]) : 0,
                        Estado = GetDictValue(map, "Estado")?.ToString() ?? "Pendiente",
                        Orden = map.ContainsKey("Orden") ? Convert.ToInt32(map["Orden"]) : 0,
                        MotivoCancelacion = GetDictValue(map, "MotivoCancelacion")?.ToString(),
                        PaqueteId = GetDictValue(map, "PaqueteId")?.ToString(),
                        PaqueteNombre = GetDictValue(map, "PaqueteNombre")?.ToString()
                    };

                    // Mapear tiempos
                    if (map.ContainsKey("TiempoInicio") && map["TiempoInicio"] is Timestamp tiempoInicio)
                    {
                        servicio.TiempoInicio = tiempoInicio.ToDateTime();
                    }

                    if (map.ContainsKey("TiempoFinalizacion") && map["TiempoFinalizacion"] is Timestamp tiempoFin)
                    {
                        servicio.TiempoFinalizacion = tiempoFin.ToDateTime();
                    }

                    // Mapear etapas
                    if (map.ContainsKey("Etapas") && map["Etapas"] is IList<object> etapasRaw)
                    {
                        foreach (var etapaItem in etapasRaw)
                        {
                            if (etapaItem is IDictionary<string, object> etapaMap)
                            {
                                var etapa = new EtapaEnLavado
                                {
                                    EtapaId = GetDictValue(etapaMap, "EtapaId")?.ToString() ?? string.Empty,
                                    Nombre = GetDictValue(etapaMap, "Nombre")?.ToString(),
                                    Estado = GetDictValue(etapaMap, "Estado")?.ToString() ?? "Pendiente",
                                    MotivoCancelacion = GetDictValue(etapaMap, "MotivoCancelacion")?.ToString()
                                };

                                if (etapaMap.ContainsKey("TiempoInicio") && etapaMap["TiempoInicio"] is Timestamp etapaInicio)
                                {
                                    etapa.TiempoInicio = etapaInicio.ToDateTime();
                                }

                                if (etapaMap.ContainsKey("TiempoFinalizacion") && etapaMap["TiempoFinalizacion"] is Timestamp etapaFin)
                                {
                                    etapa.TiempoFinalizacion = etapaFin.ToDateTime();
                                }

                                servicio.Etapas.Add(etapa);
                            }
                        }
                    }

                    servicios.Add(servicio);
                }
            }

            return servicios.OrderBy(s => s.Orden).ToList();
        }

        private static PagoLavado MapearPago(DocumentSnapshot documento)
        {
            var raw = documento.GetValue<IDictionary<string, object>>("Pago");
            if (raw == null) return new PagoLavado();

            var pago = new PagoLavado
            {
                Estado = GetDictValue(raw, "Estado")?.ToString() ?? "Pendiente",
                MontoPagado = raw.ContainsKey("MontoPagado") ? (decimal)Convert.ToDouble(raw["MontoPagado"]) : 0m
            };

            if (raw.ContainsKey("Pagos") && raw["Pagos"] is IList<object> pagosRaw)
            {
                foreach (var pagoItem in pagosRaw)
                {
                    if (pagoItem is IDictionary<string, object> pagoMap)
                    {
                        var detalle = new DetallePago
                        {
                            Id = GetDictValue(pagoMap, "Id")?.ToString() ?? string.Empty,
                            Monto = pagoMap.ContainsKey("Monto") ? (decimal)Convert.ToDouble(pagoMap["Monto"]) : 0m,
                            MedioPago = GetDictValue(pagoMap, "MedioPago")?.ToString() ?? "Efectivo",
                            Notas = GetDictValue(pagoMap, "Notas")?.ToString()
                        };

                        if (pagoMap.ContainsKey("Fecha") && pagoMap["Fecha"] is Timestamp fecha)
                        {
                            detalle.Fecha = fecha.ToDateTime();
                        }

                        pago.Pagos.Add(detalle);
                    }
                }
            }

            return pago;
        }

        /// <summary>
        /// Obtiene el precio mínimo de los lavados según los filtros aplicados.
        /// </summary>
        /// <param name="estados">Lista de estados a filtrar.</param>
        /// <param name="clienteId">ID del cliente.</param>
        /// <param name="vehiculoId">ID del vehículo.</param>
        /// <param name="fechaDesde">Fecha desde.</param>
        /// <param name="fechaHasta">Fecha hasta.</param>
        /// <param name="estadosPago">Estados de pago a filtrar.</param>
        /// <returns>Precio mínimo encontrado o 0 si no hay lavados.</returns>
        public async Task<decimal> ObtenerPrecioMinimo(
            List<string> estados = null,
            string? clienteId = null,
            string? vehiculoId = null,
            DateTime? fechaDesde = null,
            DateTime? fechaHasta = null,
            List<string>? estadosPago = null)
        {
            var lavados = await ObtenerLavadosFiltrados(
                estados, clienteId, vehiculoId, fechaDesde, fechaHasta, null, null, estadosPago, null, null);

            return lavados.Any() ? lavados.Min(l => l.Precio) : 0m;
        }

        /// <summary>
        /// Obtiene el precio máximo de los lavados según los filtros aplicados.
        /// </summary>
        /// <param name="estados">Lista de estados a filtrar.</param>
        /// <param name="clienteId">ID del cliente.</param>
        /// <param name="vehiculoId">ID del vehículo.</param>
        /// <param name="fechaDesde">Fecha desde.</param>
        /// <param name="fechaHasta">Fecha hasta.</param>
        /// <param name="estadosPago">Estados de pago a filtrar.</param>
        /// <returns>Precio máximo encontrado o 0 si no hay lavados.</returns>
        public async Task<decimal> ObtenerPrecioMaximo(
            List<string> estados = null,
            string? clienteId = null,
            string? vehiculoId = null,
            DateTime? fechaDesde = null,
            DateTime? fechaHasta = null,
            List<string>? estadosPago = null)
        {
            var lavados = await ObtenerLavadosFiltrados(
                estados, clienteId, vehiculoId, fechaDesde, fechaHasta, null, null, estadosPago, null, null);

            return lavados.Any() ? lavados.Max(l => l.Precio) : 0m;
        }

        #endregion
    }
}

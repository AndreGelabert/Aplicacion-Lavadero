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
        private readonly StockService _stockService;

        /// <summary>
        /// Inicializa una nueva instancia del servicio de lavados.
        /// </summary>
        public LavadoService(
            FirestoreDb firestore,
            ServicioService servicioService,
            PaqueteServicioService paqueteServicioService,
            PersonalService personalService,
            ConfiguracionService configuracionService,
            StockService stockService)
        {
            _firestore = firestore ?? throw new ArgumentNullException(nameof(firestore));
            _servicioService = servicioService ?? throw new ArgumentNullException(nameof(servicioService));
            _paqueteServicioService = paqueteServicioService ?? throw new ArgumentNullException(nameof(paqueteServicioService));
            _personalService = personalService ?? throw new ArgumentNullException(nameof(personalService));
            _configuracionService = configuracionService ?? throw new ArgumentNullException(nameof(configuracionService));
            _stockService = stockService ?? throw new ArgumentNullException(nameof(stockService));
        }

        public LavadoService(
            FirestoreDb firestore,
            ServicioService servicioService,
            PaqueteServicioService paqueteServicioService,
            PersonalService personalService,
            ConfiguracionService configuracionService)
            : this(firestore, servicioService, paqueteServicioService, personalService, configuracionService, new StockService(firestore))
        {
        }
        #endregion

        public ServicioEnLavado CrearServicioEnLavado(Servicio servicio, int orden, string? paqueteId, string? paqueteNombre)
        {
            var servicioEnLavado = new ServicioEnLavado
            {
                ServicioId = servicio.Id,
                ServicioNombre = servicio.Nombre,
                TipoServicio = servicio.Tipo,
                Precio = servicio.Precio,
                TiempoEstimado = servicio.TiempoEstimado,
                Estado = "Pendiente",
                Orden = orden,
                PaqueteId = paqueteId,
                PaqueteNombre = paqueteNombre,
                Receta = servicio.Etapas.Any() ? new List<RecetaInsumo>() : servicio.Receta.Select(CopiarReceta).ToList()
            };

            // Agregar etapas si las tiene
            if (servicio.Etapas != null && servicio.Etapas.Any())
            {
                foreach (var etapa in servicio.Etapas)
                {
                    servicioEnLavado.Etapas.Add(new EtapaEnLavado
                    {
                        EtapaId = etapa.Id,
                        Nombre = etapa.Nombre,
                        Estado = "Pendiente",
                        Receta = etapa.Receta.Select(CopiarReceta).ToList()
                    });
                }
            }

            return servicioEnLavado;
        }

        public async Task IniciarPrimerServicioCreacion(Lavado lavadoCreado)
        {
            if (lavadoCreado.Servicios.Any())
            {
                var primerServicio = lavadoCreado.Servicios.OrderBy(s => s.Orden).First();
                await IniciarServicio(lavadoCreado.Id, primerServicio.ServicioId);

                if (primerServicio.Etapas.Any())
                {
                    var primeraEtapa = primerServicio.Etapas.First();
                    await IniciarEtapa(lavadoCreado.Id, primerServicio.ServicioId, primeraEtapa.EtapaId);
                }
            }
        }

        public async Task<Lavado> CrearLavadoConInicio(Lavado lavado, IReadOnlyDictionary<int, string>? seleccion = null)
        {
            await CrearLavadosConInicio(new[] { (lavado, seleccion) });
            return lavado;
        }

        public async Task CrearLavadosConInicio(IReadOnlyList<(Lavado Lavado, IReadOnlyDictionary<int, string>? Seleccion)> entradas)
        {
            await ValidarHorarioOperacion();
            var config = await _configuracionService.ObtenerConfiguracion();
            if (config != null && await ContarLavadosActivos() + entradas.Count > config.CapacidadMaximaConcurrente)
                throw new InvalidOperationException($"Se ha alcanzado la capacidad máxima de {config.CapacidadMaximaConcurrente} lavados concurrentes.");
            var referencias = entradas.Select(_ => _firestore.Collection(COLLECTION_NAME).Document()).ToList();
            for (var i = 0; i < entradas.Count; i++)
            {
                var lavado = entradas[i].Lavado;
                ValidarLavado(lavado);
                lavado.Id = referencias[i].Id;
                lavado.FechaCreacion = DateTime.UtcNow;
                lavado.Estado = ESTADO_EN_PROCESO;
                lavado.TiempoInicio = DateTime.UtcNow;
                lavado.Pago ??= new PagoLavado();
            }
            await InsumosTransacciones.EjecutarAsync(_firestore, async transaction =>
            {
                var capacity = await LeerCapacidadCreacion(transaction, entradas.Count, config);
                var membership = await ValidarAsignacionesEnTransaccion(transaction, entradas.Select(x => x.Lavado));
                var recetas = await PrepararInicioEnTransaccion(transaction, entradas);
                AvanzarCapacidadCreacion(transaction, capacity);
                if (membership != null) EmployeeMembershipVersion.Advance(transaction, membership);
                EscribirInicioEnTransaccion(transaction, entradas, referencias, recetas);
            });
        }

        internal Task<string> ConvertirTurnoEnLavado(string turnoId, IReadOnlyDictionary<int, string>? seleccion) =>
            ConvertirTurnoEnLavado(turnoId, seleccion, () => DateTime.UtcNow);

        // The clock seam is internal and scoped to this invocation; callers cannot supply it over HTTP.
        internal async Task<string> ConvertirTurnoEnLavado(string turnoId, IReadOnlyDictionary<int, string>? seleccion, Func<DateTime> utcNow)
        {
            ArgumentNullException.ThrowIfNull(utcNow);
            if (string.IsNullOrWhiteSpace(turnoId) || turnoId.Contains('/') || turnoId is "." or "..")
                throw new ArgumentException("Identificador de turno inválido.", nameof(turnoId));
            var turnoRef = _firestore.Collection("turnos").Document(turnoId);
            var lavadoRef = _firestore.Collection(COLLECTION_NAME).Document();
            var resultado = await InsumosTransacciones.EjecutarAsync(_firestore, async transaction =>
            {
                var coordinator = await transaction.GetSnapshotAsync(_firestore.Collection("coordinacion").Document("turnos"));
                var doc = await transaction.GetSnapshotAsync(turnoRef);
                if (!doc.Exists) throw new InvalidOperationException("Turno no encontrado.");
                var fields = doc.ToDictionary();
                var estado = doc.GetValue<string>("Estado");
                if (estado == "EnProceso")
                {
                    if (!fields.TryGetValue("LavadoId", out var linked) || linked is not string id || string.IsNullOrWhiteSpace(id) || id.Contains('/') || id is "." or "..")
                        throw new InvalidOperationException("El vínculo con el turno es inválido.");
                    var existing = await transaction.GetSnapshotAsync(_firestore.Collection(COLLECTION_NAME).Document(id));
                    if (!existing.Exists || !existing.ContainsField("TurnoId") || existing.GetValue<string>("TurnoId") != turnoId || existing.GetValue<string>("Estado") != ESTADO_EN_PROCESO)
                        throw new InvalidOperationException("El vínculo con el turno es inválido.");
                    return id;
                }
                if (estado != "Pendiente" || fields.ContainsKey("LavadoId"))
                    throw new InvalidOperationException("El turno no está disponible para iniciar.");
                var config = await transaction.GetSnapshotAsync(_firestore.Collection("configuracion").Document("system_config"));
                if (!config.Exists) throw new InvalidOperationException("Configuración no encontrada.");
                long grace = 5;
                if (config.ToDictionary().TryGetValue("ToleranciaLlegadaTurnosMinutos", out var rawGrace) && rawGrace is long g && g >= 5)
                    grace = g;
                if (grace < 5 || grace > 525600)
                    throw new InvalidOperationException("Tolerancia de llegada no disponible.");
                if (!doc.TryGetValue<DateTime>("InicioUtc", out var start) || start == default || start.Kind != DateTimeKind.Utc)
                    throw new InvalidOperationException("Intervalo de turno inválido.");
                var nowAttempt = utcNow();
                if (nowAttempt == default || nowAttempt.Kind != DateTimeKind.Utc)
                    throw new InvalidOperationException("Hora UTC no disponible.");
                if (nowAttempt.Ticks - start.Ticks > grace * TimeSpan.TicksPerMinute)
                {
                    AvanzarCapacidadCreacion(transaction, coordinator);
                    transaction.Update(turnoRef, "Estado", "Ausente");
                    return (string?)null;
                }
                var turno = doc.ConvertTo<Turno>();
                var mapped = TurnoLavadoSnapshotMapper.Mapear(turno);
                var membership = await EmployeeMembershipVersion.ReadAsync(_firestore, transaction);
                var employees = await transaction.GetSnapshotAsync(_firestore.Collection("empleados").WhereEqualTo("Estado", "Activo"));
                var occupied = await transaction.GetSnapshotAsync(_firestore.Collection(COLLECTION_NAME).WhereEqualTo("Estado", ESTADO_EN_PROCESO));
                if (!config.ToDictionary().TryGetValue("CapacidadMaximaConcurrente", out var capacityValue) ||
                    capacityValue is not long capacity || capacity <= 0 || capacity > int.MaxValue)
                    throw new InvalidOperationException("Configuración inválida.");
                if (occupied.Count >= capacity)
                    throw new InvalidOperationException("Se ha alcanzado la capacidad máxima de lavados concurrentes.");
                var assigned = TurnoEmployeeAssignment.Seleccionar(turno.Vehiculos[0].CantidadEmpleadosRequeridos,
                    employees.Documents.Select(x => x.Id), occupied.Documents.Select(x => x.ContainsField("EmpleadosAsignadosIds") ? x.GetValue<List<string>>("EmpleadosAsignadosIds") : new List<string>()));
                var clientDoc = await transaction.GetSnapshotAsync(_firestore.Collection("clientes").Document(turno.ClienteId));
                var clienteNombre = clientDoc.Exists
                    ? (clientDoc.ContainsField("Nombre") && clientDoc.ContainsField("Apellido")
                        ? $"{clientDoc.GetValue<string>("Nombre")} {clientDoc.GetValue<string>("Apellido")}"
                        : (clientDoc.ContainsField("NombreCompleto") ? clientDoc.GetValue<string>("NombreCompleto") : null))
                    : null;
                var vehicleDoc = await transaction.GetSnapshotAsync(_firestore.Collection("vehiculos").Document(turno.Vehiculos[0].VehiculoId));
                var vehiculoPatente = vehicleDoc.Exists && vehicleDoc.ContainsField("Patente") ? vehicleDoc.GetValue<string>("Patente") : null;
                var tipoVehiculo = vehicleDoc.Exists && vehicleDoc.ContainsField("TipoVehiculo") ? vehicleDoc.GetValue<string>("TipoVehiculo") : null;
                var trajoNombre = clienteNombre;
                if (!string.IsNullOrWhiteSpace(turno.ClienteTrajoId) && turno.ClienteTrajoId != turno.ClienteId)
                {
                    var trajoDoc = await transaction.GetSnapshotAsync(_firestore.Collection("clientes").Document(turno.ClienteTrajoId));
                    if (trajoDoc.Exists)
                        trajoNombre = trajoDoc.ContainsField("Nombre") && trajoDoc.ContainsField("Apellido")
                            ? $"{trajoDoc.GetValue<string>("Nombre")} {trajoDoc.GetValue<string>("Apellido")}"
                            : (trajoDoc.ContainsField("NombreCompleto") ? trajoDoc.GetValue<string>("NombreCompleto") : trajoNombre);
                }
                var retiraNombre = clienteNombre;
                if (!string.IsNullOrWhiteSpace(turno.ClienteRetiraId) && turno.ClienteRetiraId != turno.ClienteId)
                {
                    if (turno.ClienteRetiraId == turno.ClienteTrajoId)
                        retiraNombre = trajoNombre;
                    else
                    {
                        var retiraDoc = await transaction.GetSnapshotAsync(_firestore.Collection("clientes").Document(turno.ClienteRetiraId));
                        if (retiraDoc.Exists)
                            retiraNombre = retiraDoc.ContainsField("Nombre") && retiraDoc.ContainsField("Apellido")
                                ? $"{retiraDoc.GetValue<string>("Nombre")} {retiraDoc.GetValue<string>("Apellido")}"
                                : (retiraDoc.ContainsField("NombreCompleto") ? retiraDoc.GetValue<string>("NombreCompleto") : retiraNombre);
                    }
                }
                var empNames = assigned.Select(id =>
                {
                    var ed = employees.Documents.FirstOrDefault(x => x.Id == id);
                    return ed != null && ed.TryGetValue<string>("Nombre", out var nombre) && !string.IsNullOrWhiteSpace(nombre) ? nombre : id;
                }).ToList();
                var now = DateTime.UtcNow;
                var lavado = new Lavado { Id = lavadoRef.Id, Estado = ESTADO_EN_PROCESO, ClienteId = turno.ClienteId,
                    ClienteNombre = clienteNombre, VehiculoId = turno.Vehiculos[0].VehiculoId,
                    VehiculoPatente = vehiculoPatente, TipoVehiculo = tipoVehiculo,
                    Servicios = mapped.Servicios.ToList(), Precio = mapped.Precio,
                    PrecioOriginal = mapped.PrecioOriginal, Descuento = turno.Descuento, CantidadEmpleadosRequeridos = turno.Vehiculos[0].CantidadEmpleadosRequeridos,
                    EmpleadosAsignadosIds = assigned.ToList(), EmpleadosAsignadosNombres = empNames,
                    TiempoEstimado = turno.Vehiculos[0].Servicios.Sum(x => x.TiempoEstimado),
                    FechaCreacion = now, TiempoInicio = now, Pago = new PagoLavado(), Notas = turno.Notas,
                    ClienteTrajoId = turno.ClienteTrajoId, ClienteTrajoNombre = trajoNombre,
                    ClienteRetiraId = turno.ClienteRetiraId, ClienteRetiraNombre = retiraNombre };
                ValidarLavado(lavado);
                var entries = new List<(Lavado Lavado, IReadOnlyDictionary<int, string>? Seleccion)> { (lavado, seleccion) };
                var recetas = await PrepararInicioEnTransaccion(transaction, entries);
                AvanzarCapacidadCreacion(transaction, coordinator);
                EmployeeMembershipVersion.Advance(transaction, membership);
                EscribirInicioEnTransaccion(transaction, entries, new[] { lavadoRef }, recetas, turnoId);
                transaction.Update(turnoRef, new Dictionary<string, object> { ["Estado"] = "EnProceso", ["LavadoId"] = lavadoRef.Id });
                return lavadoRef.Id;
            });
            return resultado ?? throw new InvalidOperationException("El turno no está disponible para iniciar.");
        }

        private async Task<DocumentSnapshot> LeerCapacidadCreacion(Transaction transaction, int cantidad, Configuracion? fallback)
        {
            var coordinator = await transaction.GetSnapshotAsync(_firestore.Collection("coordinacion").Document("turnos"));
            var configDoc = await transaction.GetSnapshotAsync(_firestore.Collection("configuracion").Document("system_config"));
            var activos = await transaction.GetSnapshotAsync(_firestore.Collection(COLLECTION_NAME).WhereEqualTo("Estado", ESTADO_EN_PROCESO));
            var config = configDoc.Exists ? configDoc.ConvertTo<Configuracion>() : fallback;
            if (config != null && activos.Count + cantidad > config.CapacidadMaximaConcurrente)
                throw new InvalidOperationException($"Se ha alcanzado la capacidad máxima de {config.CapacidadMaximaConcurrente} lavados concurrentes.");
            return coordinator;
        }

        private async Task<DocumentSnapshot?> ValidarAsignacionesEnTransaccion(Transaction transaction, IEnumerable<Lavado> lavados)
        {
            var activos = lavados.Where(x => x.Estado == ESTADO_EN_PROCESO && x.EmpleadosAsignadosIds.Count > 0).ToList();
            if (activos.Count == 0) return null;
            var membership = await EmployeeMembershipVersion.ReadAsync(_firestore, transaction);
            var empleados = await transaction.GetSnapshotAsync(_firestore.Collection("empleados").WhereEqualTo("Estado", "Activo"));
            var ocupados = await transaction.GetSnapshotAsync(_firestore.Collection(COLLECTION_NAME).WhereEqualTo("Estado", ESTADO_EN_PROCESO));
            var disponibles = empleados.Documents.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
            var asignados = new HashSet<string>(StringComparer.Ordinal);
            foreach (var lavado in activos)
            foreach (var id in lavado.EmpleadosAsignadosIds)
            {
                if (!disponibles.Contains(id) || !asignados.Add(id) || ocupados.Documents.Any(doc => doc.Id != lavado.Id &&
                    doc.ContainsField("EmpleadosAsignadosIds") && doc.GetValue<List<string>>("EmpleadosAsignadosIds").Contains(id)))
                    throw new InvalidOperationException("El empleado asignado no está activo o ya está ocupado.");
            }
            return membership;
        }

        private static void AvanzarCapacidadCreacion(Transaction transaction, DocumentSnapshot coordinator)
        {
            var version = coordinator.Exists ? coordinator.GetValue<long>("Version") : 0;
            transaction.Set(coordinator.Reference, new Dictionary<string, object> { ["Version"] = checked(version + 1) }, SetOptions.MergeAll);
        }

        // The caller must complete all transaction reads, including these stock reads, before writing.
        internal Task<ResultadoOperacion> ValidarDisponibilidadInicio(
            IReadOnlyList<(Lavado Lavado, IReadOnlyDictionary<int, string>? Seleccion)> entradas)
        {
            var recetas = entradas
                .Select(e => (e.Lavado.Servicios.OrderBy(s => s.Orden).FirstOrDefault(), e.Seleccion))
                .Where(x => x.Item1 != null)
                .Select(x =>
                {
                    var primero = x.Item1!;
                    var etapa = primero.Etapas.FirstOrDefault();
                    return ((IEnumerable<RecetaInsumo>)(etapa?.Receta ?? primero.Receta), x.Seleccion);
                });
            return _stockService.ValidarDisponibilidadConReservas(recetas);
        }

        internal async Task<List<StockService.RecetaServicio>> PrepararInicioEnTransaccion(
            Transaction transaction, IReadOnlyList<(Lavado Lavado, IReadOnlyDictionary<int, string>? Seleccion)> entradas)
        {
            var recetas = new List<StockService.RecetaServicio>();
            var pendientes = new Dictionary<string, decimal>(StringComparer.Ordinal);
            foreach (var (lavado, seleccion) in entradas)
            {
                var primero = lavado.Servicios.OrderBy(s => s.Orden).FirstOrDefault();
                if (primero == null) continue;
                var etapa = primero.Etapas.FirstOrDefault();
                var clave = $"{lavado.Id}_{primero.ServicioId}_{(etapa == null ? "servicio" : etapa.EtapaId)}";
                var receta = await _stockService.LeerRecetaServicio(transaction, clave, etapa?.Receta ?? primero.Receta, false, seleccion: seleccion, reservasPendientes: pendientes);
                recetas.Add(receta);
                foreach (var reserva in receta.Reservas.Where(r => !r.Existe))
                    pendientes[reserva.Contador.Id] = StockPrecision.Sumar(pendientes.GetValueOrDefault(reserva.Contador.Id), reserva.Cantidad);
                primero.Estado = "EnProceso";
                primero.TiempoInicio = DateTime.UtcNow;
                if (etapa != null)
                {
                    etapa.Estado = "EnProceso";
                    etapa.TiempoInicio = DateTime.UtcNow;
                }
            }
            return recetas;
        }

        internal static void EscribirInicioEnTransaccion(
            Transaction transaction, IReadOnlyList<(Lavado Lavado, IReadOnlyDictionary<int, string>? Seleccion)> entradas,
            IReadOnlyList<DocumentReference> referencias, IReadOnlyList<StockService.RecetaServicio> recetas, string? turnoId = null)
        {
            foreach (var receta in recetas) StockService.EscribirReservasServicio(transaction, receta, false);
            for (var i = 0; i < entradas.Count; i++)
            {
                var datos = CrearDiccionarioLavado(entradas[i].Lavado);
                if (turnoId != null) datos["TurnoId"] = turnoId;
                transaction.Create(referencias[i], datos);
            }
        }

        public record ResultadoPrecioCreacion(decimal PrecioOriginal, decimal Precio, string? Error = null);

        public ResultadoPrecioCreacion CalcularPrecioCreacion(
            List<ServicioEnLavado> serviciosEnLavado,
            Dictionary<string, PaqueteServicio> paquetesPorId,
            decimal descuento)
        {
            decimal precioTotal = 0;
            var paquetesUnicos = serviciosEnLavado
                .Where(s => !string.IsNullOrEmpty(s.PaqueteId))
                .Select(s => s.PaqueteId)
                .Distinct()
                .ToList();

            foreach (var paqueteId in paquetesUnicos)
            {
                var paquete = paquetesPorId[paqueteId!];
                var serviciosSeleccionadosPaquete = serviciosEnLavado
                    .Where(servicio => string.Equals(servicio.PaqueteId, paqueteId, StringComparison.Ordinal));
                if (PaqueteServicioService.TryCalcularPrecioSeleccionado(
                    paquete,
                    serviciosSeleccionadosPaquete,
                    out var precioPaqueteConDescuento))
                {
                    precioTotal += precioPaqueteConDescuento;
                }
                else
                {
                    return new ResultadoPrecioCreacion(0, 0, "Los servicios seleccionados no corresponden al paquete indicado.");
                }
            }

            var serviciosIndividuales = serviciosEnLavado.Where(s => string.IsNullOrEmpty(s.PaqueteId));
            foreach (var servIndiv in serviciosIndividuales)
            {
                precioTotal += servIndiv.Precio;
            }

            var precioConDescuento = precioTotal - (precioTotal * descuento / 100);
            return new ResultadoPrecioCreacion(precioTotal, precioConDescuento);
        }

        public record ResultadoRetiro(string? Error = null, Dictionary<string, string>? CamposAntes = null);

        public async Task<ResultadoRetiro> RegistrarRetiro(string lavadoId)
        {
            var referencia = _firestore.Collection(COLLECTION_NAME).Document(lavadoId);
            var fechaRetiro = DateTime.UtcNow;
            return await InsumosTransacciones.EjecutarAsync(_firestore, async transaction =>
            {
                var snapshot = await transaction.GetSnapshotAsync(referencia);
                if (!snapshot.Exists) return new ResultadoRetiro("Lavado no encontrado.");
                var lavado = MapearDocumentoALavado(snapshot);
                if (lavado.EstadoRetiro == Lavado.EstadosRetiro.Retirado)
                    return new ResultadoRetiro("El vehículo ya fue retirado.");
                var estadoPago = lavado.Pago?.Estado ?? "Pendiente";
                if (lavado.Estado != "Cancelado" && estadoPago != "Pagado")
                    return new ResultadoRetiro("El lavado debe estar pagado antes de permitir el retiro del vehículo.");

                var antes = AuditCampos.CapturarLavado(lavado);
                lavado.EstadoRetiro = Lavado.EstadosRetiro.Retirado;
                lavado.FechaRetiro = fechaRetiro;
                ValidarLavado(lavado);
                var datos = CrearDiccionarioLavado(lavado);
                transaction.Update(referencia, new Dictionary<string, object>
                {
                    ["EstadoRetiro"] = datos["EstadoRetiro"], ["FechaRetiro"] = datos["FechaRetiro"]
                });
                return new ResultadoRetiro(CamposAntes: antes);
            });
        }

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
            List<string>? estadosRetiro = null,
            int pageNumber = 1,
            int pageSize = 10,
            string? sortBy = null,
            string? sortOrder = null)
        {
            ValidarParametrosPaginacion(pageNumber, pageSize);

            sortBy ??= ORDEN_DEFECTO;
            sortOrder ??= DIRECCION_DEFECTO;

            var lavados = await ObtenerLavadosFiltrados(estados, clienteId, vehiculoId, fechaDesde, fechaHasta, precioDesde, precioHasta, estadosPago, estadosRetiro, sortBy, sortOrder);

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
            List<string>? estadosRetiro,
            int pageSize)
        {
            if (pageSize <= 0)
                throw new ArgumentException("El tamaño de página debe ser mayor a 0", nameof(pageSize));

            var totalLavados = await ObtenerTotalLavados(estados, clienteId, vehiculoId, fechaDesde, fechaHasta, precioDesde, precioHasta, estadosPago, estadosRetiro);
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
            List<string>? estadosRetiro = null,
            int pageNumber = 1,
            int pageSize = 10,
            string? sortBy = null,
            string? sortOrder = null)
        {
            var baseFiltrada = await ObtenerLavadosFiltrados(estados, clienteId, vehiculoId, fechaDesde, fechaHasta, precioDesde, precioHasta, estadosPago, estadosRetiro, sortBy, sortOrder);

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
            List<string>? estadosPago,
            List<string>? estadosRetiro)
        {
            var baseFiltrada = await ObtenerLavadosFiltrados(estados, clienteId, vehiculoId, fechaDesde, fechaHasta, precioDesde, precioHasta, estadosPago, estadosRetiro, ORDEN_DEFECTO, DIRECCION_DEFECTO);

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

            await ValidarCapacidadConcurrente();
            var config = await _configuracionService.ObtenerConfiguracion();

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
            await InsumosTransacciones.EjecutarAsync(_firestore, async transaction =>
            {
                var capacity = await LeerCapacidadCreacion(transaction, 1, config);
                var membership = await ValidarAsignacionesEnTransaccion(transaction, new[] { lavado });
                AvanzarCapacidadCreacion(transaction, capacity);
                if (membership != null) EmployeeMembershipVersion.Advance(transaction, membership);
                transaction.Create(lavadoRef, lavadoData);
            });

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
            await InsumosTransacciones.EjecutarAsync(_firestore, async transaction =>
            {
                var coordinator = await transaction.GetSnapshotAsync(_firestore.Collection("coordinacion").Document("turnos"));
                var snapshot = await transaction.GetSnapshotAsync(lavadoRef);
                if (snapshot.Exists && snapshot.ContainsField("TurnoId"))
                {
                    var turnoId = snapshot.GetValue<string>("TurnoId");
                    lavadoData["TurnoId"] = turnoId;
                    if (!string.IsNullOrWhiteSpace(turnoId))
                    {
                        var turnoRef = _firestore.Collection("turnos").Document(turnoId);
                        if (lavado.Estado is ESTADO_REALIZADO or ESTADO_REALIZADO_PARCIALMENTE)
                        {
                            transaction.Update(turnoRef, "Estado", "Completado");
                        }
                        else if (lavado.Estado == ESTADO_CANCELADO)
                        {
                            transaction.Update(turnoRef, new Dictionary<string, object>
                            {
                                ["Estado"] = "Cancelado",
                                ["MotivoCancelacion"] = lavado.MotivoCancelacion ?? "Lavado cancelado"
                            });
                        }
                    }
                }
                var membership = await ValidarAsignacionesEnTransaccion(transaction, new[] { lavado });
                AvanzarCapacidadCreacion(transaction, coordinator);
                if (membership != null) EmployeeMembershipVersion.Advance(transaction, membership);
                transaction.Set(lavadoRef, lavadoData, SetOptions.Overwrite);
            });
        }

        /// <summary>
        /// Finaliza un lavado.
        /// </summary>
        public async Task FinalizarLavado(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("El ID del lavado no puede estar vacío", nameof(id));

            var referencia = _firestore.Collection(COLLECTION_NAME).Document(id);
            await InsumosTransacciones.EjecutarAsync(_firestore, async transaction =>
            {
            var coordinator = await transaction.GetSnapshotAsync(_firestore.Collection("coordinacion").Document("turnos"));
            var snapshot = await transaction.GetSnapshotAsync(referencia);
            if (!snapshot.Exists)
                throw new ArgumentException("No se encontró el lavado", nameof(id));
            DocumentReference? turnoRef = null;
            if (snapshot.ContainsField("TurnoId"))
            {
                var fields = snapshot.ToDictionary();
                if (fields["TurnoId"] is not string turnoId || string.IsNullOrWhiteSpace(turnoId) || turnoId.Contains('/') || turnoId == "." || turnoId == "..")
                    throw new InvalidOperationException("El vínculo con el turno es inválido.");
                turnoRef = _firestore.Collection("turnos").Document(turnoId);
                var turno = await transaction.GetSnapshotAsync(turnoRef);
                if (!turno.Exists || !turno.ToDictionary().TryGetValue("LavadoId", out var lavadoId) || lavadoId is not string linkedId || linkedId != id ||
                    !turno.ToDictionary().TryGetValue("Estado", out var estado) || estado is not string estadoTurno ||
                    !fields.TryGetValue("Estado", out var estadoLavado) || estadoLavado is not string estadoActual)
                    throw new InvalidOperationException("El vínculo con el turno es inválido.");
                if (estadoTurno == "Completado" && estadoActual is ESTADO_REALIZADO or ESTADO_REALIZADO_PARCIALMENTE)
                    return;
                if (estadoTurno != "EnProceso" || estadoActual != ESTADO_EN_PROCESO)
                    throw new InvalidOperationException("El vínculo con el turno es inválido.");
            }
            var lavado = MapearDocumentoALavado(snapshot);
            if (turnoRef != null && (lavado.Servicios.Count == 0 || lavado.Servicios.All(s => s.Estado == "Cancelado")))
                throw new InvalidOperationException("No se puede completar el turno con todos los servicios cancelados.");

            // Verificar si todos los servicios están finalizados
            var serviciosPendientes = lavado.Servicios.Where(s => s.Estado == "Pendiente" || s.Estado == "EnProceso").ToList();
            
            if (serviciosPendientes.Any())
            {
                if (turnoRef != null)
                    throw new InvalidOperationException("No se puede finalizar el lavado porque hay servicios pendientes o en proceso.");
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
                // Verificar si algún servicio fue cancelado o finalizado parcialmente
                var serviciosCancelados = lavado.Servicios.Where(s => s.Estado == "Cancelado").ToList();
                if (serviciosCancelados.Any() || lavado.Servicios.Any(s => s.Estado == "RealizadoParcialmente"))
                {
                    lavado.Estado = ESTADO_REALIZADO_PARCIALMENTE;
                }
                else
                {
                    lavado.Estado = ESTADO_REALIZADO;
                }
            }

            lavado.TiempoFinalizacion = DateTime.UtcNow;
            ValidarLavado(lavado);
            var datos = CrearDiccionarioLavado(lavado);
            transaction.Update(referencia, new Dictionary<string, object>
            {
                ["Estado"] = datos["Estado"],
                ["TiempoFinalizacion"] = datos["TiempoFinalizacion"]
            });
            if (turnoRef != null)
            {
                transaction.Update(turnoRef, "Estado", "Completado");
                AvanzarCapacidadCreacion(transaction, coordinator);
            }
            });
        }

        private async Task<DocumentReference?> LeerTurnoParaFinalizacion(Transaction transaction, DocumentSnapshot snapshot)
        {
            if (!snapshot.ContainsField("TurnoId")) return null;
            var fields = snapshot.ToDictionary();
            if (fields["TurnoId"] is not string id || string.IsNullOrWhiteSpace(id) || id.Contains('/') || id is "." or "..")
                throw new InvalidOperationException("El vínculo con el turno es inválido.");
            var reference = _firestore.Collection("turnos").Document(id);
            var turno = await transaction.GetSnapshotAsync(reference);
            var data = turno.Exists ? turno.ToDictionary() : new Dictionary<string, object>();
            if (!data.TryGetValue("LavadoId", out var linked) || linked is not string washId || washId != snapshot.Id ||
                !data.TryGetValue("Estado", out var state) || state is not string turnoEstado ||
                !fields.TryGetValue("Estado", out var washState) || washState is not string lavadoEstado ||
                !((turnoEstado == "EnProceso" && lavadoEstado == ESTADO_EN_PROCESO) ||
                  (turnoEstado == "Completado" && lavadoEstado is ESTADO_REALIZADO or ESTADO_REALIZADO_PARCIALMENTE)))
                throw new InvalidOperationException("El vínculo con el turno es inválido.");
            return reference;
        }

        /// <summary>
        /// Finaliza un servicio específico dentro de un lavado.
        /// </summary>
        public async Task<ResultadoOperacion> ValidarFinalizacionServicio(string lavadoId, string servicioId, string? consumoRealJson = null, string? justificacionDesvio = null)
        {
            var actual = await ObtenerLavado(lavadoId);
            if (actual == null) return ResultadoOperacion.CrearError("No se encontró el lavado");
            var seleccionado = actual.Servicios.FirstOrDefault(s => s.ServicioId == servicioId);
            if (seleccionado == null) return ResultadoOperacion.CrearError("No se encontró el servicio en el lavado");
            if (seleccionado.Estado is "Realizado" or "RealizadoParcialmente") return ResultadoOperacion.CrearExito("El servicio ya estaba finalizado.");
            if (seleccionado.Etapas.Any()) return ResultadoOperacion.CrearExito("Validación delegada a etapas.");
            if (seleccionado.Estado != "EnProceso") return ResultadoOperacion.CrearError("El servicio no está en proceso.");
            return await _stockService.ValidarConsumoFinalizacion($"{lavadoId}_{servicioId}_servicio", seleccionado.Receta, consumoRealJson, justificacionDesvio);
        }

        public async Task<ResultadoOperacion> ValidarFinalizacionEtapa(string lavadoId, string servicioId, string etapaId, string? consumoRealJson = null, string? justificacionDesvio = null)
        {
            var actual = await ObtenerLavado(lavadoId);
            if (actual == null) return ResultadoOperacion.CrearError("No se encontró el lavado");
            var servicio = actual.Servicios.FirstOrDefault(s => s.ServicioId == servicioId);
            if (servicio == null) return ResultadoOperacion.CrearError("No se encontró el servicio en el lavado");
            var etapa = servicio.Etapas.FirstOrDefault(e => e.EtapaId == etapaId);
            if (etapa == null) return ResultadoOperacion.CrearError("No se encontró la etapa en el servicio");
            if (etapa.Estado == "Realizado") return ResultadoOperacion.CrearExito("La etapa ya estaba finalizada.");
            if (etapa.Estado != "EnProceso") return ResultadoOperacion.CrearError("La etapa no está en proceso.");
            return await _stockService.ValidarConsumoFinalizacion($"{lavadoId}_{servicioId}_{etapaId}", etapa.Receta, consumoRealJson, justificacionDesvio);
        }

        public async Task FinalizarServicio(string lavadoId, string servicioId, string? consumoRealJson = null, string? justificacionDesvio = null)
        {
            var referencia = _firestore.Collection(COLLECTION_NAME).Document(lavadoId);
            var resultado = await InsumosTransacciones.EjecutarAsync(_firestore, async transaction =>
            {
                var coordinator = await transaction.GetSnapshotAsync(_firestore.Collection("coordinacion").Document("turnos"));
                var snapshot = await transaction.GetSnapshotAsync(referencia);
                if (!snapshot.Exists) throw new ArgumentException("No se encontró el lavado", nameof(lavadoId));
                var turnoRef = await LeerTurnoParaFinalizacion(transaction, snapshot);
                var actual = MapearDocumentoALavado(snapshot);
                var seleccionado = actual.Servicios.FirstOrDefault(s => s.ServicioId == servicioId);
                if (seleccionado == null) throw new ArgumentException("No se encontró el servicio en el lavado", nameof(servicioId));
                if (seleccionado.Estado is "Realizado" or "RealizadoParcialmente") return new List<StockService.ConsumoReceta>();
                if (turnoRef != null && actual.Estado != ESTADO_EN_PROCESO)
                    throw new InvalidOperationException("El vínculo con el turno es inválido.");
                if (turnoRef != null && seleccionado.Estado == "Cancelado")
                    throw new InvalidOperationException("El servicio está cancelado.");
                var conEtapas = seleccionado.Etapas.Any();
                if (conEtapas && seleccionado.Etapas.Any(e => e.Estado is "Pendiente" or "EnProceso"))
                    throw new InvalidOperationException("No se puede finalizar el servicio porque hay etapas pendientes o en proceso.");
                if (!conEtapas && seleccionado.Estado != "EnProceso") throw new InvalidOperationException("El servicio no está en proceso.");
                StockService.RecetaServicio? receta = null;
                var consumos = new List<StockService.ConsumoReceta>();
                if (!conEtapas)
                {
                    var consumoReal = string.IsNullOrWhiteSpace(consumoRealJson)
                        ? null
                        : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, decimal>>(consumoRealJson);
                    if (consumoReal != null && consumoReal.Any(x => seleccionado.Receta.All(r => r.InsumoId != x.Key) || x.Value < 0))
                        throw new InvalidOperationException("El consumo real contiene insumos o cantidades inválidas.");
                    var clave = $"{lavadoId}_{servicioId}_servicio";
                    receta = await _stockService.LeerRecetaServicio(transaction, clave, seleccionado.Receta, true);
                    var consumiblesIds = receta.Consumos.Select(c => c.InsumoId).ToHashSet(StringComparer.Ordinal);
                    var hayDesvio = consumoReal != null && seleccionado.Receta.Where(r => consumiblesIds.Contains(r.InsumoId)).Any(r =>
                        consumoReal.TryGetValue(r.InsumoId, out var real) && real != r.DosisTeorica);
                    if (hayDesvio && string.IsNullOrWhiteSpace(justificacionDesvio))
                        throw new InvalidOperationException("Debe informar la justificación del desvío.");
                    var realesConvertidos = consumoReal == null ? new List<StockService.ConsumoReceta>() : await _stockService.ConvertirConsumosReales(transaction, seleccionado.Receta, consumoReal);
                    consumos = receta.Consumos.Select(c => realesConvertidos.FirstOrDefault(r => r.InsumoId == c.InsumoId) ?? c).Where(c => c.Cantidad > 0).ToList();
                }
                var siguiente = await LeerSiguienteServicio(transaction, actual, receta, consumos);
                if (receta != null)
                {
                    await _stockService.ConsumirEnTransaccion(transaction, $"{lavadoId}_{servicioId}_servicio", consumos, justificacionDesvio ?? "Consumo de servicio sin etapas", receta);
                    StockService.EscribirReservasServicio(transaction, receta, true);
                }
                var tieneRealizadas = seleccionado.Etapas.Any(e => e.Estado == "Realizado");
                var tieneCanceladas = seleccionado.Etapas.Any(e => e.Estado == "Cancelado");
                if (conEtapas && !tieneRealizadas && tieneCanceladas)
                {
                    seleccionado.Estado = "Cancelado";
                    seleccionado.MotivoCancelacion = "Todas las etapas fueron canceladas";
                }
                else
                {
                    seleccionado.Estado = conEtapas && tieneCanceladas ? "RealizadoParcialmente" : "Realizado";
                }
                seleccionado.TiempoFinalizacion = DateTime.UtcNow;
                AplicarSiguienteServicio(transaction, siguiente);
                var cerrado = FinalizarLavadoSiCorresponde(actual);
                if (turnoRef != null && cerrado && actual.Estado is ESTADO_REALIZADO or ESTADO_REALIZADO_PARCIALMENTE)
                {
                    transaction.Update(turnoRef, "Estado", "Completado");
                    AvanzarCapacidadCreacion(transaction, coordinator);
                }
                ValidarLavado(actual);
                var datos = CrearDiccionarioLavado(actual);
                transaction.Update(referencia, new Dictionary<string, object>
                {
                    ["Servicios"] = datos["Servicios"],
                    ["Estado"] = datos["Estado"],
                    ["TiempoFinalizacion"] = datos["TiempoFinalizacion"]
                });
                return consumos;
            });
            await _stockService.EvaluarConsumos(resultado.Select(x => x.InsumoId));
        }

        /// <summary>
        /// Finaliza una etapa específica dentro de un servicio.
        /// </summary>
        public async Task<bool> FinalizarEtapa(string lavadoId, string servicioId, string etapaId, string? consumoRealJson = null, string? justificacionDesvio = null)
        {
            var consumoReal = string.IsNullOrWhiteSpace(consumoRealJson)
                ? null
                : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, decimal>>(consumoRealJson);
            var referencia = _firestore.Collection(COLLECTION_NAME).Document(lavadoId);
            var consumidos = await InsumosTransacciones.EjecutarAsync(_firestore, async transaction =>
            {
                var coordinator = await transaction.GetSnapshotAsync(_firestore.Collection("coordinacion").Document("turnos"));
                var snapshot = await transaction.GetSnapshotAsync(referencia);
                if (!snapshot.Exists) throw new ArgumentException("No se encontró el lavado", nameof(lavadoId));
                var turnoRef = await LeerTurnoParaFinalizacion(transaction, snapshot);
                var lavado = MapearDocumentoALavado(snapshot);
                var servicio = lavado.Servicios.FirstOrDefault(s => s.ServicioId == servicioId)
                    ?? throw new ArgumentException("No se encontró el servicio en el lavado", nameof(servicioId));
                var etapa = servicio.Etapas.FirstOrDefault(e => e.EtapaId == etapaId)
                    ?? throw new ArgumentException("No se encontró la etapa en el servicio", nameof(etapaId));
                if (etapa.Estado == "Realizado") return (Finalizada: false, Consumos: new List<StockService.ConsumoReceta>());
                if (turnoRef != null && lavado.Estado != ESTADO_EN_PROCESO)
                    throw new InvalidOperationException("El vínculo con el turno es inválido.");
                if (etapa.Estado != "EnProceso") throw new InvalidOperationException("La etapa no está en proceso.");
                if (consumoReal != null && consumoReal.Any(x => etapa.Receta.All(r => r.InsumoId != x.Key) || x.Value < 0))
                    throw new InvalidOperationException("El consumo real contiene insumos o cantidades inválidas.");
                var clave = $"{lavadoId}_{servicioId}_{etapaId}";
                var receta = await _stockService.LeerRecetaServicio(transaction, clave, etapa.Receta, true);
                var consumiblesIds = receta.Consumos.Select(c => c.InsumoId).ToHashSet(StringComparer.Ordinal);
                var hayDesvio = consumoReal != null && etapa.Receta.Where(r => consumiblesIds.Contains(r.InsumoId)).Any(r =>
                    consumoReal.TryGetValue(r.InsumoId, out var real) && real != r.DosisTeorica);
                if (hayDesvio && string.IsNullOrWhiteSpace(justificacionDesvio))
                    throw new InvalidOperationException("Debe informar la justificación del desvío.");
                var realesConvertidos = consumoReal == null ? new List<StockService.ConsumoReceta>() : await _stockService.ConvertirConsumosReales(transaction, etapa.Receta, consumoReal);
                var cantidades = receta.Consumos.Select(c => realesConvertidos.FirstOrDefault(r => r.InsumoId == c.InsumoId) ?? c).Where(c => c.Cantidad > 0).ToList();
                var siguiente = servicio.Etapas.FirstOrDefault(e => e.Estado == "Pendiente");
                StockService.RecetaServicio? recetaSiguiente = null;
                if (siguiente != null && siguiente.Receta.All(r => r.Modalidad != "Tipo"))
                {
                    try
                    {
                        recetaSiguiente = await _stockService.LeerRecetaServicio(transaction,
                            $"{lavadoId}_{servicioId}_{siguiente.EtapaId}", siguiente.Receta, false, receta, cantidades);
                    }
                    catch (StockService.DisponibilidadInsuficienteException) { }
                }
                var terminaServicio = siguiente == null && !servicio.Etapas.Any(e => e != etapa && e.Estado == "EnProceso");
                var siguienteServicio = terminaServicio
                    ? await LeerSiguienteServicio(transaction, lavado, receta, cantidades)
                    : null;
                await _stockService.ConsumirEnTransaccion(transaction, clave, cantidades, justificacionDesvio ?? string.Empty, receta);
                StockService.EscribirReservasServicio(transaction, receta, true);
                if (recetaSiguiente != null) StockService.EscribirReservasServicio(transaction, recetaSiguiente, false);
                etapa.Estado = "Realizado";
                etapa.TiempoFinalizacion = DateTime.UtcNow;
                var pendientes = servicio.Etapas.Where(e => e.Estado is "Pendiente" or "EnProceso").ToList();
                if (pendientes.Count == 0)
                {
                    servicio.Estado = servicio.Etapas.Any(e => e.Estado == "Cancelado") ? "RealizadoParcialmente" : "Realizado";
                    servicio.TiempoFinalizacion = DateTime.UtcNow;
                    AplicarSiguienteServicio(transaction, siguienteServicio);
                }
                else
                {
                    if (siguiente != null && recetaSiguiente != null)
                    {
                        siguiente.Estado = "EnProceso";
                        siguiente.TiempoInicio = DateTime.UtcNow;
                    }
                }
                var cerrado = FinalizarLavadoSiCorresponde(lavado);
                if (turnoRef != null && cerrado && lavado.Estado is ESTADO_REALIZADO or ESTADO_REALIZADO_PARCIALMENTE)
                {
                    transaction.Update(turnoRef, "Estado", "Completado");
                    AvanzarCapacidadCreacion(transaction, coordinator);
                }
                ValidarLavado(lavado);
                var datos = CrearDiccionarioLavado(lavado);
                transaction.Update(referencia, new Dictionary<string, object>
                {
                    ["Servicios"] = datos["Servicios"], ["Estado"] = datos["Estado"],
                    ["TiempoFinalizacion"] = datos["TiempoFinalizacion"]
                });
                return (Finalizada: true, Consumos: cantidades);
            });
            await _stockService.EvaluarConsumos(consumidos.Consumos.Select(x => x.InsumoId));
            return consumidos.Finalizada;
        }

        /// <summary>
        /// Cancela un lavado completo.
        /// </summary>
        public async Task CancelarLavado(string id, string motivo, IReadOnlyDictionary<string, decimal>? consumosNoRegistrados = null)
        {
            if (string.IsNullOrWhiteSpace(motivo))
                throw new ArgumentException("El motivo de cancelación es obligatorio", nameof(motivo));

            var referencia = _firestore.Collection(COLLECTION_NAME).Document(id);
            var serviciosIds = await InsumosTransacciones.EjecutarAsync(_firestore, async transaction =>
            {
                var coordinator = await transaction.GetSnapshotAsync(_firestore.Collection("coordinacion").Document("turnos"));
                var snapshot = await transaction.GetSnapshotAsync(referencia);
                if (!snapshot.Exists) throw new ArgumentException("No se encontró el lavado", nameof(id));
                var turnoRef = await LeerTurnoParaFinalizacion(transaction, snapshot);
                var lavado = MapearDocumentoALavado(snapshot);
                var mermas = await _stockService.LeerMermasPendientes(transaction, ReferenciasConsumos(id, lavado.Servicios));
                if (lavado.Estado == ESTADO_CANCELADO) { _stockService.EscribirMermasPendientes(transaction, mermas, motivo); return new List<string>(); }
                var activos = lavado.Servicios.Where(s => s.Estado is "Pendiente" or "EnProceso").ToList();
                var reservas = await LeerReservasCancelacion(transaction, id, activos);
                ValidarProductosCancelacion(activos, consumosNoRegistrados);
                if (consumosNoRegistrados != null)
                    await _stockService.ConsumirMermaEnTransaccion(transaction, id, consumosNoRegistrados, motivo, reservas);
                _stockService.EscribirMermasPendientes(transaction, mermas, motivo);
                foreach (var servicio in activos) CancelarServicioEnMemoria(servicio, motivo);
                lavado.Estado = ESTADO_CANCELADO;
                lavado.MotivoCancelacion = motivo;
                lavado.TiempoFinalizacion ??= DateTime.UtcNow;
                lavado.Pago ??= new PagoLavado();
                if (lavado.Pago.Estado != "Pagado") lavado.Pago.Estado = "Cancelado";
                if (turnoRef != null)
                {
                    transaction.Update(turnoRef, new Dictionary<string, object>
                    {
                        ["Estado"] = "Cancelado",
                        ["MotivoCancelacion"] = motivo
                    });
                    AvanzarCapacidadCreacion(transaction, coordinator);
                }
                ValidarLavado(lavado);
                StockService.EscribirReservasServicio(transaction, reservas, true);
                var datos = CrearDiccionarioLavado(lavado);
                transaction.Update(referencia, new Dictionary<string, object>
                {
                    ["Servicios"] = datos["Servicios"], ["Estado"] = datos["Estado"],
                    ["MotivoCancelacion"] = datos["MotivoCancelacion"],
                    ["TiempoFinalizacion"] = datos["TiempoFinalizacion"], ["Pago"] = datos["Pago"]
                });
                return lavado.Servicios.Select(s => s.ServicioId).ToList();
            });
            if (consumosNoRegistrados != null && serviciosIds.Count > 0)
                await _stockService.EvaluarConsumos(consumosNoRegistrados.Where(x => x.Value > 0).Select(x => x.Key));
        }

        /// <summary>
        /// Cancela un servicio específico dentro de un lavado.
        /// </summary>
        public async Task CancelarServicio(string lavadoId, string servicioId, string motivo, IReadOnlyDictionary<string, decimal>? consumosNoRegistrados = null)
        {
            if (string.IsNullOrWhiteSpace(motivo))
                throw new ArgumentException("El motivo de cancelación es obligatorio", nameof(motivo));

            var referencia = _firestore.Collection(COLLECTION_NAME).Document(lavadoId);
            var sinEtapas = await InsumosTransacciones.EjecutarAsync(_firestore, async transaction =>
            {
                var coordinator = await transaction.GetSnapshotAsync(_firestore.Collection("coordinacion").Document("turnos"));
                var snapshot = await transaction.GetSnapshotAsync(referencia);
                if (!snapshot.Exists) throw new ArgumentException("No se encontró el lavado", nameof(lavadoId));
                var turnoRef = await LeerTurnoParaFinalizacion(transaction, snapshot);
                var actual = MapearDocumentoALavado(snapshot);
                var seleccionado = actual.Servicios.FirstOrDefault(s => s.ServicioId == servicioId);
                if (seleccionado == null) throw new ArgumentException("No se encontró el servicio en el lavado", nameof(servicioId));
                var mermas = await _stockService.LeerMermasPendientes(transaction, ReferenciasConsumos(lavadoId, new[] { seleccionado }));
                if (seleccionado.Estado == "Cancelado") { _stockService.EscribirMermasPendientes(transaction, mermas, motivo); return false; }
                var receta = await LeerReservasCancelacion(transaction, lavadoId, new[] { seleccionado });
                ValidarProductosCancelacion(new[] { seleccionado }, consumosNoRegistrados);
                if (consumosNoRegistrados != null)
                    await _stockService.ConsumirMermaEnTransaccion(transaction, $"{lavadoId}_{servicioId}", consumosNoRegistrados, motivo, receta);
                _stockService.EscribirMermasPendientes(transaction, mermas, motivo);
                StockService.EscribirReservasServicio(transaction, receta, true);
                CancelarServicioEnMemoria(seleccionado, motivo);
                if (actual.Servicios.All(s => s.Estado == "Cancelado"))
                {
                    actual.Estado = ESTADO_CANCELADO;
                    actual.MotivoCancelacion = "Todos los servicios fueron cancelados";
                    actual.TiempoFinalizacion = DateTime.UtcNow;
                    actual.Pago ??= new PagoLavado();
                    if (actual.Pago.Estado != "Pagado") actual.Pago.Estado = "Cancelado";
                    if (turnoRef != null)
                    {
                        transaction.Update(turnoRef, new Dictionary<string, object>
                        {
                            ["Estado"] = "Cancelado",
                            ["MotivoCancelacion"] = "Todos los servicios fueron cancelados"
                        });
                        AvanzarCapacidadCreacion(transaction, coordinator);
                    }
                }
                else
                {
                    var cerrado = FinalizarLavadoSiCorresponde(actual);
                    if (turnoRef != null && cerrado && actual.Estado is ESTADO_REALIZADO or ESTADO_REALIZADO_PARCIALMENTE)
                    {
                        transaction.Update(turnoRef, "Estado", "Completado");
                        AvanzarCapacidadCreacion(transaction, coordinator);
                    }
                }
                ValidarLavado(actual);
                var datos = CrearDiccionarioLavado(actual);
                transaction.Update(referencia, new Dictionary<string, object>
                {
                    ["Servicios"] = datos["Servicios"], ["Estado"] = datos["Estado"],
                    ["MotivoCancelacion"] = datos["MotivoCancelacion"],
                    ["TiempoFinalizacion"] = datos["TiempoFinalizacion"], ["Pago"] = datos["Pago"]
                });
                return true;
            });
            if (sinEtapas && consumosNoRegistrados != null)
                await _stockService.EvaluarConsumos(consumosNoRegistrados.Where(x => x.Value > 0).Select(x => x.Key));
        }

        private static IEnumerable<string> ReferenciasConsumos(string lavadoId, IEnumerable<ServicioEnLavado> servicios) =>
            servicios.SelectMany(s => s.Etapas.Any()
                ? s.Etapas.Select(e => $"{lavadoId}_{s.ServicioId}_{e.EtapaId}")
                : new[] { $"{lavadoId}_{s.ServicioId}_servicio" });

        private static void ValidarProductosCancelacion(IEnumerable<ServicioEnLavado> servicios, IReadOnlyDictionary<string, decimal>? cantidades)
        {
            if (cantidades == null) return;
            var productos = servicios.Where(s => s.Estado == "EnProceso")
                .SelectMany(s => s.Etapas.Any()
                    ? s.Etapas.Where(e => e.Estado == "EnProceso").SelectMany(e => e.Receta)
                    : s.Receta)
                .Select(r => r.InsumoId).Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.Ordinal);
            if (cantidades.Keys.Any(id => !productos.Contains(id)))
                throw new InvalidOperationException("El consumo adicional contiene productos que no pertenecen a la operación activa.");
        }

        private async Task<StockService.RecetaServicio> LeerReservasCancelacion(
            Transaction transaction, string lavadoId, IEnumerable<ServicioEnLavado> servicios)
        {
            var reservas = new List<StockService.ReservaServicio>();
            foreach (var servicio in servicios)
            {
                var recetas = servicio.Etapas.Any()
                    ? servicio.Etapas.Where(e => e.Estado is "Pendiente" or "EnProceso")
                        .Select(e => (Clave: $"{lavadoId}_{servicio.ServicioId}_{e.EtapaId}", e.Receta))
                    : new[] { (Clave: $"{lavadoId}_{servicio.ServicioId}_servicio", servicio.Receta) };
                foreach (var (clave, receta) in recetas)
                {
                    var leida = await _stockService.LeerRecetaServicio(transaction, clave, receta, true);
                    foreach (var reserva in leida.Reservas)
                    {
                        var anterior = reservas.Where(r => r.Contador.Id == reserva.Contador.Id && r.Existe && !r.Liberada)
                            .Sum(r => r.Cantidad);
                        if (reserva.Existe && !reserva.Liberada && reserva.Total - anterior < reserva.Cantidad)
                            throw new InvalidOperationException("El contador de reservas es inconsistente.");
                        reservas.Add(reserva with { Total = reserva.Total - anterior });
                    }
                }
            }
            return new StockService.RecetaServicio(new(), reservas);
        }

        private static void CancelarServicioEnMemoria(ServicioEnLavado servicio, string motivo)
        {
            servicio.Estado = "Cancelado";
            servicio.MotivoCancelacion = motivo;
            servicio.TiempoFinalizacion = DateTime.UtcNow;
            foreach (var etapa in servicio.Etapas.Where(e => e.Estado is "Pendiente" or "EnProceso"))
            {
                etapa.Estado = "Cancelado";
                etapa.MotivoCancelacion = motivo;
                etapa.TiempoFinalizacion = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Cancela una etapa específica dentro de un servicio.
        /// </summary>
        public async Task CancelarEtapa(string lavadoId, string servicioId, string etapaId, string motivo, IReadOnlyDictionary<string, decimal>? consumosNoRegistrados = null)
        {
            if (string.IsNullOrWhiteSpace(motivo))
                throw new ArgumentException("El motivo de cancelación es obligatorio", nameof(motivo));

            var referencia = _firestore.Collection(COLLECTION_NAME).Document(lavadoId);
            await InsumosTransacciones.EjecutarAsync(_firestore, async transaction =>
            {
                var coordinator = await transaction.GetSnapshotAsync(_firestore.Collection("coordinacion").Document("turnos"));
                var snapshot = await transaction.GetSnapshotAsync(referencia);
                if (!snapshot.Exists) throw new ArgumentException("No se encontró el lavado", nameof(lavadoId));
                var turnoRef = await LeerTurnoParaFinalizacion(transaction, snapshot);
                var lavado = MapearDocumentoALavado(snapshot);
                var servicio = lavado.Servicios.FirstOrDefault(s => s.ServicioId == servicioId)
                    ?? throw new ArgumentException("No se encontró el servicio en el lavado", nameof(servicioId));
                var etapa = servicio.Etapas.FirstOrDefault(e => e.EtapaId == etapaId)
                    ?? throw new ArgumentException("No se encontró la etapa en el servicio", nameof(etapaId));
                if (etapa.Estado == "Cancelado") return;
                if (etapa.Estado == "Realizado") throw new InvalidOperationException("No se puede cancelar una etapa que ya fue realizada.");
                var receta = await _stockService.LeerRecetaServicio(transaction, $"{lavadoId}_{servicioId}_{etapaId}", etapa.Receta, true);
                if (consumosNoRegistrados != null)
                {
                    var productosEtapa = etapa.Receta.Select(r => r.InsumoId).Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet(StringComparer.Ordinal);
                    if (consumosNoRegistrados.Keys.Any(id => !productosEtapa.Contains(id)))
                        throw new InvalidOperationException("El consumo adicional contiene productos que no pertenecen a la operación activa.");
                    await _stockService.ConsumirMermaEnTransaccion(transaction, $"{lavadoId}_{servicioId}_{etapaId}", consumosNoRegistrados, motivo, receta);
                }
                StockService.EscribirReservasServicio(transaction, receta, true);
                etapa.Estado = "Cancelado";
                etapa.MotivoCancelacion = motivo;
                etapa.TiempoFinalizacion = DateTime.UtcNow;

                var pendientes = servicio.Etapas.Where(e => e.Estado is "Pendiente" or "EnProceso").ToList();
                if (pendientes.Count == 0)
                {
                    var tieneRealizadas = servicio.Etapas.Any(e => e.Estado == "Realizado");
                    var tieneCanceladas = servicio.Etapas.Any(e => e.Estado == "Cancelado");

                    if (!tieneRealizadas && tieneCanceladas)
                    {
                        servicio.Estado = "Cancelado";
                        servicio.MotivoCancelacion = "Todas las etapas fueron canceladas";
                    }
                    else if (tieneRealizadas && tieneCanceladas)
                    {
                        servicio.Estado = "RealizadoParcialmente";
                    }
                    else
                    {
                        servicio.Estado = "Realizado";
                    }
                    servicio.TiempoFinalizacion = DateTime.UtcNow;
                }

                if (lavado.Servicios.All(s => s.Estado == "Cancelado"))
                {
                    lavado.Estado = ESTADO_CANCELADO;
                    lavado.MotivoCancelacion = "Todos los servicios fueron cancelados";
                    lavado.TiempoFinalizacion = DateTime.UtcNow;
                    lavado.Pago ??= new PagoLavado();
                    if (lavado.Pago.Estado != "Pagado") lavado.Pago.Estado = "Cancelado";
                    if (turnoRef != null)
                    {
                        transaction.Update(turnoRef, new Dictionary<string, object>
                        {
                            ["Estado"] = "Cancelado",
                            ["MotivoCancelacion"] = "Todos los servicios fueron cancelados"
                        });
                        AvanzarCapacidadCreacion(transaction, coordinator);
                    }
                }
                else
                {
                    var cerrado = FinalizarLavadoSiCorresponde(lavado);
                    if (turnoRef != null && cerrado && lavado.Estado is ESTADO_REALIZADO or ESTADO_REALIZADO_PARCIALMENTE)
                    {
                        transaction.Update(turnoRef, "Estado", "Completado");
                        AvanzarCapacidadCreacion(transaction, coordinator);
                    }
                }

                ValidarLavado(lavado);
                var datos = CrearDiccionarioLavado(lavado);
                transaction.Update(referencia, new Dictionary<string, object>
                {
                    ["Servicios"] = datos["Servicios"],
                    ["Estado"] = datos["Estado"],
                    ["MotivoCancelacion"] = datos["MotivoCancelacion"],
                    ["TiempoFinalizacion"] = datos["TiempoFinalizacion"],
                    ["Pago"] = datos["Pago"]
                });
            });
            if (consumosNoRegistrados != null)
                await _stockService.EvaluarConsumos(consumosNoRegistrados.Where(x => x.Value > 0).Select(x => x.Key));
        }

        /// <summary>
        /// Registra un pago para un lavado.
        /// </summary>
        public async Task RegistrarPago(string lavadoId, decimal monto, string medioPago, string? notas = null)
        {
            var referencia = _firestore.Collection(COLLECTION_NAME).Document(lavadoId);
            var nuevoPago = new DetallePago
            {
                Id = Guid.NewGuid().ToString(), Monto = monto, MedioPago = medioPago,
                Fecha = DateTime.UtcNow, Notas = notas
            };
            await InsumosTransacciones.EjecutarAsync(_firestore, async transaction =>
            {
                var snapshot = await transaction.GetSnapshotAsync(referencia);
                if (!snapshot.Exists)
                    throw new ArgumentException("No se encontró el lavado", nameof(lavadoId));
                var lavado = MapearDocumentoALavado(snapshot);
                if (monto <= 0)
                    throw new ArgumentException("El monto debe ser mayor a 0", nameof(monto));
                lavado.Pago ??= new PagoLavado();
                lavado.Pago.Pagos.Add(nuevoPago);
                lavado.Pago.MontoPagado = lavado.Pago.Pagos.Sum(p => p.Monto);
                if (lavado.Pago.MontoPagado >= lavado.Precio) lavado.Pago.Estado = "Pagado";
                else if (lavado.Pago.MontoPagado > 0) lavado.Pago.Estado = "Parcial";
                ValidarLavado(lavado);
                transaction.Update(referencia, "Pago", CrearDiccionarioLavado(lavado)["Pago"]);
            });
        }

        /// <summary>
        /// Inicia un servicio específico dentro de un lavado.
        /// </summary>
        public async Task IniciarServicio(string lavadoId, string servicioId, IReadOnlyDictionary<int, string>? seleccion = null)
        {
            var referencia = _firestore.Collection(COLLECTION_NAME).Document(lavadoId);
            await InsumosTransacciones.EjecutarAsync(_firestore, async transaction =>
            {
                var snapshot = await transaction.GetSnapshotAsync(referencia);
                if (!snapshot.Exists) throw new ArgumentException("No se encontró el lavado", nameof(lavadoId));
                var turnoRef = await LeerTurnoParaFinalizacion(transaction, snapshot);
                var lavado = MapearDocumentoALavado(snapshot);
                if (turnoRef != null && lavado.Estado != ESTADO_EN_PROCESO)
                    throw new InvalidOperationException("El vínculo con el turno es inválido.");
                var servicio = lavado.Servicios.FirstOrDefault(s => s.ServicioId == servicioId);
                if (servicio == null) throw new ArgumentException("No se encontró el servicio en el lavado", nameof(servicioId));
                if (servicio.Estado == "EnProceso" && seleccion != null &&
                    seleccion.Count == servicio.Receta.Count(r => r.Modalidad == "Tipo") &&
                    seleccion.All(x => x.Key >= 0 && x.Key < servicio.Receta.Count && servicio.Receta[x.Key].Modalidad == "Tipo" && servicio.Receta[x.Key].InsumoId == x.Value)) return;
                if (servicio.Estado != "Pendiente") throw new InvalidOperationException("El servicio ya fue iniciado o finalizado.");
                var receta = await _stockService.LeerRecetaServicio(transaction, $"{lavadoId}_{servicioId}_servicio", servicio.Receta, false, seleccion: seleccion);
                StockService.EscribirReservasServicio(transaction, receta, false);
                servicio.Estado = "EnProceso";
                servicio.TiempoInicio = DateTime.UtcNow;
                ValidarLavado(lavado);
                transaction.Update(referencia, "Servicios", CrearDiccionarioLavado(lavado)["Servicios"]);
            });
        }

        /// <summary>
        /// Inicia una etapa específica dentro de un servicio.
        /// </summary>
        public async Task IniciarEtapa(string lavadoId, string servicioId, string etapaId, IReadOnlyDictionary<int, string>? seleccion = null)
        {
            var referencia = _firestore.Collection(COLLECTION_NAME).Document(lavadoId);
            await InsumosTransacciones.EjecutarAsync(_firestore, async transaction =>
            {
                var snapshot = await transaction.GetSnapshotAsync(referencia);
                if (!snapshot.Exists) throw new ArgumentException("No se encontró el lavado", nameof(lavadoId));
                var turnoRef = await LeerTurnoParaFinalizacion(transaction, snapshot);
                var lavado = MapearDocumentoALavado(snapshot);
                if (turnoRef != null && lavado.Estado != ESTADO_EN_PROCESO)
                    throw new InvalidOperationException("El vínculo con el turno es inválido.");
                var servicio = lavado.Servicios.FirstOrDefault(s => s.ServicioId == servicioId)
                    ?? throw new ArgumentException("No se encontró el servicio en el lavado", nameof(servicioId));
                var etapa = servicio.Etapas.FirstOrDefault(e => e.EtapaId == etapaId)
                    ?? throw new ArgumentException("No se encontró la etapa en el servicio", nameof(etapaId));
                if (etapa.Estado == "EnProceso" && seleccion != null &&
                    seleccion.Count == etapa.Receta.Count(r => r.Modalidad == "Tipo") &&
                    seleccion.All(x => x.Key >= 0 && x.Key < etapa.Receta.Count && etapa.Receta[x.Key].Modalidad == "Tipo" && etapa.Receta[x.Key].InsumoId == x.Value)) return;
                if (etapa.Estado != "Pendiente") throw new InvalidOperationException("La etapa ya fue iniciada o finalizada.");
                var etapaEnProceso = servicio.Etapas.FirstOrDefault(e => e.Estado == "EnProceso");
                if (etapaEnProceso != null && etapaEnProceso.EtapaId != etapaId)
                    throw new InvalidOperationException($"No se puede iniciar esta etapa porque la etapa '{etapaEnProceso.Nombre}' está en proceso. Las etapas deben completarse en orden.");

                // Las etapas de un mismo servicio no pueden ejecutarse fuera de orden:
                // Todas las etapas precedentes deben estar en estado terminal ("Realizado" o "Cancelado").
                var indiceEtapa = servicio.Etapas.IndexOf(etapa);
                for (int i = 0; i < indiceEtapa; i++)
                {
                    var anterior = servicio.Etapas[i];
                    if (anterior.Estado is not ("Realizado" or "Cancelado"))
                    {
                        throw new InvalidOperationException($"No se puede iniciar la etapa '{etapa.Nombre}' porque la etapa anterior '{anterior.Nombre}' aún no ha finalizado ni ha sido cancelada.");
                    }
                }
                var receta = await _stockService.LeerRecetaServicio(transaction, $"{lavadoId}_{servicioId}_{etapaId}", etapa.Receta, false, seleccion: seleccion);
                StockService.EscribirReservasServicio(transaction, receta, false);
                etapa.Estado = "EnProceso";
                etapa.TiempoInicio = DateTime.UtcNow;
                if (servicio.Estado == "Pendiente")
                {
                    servicio.Estado = "EnProceso";
                    servicio.TiempoInicio = DateTime.UtcNow;
                }
                ValidarLavado(lavado);
                transaction.Update(referencia, "Servicios", CrearDiccionarioLavado(lavado)["Servicios"]);
            });
        }

        #endregion

        #region Asignación de Empleados

        public record ResultadoAsignacionCreacion(int CantidadEmpleadosRequeridos, List<Empleado> EmpleadosAsignados);

        public async Task<ResultadoAsignacionCreacion> PrepararAsignacionCreacion(int? cantidadRequerida, IReadOnlySet<string>? excluidos = null)
        {
            var cantidad = cantidadRequerida ?? 1;
            var empleados = await AsignarEmpleadosAleatorios(cantidad, excluidos);
            return new ResultadoAsignacionCreacion(cantidad, empleados);
        }

        /// <summary>
        /// Asigna empleados aleatoriamente a un lavado basándose en disponibilidad.
        /// </summary>
        public async Task<List<Empleado>> AsignarEmpleadosAleatorios(int cantidad, IReadOnlySet<string>? excluidos = null)
        {
            var empleadosActivos = await _personalService.ObtenerEmpleados(
                estados: new List<string> { "Activo" },
                pageNumber: 1,
                pageSize: int.MaxValue
            );

            // Filtrar empleados que no tienen lavados activos
            var empleadosDisponibles = new List<Empleado>();
            foreach (var empleado in empleadosActivos)
            {
                if (excluidos?.Contains(empleado.Id) == true) continue;
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
                throw new HorarioOperacionException($"El lavadero está cerrado los días {diaSemana}.");
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
                throw new HorarioOperacionException("El lavadero está fuera de horario de operación.");
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

        private async Task<(ServicioEnLavado Servicio, EtapaEnLavado? Etapa, StockService.RecetaServicio Receta)?> LeerSiguienteServicio(
            Transaction transaction, Lavado lavado, StockService.RecetaServicio? liberacion, IReadOnlyCollection<StockService.ConsumoReceta> consumos)
        {
            var servicio = lavado.Servicios.Where(s => s.Estado == "Pendiente").OrderBy(s => s.Orden).FirstOrDefault();
            if (servicio == null) return null;
            var etapa = servicio.Etapas.FirstOrDefault();
            if ((etapa?.Receta ?? servicio.Receta).Any(r => r.Modalidad == "Tipo")) return null;
            try
            {
                var receta = await _stockService.LeerRecetaServicio(transaction,
                    $"{lavado.Id}_{servicio.ServicioId}_{(etapa == null ? "servicio" : etapa.EtapaId)}",
                    etapa?.Receta ?? servicio.Receta, false, liberacion, consumos);
                return (servicio, etapa, receta);
            }
            catch (StockService.DisponibilidadInsuficienteException) { return null; }
        }

        private static void AplicarSiguienteServicio(Transaction transaction,
            (ServicioEnLavado Servicio, EtapaEnLavado? Etapa, StockService.RecetaServicio Receta)? siguiente)
        {
            if (siguiente == null) return;
            var (servicio, etapa, receta) = siguiente.Value;
            StockService.EscribirReservasServicio(transaction, receta, false);
            servicio.Estado = "EnProceso";
            servicio.TiempoInicio = DateTime.UtcNow;
            if (etapa != null)
            {
                etapa.Estado = "EnProceso";
                etapa.TiempoInicio = DateTime.UtcNow;
            }
        }

        private async Task FinalizarLavadoAutomaticamente(Lavado lavado)
        {
            // Recargar el lavado para tener los datos más recientes
            var lavadoActualizado = await ObtenerLavado(lavado.Id);
            if (lavadoActualizado == null) return;
            if (FinalizarLavadoSiCorresponde(lavadoActualizado))
                await ActualizarLavado(lavadoActualizado);
        }

        private static bool FinalizarLavadoSiCorresponde(Lavado lavadoActualizado)
        {
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
                    return false;
                }
                else if (serviciosRealizados.Any() && serviciosCancelados.Any())
                {
                    // Algunos realizados, algunos cancelados
                    lavadoActualizado.Estado = ESTADO_REALIZADO_PARCIALMENTE;
                }
                else if (serviciosRealizados.Count == lavadoActualizado.Servicios.Count)
                {
                    // Todos los servicios realizados (completamente o parcialmente)
                    var hayParciales = lavadoActualizado.Servicios.Any(s => s.Estado == "RealizadoParcialmente");
                    lavadoActualizado.Estado = hayParciales ? ESTADO_REALIZADO_PARCIALMENTE : ESTADO_REALIZADO;
                }
                else if (serviciosRealizados.Any())
                {
                    // Hay servicios realizados (algunos parcialmente)
                    var hayParciales = lavadoActualizado.Servicios.Any(s => s.Estado == "RealizadoParcialmente");
                    lavadoActualizado.Estado = hayParciales ? ESTADO_REALIZADO_PARCIALMENTE : ESTADO_REALIZADO;
                }
                
                lavadoActualizado.TiempoFinalizacion = DateTime.UtcNow;
                return true;
            }
            return false;
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
            List<string>? estadosRetiro,
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

            // Filtro de estados de retiro en memoria
            if (estadosRetiro != null && estadosRetiro.Any())
            {
                lavados = lavados.Where(l => estadosRetiro.Contains(l.EstadoRetiro ?? "Pendiente")).ToList();
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
            List<string>? estadosPago,
            List<string>? estadosRetiro)
        {
            var lavados = await ObtenerLavadosFiltrados(estados, clienteId, vehiculoId, fechaDesde, fechaHasta, precioDesde, precioHasta, estadosPago, estadosRetiro, null, null);
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
                "EstadoRetiro" => l => l.EstadoRetiro ?? string.Empty,
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
                ["Receta"] = s.Receta.Select(r => new Dictionary<string, object>
                {
                    ["Modalidad"] = r.Modalidad,
                    ["TipoId"] = r.TipoId,
                    ["NombreTipo"] = r.NombreTipo,
                    ["Categoria"] = r.Categoria,
                    ["InsumoId"] = r.InsumoId,
                    ["NombreInsumo"] = r.NombreInsumo,
                    ["UnidadMedida"] = r.UnidadMedida,
                    ["DosisTeorica"] = (double)r.DosisTeorica,
                    ["EsHerramienta"] = r.EsHerramienta
                }).ToList(),
                ["Etapas"] = s.Etapas.Select(e => new Dictionary<string, object>
                {
                    ["EtapaId"] = e.EtapaId,
                    ["Nombre"] = e.Nombre ?? string.Empty,
                    ["Estado"] = e.Estado,
                    ["TiempoInicio"] = e.TiempoInicio.HasValue ? Timestamp.FromDateTime(e.TiempoInicio.Value.ToUniversalTime()) : (object?)null!,
                    ["TiempoFinalizacion"] = e.TiempoFinalizacion.HasValue ? Timestamp.FromDateTime(e.TiempoFinalizacion.Value.ToUniversalTime()) : (object?)null!,
                    ["MotivoCancelacion"] = e.MotivoCancelacion ?? string.Empty,
                    ["Receta"] = e.Receta.Select(r => new Dictionary<string, object>
                    {
                        ["Modalidad"] = r.Modalidad,
                        ["TipoId"] = r.TipoId,
                        ["NombreTipo"] = r.NombreTipo,
                        ["Categoria"] = r.Categoria,
                        ["InsumoId"] = r.InsumoId,
                        ["NombreInsumo"] = r.NombreInsumo,
                        ["UnidadMedida"] = r.UnidadMedida,
                        ["DosisTeorica"] = (double)r.DosisTeorica,
                        ["EsHerramienta"] = r.EsHerramienta
                    }).ToList()
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
                ["PreguntasFinalizacion"] = lavado.PreguntasFinalizacion,
                ["EstadoRetiro"] = lavado.EstadoRetiro ?? Lavado.EstadosRetiro.Pendiente,
                ["ClienteTrajoId"] = lavado.ClienteTrajoId ?? string.Empty,
                ["ClienteTrajoNombre"] = lavado.ClienteTrajoNombre ?? string.Empty,
                ["ClienteRetiraId"] = lavado.ClienteRetiraId ?? string.Empty,
                ["ClienteRetiraNombre"] = lavado.ClienteRetiraNombre ?? string.Empty,
                ["FechaRetiro"] = lavado.FechaRetiro.HasValue ? Timestamp.FromDateTime(lavado.FechaRetiro.Value.ToUniversalTime()) : (object?)null!
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
                PreguntasFinalizacion = documento.ContainsField("PreguntasFinalizacion") ? documento.GetValue<int>("PreguntasFinalizacion") : 0,
                EstadoRetiro = documento.ContainsField("EstadoRetiro") ? documento.GetValue<string>("EstadoRetiro") ?? Lavado.EstadosRetiro.Pendiente : Lavado.EstadosRetiro.Pendiente,
                ClienteTrajoId = documento.ContainsField("ClienteTrajoId") ? documento.GetValue<string>("ClienteTrajoId") : null,
                ClienteTrajoNombre = documento.ContainsField("ClienteTrajoNombre") ? documento.GetValue<string>("ClienteTrajoNombre") : null,
                ClienteRetiraId = documento.ContainsField("ClienteRetiraId") ? documento.GetValue<string>("ClienteRetiraId") : null,
                ClienteRetiraNombre = documento.ContainsField("ClienteRetiraNombre") ? documento.GetValue<string>("ClienteRetiraNombre") : null
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

            if (documento.ContainsField("FechaRetiro"))
            {
                var timestamp = documento.GetValue<Timestamp?>("FechaRetiro");
                lavado.FechaRetiro = timestamp?.ToDateTime();
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

        private static RecetaInsumo CopiarReceta(RecetaInsumo receta) => new()
        {
            Modalidad = receta.Modalidad,
            TipoId = receta.TipoId,
            NombreTipo = receta.NombreTipo,
            Categoria = receta.Categoria,
            InsumoId = receta.InsumoId,
            NombreInsumo = receta.NombreInsumo,
            UnidadMedida = receta.UnidadMedida,
            DosisTeorica = receta.DosisTeorica,
            EsHerramienta = receta.EsHerramienta
        };

        private static List<RecetaInsumo> LeerReceta(IDictionary<string, object> map, string campo)
        {
            if (!map.TryGetValue(campo, out var raw) || raw is not IList<object> lista)
                return new();
            return lista.OfType<IDictionary<string, object>>().Select(x => new RecetaInsumo
            {
                Modalidad = GetDictValue(x, "Modalidad")?.ToString() ?? "Producto",
                TipoId = GetDictValue(x, "TipoId")?.ToString() ?? string.Empty,
                NombreTipo = GetDictValue(x, "NombreTipo")?.ToString() ?? string.Empty,
                Categoria = GetDictValue(x, "Categoria")?.ToString() ?? string.Empty,
                InsumoId = GetDictValue(x, "InsumoId")?.ToString() ?? string.Empty,
                NombreInsumo = GetDictValue(x, "NombreInsumo")?.ToString() ?? string.Empty,
                UnidadMedida = GetDictValue(x, "UnidadMedida")?.ToString() ?? string.Empty,
                DosisTeorica = GetDictValue(x, "DosisTeorica") == null ? 0m : Convert.ToDecimal(GetDictValue(x, "DosisTeorica")),
                EsHerramienta = GetDictValue(x, "EsHerramienta") is bool b && b
            }).ToList();
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
                    servicio.Receta = LeerReceta(map, "Receta");

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
                                etapa.Receta = LeerReceta(etapaMap, "Receta");

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
                estados, clienteId, vehiculoId, fechaDesde, fechaHasta, null, null, estadosPago, null, null, null);

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
                estados, clienteId, vehiculoId, fechaDesde, fechaHasta, null, null, estadosPago, null, null, null);

            return lavados.Any() ? lavados.Max(l => l.Precio) : 0m;
        }

        #endregion
    }
}

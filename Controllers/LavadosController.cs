using Firebase.Models;
using Firebase.Services;
using FirebaseLoginCustom.Models;
using FirebaseLoginCustom.Utils;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Security.Claims;

namespace FirebaseLoginCustom.Controllers
{
    /// <summary>
    /// Controlador para la gestión de lavados/realizaciones de servicio.
    /// Incluye filtros, paginación, búsqueda, y acciones AJAX.
    /// </summary>
    [AutorizacionRequerida]
    public class LavadosController : Controller
    {
        #region Dependencias
        private readonly ILogger<LavadosController> _logger;
        private readonly AuditService _auditService;
        private readonly LavadoService _lavadoService;
        private readonly ServicioService _servicioService;
        private readonly PaqueteServicioService _paqueteServicioService;
        private readonly ClienteService _clienteService;
        private readonly VehiculoService _vehiculoService;
        private readonly PersonalService _personalService;
        private readonly ConfiguracionService _configuracionService;

        /// <summary>
        /// Crea una nueva instancia de <see cref="LavadosController"/>.
        /// </summary>
        public LavadosController(
            ILogger<LavadosController> logger,
            AuditService auditService,
            LavadoService lavadoService,
            ServicioService servicioService,
            PaqueteServicioService paqueteServicioService,
            ClienteService clienteService,
            VehiculoService vehiculoService,
            PersonalService personalService,
            ConfiguracionService configuracionService)
        {
            _logger = logger;
            _auditService = auditService;
            _lavadoService = lavadoService;
            _servicioService = servicioService;
            _paqueteServicioService = paqueteServicioService;
            _clienteService = clienteService;
            _vehiculoService = vehiculoService;
            _personalService = personalService;
            _configuracionService = configuracionService;
        }
        #endregion

        #region Vistas Principales

        /// <summary>
        /// Página principal de lavados con filtros, orden y paginación.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(
            List<string>? estados,
            string? clienteId,
            string? vehiculoId,
            int pageNumber = 1,
            int pageSize = 10,
            string? sortBy = null,
            string? sortOrder = null)
        {
            estados = ConfigurarEstadosDefecto(estados);

            sortBy ??= "FechaCreacion";
            sortOrder ??= "desc";

            var lavados = await _lavadoService.ObtenerLavados(
                estados, clienteId, vehiculoId, null, null, pageNumber, pageSize, sortBy, sortOrder);

            var totalPages = await _lavadoService.ObtenerTotalPaginas(
                estados, clienteId, vehiculoId, null, null, pageSize);

            var currentPage = Math.Clamp(pageNumber, 1, Math.Max(totalPages, 1));
            var visiblePages = GetVisiblePages(currentPage, totalPages);

            await ConfigurarViewBag(estados, clienteId, vehiculoId, pageSize, currentPage, totalPages, visiblePages, sortBy, sortOrder);

            return View(lavados);
        }

        /// <summary>
        /// Página de privacidad.
        /// </summary>
        public IActionResult Privacy()
        {
            return View();
        }

        /// <summary>
        /// Vista de error.
        /// </summary>
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
        #endregion

        #region Búsqueda y Tabla Parcial

        /// <summary>
        /// Busca lavados por término de búsqueda.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SearchPartial(
            string searchTerm,
            List<string>? estados,
            string? clienteId,
            string? vehiculoId,
            int pageNumber = 1,
            int pageSize = 10,
            string? sortBy = null,
            string? sortOrder = null)
        {
            estados = ConfigurarEstadosDefecto(estados);

            sortBy ??= "FechaCreacion";
            sortOrder ??= "desc";

            var lavados = await _lavadoService.BuscarLavados(
                searchTerm, estados, clienteId, vehiculoId, null, null, pageNumber, pageSize, sortBy, sortOrder);

            var totalLavados = await _lavadoService.ObtenerTotalLavadosBusqueda(
                searchTerm, estados, clienteId, vehiculoId, null, null);

            var totalPages = Math.Max((int)Math.Ceiling(totalLavados / (double)pageSize), 1);

            ViewBag.CurrentPage = pageNumber;
            ViewBag.TotalPages = totalPages;
            ViewBag.VisiblePages = GetVisiblePages(pageNumber, totalPages);
            ViewBag.Estados = estados;
            ViewBag.ClienteId = clienteId;
            ViewBag.VehiculoId = vehiculoId;
            ViewBag.SortBy = sortBy;
            ViewBag.SortOrder = sortOrder;
            ViewBag.SearchTerm = searchTerm;

            return PartialView("_LavadoTable", lavados);
        }

        /// <summary>
        /// Devuelve la tabla parcial con filtros y orden.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> TablePartial(
            List<string>? estados,
            string? clienteId,
            string? vehiculoId,
            int pageNumber = 1,
            int pageSize = 10,
            string? sortBy = null,
            string? sortOrder = null)
        {
            estados = ConfigurarEstadosDefecto(estados);

            sortBy ??= "FechaCreacion";
            sortOrder ??= "desc";

            var lavados = await _lavadoService.ObtenerLavados(
                estados, clienteId, vehiculoId, null, null, pageNumber, pageSize, sortBy, sortOrder);

            var totalPages = await _lavadoService.ObtenerTotalPaginas(
                estados, clienteId, vehiculoId, null, null, pageSize);
            totalPages = Math.Max(totalPages, 1);

            ViewBag.CurrentPage = pageNumber;
            ViewBag.TotalPages = totalPages;
            ViewBag.VisiblePages = GetVisiblePages(pageNumber, totalPages);
            ViewBag.Estados = estados;
            ViewBag.ClienteId = clienteId;
            ViewBag.VehiculoId = vehiculoId;
            ViewBag.SortBy = sortBy;
            ViewBag.SortOrder = sortOrder;

            return PartialView("_LavadoTable", lavados);
        }

        #endregion

        #region Vistas Parciales

        /// <summary>
        /// Devuelve el formulario parcial para crear un nuevo lavado.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> FormPartial(string? id)
        {
            await CargarDatosFormulario();

            if (!string.IsNullOrEmpty(id))
            {
                var lavado = await _lavadoService.ObtenerLavado(id);
                return PartialView("_LavadoForm", lavado);
            }

            return PartialView("_LavadoForm", null);
        }

        /// <summary>
        /// Devuelve el detalle de un lavado.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> DetailPartial(string id)
        {
            var lavado = await _lavadoService.ObtenerLavado(id);
            if (lavado == null)
            {
                return NotFound();
            }

            var config = await _configuracionService.ObtenerConfiguracion();
            ViewBag.Configuracion = config;

            return PartialView("_LavadoDetail", lavado);
        }

        /// <summary>
        /// Vista completa de detalle de un lavado.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Detalle(string id)
        {
            var lavado = await _lavadoService.ObtenerLavado(id);
            if (lavado == null)
            {
                return NotFound();
            }

            var config = await _configuracionService.ObtenerConfiguracion();
            ViewBag.Configuracion = config;

            return View("Detalle", lavado);
        }

        #endregion

        #region Operaciones CRUD

        /// <summary>
        /// Crea un nuevo lavado vía AJAX.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearLavadoAjax([FromBody] CrearLavadoRequest request)
        {
            try
            {
                // Validar cliente
                var cliente = await _clienteService.ObtenerCliente(request.ClienteId);
                if (cliente == null)
                {
                    return Json(new { success = false, message = "Cliente no encontrado." });
                }

                // Obtener vehículos seleccionados
                var vehiculosConServicios = request.VehiculosServicios;
                if (vehiculosConServicios == null || !vehiculosConServicios.Any())
                {
                    return Json(new { success = false, message = "Debe seleccionar al menos un vehículo con servicios." });
                }

                var lavadosCreados = new List<Lavado>();

                foreach (var vehiculoServicios in vehiculosConServicios)
                {
                    var vehiculo = await _vehiculoService.ObtenerVehiculo(vehiculoServicios.VehiculoId);
                    if (vehiculo == null)
                    {
                        return Json(new { success = false, message = $"Vehículo con ID {vehiculoServicios.VehiculoId} no encontrado." });
                    }

                    // Construir lista de servicios y calcular precios
                    var serviciosEnLavado = new List<ServicioEnLavado>();
                    var tiempoEstimadoTotal = 0;
                    decimal precioTotal = 0;
                    var paquetesYaContados = new HashSet<string>(); // Para contar precio de cada paquete solo una vez

                    var orden = 0;
                    foreach (var servicioItem in vehiculoServicios.Servicios)
                    {
                        var servicio = await _servicioService.ObtenerServicio(servicioItem.ServicioId);
                        if (servicio == null)
                        {
                            return Json(new { success = false, message = $"Servicio con ID {servicioItem.ServicioId} no encontrado." });
                        }

                        // Validar que el servicio sea para el tipo de vehículo correcto
                        if (!string.Equals(servicio.TipoVehiculo, vehiculo.TipoVehiculo, StringComparison.OrdinalIgnoreCase))
                        {
                            return Json(new { success = false, message = $"El servicio '{servicio.Nombre}' no es compatible con el tipo de vehículo '{vehiculo.TipoVehiculo}'." });
                        }

                        var servicioEnLavado = new ServicioEnLavado
                        {
                            ServicioId = servicio.Id,
                            ServicioNombre = servicio.Nombre,
                            TipoServicio = servicio.Tipo,
                            Precio = servicio.Precio,
                            TiempoEstimado = servicio.TiempoEstimado,
                            Estado = "Pendiente",
                            Orden = orden++,
                            PaqueteId = servicioItem.PaqueteId,
                            PaqueteNombre = servicioItem.PaqueteNombre
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
                                    Estado = "Pendiente"
                                });
                            }
                        }

                        serviciosEnLavado.Add(servicioEnLavado);
                        tiempoEstimadoTotal += servicio.TiempoEstimado;
                    }

                    // CALCULAR PRECIO TOTAL considerando paquetes
                    // Primero identificar qué paquetes hay
                    var paquetesUnicos = serviciosEnLavado
                        .Where(s => !string.IsNullOrEmpty(s.PaqueteId))
                        .Select(s => s.PaqueteId)
                        .Distinct()
                        .ToList();

                    // Calcular y sumar precio de cada paquete UNA SOLA VEZ
                    foreach (var paqueteId in paquetesUnicos)
                    {
                        var paquete = await _paqueteServicioService.ObtenerPaquete(paqueteId);
                        if (paquete != null)
                        {
                            // CALCULAR precio del paquete en tiempo real
                            // Sumar precios de los servicios del paquete
                            decimal precioServiciosPaquete = 0;
                            foreach (var servicioId in paquete.ServiciosIds)
                            {
                                var servicio = await _servicioService.ObtenerServicio(servicioId);
                                if (servicio != null)
                                {
                                    precioServiciosPaquete += servicio.Precio;
                                }
                            }

                            // Aplicar descuento del paquete
                            var precioPaqueteConDescuento = precioServiciosPaquete - (precioServiciosPaquete * paquete.PorcentajeDescuento / 100);
                            precioTotal += precioPaqueteConDescuento;
                        }
                    }

                    // Sumar servicios individuales (los que NO tienen PaqueteId)
                    var serviciosIndividuales = serviciosEnLavado.Where(s => string.IsNullOrEmpty(s.PaqueteId));
                    foreach (var servIndiv in serviciosIndividuales)
                    {
                        precioTotal += servIndiv.Precio;
                    }

                    // Aplicar descuento
                    var descuento = request.Descuento;
                    var precioConDescuento = precioTotal - (precioTotal * descuento / 100);

                    // Asignar empleados aleatoriamente
                // Validar cantidad de empleados solicitados
                var config = await _configuracionService.ObtenerConfiguracion();
                if (request.CantidadEmpleados > config.EmpleadosMaximosPorLavado)
                {
                    return Json(new { success = false, message = $"La cantidad de empleados solicitados ({request.CantidadEmpleados}) excede el máximo permitido por lavado ({config.EmpleadosMaximosPorLavado})." });
                }

                var empleadosAsignados = await _lavadoService.AsignarEmpleadosAleatorios(request.CantidadEmpleados);

                    var lavado = new Lavado
                    {
                        Id = string.Empty, // Se generará en el servicio
                        Estado = "EnProceso",
                        ClienteId = cliente.Id,
                        ClienteNombre = cliente.NombreCompleto,
                        VehiculoId = vehiculo.Id,
                        VehiculoPatente = vehiculo.Patente,
                        TipoVehiculo = vehiculo.TipoVehiculo,
                        Servicios = serviciosEnLavado,
                        PrecioOriginal = precioTotal,
                        Precio = precioConDescuento,
                        Descuento = descuento,
                        CantidadEmpleadosRequeridos = request.CantidadEmpleados,
                        EmpleadosAsignadosIds = empleadosAsignados.Select(e => e.Id).ToList(),
                        EmpleadosAsignadosNombres = empleadosAsignados.Select(e => e.NombreCompleto).ToList(),
                        TiempoEstimado = tiempoEstimadoTotal,
                        Notas = request.Notas
                    };

                    var lavadoCreado = await _lavadoService.CrearLavado(lavado);
                    lavadosCreados.Add(lavadoCreado);

                    // Iniciar automáticamente el primer servicio
                    if (lavadoCreado.Servicios.Any())
                    {
                        var primerServicio = lavadoCreado.Servicios.OrderBy(s => s.Orden).First();
                        await _lavadoService.IniciarServicio(lavadoCreado.Id, primerServicio.ServicioId);

                        // Si el servicio tiene etapas, iniciar la primera
                        if (primerServicio.Etapas.Any())
                        {
                            var primeraEtapa = primerServicio.Etapas.First();
                            await _lavadoService.IniciarEtapa(lavadoCreado.Id, primerServicio.ServicioId, primeraEtapa.EtapaId);
                        }
                    }

                    await RegistrarEvento("Creación de lavado", lavadoCreado.Id, "Lavado");
                }

                return Json(new
                {
                    success = true,
                    message = lavadosCreados.Count == 1
                        ? "Lavado creado correctamente."
                        : $"Se crearon {lavadosCreados.Count} lavados correctamente.",
                    lavadosIds = lavadosCreados.Select(l => l.Id).ToList()
                });
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear lavado");
                return Json(new { success = false, message = "Error al crear el lavado: " + ex.Message });
            }
        }

        /// <summary>
        /// Finaliza un lavado completo.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FinalizarLavado(string id)
        {
            try
            {
                await _lavadoService.FinalizarLavado(id);
                await RegistrarEvento("Finalización de lavado", id, "Lavado");

                return Json(new { success = true, message = "Lavado finalizado correctamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Finaliza un servicio específico dentro de un lavado.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FinalizarServicio(string lavadoId, string servicioId)
        {
            try
            {
                await _lavadoService.FinalizarServicio(lavadoId, servicioId);
                await RegistrarEvento("Finalización de servicio en lavado", lavadoId, "Lavado");

                return Json(new { success = true, message = "Servicio finalizado correctamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Finaliza una etapa específica dentro de un servicio.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FinalizarEtapa(string lavadoId, string servicioId, string etapaId)
        {
            try
            {
                await _lavadoService.FinalizarEtapa(lavadoId, servicioId, etapaId);
                await RegistrarEvento("Finalización de etapa en lavado", lavadoId, "Lavado");

                return Json(new { success = true, message = "Etapa finalizada correctamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Inicia un servicio específico.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IniciarServicio(string lavadoId, string servicioId)
        {
            try
            {
                await _lavadoService.IniciarServicio(lavadoId, servicioId);
                await RegistrarEvento("Inicio de servicio en lavado", lavadoId, "Lavado");

                return Json(new { success = true, message = "Servicio iniciado correctamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Inicia una etapa específica.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IniciarEtapa(string lavadoId, string servicioId, string etapaId)
        {
            try
            {
                await _lavadoService.IniciarEtapa(lavadoId, servicioId, etapaId);
                await RegistrarEvento("Inicio de etapa en lavado", lavadoId, "Lavado");

                return Json(new { success = true, message = "Etapa iniciada correctamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Cancela un lavado completo.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelarLavado(string id, string motivo)
        {
            try
            {
                await _lavadoService.CancelarLavado(id, motivo);
                await RegistrarEvento("Cancelación de lavado", id, "Lavado");

                return Json(new { success = true, message = "Lavado cancelado correctamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Cancela un servicio específico.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelarServicio(string lavadoId, string servicioId, string motivo)
        {
            try
            {
                await _lavadoService.CancelarServicio(lavadoId, servicioId, motivo);
                await RegistrarEvento("Cancelación de servicio en lavado", lavadoId, "Lavado");

                return Json(new { success = true, message = "Servicio cancelado correctamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Cancela una etapa específica.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelarEtapa(string lavadoId, string servicioId, string etapaId, string motivo)
        {
            try
            {
                await _lavadoService.CancelarEtapa(lavadoId, servicioId, etapaId, motivo);
                await RegistrarEvento("Cancelación de etapa en lavado", lavadoId, "Lavado");

                return Json(new { success = true, message = "Etapa cancelada correctamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Registra un pago para un lavado.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarPago(string lavadoId, decimal monto, string medioPago, string? notas)
        {
            try
            {
                await _lavadoService.RegistrarPago(lavadoId, monto, medioPago, notas);
                await RegistrarEvento("Registro de pago en lavado", lavadoId, "Lavado");

                return Json(new { success = true, message = "Pago registrado correctamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region APIs de datos

        /// <summary>
        /// Obtiene los clientes para el selector.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerClientes(string? search)
        {
            var clientes = await _clienteService.ObtenerClientes(
                searchTerm: search ?? string.Empty,
                pageNumber: 1,
                pageSize: 50,
                sortBy: "Nombre",
                sortOrder: "asc",
                estados: new List<string> { "Activo" });

            return Json(clientes.Select(c => new
            {
                id = c.Id,
                nombre = c.NombreCompleto,
                documento = $"{c.TipoDocumento}: {c.NumeroDocumento}"
            }));
        }

        /// <summary>
        /// Obtiene los vehículos de un cliente.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerVehiculosCliente(string clienteId)
        {
            var vehiculos = await _vehiculoService.ObtenerVehiculosPorCliente(clienteId);

            // Filtrar solo activos
            vehiculos = vehiculos.Where(v => v.Estado == "Activo").ToList();

            return Json(vehiculos.Select(v => new
            {
                id = v.Id,
                patente = v.Patente,
                tipoVehiculo = v.TipoVehiculo,
                marca = v.Marca,
                modelo = v.Modelo,
                color = v.Color,
                descripcion = $"{v.Patente} - {v.Marca} {v.Modelo} ({v.Color})"
            }));
        }

        /// <summary>
        /// Obtiene los servicios disponibles para un tipo de vehículo.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerServiciosPorTipoVehiculo(string tipoVehiculo)
        {
            var servicios = await _servicioService.ObtenerServicios(
                estados: new List<string> { "Activo" },
                tiposVehiculo: new List<string> { tipoVehiculo },
                pageNumber: 1,
                pageSize: 100);

            return Json(servicios.Select(s => new
            {
                id = s.Id,
                nombre = s.Nombre,
                tipo = s.Tipo,
                tipoVehiculo = s.TipoVehiculo,
                precio = s.Precio,
                tiempoEstimado = s.TiempoEstimado,
                tieneEtapas = s.Etapas != null && s.Etapas.Any(),
                cantidadEtapas = s.Etapas?.Count ?? 0,
                descripcion = s.Descripcion
            }));
        }

        /// <summary>
        /// Obtiene los paquetes disponibles para un tipo de vehículo.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerPaquetesPorTipoVehiculo(string tipoVehiculo)
        {
            var paquetes = await _paqueteServicioService.ObtenerPaquetes(
                estados: new List<string> { "Activo" },
                tiposVehiculo: new List<string> { tipoVehiculo },
                pageNumber: 1,
                pageSize: 100);

            var paquetesConServicios = new List<object>();
            foreach (var p in paquetes)
            {
                // Calcular precio original sumando los precios de los servicios
                decimal precioOriginalCalculado = 0;
                var serviciosDelPaquete = new List<object>();
                
                foreach (var servicioId in p.ServiciosIds)
                {
                    var servicio = await _servicioService.ObtenerServicio(servicioId);
                    if (servicio != null)
                    {
                        precioOriginalCalculado += servicio.Precio;
                        serviciosDelPaquete.Add(new
                        {
                            id = servicio.Id,
                            nombre = servicio.Nombre,
                            tipo = servicio.Tipo,
                            precio = servicio.Precio
                        });
                    }
                }

                paquetesConServicios.Add(new
                {
                    id = p.Id,
                    nombre = p.Nombre,
                    tipoVehiculo = p.TipoVehiculo,
                    precio = p.Precio,
                    precioOriginal = precioOriginalCalculado,
                    descuento = p.PorcentajeDescuento,
                    tiempoEstimado = p.TiempoEstimado,
                    servicios = serviciosDelPaquete
                });
            }

            return Json(paquetesConServicios);
        }

        /// <summary>
        /// Obtiene la configuración del sistema.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerConfiguracion()
        {
            var config = await _configuracionService.ObtenerConfiguracion();
            if (config == null)
            {
                return Json(new
                {
                    tiempoNotificacionMinutos = 15,
                    tiempoToleranciaMinutos = 15,
                    intervaloPreguntas = 5,
                    empleadosMaximosPorLavado = 3
                });
            }

            return Json(new
            {
                tiempoNotificacionMinutos = config.TiempoNotificacionMinutos,
                tiempoToleranciaMinutos = config.TiempoToleranciaMinutos,
                intervaloPreguntas = config.IntervaloPreguntas,
                empleadosMaximosPorLavado = config.EmpleadosMaximosPorLavado
            });
        }

        /// <summary>
        /// Obtiene información sobre empleados disponibles.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerEmpleadosDisponibles()
        {
            try
            {
                var empleadosActivos = await _personalService.ObtenerEmpleados(
                    estados: new List<string> { "Activo" },
                    pageNumber: 1,
                    pageSize: 100
                );

                var empleadosDisponibles = new List<object>();
                foreach (var empleado in empleadosActivos)
                {
                    var lavadosActivos = await _lavadoService.ObtenerLavadosActivosPorEmpleado(empleado.Id);
                    if (!lavadosActivos.Any())
                    {
                        empleadosDisponibles.Add(new
                        {
                            id = empleado.Id,
                            nombre = empleado.NombreCompleto
                        });
                    }
                }

                var config = await _configuracionService.ObtenerConfiguracion();

                return Json(new
                {
                    totalActivos = empleadosActivos.Count,
                    totalDisponibles = empleadosDisponibles.Count,
                    empleadosMaximosPorLavado = config.EmpleadosMaximosPorLavado,
                    empleados = empleadosDisponibles
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener empleados disponibles");
                return Json(new
                {
                    totalActivos = 0,
                    totalDisponibles = 0,
                    empleadosMaximosPorLavado = 3,
                    empleados = new List<object>()
                });
            }
        }

        #endregion

        #region Operaciones de Sesión

        /// <summary>
        /// Cierra la sesión del usuario actual y registra auditoría.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(userId))
            {
                await _auditService.LogEvent(
                    userId: userId,
                    userEmail: email,
                    action: "Cierre de sesión",
                    targetId: userId,
                    targetType: "Empleado");
            }
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Login");
        }
        #endregion

        #region Métodos Privados

        private static List<string> ConfigurarEstadosDefecto(List<string>? estados)
        {
            estados ??= new List<string>();
            if (!estados.Any())
            {
                // Por defecto mostrar TODOS los estados
                estados.AddRange(new[] { "Pendiente", "EnProceso", "Realizado", "RealizadoParcialmente", "Cancelado" });
            }
            return estados;
        }

        private async Task ConfigurarViewBag(
            List<string> estados,
            string? clienteId,
            string? vehiculoId,
            int pageSize,
            int currentPage,
            int totalPages,
            List<int> visiblePages,
            string sortBy,
            string sortOrder)
        {
            ViewBag.TotalPages = totalPages;
            ViewBag.VisiblePages = visiblePages;
            ViewBag.CurrentPage = currentPage;
            ViewBag.Estados = estados;
            ViewBag.ClienteId = clienteId;
            ViewBag.VehiculoId = vehiculoId;
            ViewBag.PageSize = pageSize;
            ViewBag.SortBy = sortBy;
            ViewBag.SortOrder = sortOrder;

            // Cargar datos para filtros
            ViewBag.TodosLosEstados = new List<string> { "Pendiente", "EnProceso", "Realizado", "RealizadoParcialmente", "Cancelado" };

            // Obtener configuración
            var config = await _configuracionService.ObtenerConfiguracion();
            ViewBag.Configuracion = config;
        }

        private async Task CargarDatosFormulario()
        {
            ViewBag.MediosPago = new List<string>
            {
                "Efectivo",
                "Tarjeta de Débito",
                "Tarjeta de Crédito",
                "Transferencia Bancaria",
                "MercadoPago",
                "Otro"
            };

            var config = await _configuracionService.ObtenerConfiguracion();
            ViewBag.Configuracion = config;
        }

        private List<int> GetVisiblePages(int currentPage, int totalPages, int range = 2)
        {
            var start = Math.Max(1, currentPage - range);
            var end = Math.Min(totalPages, currentPage + range);
            return Enumerable.Range(start, end - start + 1).ToList();
        }

        private async Task RegistrarEvento(string accion, string targetId, string entidad)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            if (userId != null && userEmail != null)
            {
                await _auditService.LogEvent(userId, userEmail, accion, targetId, entidad);
            }
        }

        #endregion
    }

    #region Request Models

    /// <summary>
    /// Modelo para la solicitud de creación de lavado.
    /// </summary>
    public class CrearLavadoRequest
    {
        public string ClienteId { get; set; } = string.Empty;
        public List<VehiculoServiciosRequest> VehiculosServicios { get; set; } = new List<VehiculoServiciosRequest>();
        public int CantidadEmpleados { get; set; } = 1;
        public decimal Descuento { get; set; }
        public string? Notas { get; set; }
    }

    /// <summary>
    /// Modelo para los servicios de un vehículo.
    /// </summary>
    public class VehiculoServiciosRequest
    {
        public string VehiculoId { get; set; } = string.Empty;
        public List<ServicioItemRequest> Servicios { get; set; } = new List<ServicioItemRequest>();
    }

    /// <summary>
    /// Modelo para un servicio individual.
    /// </summary>
    public class ServicioItemRequest
    {
        public string ServicioId { get; set; } = string.Empty;
        public string? PaqueteId { get; set; }
        public string? PaqueteNombre { get; set; }
    }

    #endregion
}
using Firebase.Models;
using Firebase.Security;
using Firebase.Services;
using FirebaseLoginCustom.Models;
using FirebaseLoginCustom.Utils;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Security.Claims;

namespace FirebaseLoginCustom.Controllers
{
    /// <summary>
    /// Controlador para la gestión de lavados/realizaciones de servicio.
    /// Incluye filtros, paginación, búsqueda, y acciones AJAX.
    /// </summary>
    [Authorize]
    public class LavadosController : Controller
    {
        #region Dependencias
        private readonly ILogger<LavadosController> _logger;
        private readonly IAuditWriter _auditService;
        private readonly LavadoService _lavadoService;
        private readonly LavadoCreationService _lavadoCreationService;
        private readonly LavadoAssociatedClientService _lavadoAssociatedClientService;
        private readonly ServicioService _servicioService;
        private readonly PaqueteServicioService _paqueteServicioService;
        private readonly ClienteService _clienteService;
        private readonly VehiculoService _vehiculoService;
        private readonly PersonalService _personalService;
        private readonly ConfiguracionService _configuracionService;
        private readonly TipoVehiculoService _tipoVehiculoService;
        private readonly TipoDocumentoService _tipoDocumentoService;

        /// <summary>
        /// Crea una nueva instancia de <see cref="LavadosController"/>.
        /// </summary>
        public LavadosController(
            ILogger<LavadosController> logger,
            IAuditWriter auditService,
            LavadoService lavadoService,
            ServicioService servicioService,
            PaqueteServicioService paqueteServicioService,
            ClienteService clienteService,
            VehiculoService vehiculoService,
            PersonalService personalService,
            ConfiguracionService configuracionService,
            TipoVehiculoService tipoVehiculoService,
            TipoDocumentoService tipoDocumentoService,
            LavadoCreationService lavadoCreationService,
            LavadoAssociatedClientService lavadoAssociatedClientService)
        {
            _logger = logger;
            _auditService = auditService;
            _lavadoService = lavadoService;
            _lavadoCreationService = lavadoCreationService;
            _lavadoAssociatedClientService = lavadoAssociatedClientService;
            _servicioService = servicioService;
            _paqueteServicioService = paqueteServicioService;
            _clienteService = clienteService;
            _vehiculoService = vehiculoService;
            _personalService = personalService;
            _configuracionService = configuracionService;
            _tipoVehiculoService = tipoVehiculoService;
            _tipoDocumentoService = tipoDocumentoService;
        }
        #endregion

        #region Vistas Principales

        /// <summary>
        /// Página principal de lavados con filtros, orden y paginación.
        /// </summary>
        [HttpGet]
        [Permiso("Lavados.Consultar", "Lavados.Crear")]
        public async Task<IActionResult> Index(
            List<string>? estados,
            string? clienteId,
            string? vehiculoId,
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            decimal? precioDesde,
            decimal? precioHasta,
            List<string>? estadosPago,
            List<string>? estadosRetiro,
            int pageNumber = 1,
            int pageSize = 10,
            string? sortBy = null,
            string? sortOrder = null)
        {
            ViewBag.TiposDocumento = await _tipoDocumentoService.ObtenerTiposDocumentoCompletos();
            if (!User.TienePermiso("Lavados.Consultar"))
            {
                await CargarDatosFormulario();
                return View(new List<Lavado>());
            }
            estados = ConfigurarEstadosDefecto(estados);

            sortBy ??= "FechaCreacion";
            sortOrder ??= "desc";

            var lavados = await _lavadoService.ObtenerLavados(
                estados, clienteId, vehiculoId, fechaDesde, fechaHasta, precioDesde, precioHasta, estadosPago, estadosRetiro, pageNumber, pageSize, sortBy, sortOrder);

            var totalPages = await _lavadoService.ObtenerTotalPaginas(
                estados, clienteId, vehiculoId, fechaDesde, fechaHasta, precioDesde, precioHasta, estadosPago, estadosRetiro, pageSize);

            var currentPage = Math.Clamp(pageNumber, 1, Math.Max(totalPages, 1));
            var visiblePages = GetVisiblePages(currentPage, totalPages);

            await ConfigurarViewBag(estados, clienteId, vehiculoId, fechaDesde, fechaHasta, precioDesde, precioHasta, estadosPago, estadosRetiro, pageSize, currentPage, totalPages, visiblePages, sortBy, sortOrder);

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
        [Permiso("Lavados.Consultar")]
        public async Task<IActionResult> SearchPartial(
            string searchTerm,
            List<string>? estados,
            string? clienteId,
            string? vehiculoId,
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            decimal? precioDesde,
            decimal? precioHasta,
            List<string>? estadosPago,
            List<string>? estadosRetiro,
            int pageNumber = 1,
            int pageSize = 10,
            string? sortBy = null,
            string? sortOrder = null)
        {
            estados = ConfigurarEstadosDefecto(estados);

            sortBy ??= "FechaCreacion";
            sortOrder ??= "desc";

            var lavados = await _lavadoService.BuscarLavados(
                searchTerm, estados, clienteId, vehiculoId, fechaDesde, fechaHasta, precioDesde, precioHasta, estadosPago, estadosRetiro, pageNumber, pageSize, sortBy, sortOrder);

            var totalLavados = await _lavadoService.ObtenerTotalLavadosBusqueda(
                searchTerm, estados, clienteId, vehiculoId, fechaDesde, fechaHasta, precioDesde, precioHasta, estadosPago, estadosRetiro);

            var totalPages = Math.Max((int)Math.Ceiling(totalLavados / (double)pageSize), 1);

            PrepararMetadataPartial(pageNumber, totalPages, estados, clienteId, vehiculoId, fechaDesde, fechaHasta, precioDesde, precioHasta, estadosPago, estadosRetiro, sortBy, sortOrder);

            ViewBag.SearchTerm = searchTerm;

            return PartialView("_LavadoTable", lavados);
        }

        /// <summary>
        /// Devuelve la tabla parcial con filtros y orden.
        /// </summary>
        [HttpGet]
        [Permiso("Lavados.Consultar")]
        public async Task<IActionResult> TablePartial(
            List<string>? estados,
            string? clienteId,
            string? vehiculoId,
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            decimal? precioDesde,
            decimal? precioHasta,
            List<string>? estadosPago,
            List<string>? estadosRetiro,
            int pageNumber = 1,
            int pageSize = 10,
            string? sortBy = null,
            string? sortOrder = null)
        {
            estados = ConfigurarEstadosDefecto(estados);

            sortBy ??= "FechaCreacion";
            sortOrder ??= "desc";

            var lavados = await _lavadoService.ObtenerLavados(
                estados, clienteId, vehiculoId, fechaDesde, fechaHasta, precioDesde, precioHasta, estadosPago, estadosRetiro, pageNumber, pageSize, sortBy, sortOrder);

            var totalPages = await _lavadoService.ObtenerTotalPaginas(
                estados, clienteId, vehiculoId, fechaDesde, fechaHasta, precioDesde, precioHasta, estadosPago, estadosRetiro, pageSize);
            totalPages = Math.Max(totalPages, 1);

            PrepararMetadataPartial(pageNumber, totalPages, estados, clienteId, vehiculoId, fechaDesde, fechaHasta, precioDesde, precioHasta, estadosPago, estadosRetiro, sortBy, sortOrder);


            return PartialView("_LavadoTable", lavados);
        }

        #endregion

        #region Vistas Parciales

        /// <summary>
        /// Devuelve el formulario parcial para crear un nuevo lavado.
        /// </summary>
        [HttpGet]
        [Permiso("Lavados.Consultar", "Lavados.Crear")]
        public async Task<IActionResult> FormPartial(string? id)
        {
            if (!string.IsNullOrEmpty(id) && !User.TienePermiso("Lavados.Consultar"))
                return Forbid();
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
        [Permiso("Lavados.Consultar")]
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
        [Permiso("Lavados.Consultar")]
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
        [Permiso("Lavados.Crear")]
        public async Task<IActionResult> CrearLavadoAjax([FromBody] CrearLavadoRequest request)
        {
            try
            {
                var preparacion = await _lavadoCreationService.Preparar(request);
                if (preparacion.Error != null)
                {
                    return Json(new { success = false, message = preparacion.Error });
                }

                var lavadosCreados = new List<Lavado>();
                var contexto = preparacion.Contexto!;
                foreach (var vehiculoServicios in contexto.Vehiculos)
                {
                    var resultado = await _lavadoCreationService.CrearVehiculo(request, contexto, vehiculoServicios);
                    if (resultado.Error != null)
                    {
                        return Json(new { success = false, message = resultado.Error });
                    }
                    var lavadoCreado = resultado.Lavado!;
                    lavadosCreados.Add(lavadoCreado);
                    await RegistrarLavado("Creación de lavado", lavadoCreado.Id, new());
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear lavado");
                return Json(new { success = false, message = "No se pudo crear el lavado. Inténtalo nuevamente." });
            }
        }

        /// <summary>
        /// Finaliza un lavado completo.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permiso("Lavados.Modificar")]
        public async Task<IActionResult> FinalizarLavado(string id)
        {
            try
            {
                var antes = AuditCampos.CapturarLavado((await _lavadoService.ObtenerLavado(id))!);
                await _lavadoService.FinalizarLavado(id);
                await RegistrarLavado("Finalización de lavado", id, antes);

                return Json(new { success = true, message = "Lavado finalizado correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al finalizar lavado {LavadoId}", id);
                return Json(new { success = false, message = "No se pudo finalizar el lavado. Inténtalo nuevamente." });
            }
        }

        /// <summary>
        /// Finaliza un servicio específico dentro de un lavado.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permiso("Lavados.Modificar")]
        public async Task<IActionResult> FinalizarServicio(string lavadoId, string servicioId)
        {
            try
            {
                var antes = AuditCampos.CapturarLavado((await _lavadoService.ObtenerLavado(lavadoId))!);
                await _lavadoService.FinalizarServicio(lavadoId, servicioId);
                await RegistrarLavado("Finalización de servicio en lavado", lavadoId, antes);

                return Json(new { success = true, message = "Servicio finalizado correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al finalizar servicio {ServicioId} del lavado {LavadoId}", servicioId, lavadoId);
                return Json(new { success = false, message = "No se pudo finalizar el servicio. Inténtalo nuevamente." });
            }
        }

        /// <summary>
        /// Finaliza una etapa específica dentro de un servicio.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permiso("Lavados.Modificar")]
        public async Task<IActionResult> FinalizarEtapa(string lavadoId, string servicioId, string etapaId)
        {
            try
            {
                var antes = AuditCampos.CapturarLavado((await _lavadoService.ObtenerLavado(lavadoId))!);
                await _lavadoService.FinalizarEtapa(lavadoId, servicioId, etapaId);
                await RegistrarLavado("Finalización de etapa en lavado", lavadoId, antes);

                return Json(new { success = true, message = "Etapa finalizada correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al finalizar etapa {EtapaId} del servicio {ServicioId} en lavado {LavadoId}", etapaId, servicioId, lavadoId);
                return Json(new { success = false, message = "No se pudo finalizar la etapa. Inténtalo nuevamente." });
            }
        }

        /// <summary>
        /// Inicia un servicio específico.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permiso("Lavados.Modificar")]
        public async Task<IActionResult> IniciarServicio(string lavadoId, string servicioId)
        {
            try
            {
                var antes = AuditCampos.CapturarLavado((await _lavadoService.ObtenerLavado(lavadoId))!);
                await _lavadoService.IniciarServicio(lavadoId, servicioId);
                await RegistrarLavado("Inicio de servicio en lavado", lavadoId, antes);

                return Json(new { success = true, message = "Servicio iniciado correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al iniciar servicio {ServicioId} del lavado {LavadoId}", servicioId, lavadoId);
                return Json(new { success = false, message = "No se pudo iniciar el servicio. Inténtalo nuevamente." });
            }
        }

        /// <summary>
        /// Inicia una etapa específica.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permiso("Lavados.Modificar")]
        public async Task<IActionResult> IniciarEtapa(string lavadoId, string servicioId, string etapaId)
        {
            try
            {
                var antes = AuditCampos.CapturarLavado((await _lavadoService.ObtenerLavado(lavadoId))!);
                await _lavadoService.IniciarEtapa(lavadoId, servicioId, etapaId);
                await RegistrarLavado("Inicio de etapa en lavado", lavadoId, antes);

                return Json(new { success = true, message = "Etapa iniciada correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al iniciar etapa {EtapaId} del servicio {ServicioId} en lavado {LavadoId}", etapaId, servicioId, lavadoId);
                return Json(new { success = false, message = "No se pudo iniciar la etapa. Inténtalo nuevamente." });
            }
        }

        /// <summary>
        /// Cancela un lavado completo.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permiso("Lavados.Modificar")]
        public async Task<IActionResult> CancelarLavado(string id, string motivo)
        {
            try
            {
                var antes = AuditCampos.CapturarLavado((await _lavadoService.ObtenerLavado(id))!);
                await _lavadoService.CancelarLavado(id, motivo);
                await RegistrarLavado("Cancelación de lavado", id, antes);

                return Json(new { success = true, message = "Lavado cancelado correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cancelar lavado {LavadoId}", id);
                return Json(new { success = false, message = "No se pudo cancelar el lavado. Inténtalo nuevamente." });
            }
        }

        /// <summary>
        /// Cancela un servicio específico.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permiso("Lavados.Modificar")]
        public async Task<IActionResult> CancelarServicio(string lavadoId, string servicioId, string motivo)
        {
            try
            {
                var antes = AuditCampos.CapturarLavado((await _lavadoService.ObtenerLavado(lavadoId))!);
                await _lavadoService.CancelarServicio(lavadoId, servicioId, motivo);
                await RegistrarLavado("Cancelación de servicio en lavado", lavadoId, antes);

                return Json(new { success = true, message = "Servicio cancelado correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cancelar servicio {ServicioId} del lavado {LavadoId}", servicioId, lavadoId);
                return Json(new { success = false, message = "No se pudo cancelar el servicio. Inténtalo nuevamente." });
            }
        }

        /// <summary>
        /// Cancela una etapa específica.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permiso("Lavados.Modificar")]
        public async Task<IActionResult> CancelarEtapa(string lavadoId, string servicioId, string etapaId, string motivo)
        {
            try
            {
                var antes = AuditCampos.CapturarLavado((await _lavadoService.ObtenerLavado(lavadoId))!);
                await _lavadoService.CancelarEtapa(lavadoId, servicioId, etapaId, motivo);
                await RegistrarLavado("Cancelación de etapa en lavado", lavadoId, antes);

                return Json(new { success = true, message = "Etapa cancelada correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cancelar etapa {EtapaId} del servicio {ServicioId} en lavado {LavadoId}", etapaId, servicioId, lavadoId);
                return Json(new { success = false, message = "No se pudo cancelar la etapa. Inténtalo nuevamente." });
            }
        }

        /// <summary>
        /// Registra un pago para un lavado.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permiso("Lavados.Modificar")]
        public async Task<IActionResult> RegistrarPago(string lavadoId, decimal monto, string medioPago, string? notas)
        {
            try
            {
                var antes = AuditCampos.CapturarLavado((await _lavadoService.ObtenerLavado(lavadoId))!);
                await _lavadoService.RegistrarPago(lavadoId, monto, medioPago, notas);
                await RegistrarLavado("Registro de pago en lavado", lavadoId, antes);

                return Json(new { success = true, message = "Pago registrado correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar pago del lavado {LavadoId}", lavadoId);
                return Json(new { success = false, message = "No se pudo registrar el pago. Inténtalo nuevamente." });
            }
        }

        /// <summary>
        /// Registra el retiro del vehículo del lavadero.
        /// Solo permite el retiro si el lavado está pagado o fue cancelado.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permiso("Lavados.Modificar")]
        public async Task<IActionResult> RegistrarRetiro(string lavadoId)
        {
            try
            {
                var resultado = await _lavadoService.RegistrarRetiro(lavadoId);
                if (resultado.Error != null)
                {
                    return Json(new { success = false, message = resultado.Error });
                }
                var antes = resultado.CamposAntes!;
                await RegistrarLavado("Retiro de vehículo", lavadoId, antes);

                return Json(new { success = true, message = "Retiro registrado correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar retiro del lavado {LavadoId}", lavadoId);
                return Json(new { success = false, message = "No se pudo registrar el retiro. Inténtalo nuevamente." });
            }
        }

        #endregion

        #region APIs de datos

        /// <summary>
        /// Obtiene los clientes para el selector.
        /// </summary>
        [HttpGet]
        [Permiso("Lavados.Consultar")]
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
        /// Busca vehículos por patente para el selector.
        /// </summary>
        [HttpGet]
        [Permiso("Lavados.Consultar", "Lavados.Crear")]
        public async Task<IActionResult> BuscarVehiculosPorPatente(string? search)
        {
            if (string.IsNullOrWhiteSpace(search) || search.Length < 2)
            {
                return Json(new List<object>());
            }

            if (!User.TienePermiso("Lavados.Consultar"))
            {
                var vehiculo = await _vehiculoService.ObtenerVehiculoPorPatente(search.Trim().ToUpperInvariant());
                if (vehiculo == null || vehiculo.Estado != "Activo")
                    return Json(new List<object>());
                return Json(new[] { new { id = vehiculo.Id, patente = vehiculo.Patente, tipoVehiculo = vehiculo.TipoVehiculo, marca = vehiculo.Marca, modelo = vehiculo.Modelo, color = vehiculo.Color } });
            }

            var vehiculos = await _vehiculoService.ObtenerVehiculos(
                searchTerm: search,
                tiposVehiculo: null,
                marcas: null,
                colores: null,
                pageNumber: 1,
                pageSize: 20,
                sortBy: "Patente",
                sortOrder: "asc",
                estados: new List<string> { "Activo" });

            return Json(vehiculos.Select(v => new
            {
                id = v.Id,
                patente = v.Patente,
                tipoVehiculo = v.TipoVehiculo,
                marca = v.Marca,
                modelo = v.Modelo,
                color = v.Color,
                clienteNombre = v.ClienteNombreCompleto,
                clientesIds = v.ClientesIds ?? new List<string>(),
                descripcion = $"{v.Patente} - {v.Marca} {v.Modelo} ({v.Color})"
            }));
        }

        /// <summary>
        /// Obtiene los clientes asociados a un vehículo.
        /// </summary>
        [HttpGet]
        [Permiso("Lavados.Consultar", "Lavados.Crear")]
        public async Task<IActionResult> ObtenerClientesVehiculo(string vehiculoId, string? patente = null)
        {
            var consultar = User.TienePermiso("Lavados.Consultar");
            if (!consultar && string.IsNullOrWhiteSpace(patente))
                return Json(new List<object>());
            var vehiculo = consultar
                ? await _vehiculoService.ObtenerVehiculo(vehiculoId)
                : await _vehiculoService.ObtenerVehiculoPorPatente(patente!.Trim().ToUpperInvariant());
            if (vehiculo == null || (!consultar && (vehiculo.Id != vehiculoId || vehiculo.Estado != "Activo")))
            {
                return Json(new List<object>());
            }

            var clientesIds = new List<string>();
            
            // Agregar el cliente principal si existe
            if (!string.IsNullOrEmpty(vehiculo.ClienteId))
            {
                clientesIds.Add(vehiculo.ClienteId);
            }

            // Agregar los clientes asociados
            if (vehiculo.ClientesIds != null)
            {
                foreach (var clienteId in vehiculo.ClientesIds)
                {
                    if (!clientesIds.Contains(clienteId))
                    {
                        clientesIds.Add(clienteId);
                    }
                }
            }

            var clientes = new List<object>();
            foreach (var clienteId in clientesIds)
            {
                var cliente = await _clienteService.ObtenerCliente(clienteId);
                if (cliente != null && cliente.Estado == "Activo")
                {
                    if (!consultar)
                        clientes.Add(new { id = cliente.Id, nombre = cliente.NombreCompleto, documento = $"{cliente.TipoDocumento}: {cliente.NumeroDocumento}" });
                    else clientes.Add(new
                    {
                        id = cliente.Id,
                        nombre = cliente.NombreCompleto,
                        documento = $"{cliente.TipoDocumento}: {cliente.NumeroDocumento}",
                        telefono = cliente.Telefono
                    });
                }
            }

            return Json(clientes);
        }

        /// <summary>
        /// Registra un cliente nuevo y lo asocia al vehículo seleccionado en el formulario de lavado.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permiso("Clientes.Crear")]
        public async Task<IActionResult> RegistrarClienteAsociado([FromBody] RegistroClienteAsociadoRequest request)
        {
            var resultado = await _lavadoAssociatedClientService.Registrar(request);
            if (resultado.Error != null)
                return Json(new { success = false, message = resultado.Error });
            var cliente = resultado.Cliente!;

            var clientePersistido = await _clienteService.ObtenerCliente(cliente.Id) ?? throw new InvalidOperationException("Cliente no encontrado después de crear.");
            await RegistrarEvento("Creacion de cliente y asociacion a vehiculo", cliente.Id, "Cliente", new(), AuditCampos.CapturarCliente(clientePersistido), clientePersistido.NombreCompleto);
            return Json(new
            {
                success = true,
                message = "Cliente registrado y asociado correctamente.",
                cliente = new { id = cliente.Id, nombre = cliente.NombreCompleto, documento = $"{cliente.TipoDocumento}: {cliente.NumeroDocumento}" }
            });
        }

        public class RegistroClienteAsociadoRequest
        {
            public string VehiculoId { get; set; } = string.Empty;
            public string ClaveAsociacion { get; set; } = string.Empty;
            public string TipoDocumento { get; set; } = string.Empty;
            public string NumeroDocumento { get; set; } = string.Empty;
            public string Nombre { get; set; } = string.Empty;
            public string Apellido { get; set; } = string.Empty;
            public string Telefono { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
        }

        /// <summary>
        /// Obtiene la cantidad de empleados requeridos para un tipo de vehículo.
        /// </summary>
        [HttpGet]
        [Permiso("Lavados.Consultar", "Lavados.Crear")]
        public async Task<IActionResult> ObtenerEmpleadosPorTipoVehiculo(string tipoVehiculo)
        {
            var tipo = await _tipoVehiculoService.ObtenerTipoVehiculoPorNombre(tipoVehiculo);
            var cantidadEmpleados = tipo?.CantidadEmpleadosRequeridos ?? 1;
            
            return Json(new { cantidadEmpleadosRequeridos = cantidadEmpleados });
        }

        /// <summary>
        /// Obtiene los vehículos de un cliente.
        /// </summary>
        [HttpGet]
        [Permiso("Lavados.Consultar")]
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
        [Permiso("Lavados.Consultar", "Lavados.Crear")]
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
        [Permiso("Lavados.Consultar", "Lavados.Crear")]
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
        [Permiso("Lavados.Consultar", "Lavados.Crear")]
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
        [Permiso("Lavados.Consultar", "Lavados.Crear")]
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

                if (!User.TienePermiso("Lavados.Consultar"))
                    return Json(new { totalActivos = empleadosActivos.Count, totalDisponibles = empleadosDisponibles.Count, empleadosMaximosPorLavado = config.EmpleadosMaximosPorLavado });

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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var loginController = ActivatorUtilities.GetServiceOrCreateInstance<LoginController>(HttpContext.RequestServices);
            loginController.ControllerContext = ControllerContext;
            return await loginController.Logout();
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
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            decimal? precioDesde,
            decimal? precioHasta,
            List<string>? estadosPago,
            List<string>? estadosRetiro,
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
            ViewBag.FechaDesde = fechaDesde;
            ViewBag.FechaHasta = fechaHasta;
            ViewBag.PrecioDesde = precioDesde;
            ViewBag.PrecioHasta = precioHasta;
            ViewBag.EstadosPago = estadosPago;
            ViewBag.EstadosRetiro = estadosRetiro;
            ViewBag.PageSize = pageSize;
            ViewBag.SortBy = sortBy;
            ViewBag.SortOrder = sortOrder;

            // Cargar datos para filtros
            ViewBag.TodosLosEstados = new List<string> { "Pendiente", "EnProceso", "Realizado", "RealizadoParcialmente", "Cancelado" };
            ViewBag.TodosLosEstadosPago = new List<string> { "Pendiente", "Parcial", "Pagado", "Cancelado" };

            // Obtener límites de precios para mostrar en el formulario
            ViewBag.PrecioMinimo = await _lavadoService.ObtenerPrecioMinimo(estados, clienteId, vehiculoId, fechaDesde, fechaHasta, estadosPago);
            ViewBag.PrecioMaximo = await _lavadoService.ObtenerPrecioMaximo(estados, clienteId, vehiculoId, fechaDesde, fechaHasta, estadosPago);

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

        private void PrepararMetadataPartial(int pageNumber, int totalPages, List<string>? estados, string? clienteId, string? vehiculoId, DateTime? fechaDesde, DateTime? fechaHasta, decimal? precioDesde, decimal? precioHasta, List<string>? estadosPago, List<string>? estadosRetiro, string sortBy, string sortOrder)
        {
            ViewBag.CurrentPage = pageNumber;
            ViewBag.TotalPages = totalPages;
            ViewBag.VisiblePages = GetVisiblePages(pageNumber, totalPages);
            ViewBag.Estados = estados;
            ViewBag.ClienteId = clienteId;
            ViewBag.VehiculoId = vehiculoId;
            ViewBag.FechaDesde = fechaDesde;
            ViewBag.FechaHasta = fechaHasta;
            ViewBag.PrecioDesde = precioDesde;
            ViewBag.PrecioHasta = precioHasta;
            ViewBag.EstadosPago = estadosPago;
            ViewBag.EstadosRetiro = estadosRetiro;
            ViewBag.SortBy = sortBy;
            ViewBag.SortOrder = sortOrder;
        }

        private async Task RegistrarLavado(string accion, string id, Dictionary<string, string> antes)
        {
            var lavado = await _lavadoService.ObtenerLavado(id) ?? throw new InvalidOperationException("Lavado no encontrado después de modificar.");
            await RegistrarEvento(accion, id, "Lavado", antes, AuditCampos.CapturarLavado(lavado), lavado.VehiculoPatente ?? id);
        }

        private async Task RegistrarEvento(string accion, string targetId, string entidad, Dictionary<string, string> antes, Dictionary<string, string> despues, string nombre)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            if (userId != null && userEmail != null)
            {
                await _auditService.LogEvent(userId, userEmail, accion, targetId, entidad, antes, despues, nombre);
            }
        }

        #endregion
    }

}

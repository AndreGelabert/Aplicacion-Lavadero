using Firebase.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

/// <summary>
/// Controlador para la gestión de paquetes de servicios del lavadero.
/// Incluye filtros, paginación, búsqueda, acciones AJAX de creación/actualización.
/// </summary>
[Authorize(Roles = "Administrador")]
public class PaqueteServicioController : Controller
{
    #region Dependencias
    private readonly PaqueteServicioService _paqueteServicioService;
    private readonly ServicioService _servicioService;
    private readonly TipoVehiculoService _tipoVehiculoService;
    private readonly AuditService _auditService;

    /// <summary>
    /// Crea una nueva instancia del controlador de paquetes de servicios.
    /// </summary>
    public PaqueteServicioController(
        PaqueteServicioService paqueteServicioService,
        ServicioService servicioService,
        TipoVehiculoService tipoVehiculoService,
        AuditService auditService)
    {
        _paqueteServicioService = paqueteServicioService;
        _servicioService = servicioService;
        _tipoVehiculoService = tipoVehiculoService;
        _auditService = auditService;
    }
    #endregion

    #region Vistas Principales

    /// <summary>
    /// Página principal de paquetes de servicios con filtros, orden y paginación.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        List<string> estados,
        List<string> tiposVehiculo,
        int pageNumber = 1,
        int pageSize = 10,
        string editId = null,
        string sortBy = null,
        string sortOrder = null,
        decimal? precioMin = null,
        decimal? precioMax = null,
        decimal? descuentoMin = null,
        decimal? descuentoMax = null,
        int? serviciosCantidad = null)
    {
        estados = ConfigurarEstadosDefecto(estados);

        sortBy ??= "Nombre";
        sortOrder ??= "asc";

        var (paquetes, currentPage, totalPages, visiblePages) = await ObtenerDatosPaquetes(
            estados, tiposVehiculo, pageNumber, pageSize, sortBy, sortOrder,
            precioMin, precioMax, null, null, descuentoMin, descuentoMax, serviciosCantidad, serviciosCantidad);

        var tiposVehiculoList = await CargarListaTiposVehiculo();
        var cantidadesServicios = await _paqueteServicioService.ObtenerValoresCantidadServicios();

        ConfigurarViewBag(estados, tiposVehiculo, tiposVehiculoList,
            pageSize, currentPage, totalPages, visiblePages, sortBy, sortOrder,
            precioMin, precioMax, null, null, descuentoMin, descuentoMax, serviciosCantidad, serviciosCantidad);
        ViewBag.CantidadesServicios = cantidadesServicios;
        ViewBag.ServiciosCantidad = serviciosCantidad;

        await ConfigurarFormulario(editId);

        return View(paquetes);
    }
    #endregion

    #region Operaciones CRUD - AJAX

    /// <summary>
    /// Crear un paquete vía AJAX.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearPaqueteAjax(PaqueteServicio paquete, string? ServiciosIdsJson)
    {
        try
        {
            if (!string.IsNullOrEmpty(ServiciosIdsJson))
            {
                paquete.ServiciosIds = System.Text.Json.JsonSerializer.Deserialize<List<string>>(ServiciosIdsJson) ?? new List<string>();
            }

            var resultado = await ProcesarCreacionPaquete(paquete);
            return await PrepararRespuestaAjax(resultado, paquete, "Creacion de paquete de servicios");
        }
        catch (Exception ex)
        {
            return await ManejiarExcepcionAjax(ex, paquete);
        }
    }

    /// <summary>
    /// Actualizar paquete vía AJAX.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActualizarPaqueteAjax(PaqueteServicio paquete, string? ServiciosIdsJson)
    {
        try
        {
            if (!string.IsNullOrEmpty(ServiciosIdsJson))
            {
                paquete.ServiciosIds = System.Text.Json.JsonSerializer.Deserialize<List<string>>(ServiciosIdsJson) ?? new List<string>();
            }

            var resultado = await ProcesarActualizacionPaquete(paquete);
            return await PrepararRespuestaAjax(resultado, paquete, "Actualizacion de paquete de servicios");
        }
        catch (Exception ex)
        {
            return await ManejiarExcepcionAjax(ex, paquete);
        }
    }
    #endregion

    #region Búsqueda y Tabla Parcial

    /// <summary>
    /// Busca paquetes por término de búsqueda (parcial para actualización dinámica).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> SearchPartial(
        string searchTerm,
        List<string> estados,
        List<string> tiposVehiculo,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = null,
        string sortOrder = null,
        decimal? precioMin = null,
        decimal? precioMax = null,
        decimal? descuentoMin = null,
        decimal? descuentoMax = null,
        int? serviciosCantidad = null)
    {
        estados = ConfigurarEstadosDefecto(estados);

        sortBy ??= "Nombre";
        sortOrder ??= "asc";

        var paquetes = await _paqueteServicioService.BuscarPaquetes(
            searchTerm, estados, tiposVehiculo, pageNumber, pageSize, sortBy, sortOrder,
            precioMin, precioMax, null, null, descuentoMin, descuentoMax, serviciosCantidad, serviciosCantidad);
        var totalPaquetes = await _paqueteServicioService.ObtenerTotalPaquetesBusqueda(
            searchTerm, estados, tiposVehiculo, precioMin, precioMax, null, null, descuentoMin, descuentoMax, serviciosCantidad, serviciosCantidad);

        var totalPages = Math.Max((int)Math.Ceiling(totalPaquetes / (double)pageSize), 1);

        ViewBag.CurrentPage = pageNumber;
        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = GetVisiblePages(pageNumber, totalPages);
        ViewBag.Estados = estados;
        ViewBag.TiposVehiculo = tiposVehiculo;
        ViewBag.SortBy = sortBy;
        ViewBag.SortOrder = sortOrder;
        ViewBag.SearchTerm = searchTerm;
        ViewBag.PrecioMin = precioMin;
        ViewBag.PrecioMax = precioMax;
        ViewBag.DescuentoMin = descuentoMin;
        ViewBag.DescuentoMax = descuentoMax;
        ViewBag.ServiciosCantidad = serviciosCantidad;
        ViewBag.CantidadesServicios = await _paqueteServicioService.ObtenerValoresCantidadServicios();

        return PartialView("_PaqueteServicioTable", paquetes);
    }

    /// <summary>
    /// Devuelve la tabla parcial (sin búsqueda) con filtros y orden.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> TablePartial(
        List<string> estados,
        List<string> tiposVehiculo,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = null,
        string sortOrder = null,
        decimal? precioMin = null,
        decimal? precioMax = null,
        decimal? descuentoMin = null,
        decimal? descuentoMax = null,
        int? serviciosCantidad = null)
    {
        estados = ConfigurarEstadosDefecto(estados);

        sortBy ??= "Nombre";
        sortOrder ??= "asc";

        var paquetes = await _paqueteServicioService.ObtenerPaquetes(
            estados, tiposVehiculo, pageNumber, pageSize, sortBy, sortOrder,
            precioMin, precioMax, null, null, descuentoMin, descuentoMax, serviciosCantidad, serviciosCantidad);
        var totalPages = await _paqueteServicioService.ObtenerTotalPaginas(
            estados, tiposVehiculo, pageSize, precioMin, precioMax, null, null, descuentoMin, descuentoMax, serviciosCantidad, serviciosCantidad);
        totalPages = Math.Max(totalPages, 1);

        ViewBag.CurrentPage = pageNumber;
        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = GetVisiblePages(pageNumber, totalPages);
        ViewBag.Estados = estados;
        ViewBag.TiposVehiculo = tiposVehiculo;
        ViewBag.SortBy = sortBy;
        ViewBag.SortOrder = sortOrder;
        ViewBag.PrecioMin = precioMin;
        ViewBag.PrecioMax = precioMax;
        ViewBag.DescuentoMin = descuentoMin;
        ViewBag.DescuentoMax = descuentoMax;
        ViewBag.ServiciosCantidad = serviciosCantidad;
        ViewBag.CantidadesServicios = await _paqueteServicioService.ObtenerValoresCantidadServicios();

        return PartialView("_PaqueteServicioTable", paquetes);
    }

    /// <summary>
    /// Devuelve dinámicamente el rango de precio posible para los filtros actuales (sin aplicar precioMin/Max)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> PriceRange(
        List<string> estados,
        List<string> tiposVehiculo,
        string searchTerm,
        decimal? descuentoMin,
        decimal? descuentoMax,
        int? serviciosCantidad)
    {
        try
        {
            var (min, max) = await _paqueteServicioService.ObtenerRangoPrecio(
                estados, tiposVehiculo, searchTerm, null, null, descuentoMin, descuentoMax, serviciosCantidad, serviciosCantidad);
            return Json(new { success = true, min, max });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
    #endregion

    #region Cambio de Estado

    /// <summary>
    /// Desactiva un paquete.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> DeactivatePaquete(string id)
        => await CambiarEstadoPaquete(id, "Inactivo", "Desactivacion de paquete de servicios");

    /// <summary>
    /// Reactiva un paquete.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ReactivatePaquete(string id)
        => await CambiarEstadoPaquete(id, "Activo", "Reactivacion de paquete de servicios");
    #endregion

    #region Vistas Parciales

    /// <summary>
    /// Devuelve el formulario parcial (usado por AJAX para editar/crear).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> FormPartial(string id)
    {
        await CargarListasForm();
        PaqueteServicio paquete = null;
        if (!string.IsNullOrEmpty(id))
            paquete = await _paqueteServicioService.ObtenerPaquete(id);

        return PartialView("_PaqueteServicioForm", paquete);
    }

    /// <summary>
    /// Obtiene los servicios activos filtrados por tipo de vehículo.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ObtenerServiciosPorTipoVehiculo(string tipoVehiculo)
    {
        if (string.IsNullOrWhiteSpace(tipoVehiculo))
        {
            return Json(new { success = false, message = "Debe seleccionar un tipo de vehículo" });
        }

        var servicios = await _servicioService.ObtenerServicios(
            new List<string> { "Activo" }, null, new List<string> { tipoVehiculo }, 1, 1000, "Nombre", "asc");

        var serviciosData = servicios.Select(s => new
        {
            id = s.Id,
            nombre = s.Nombre,
            tipo = s.Tipo,
            precio = s.Precio,
            tiempoEstimado = s.TiempoEstimado
        }).ToList();

        return Json(new { success = true, servicios = serviciosData });
    }
    #endregion

    #region Métodos Privados - Procesamiento de Negocio

    /// <summary>
    /// Configura los estados por defecto cuando no se recibe ninguno desde la vista.
    /// </summary>
    private static List<string> ConfigurarEstadosDefecto(List<string> estados)
    {
        estados ??= new List<string>();
        if (!estados.Any())
            estados.Add("Activo");
        return estados;
    }

    /// <summary>
    /// Obtiene los paquetes según los filtros y orden actual, calcula la paginación y las páginas visibles.
    /// </summary>
    private async Task<(List<PaqueteServicio> paquetes, int currentPage, int totalPages, List<int> visiblePages)>
        ObtenerDatosPaquetes(List<string> estados, List<string> tiposVehiculo,
        int pageNumber, int pageSize, string sortBy, string sortOrder,
        decimal? precioMin, decimal? precioMax, int? tiempoMin, int? tiempoMax,
        decimal? descuentoMin, decimal? descuentoMax, int? serviciosCantidadMin, int? serviciosCantidadMax)
    {
        var paquetes = await _paqueteServicioService.ObtenerPaquetes(
            estados, tiposVehiculo, pageNumber, pageSize, sortBy, sortOrder,
            precioMin, precioMax, tiempoMin, tiempoMax, descuentoMin, descuentoMax, serviciosCantidadMin, serviciosCantidadMax);
        var totalPages = Math.Max(await _paqueteServicioService.ObtenerTotalPaginas(
            estados, tiposVehiculo, pageSize,
            precioMin, precioMax, tiempoMin, tiempoMax, descuentoMin, descuentoMax, serviciosCantidadMin, serviciosCantidadMax), 1);
        var currentPage = Math.Clamp(pageNumber, 1, totalPages);
        var visiblePages = GetVisiblePages(currentPage, totalPages);

        return (paquetes, currentPage, totalPages, visiblePages);
    }

    /// <summary>
    /// Carga la lista de tipos de vehículo.
    /// </summary>
    private async Task<List<string>> CargarListaTiposVehiculo()
    {
        return await _tipoVehiculoService.ObtenerTiposVehiculos() ?? new List<string>();
    }

    /// <summary>
    /// Configura el formulario en modo creación o edición según el ID de edición recibido.
    /// </summary>
    private async Task ConfigurarFormulario(string editId)
    {
        if (!string.IsNullOrEmpty(editId))
        {
            var paquete = await _paqueteServicioService.ObtenerPaquete(editId);
            ViewBag.EditPaquete = paquete;
            ViewBag.FormTitle = "Editando un Paquete de Servicios";
            ViewBag.SubmitButtonText = "Guardar";
            ViewBag.ClearButtonText = "Cancelar";
            ViewBag.FormAction = "ActualizarPaqueteAjax";
        }
        else
        {
            ViewBag.FormTitle = "Registrando un Paquete de Servicios";
            ViewBag.SubmitButtonText = "Registrar";
            ViewBag.ClearButtonText = "Limpiar Campos";
            ViewBag.FormAction = "CrearPaqueteAjax";
        }
    }

    /// <summary>
    /// Procesa la creación de un paquete aplicando validaciones y verificación de duplicados.
    /// </summary>
    private async Task<ResultadoOperacion> ProcesarCreacionPaquete(PaqueteServicio paquete)
    {
        if (string.IsNullOrEmpty(paquete.Id))
        {
            paquete.Id = "temp-" + Guid.NewGuid().ToString();
            ModelState.Clear();
            TryValidateModel(paquete);
        }

        ValidatePaquete(paquete);
        if (!ModelState.IsValid)
        {
            return ResultadoOperacion.CrearError("Por favor, complete todos los campos obligatorios correctamente.");
        }

        if (await _paqueteServicioService.ExistePaqueteConNombre(paquete.Nombre))
        {
            var mensaje = $"Ya existe un paquete con el nombre '{paquete.Nombre}'.";
            ModelState.AddModelError("Nombre", "Ya existe un paquete con este nombre.");
            return ResultadoOperacion.CrearError(mensaje);
        }

        paquete.Estado = "Activo";
        await _paqueteServicioService.CrearPaquete(paquete);
        return ResultadoOperacion.CrearExito("Paquete de servicios creado correctamente.");
    }

    /// <summary>
    /// Procesa la actualización de un paquete aplicando validaciones y verificación de duplicados.
    /// </summary>
    private async Task<ResultadoOperacion> ProcesarActualizacionPaquete(PaqueteServicio paquete)
    {
        ValidatePaquete(paquete);
        if (!ModelState.IsValid)
        {
            return ResultadoOperacion.CrearError("Por favor, complete todos los campos obligatorios correctamente.");
        }

        var paqueteActual = await _paqueteServicioService.ObtenerPaquete(paquete.Id);
        if (paqueteActual == null)
        {
            ModelState.AddModelError("", "No se pudo encontrar el paquete a actualizar.");
            return ResultadoOperacion.CrearError("No se pudo encontrar el paquete a actualizar.");
        }

        if (await _paqueteServicioService.ExistePaqueteConNombre(paquete.Nombre, paquete.Id))
        {
            var mensaje = $"Ya existe un paquete con el nombre '{paquete.Nombre}'.";
            ModelState.AddModelError("Nombre", "Ya existe un paquete con este nombre.");
            return ResultadoOperacion.CrearError(mensaje);
        }

        paquete.Estado = paqueteActual.Estado;
        await _paqueteServicioService.ActualizarPaquete(paquete);
        return ResultadoOperacion.CrearExito("Paquete de servicios actualizado correctamente.");
    }

    /// <summary>
    /// Cambia el estado de un paquete y registra el evento de auditoría.
    /// </summary>
    private async Task<IActionResult> CambiarEstadoPaquete(string id, string nuevoEstado, string accionAuditoria)
    {
        await _paqueteServicioService.CambiarEstadoPaquete(id, nuevoEstado);
        TempData["StateChangeEvent_UserId"] = id;
        TempData["StateChangeEvent_NewState"] = nuevoEstado;
        await RegistrarEvento(accionAuditoria, id, "PaqueteServicio");
        return RedirectToAction("Index");
    }
    #endregion

    #region Métodos Privados - Respuestas y Manejo de Errores

    /// <summary>
    /// Prepara la respuesta de un envío AJAX de formulario.
    /// </summary>
    private async Task<IActionResult> PrepararRespuestaAjax(ResultadoOperacion resultado, PaqueteServicio paquete, string accionAuditoria)
    {
        if (!resultado.EsExitoso)
        {
            Response.Headers["X-Form-Valid"] = "false";
            await CargarListasForm();
            return PartialView("_PaqueteServicioForm", paquete);
        }

        await RegistrarEvento(accionAuditoria, paquete.Id, "PaqueteServicio");
        Response.Headers["X-Form-Valid"] = "true";
        Response.Headers["X-Form-Message"] = resultado.MensajeExito;
        await CargarListasForm();
        return PartialView("_PaqueteServicioForm", null);
    }

    /// <summary>
    /// Maneja excepciones en operaciones AJAX devolviendo el formulario parcial con errores.
    /// </summary>
    private async Task<IActionResult> ManejiarExcepcionAjax(Exception ex, PaqueteServicio paquete)
    {
        ModelState.AddModelError("", ex.Message);
        Response.Headers["X-Form-Valid"] = "false";
        await CargarListasForm();
        return PartialView("_PaqueteServicioForm", paquete);
    }
    #endregion

    #region Métodos Privados - Utilidades

    /// <summary>
    /// Calcula el conjunto de páginas visibles alrededor de la página actual.
    /// </summary>
    private List<int> GetVisiblePages(int currentPage, int totalPages, int range = 2)
    {
        var start = Math.Max(1, currentPage - range);
        var end = Math.Min(totalPages, currentPage + range);
        return Enumerable.Range(start, end - start + 1).ToList();
    }

    /// <summary>
    /// Valida campos del modelo de paquete y agrega errores a ModelState.
    /// </summary>
    private void ValidatePaquete(PaqueteServicio paquete)
    {
        if (!string.IsNullOrEmpty(paquete.Nombre) && !System.Text.RegularExpressions.Regex.IsMatch(paquete.Nombre, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$"))
        {
            ModelState.AddModelError("Nombre", "El nombre solo puede contener letras y espacios.");
        }

        // Validación ajustada:5..95
        if (paquete.PorcentajeDescuento < 5 || paquete.PorcentajeDescuento > 95)
        {
            ModelState.AddModelError("PorcentajeDescuento", "El porcentaje de descuento debe estar entre 5 y 95.");
        }

        if (paquete.ServiciosIds == null || paquete.ServiciosIds.Count < 2)
        {
            ModelState.AddModelError("ServiciosIds", "Debe seleccionar al menos 2 servicios.");
        }
    }

    /// <summary>
    /// Copia a ViewBag la información común necesaria para renderizar la vista Index.
    /// </summary>
    private void ConfigurarViewBag(
        List<string> estados, List<string> tiposVehiculo,
        List<string> tiposVehiculoList,
        int pageSize, int currentPage, int totalPages, List<int> visiblePages,
        string sortBy, string sortOrder,
        decimal? precioMin, decimal? precioMax, int? tiempoMin, int? tiempoMax,
        decimal? descuentoMin, decimal? descuentoMax, int? serviciosMin, int? serviciosMax)
    {
        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = visiblePages;
        ViewBag.CurrentPage = currentPage;
        ViewBag.Estados = estados;
        ViewBag.TiposVehiculo = tiposVehiculo;
        ViewBag.TodosLosTiposVehiculo = tiposVehiculoList;
        ViewBag.PageSize = pageSize;
        ViewBag.SortBy = sortBy;
        ViewBag.SortOrder = sortOrder;
        ViewBag.PrecioMin = precioMin;
        ViewBag.PrecioMax = precioMax;
        ViewBag.TiempoMin = tiempoMin;
        ViewBag.TiempoMax = tiempoMax;
        ViewBag.DescuentoMin = descuentoMin;
        ViewBag.DescuentoMax = descuentoMax;
        ViewBag.ServiciosMin = serviciosMin;
        ViewBag.ServiciosMax = serviciosMax;
    }

    /// <summary>
    /// Registra un evento de auditoría asociado al usuario actual.
    /// </summary>
    private async Task RegistrarEvento(string accion, string targetId, string entidad)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        await _auditService.LogEvent(userId, userEmail, accion, targetId, entidad);
    }

    /// <summary>
    /// Carga en ViewBag las listas necesarias para renderizar el formulario parcial.
    /// </summary>
    private async Task CargarListasForm()
    {
        ViewBag.TodosLosTiposVehiculo = await _tipoVehiculoService.ObtenerTiposVehiculos() ?? new List<string>();
    }
    #endregion
}

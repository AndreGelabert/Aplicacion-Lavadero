using Firebase.Services;
using Firebase.Models;
using Firebase.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

/// <summary>
/// Controlador para la gestión de servicios del lavadero.
/// Incluye filtros, paginación, búsqueda, acciones AJAX de creación/actualización,
/// y administración de tipos (servicio/vehículo).
/// </summary>
[Authorize]
public class ServicioController : Controller
{
    #region Dependencias
    private readonly ServicioService _servicioService;
    private readonly ServicioQueryService _queries;
    private readonly IAuditWriter _auditService;
    private readonly TipoServicioService _tipoServicioService;
    private readonly TipoVehiculoService _tipoVehiculoService;
    private readonly ServicioLifecycleService _lifecycle;
    private readonly ServicioFormService _form;
    private readonly ServicioTypeService _types;
    private readonly VehiculoService _vehiculoService; // ✅ NUEVO

    /// <summary>
    /// Crea una nueva instancia del controlador de servicios.
    /// </summary>
    public ServicioController(
        ServicioService servicioService,
        IAuditWriter auditService,
        TipoServicioService tipoServicioService,
        TipoVehiculoService tipoVehiculoService,
        VehiculoService vehiculoService,
        ServicioQueryService queries,
        ServicioLifecycleService lifecycle,
        ServicioFormService form,
        ServicioTypeService types)
    {
        _servicioService = servicioService;
        _queries = queries;
        _auditService = auditService;
        _tipoServicioService = tipoServicioService;
        _tipoVehiculoService = tipoVehiculoService;
        _lifecycle = lifecycle;
        _form = form;
        _types = types;
        _vehiculoService = vehiculoService; // ✅ NUEVO
    }
    #endregion

    #region Vistas Principales

    /// <summary>
    /// Página principal de servicios con filtros, orden y paginación.
    /// </summary>
    [HttpGet]
    [Permiso("Servicios.Consultar", "Servicios.Crear")]
    public async Task<IActionResult> Index(
        List<string> estados,
        List<string> tipos,
        List<string> tiposVehiculo,
        decimal? precioDesde = null,
        decimal? precioHasta = null,
        int pageNumber = 1,
        int pageSize = 10,
        string editId = null,
        string sortBy = null,
        string sortOrder = null)
    {
        if (!User.TienePermiso("Servicios.Consultar"))
        {
            await CargarListasForm();
            await ConfigurarFormulario(string.Empty);
            return View(new List<Servicio>());
        }
        estados = ConfigurarEstadosDefecto(estados);

        sortBy ??= "Nombre";
        sortOrder ??= "asc";

        var (servicios, currentPage, totalPages, visiblePages) = await ObtenerDatosServicios(
            estados, tipos, tiposVehiculo, precioDesde, precioHasta, pageNumber, pageSize, sortBy, sortOrder);

        var (tiposServicio, tiposVehiculoList) = await CargarListasDropdown();

        await ConfigurarViewBag(estados, tipos, tiposVehiculo, precioDesde, precioHasta, tiposServicio, tiposVehiculoList,
            pageSize, currentPage, totalPages, visiblePages, sortBy, sortOrder);

        await ConfigurarFormulario(editId);

        return View(servicios);
    }
    #endregion

    #region Operaciones CRUD - AJAX

    /// <summary>
    /// Crear un servicio vía AJAX.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Permiso("Servicios.Crear")]
    public async Task<IActionResult> CrearServicioAjax(Servicio servicio, string? EtapasJson)
    {
        try
        {
            if (!string.IsNullOrEmpty(EtapasJson))
            {
                servicio.Etapas = System.Text.Json.JsonSerializer.Deserialize<List<Etapa>>(EtapasJson) ?? new List<Etapa>();
            }

            var resultado = await ProcesarCreacionServicio(servicio);
            var despuesCaptura = resultado.EsExitoso ? AuditCampos.CapturarServicio(await ObtenerServicioParaAuditoria(servicio.Id)) : null;
            return await PrepararRespuestaAjax(resultado, servicio, "Creacion de servicio",
                antes: new Dictionary<string, string>(), despues: despuesCaptura, nombreHistorico: servicio.Nombre);
        }
        catch (Exception ex)
        {
            return await ManejiarExcepcionAjax(ex, servicio);
        }
    }

    /// <summary>
    /// Actualizar servicio vía AJAX.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Permiso("Servicios.Modificar")]
    public async Task<IActionResult> ActualizarServicioAjax(Servicio servicio, string? EtapasJson)
    {
        try
        {
            if (!string.IsNullOrEmpty(EtapasJson))
            {
                servicio.Etapas = System.Text.Json.JsonSerializer.Deserialize<List<Etapa>>(EtapasJson) ?? new List<Etapa>();
            }

            var servicioAntes = await _servicioService.ObtenerServicio(servicio.Id);
            var antesCaptura = AuditCampos.CapturarServicio(servicioAntes);
            var resultado = await ProcesarActualizacionServicio(servicio);
            var despuesActualizar = resultado.EsExitoso ? AuditCampos.CapturarServicio(await ObtenerServicioParaAuditoria(servicio.Id)) : null;
            return await PrepararRespuestaAjax(resultado, servicio, "Actualizacion de servicio",
                antes: antesCaptura, despues: despuesActualizar, nombreHistorico: servicioAntes?.Nombre ?? servicio.Nombre);
        }
        catch (Exception ex)
        {
            return await ManejiarExcepcionAjax(ex, servicio);
        }
    }
    #endregion

    #region Búsqueda y Tabla Parcial

    /// <summary>
    /// Busca servicios por término de búsqueda (parcial para actualización dinámica).
    /// </summary>
    [HttpGet]
    [Permiso("Servicios.Consultar")]
    public async Task<IActionResult> SearchPartial(
        string searchTerm,
        List<string> estados,
        List<string> tipos,
        List<string> tiposVehiculo,
        decimal? precioDesde = null,
        decimal? precioHasta = null,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = null,
        string sortOrder = null)
    {
        estados = ConfigurarEstadosDefecto(estados);

        sortBy ??= "Nombre";
        sortOrder ??= "asc";

        var resultado = await _queries.Buscar(searchTerm, estados, tipos, tiposVehiculo,
            precioDesde, precioHasta, pageNumber, pageSize, sortBy, sortOrder);
        var servicios = resultado.Servicios;
        var totalPages = resultado.TotalPages;

        ConfigurarMetadataTabla(pageNumber, totalPages, estados, tipos, tiposVehiculo, sortBy, sortOrder);
        ViewBag.SearchTerm = searchTerm;

        return PartialView("_ServicioTable", servicios);
    }

    /// <summary>
    /// Devuelve la tabla parcial (sin búsqueda) con filtros y orden.
    /// </summary>
    [HttpGet]
    [Permiso("Servicios.Consultar")]
    public async Task<IActionResult> TablePartial(
        List<string> estados,
        List<string> tipos,
        List<string> tiposVehiculo,
        decimal? precioDesde = null,
        decimal? precioHasta = null,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = null,
        string sortOrder = null)
    {
        estados = ConfigurarEstadosDefecto(estados);

        sortBy ??= "Nombre";
        sortOrder ??= "asc";

        var resultado = await _queries.ObtenerListado(estados, tipos, tiposVehiculo,
            precioDesde, precioHasta, pageNumber, pageSize, sortBy, sortOrder);
        var servicios = resultado.Servicios;
        var totalPages = resultado.TotalPages;

        ConfigurarMetadataTabla(pageNumber, totalPages, estados, tipos, tiposVehiculo, sortBy, sortOrder);

        return PartialView("_ServicioTable", servicios);
    }
    #endregion

    private void ConfigurarMetadataTabla(int pageNumber, int totalPages,
        List<string> estados, List<string> tipos, List<string> tiposVehiculo,
        string sortBy, string sortOrder)
    {
        ViewBag.CurrentPage = pageNumber;
        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = GetVisiblePages(pageNumber, totalPages);
        ViewBag.Estados = estados;
        ViewBag.Tipos = tipos;
        ViewBag.TiposVehiculo = tiposVehiculo;
        ViewBag.SortBy = sortBy;
        ViewBag.SortOrder = sortOrder;
    }

    #region Cambio de Estado

    /// <summary>
    /// Desactiva un servicio.
    /// </summary>
    [HttpPost]
    [Permiso("Servicios.Eliminar")]
    public async Task<IActionResult> DeactivateServicio(string id)
        => await CambiarEstadoServicio(id, "Inactivo", "Desactivacion de servicio");

    /// <summary>
    /// Reactiva un servicio.
    /// </summary>
    [HttpPost]
    [Permiso("Servicios.Modificar")]
    public async Task<IActionResult> ReactivateServicio(string id)
        => await CambiarEstadoServicio(id, "Activo", "Reactivacion de servicio");
    #endregion

    #region Gestión de Tipos (Servicio y Vehículo)

    /// <summary>
    /// Crear nuevo tipo de servicio con respuesta AJAX (incluye fallback).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Permiso("Servicios.Crear")]
    public async Task<IActionResult> CrearTipoServicio(string nombreTipo)
    {
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            try
            {
                var resultado = await _types.CrearServicioAjax(nombreTipo);
                if (resultado.Error != null)
                    return Json(new { success = false, message = resultado.Error });
                var docId = resultado.Id!;
                await RegistrarCreacionTipo("Creacion de tipo de servicio", docId, "TipoServicio");

                var tiposActualizados = await _tipoServicioService.ObtenerTiposServicio();

                return Json(new
                {
                    success = true,
                    message = "Tipo de servicio creado correctamente.",
                    tipos = tiposActualizados
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al crear el tipo de servicio: {ex.Message}" });
            }
        }

        // Fallback (mantener por si se usa fuera de AJAX)
        return await ResponderCreacionTipo(await _types.CrearServicioFallback(nombreTipo),
            "TipoServicio", "Creacion de tipo de servicio");
    }

    /// <summary>
    /// Eliminar tipo de servicio con respuesta AJAX (incluye fallback).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Permiso("Servicios.Eliminar")]
    public async Task<IActionResult> EliminarTipoServicio(string nombreTipo)
    {
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            try
            {
                var resultado = await _types.EliminarServicioAjax(nombreTipo);
                if (resultado.Error != null)
                    return Json(new { success = false, message = resultado.Error });
                var idsEliminados = resultado.Ids!;
                foreach (var id in idsEliminados)
                {
                    await RegistrarEvento("Eliminacion de tipo de servicio", id, "TipoServicio", new() { ["Nombre"] = nombreTipo }, new(), nombreTipo);
                }

                var tiposActualizados = await _tipoServicioService.ObtenerTiposServicio();

                return Json(new
                {
                    success = true,
                    message = "Tipo de servicio eliminado correctamente.",
                    tipos = tiposActualizados
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al eliminar el tipo de servicio: {ex.Message}" });
            }
        }

        // Fallback
        return await ResponderEliminacionTipo(await _types.EliminarServicioFallback(nombreTipo),
            nombreTipo, "TipoServicio", "Eliminacion de tipo de servicio");
    }

    /// <summary>
    /// Crear nuevo tipo de vehículo con respuesta AJAX (incluye fallback).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Permiso("Servicios.Crear", "Vehiculos.Crear")]
    public async Task<IActionResult> CrearTipoVehiculo(string nombreTipo, string? formatoPatente = null, int cantidadEmpleadosRequeridos = 1)
    {
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            try {
                var resultado = await _types.CrearVehiculoAjax(nombreTipo, formatoPatente, cantidadEmpleadosRequeridos);
                if (resultado.Error != null)
                    return Json(new { success = false, message = resultado.Error });
                var vehDocId = resultado.Id!;
                await RegistrarCreacionTipo("Creacion de tipo de vehiculo", vehDocId, "TipoVehiculo");

                var tiposActualizados = await _tipoVehiculoService.ObtenerTiposVehiculosCompletos();

                return Json(new
                {
                    success = true,
                    message = "Tipo de vehículo creado correctamente.",
                    tipos = tiposActualizados.Select(t => t.Nombre).ToList(),
                    tiposCompletos = tiposActualizados.Select(t => new 
                    { 
                        nombre = t.Nombre, 
                        formatoPatente = t.FormatoPatente,
                        cantidadEmpleadosRequeridos = t.CantidadEmpleadosRequeridos,
                        regex = !string.IsNullOrWhiteSpace(t.FormatoPatente) ? t.ObtenerRegexPattern() : null
                    })
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al crear el tipo de vehículo: {ex.Message}" });
            }
        }

        // Fallback
        return await ResponderCreacionTipo(await _types.CrearVehiculoFallback(nombreTipo, formatoPatente, cantidadEmpleadosRequeridos),
            "TipoVehiculo", "Creacion de tipo de vehiculo");
    }

    /// <summary>
    /// Obtiene el formato de patente para un tipo de vehículo específico.
    /// </summary>
    [HttpGet]
    [Permiso("Servicios.Consultar", "Vehiculos.Consultar")]
    public async Task<IActionResult> ObtenerFormatoPatente(string nombreTipo)
    {
        if (string.IsNullOrWhiteSpace(nombreTipo))
        {
            return Json(new { success = false, formatoPatente = (string?)null, regex = (string?)null });
        }

        var tipo = await _tipoVehiculoService.ObtenerTipoVehiculoPorNombre(nombreTipo);
        if (tipo == null)
        {
            return Json(new { success = false, formatoPatente = (string?)null, regex = (string?)null });
        }

        return Json(new
        {
            success = true,
            formatoPatente = tipo.FormatoPatente,
            regex = tipo.ObtenerRegexPattern()
        });
    }

    /// <summary>
    /// Obtiene todos los tipos de vehículo con sus formatos de patente.
    /// </summary>
    [HttpGet]
    [Permiso("Servicios.Consultar", "Vehiculos.Consultar")]
    public async Task<IActionResult> ObtenerTiposConFormatos()
    {
        var tipos = await _tipoVehiculoService.ObtenerTiposVehiculosCompletos();
        return Json(tipos.Select(t => new
        {
            nombre = t.Nombre,
            formatoPatente = t.FormatoPatente,
            regex = t.ObtenerRegexPattern()
        }));
    }

    /// <summary>
    /// Eliminar tipo de vehículo con respuesta AJAX (incluye fallback).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Permiso("Servicios.Eliminar", "Vehiculos.Eliminar")]
    public async Task<IActionResult> EliminarTipoVehiculo(string nombreTipo)
    {
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            try
            {
                var resultado = await _types.EliminarVehiculoAjax(nombreTipo);
                if (resultado.Error != null)
                    return Json(new { success = false, message = resultado.Error });
                var capturas = resultado.Capturas!;
                var vehIdsEliminados = resultado.Ids!;
                foreach (var id in vehIdsEliminados)
                {
                    await RegistrarEliminacionTipoVehiculo(id, capturas);
                }

                var tiposActualizados = await _tipoVehiculoService.ObtenerTiposVehiculos();

                return Json(new
                {
                    success = true,
                    message = "Tipo de vehículo eliminado correctamente.",
                    tipos = tiposActualizados
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al eliminar el tipo de vehículo: {ex.Message}" });
            }
        }

        // Fallback
        return await ResponderEliminacionTipo(await _types.EliminarVehiculoFallback(nombreTipo),
            nombreTipo, "TipoVehiculo", "Eliminacion de tipo de vehiculo");
    }
    #endregion

    #region Vistas Parciales

    /// <summary>
    /// Devuelve el formulario parcial (usado por AJAX para editar/crear).
    /// </summary>
    [HttpGet]
    [Permiso("Servicios.Consultar")]
    public async Task<IActionResult> FormPartial(string id)
    {
        await CargarListasForm();
        Servicio servicio = null;
        if (!string.IsNullOrEmpty(id))
            servicio = await _servicioService.ObtenerServicio(id);

        return PartialView("_ServicioForm", servicio);
    }
    #endregion

    #region Métodos Privados - Procesamiento de Negocio

    /// <summary>
    /// Configura los estados por defecto cuando no se recibe ninguno desde la vista.
    /// </summary>
    /// <param name="estados">Lista de estados seleccionados por el usuario (puede ser null o vacía).</param>
    /// <returns>Lista con al menos un estado; si estaba vacía, contiene "Activo".</returns>
    private static List<string> ConfigurarEstadosDefecto(List<string> estados)
    {
        estados ??= new List<string>();
        if (!estados.Any())
            estados.Add("Activo");
        return estados;
    }

    /// <summary>
    /// Obtiene los servicios según los filtros y orden actual, calcula la paginación y las páginas visibles.
    /// </summary>
    /// <param name="estados">Estados a filtrar.</param>
    /// <param name="tipos">Tipos de servicio a filtrar.</param>
    /// <param name="tiposVehiculo">Tipos de vehículo a filtrar.</param>
    /// <param name="precioDesde">Precio mínimo.</param>
    /// <param name="precioHasta">Precio máximo.</param>
    /// <param name="pageNumber">Número de página actual (1-based).</param>
    /// <param name="pageSize">Cantidad de elementos por página.</param>
    /// <param name="sortBy">Campo por el cual ordenar.</param>
    /// <param name="sortOrder">Dirección del ordenamiento (asc/desc).</param>
    /// <returns>Tupla con la lista de servicios, página actual, total de páginas y páginas visibles.</returns>
    private async Task<(List<Servicio> servicios, int currentPage, int totalPages, List<int> visiblePages)>
        ObtenerDatosServicios(List<string> estados, List<string> tipos, List<string> tiposVehiculo,
        decimal? precioDesde, decimal? precioHasta, int pageNumber, int pageSize, string sortBy, string sortOrder)
    {
        var resultado = await _queries.ObtenerListado(estados, tipos, tiposVehiculo,
            precioDesde, precioHasta, pageNumber, pageSize, sortBy, sortOrder);
        var servicios = resultado.Servicios;
        var totalPages = resultado.TotalPages;
        var currentPage = Math.Clamp(pageNumber, 1, totalPages);
        var visiblePages = GetVisiblePages(currentPage, totalPages);

        return (servicios, currentPage, totalPages, visiblePages);
    }

    /// <summary>
    /// Carga las listas utilizadas por los dropdowns del formulario (tipos de servicio y de vehículo).
    /// </summary>
    /// <returns>Tupla con la lista de tipos de servicio y la de tipos de vehículo.</returns>
    private async Task<(List<string> tiposServicio, List<string> tiposVehiculo)> CargarListasDropdown()
    {
        var tiposServicio = await _tipoServicioService.ObtenerTiposServicio() ?? new List<string>();
        var tiposVehiculo = await _tipoVehiculoService.ObtenerTiposVehiculos() ?? new List<string>();
        return (tiposServicio, tiposVehiculo);
    }

    /// <summary>
    /// Configura el formulario en modo creación o edición según el ID de edición recibido.
    /// </summary>
    /// <param name="editId">ID del servicio a editar. Si es null o vacío, configura modo creación.</param>
    private async Task ConfigurarFormulario(string editId)
    {
        if (!string.IsNullOrEmpty(editId))
        {
            var servicio = await _servicioService.ObtenerServicio(editId);
            ViewBag.EditServicio = servicio;
            ViewBag.FormTitle = "Editando un Servicio";
            ViewBag.SubmitButtonText = "Guardar";
            ViewBag.ClearButtonText = "Cancelar";
            ViewBag.FormAction = "ActualizarServicioAjax";
        }
        else
        {
            ViewBag.FormTitle = "Registrando un Servicio";
            ViewBag.SubmitButtonText = "Registrar";
            ViewBag.ClearButtonText = "Limpiar Campos";
            ViewBag.FormAction = "CrearServicioAjax";
        }
    }

    /// <summary>
    /// Procesa la creación de un servicio aplicando validaciones y verificación de duplicados.
    /// </summary>
    /// <param name="servicio">Modelo de servicio a crear.</param>
    /// <returns>Resultado de la operación con éxito o detalle de error.</returns>
    private async Task<ResultadoOperacion> ProcesarCreacionServicio(Servicio servicio)
    {
        if (string.IsNullOrEmpty(servicio.Id))
        {
            servicio.Id = "temp-" + Guid.NewGuid().ToString();
            ModelState.Clear();
            TryValidateModel(servicio);
        }

        ValidateServicio(servicio);
        if (!ModelState.IsValid)
        {
            return ResultadoOperacion.CrearError("Por favor, complete todos los campos obligatorios correctamente.");
        }

        return AplicarResultadoFormulario(await _form.Crear(servicio));
    }

    /// <summary>
    /// Procesa la actualización de un servicio aplicando validaciones y verificación de duplicados.
    /// </summary>
    /// <param name="servicio">Modelo de servicio con los cambios a guardar.</param>
    /// <returns>Resultado de la operación con éxito o detalle de error.</returns>
    private async Task<ResultadoOperacion> ProcesarActualizacionServicio(Servicio servicio)
    {
        ValidateServicio(servicio);
        if (!ModelState.IsValid)
        {
            return ResultadoOperacion.CrearError("Por favor, complete todos los campos obligatorios correctamente.");
        }

        return AplicarResultadoFormulario(await _form.Actualizar(servicio));
    }

    private ResultadoOperacion AplicarResultadoFormulario(ServicioFormService.ResultadoFormulario resultado)
    {
        if (resultado.Error != null)
            ModelState.AddModelError(resultado.Error.Campo, resultado.Error.Mensaje);
        return resultado.Operacion;
    }

    /// <summary>
    /// Cambia el estado de un servicio y registra el evento de auditoría.
    /// </summary>
    /// <param name="id">ID del servicio a actualizar.</param>
    /// <param name="nuevoEstado">Nuevo estado a aplicar (por ejemplo, "Activo" o "Inactivo").</param>
    /// <param name="accionAuditoria">Texto de la acción a registrar en auditoría.</param>
    /// <returns>Redirección a <c>Index</c> tras aplicar el cambio.</returns>
    private async Task<IActionResult> CambiarEstadoServicio(string id, string nuevoEstado, string accionAuditoria)
    {
        try
        {
            var resultado = await _lifecycle.CambiarEstado(id, nuevoEstado);
            if (resultado.Error != null)
                return Json(new { success = false, message = resultado.Error });

            var servicioAntesCambio = resultado.ServicioAntes;
            var antesCambio = resultado.Antes;
            TempData["StateChangeEvent_UserId"] = id;
            TempData["StateChangeEvent_NewState"] = nuevoEstado;
            var despuesCambio = AuditCampos.CapturarServicio(await ObtenerServicioParaAuditoria(id));
            await RegistrarEvento(accionAuditoria, id, "Servicio", antesCambio, despuesCambio, servicioAntesCambio?.Nombre);
            return Json(new { success = true, message = nuevoEstado == "Inactivo" ? "Servicio desactivado correctamente." : "Servicio reactivado correctamente." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Error al cambiar el estado del servicio: {ex.Message}" });
        }
    }
    #endregion

    #region Métodos Privados - Respuestas y Manejo de Errores

    /// <summary>
    /// Prepara la respuesta de un envío AJAX de formulario, seteando headers y devolviendo el parcial del formulario.
    /// </summary>
    /// <param name="resultado">Resultado de la operación ejecutada.</param>
    /// <param name="servicio">Modelo de servicio enviado por el formulario.</param>
    /// <param name="accionAuditoria">Texto de acción a registrar en auditoría cuando es exitoso.</param>
    /// <returns>Partial view <c>_ServicioForm</c> con el formulario (limpio o con errores).</returns>
    /// <remarks>
    /// Headers usados por el cliente:
    /// - X-Form-Valid: "true" o "false"
    /// - X-Form-Message: mensaje de éxito cuando corresponde
    /// </remarks>
    private async Task<IActionResult> PrepararRespuestaAjax(ResultadoOperacion resultado, Servicio servicio, string accionAuditoria,
        Dictionary<string, string> antes = null, Dictionary<string, string> despues = null, string nombreHistorico = null)
    {
        if (!resultado.EsExitoso)
        {
            Response.Headers["X-Form-Valid"] = "false";
            await CargarListasForm();
            return PartialView("_ServicioForm", servicio);
        }

        await RegistrarEvento(accionAuditoria, servicio.Id, "Servicio", antes, despues, nombreHistorico);
        Response.Headers["X-Form-Valid"] = "true";
        Response.Headers["X-Form-Message"] = resultado.MensajeExito;
        await CargarListasForm();
        return PartialView("_ServicioForm", null);
    }

    /// <summary>
    /// Maneja excepciones en operaciones AJAX devolviendo el formulario parcial con errores.
    /// </summary>
    /// <param name="ex">Excepción capturada.</param>
    /// <param name="servicio">Modelo de servicio que se intentaba procesar.</param>
    /// <returns>Partial view <c>_ServicioForm</c> con ModelState poblado y encabezado de error.</returns>
    private async Task<IActionResult> ManejiarExcepcionAjax(Exception ex, Servicio servicio)
    {
        ModelState.AddModelError("", ex.Message);
        Response.Headers["X-Form-Valid"] = "false";
        await CargarListasForm();
        return PartialView("_ServicioForm", servicio);
    }
    #endregion

    #region Métodos Privados - Utilidades

    /// <summary>
    /// Calcula el conjunto de páginas visibles alrededor de la página actual.
    /// </summary>
    /// <param name="currentPage">Página actual (1-based).</param>
    /// <param name="totalPages">Total de páginas.</param>
    /// <param name="range">Cantidad de páginas a mostrar a cada lado de la actual.</param>
    /// <returns>Lista de números de página a mostrar.</returns>
    private List<int> GetVisiblePages(int currentPage, int totalPages, int range = 2)
    {
        var start = Math.Max(1, currentPage - range);
        var end = Math.Min(totalPages, currentPage + range);
        return Enumerable.Range(start, end - start + 1).ToList();
    }

    /// <summary>
    /// Valida campos del modelo de servicio y agrega errores a <see cref="ModelState"/>.
    /// </summary>
    /// <param name="servicio">Modelo a validar.</param>
    private void ValidateServicio(Servicio servicio)
    {
        foreach (var error in _form.Validar(servicio))
            ModelState.AddModelError(error.Campo, error.Mensaje);
    }

    /// <summary>
    /// Prepara la vista Index incluyendo el formulario con errores y recarga la tabla inicial.
    /// </summary>
    /// <param name="servicio">Modelo de servicio que produjo el error.</param>
    /// <param name="mensaje">Mensaje de error opcional para mostrar.</param>
    /// <returns>Vista Index con datos actualizados y formulario en estado de error.</returns>
    private async Task<IActionResult> PrepararVistaConError(Servicio servicio, string mensaje = null)
    {
        var (tiposServicio, tiposVehiculoList) = await CargarListasDropdown();
        ViewBag.TiposServicio = tiposServicio;
        ViewBag.TodosLosTipos = tiposServicio;
        ViewBag.TodosLosTiposVehiculo = tiposVehiculoList;
        ViewBag.EditServicio = servicio;

        bool esCreacion = servicio.Id?.StartsWith("temp-") == true;
        ViewBag.FormTitle = esCreacion ? "Registrando un Servicio" : "Editando un Servicio";
        ViewBag.SubmitButtonText = esCreacion ? "Registrar" : "Guardar";
        ViewBag.ClearButtonText = esCreacion ? "Limpiar Campos" : "Cancelar";
        ViewBag.FormAction = esCreacion ? "CrearServicioAjax" : "ActualizarServicioAjax";

        var servicios = await _servicioService.ObtenerServicios(
            new List<string> { "Activo" }, null, null, null, null, 1, 10, null, null);

        var totalPages = Math.Max(await _servicioService.ObtenerTotalPaginas(
            new List<string> { "Activo" }, null, null, null, null, 10), 1);

        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = GetVisiblePages(1, totalPages);
        ViewBag.CurrentPage = 1;

        return View("Index", servicios);
    }

    /// <summary>
    /// Copia a ViewBag la información común necesaria para renderizar la vista Index.
    /// </summary>
    /// <param name="estados">Estados activos en el filtro.</param>
    /// <param name="tipos">Tipos de servicio activos en el filtro.</param>
    /// <param name="tiposVehiculo">Tipos de vehículo activos en el filtro.</param>
    /// <param name="precioDesde">Precio mínimo.</param>
    /// <param name="precioHasta">Precio máximo.</param>
    /// <param name="tiposServicio">Lista completa para el dropdown de tipos de servicio.</param>
    /// <param name="tiposVehiculoList">Lista completa para el dropdown de tipos de vehículo.</param>
    /// <param name="pageSize">Tamaño de página actual.</param>
    /// <param name="currentPage">Página actual.</param>
    /// <param name="totalPages">Total de páginas.</param>
    /// <param name="visiblePages">Rango de páginas visibles.</param>
    /// <param name="sortBy">Campo de ordenamiento.</param>
    /// <param name="sortOrder">Dirección del ordenamiento.</param>
    private async Task ConfigurarViewBag(
        List<string> estados, List<string> tipos, List<string> tiposVehiculo,
        decimal? precioDesde, decimal? precioHasta,
        List<string> tiposServicio, List<string> tiposVehiculoList,
        int pageSize, int currentPage, int totalPages, List<int> visiblePages,
        string sortBy, string sortOrder)
    {
        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = visiblePages;
        ViewBag.CurrentPage = currentPage;
        ViewBag.Estados = estados;
        ViewBag.Tipos = tipos;
        ViewBag.TiposVehiculo = tiposVehiculo;
        ViewBag.PrecioDesde = precioDesde;
        ViewBag.PrecioHasta = precioHasta;
        ViewBag.TodosLosTiposVehiculo = tiposVehiculoList;
        ViewBag.PageSize = pageSize;
        ViewBag.TiposServicio = tiposServicio;
        ViewBag.TodosLosTipos = tiposServicio;
        ViewBag.SortBy = sortBy;
        ViewBag.SortOrder = sortOrder;

        // Obtener límites de precios para mostrar en el formulario
        ViewBag.PrecioMinimo = await _servicioService.ObtenerPrecioMinimo(estados, tipos, tiposVehiculo);
        ViewBag.PrecioMaximo = await _servicioService.ObtenerPrecioMaximo(estados, tipos, tiposVehiculo);
    }

    /// <summary>
    /// Registra un evento de auditoría asociado al usuario actual.
    /// </summary>
    /// <param name="accion">Nombre de la acción ejecutada (por ejemplo, "Creacion de servicio").</param>
    /// <param name="targetId">ID de la entidad objetivo sobre la que se actuó.</param>
    /// <param name="entidad">Tipo de entidad (por ejemplo, "Servicio").</param>
    private async Task<Servicio> ObtenerServicioParaAuditoria(string id) =>
        await _servicioService.ObtenerServicio(id) ?? throw new InvalidOperationException("No se encontró el servicio para auditoría.");

    private async Task RegistrarCreacionTipo(string accion, string id, string entidad)
    {
        Dictionary<string, string> despues;
        string nombre;
        if (entidad == "TipoServicio")
        {
            nombre = await _tipoServicioService.ObtenerNombrePorId(id) ?? throw new InvalidOperationException("No se encontró el tipo de servicio para auditoría.");
            despues = new() { ["Nombre"] = nombre };
        }
        else
        {
            var tipo = await _tipoVehiculoService.ObtenerParaAuditoriaPorId(id) ?? throw new InvalidOperationException("No se encontró el tipo de vehículo para auditoría.");
            nombre = tipo.Nombre;
            despues = AuditCampos.CapturarTipoVehiculo(tipo);
        }
        await RegistrarEvento(accion, id, entidad, new(), despues, nombre);
    }

    private async Task RegistrarEliminacionTipoVehiculo(string id, Dictionary<string, TipoVehiculo> capturas)
    {
        if (!capturas.TryGetValue(id, out var tipo))
            throw new InvalidOperationException("No se encontró el tipo de vehículo eliminado para auditoría.");
        await RegistrarEvento("Eliminacion de tipo de vehiculo", id, "TipoVehiculo", AuditCampos.CapturarTipoVehiculo(tipo), new(), tipo.Nombre);
    }

    private async Task RegistrarEvento(string accion, string targetId, string entidad,
        Dictionary<string, string> antes = null, Dictionary<string, string> despues = null, string nombreHistorico = null)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (antes != null || despues != null)
            await _auditService.LogEvent(userId, userEmail, accion, targetId, entidad, antes, despues, nombreHistorico);
        else
            await _auditService.LogEvent(userId, userEmail, accion, targetId, entidad);
    }

    /// <summary>
    /// Carga en ViewBag las listas necesarias para renderizar el formulario parcial.
    /// </summary>
    private async Task CargarListasForm()
    {
        ViewBag.TiposServicio = await _tipoServicioService.ObtenerTiposServicio() ?? new List<string>();
        ViewBag.TodosLosTiposVehiculo = await _tipoVehiculoService.ObtenerTiposVehiculos() ?? new List<string>();
    }

    private async Task<IActionResult> ResponderCreacionTipo(ServicioTypeService.Creacion resultado, string entidad, string accionAuditoria)
    {
        if (resultado.Error != null)
            TempData["Error"] = resultado.Error;
        else if (resultado.Id != null)
        {
            await RegistrarCreacionTipo(accionAuditoria, resultado.Id, entidad);
            TempData["Success"] = $"{entidad} creado correctamente.";
        }
        return RedirectToAction("Index");
    }

    private async Task<IActionResult> ResponderEliminacionTipo(ServicioTypeService.Eliminacion resultado,
        string nombreTipo, string entidad, string accionAuditoria)
    {
        if (resultado.Error != null)
            TempData["Error"] = resultado.Error;
        else if (resultado.Ids != null)
        {
            foreach (var id in resultado.Ids)
            {
                if (entidad == "TipoVehiculo")
                    await RegistrarEliminacionTipoVehiculo(id, resultado.Capturas!);
                else
                    await RegistrarEvento(accionAuditoria, id, entidad, new() { ["Nombre"] = nombreTipo }, new(), nombreTipo);
            }
            TempData["Success"] = $"{entidad} eliminado correctamente.";
        }
        return RedirectToAction("Index");
    }
    #endregion
}


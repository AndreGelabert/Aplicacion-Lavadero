using Firebase.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

/// <summary>
/// Controlador para la gestión de servicios del lavadero.
/// Maneja operaciones CRUD, tipos de servicio y tipos de vehículo.
/// </summary>
[Authorize(Roles = "Administrador")]
public class ServicioController : Controller
{
    #region Dependencias
    private readonly ServicioService _servicioService;
    private readonly AuditService _auditService;
    private readonly TipoServicioService _tipoServicioService;
    private readonly TipoVehiculoService _tipoVehiculoService;

    public ServicioController(
        ServicioService servicioService,
        AuditService auditService,
        TipoServicioService tipoServicioService,
        TipoVehiculoService tipoVehiculoService)
    {
        _servicioService = servicioService;
        _auditService = auditService;
        _tipoServicioService = tipoServicioService;
        _tipoVehiculoService = tipoVehiculoService;
    }
    #endregion

    #region Vistas Principales
    /// <summary>
    /// Página principal de servicios con filtros y paginación
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        List<string> estados,
        List<string> tipos,
        List<string> tiposVehiculo,
        string firstDocId = null,
        string lastDocId = null,
        int pageNumber = 1,
        int pageSize = 10,
        string editId = null,
        string sortBy = null,
        string sortOrder = null)
    {
        // Configurar estados por defecto
        estados = ConfigurarEstadosDefecto(estados);

        // Configurar ordenamiento por defecto
        sortBy ??= "Nombre";
        sortOrder ??= "asc";

        // Obtener datos de servicios
        var (servicios, currentPage, totalPages, visiblePages) = await ObtenerDatosServicios(
            estados, tipos, tiposVehiculo, pageNumber, pageSize, sortBy, sortOrder);

        // Cargar listas para dropdowns
        var (tiposServicio, tiposVehiculoList) = await CargarListasDropdown();

        // Configurar ViewBag
        ConfigurarViewBag(estados, tipos, tiposVehiculo, tiposServicio, tiposVehiculoList,
            pageSize, currentPage, totalPages, visiblePages,
            servicios.FirstOrDefault()?.Id, servicios.LastOrDefault()?.Id, sortBy, sortOrder);

        // Configurar formulario (creación vs edición)
        await ConfigurarFormulario(editId);

        return View(servicios);
    }
    #endregion

    #region Operaciones CRUD - Formulario Tradicional
    /// <summary>
    /// Crear un nuevo servicio (formulario tradicional)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CrearServicio(Servicio servicio, string? EtapasJson)
    {
        try
        {
            // Parsear y asignar etapas si existen
            if (!string.IsNullOrEmpty(EtapasJson))
            {
                servicio.Etapas = System.Text.Json.JsonSerializer.Deserialize<List<Etapa>>(EtapasJson) ?? new List<Etapa>();
            }

            var resultado = await ProcesarCreacionServicio(servicio);
            if (!resultado.EsExitoso)
            {
                return await PrepararVistaConError(servicio, resultado.MensajeError);
            }

            await RegistrarEvento("Creacion de servicio", servicio.Id, "Servicio");
            TempData["Success"] = "Servicio creado correctamente.";
            return RedirectToAction("Index");
        }
        catch (Exception ex)
        {
            return await ManejiarExcepcion(ex, servicio);
        }
    }

    /// <summary>
    /// Actualizar un servicio existente (formulario tradicional)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ActualizarServicio(Servicio servicio, string? EtapasJson)
    {
        try
        {
            // Parsear y asignar etapas si existen
            if (!string.IsNullOrEmpty(EtapasJson))
            {
                servicio.Etapas = System.Text.Json.JsonSerializer.Deserialize<List<Etapa>>(EtapasJson) ?? new List<Etapa>();
            }

            var resultado = await ProcesarActualizacionServicio(servicio);
            if (!resultado.EsExitoso)
            {
                return await PrepararVistaConError(servicio, resultado.MensajeError);
            }

            await RegistrarEvento("Actualizacion de servicio", servicio.Id, "Servicio");
            TempData["Success"] = "Servicio actualizado correctamente.";
            return RedirectToAction("Index");
        }
        catch (Exception ex)
        {
            return await ManejiarExcepcion(ex, servicio);
        }
    }

    /// <summary>
    /// Busca servicios por término de búsqueda
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> SearchPartial(
        string searchTerm,
        List<string> estados,
        List<string> tipos,
        List<string> tiposVehiculo,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = null,
        string sortOrder = null)
    {
        estados = ConfigurarEstadosDefecto(estados);

        sortBy ??= "Nombre";
        sortOrder ??= "asc";

        var servicios = await _servicioService.BuscarServicios(
            searchTerm, estados, tipos, tiposVehiculo, pageNumber, pageSize, sortBy, sortOrder);

        var totalServicios = await _servicioService.ObtenerTotalServiciosBusqueda(
            searchTerm, estados, tipos, tiposVehiculo);

        var totalPages = Math.Max((int)Math.Ceiling(totalServicios / (double)pageSize), 1);

        ViewBag.CurrentPage = pageNumber;
        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = GetVisiblePages(pageNumber, totalPages);
        ViewBag.Estados = estados;
        ViewBag.Tipos = tipos;
        ViewBag.TiposVehiculo = tiposVehiculo;
        ViewBag.SortBy = sortBy;
        ViewBag.SortOrder = sortOrder;
        ViewBag.SearchTerm = searchTerm;

        return PartialView("_ServicioTable", servicios);
    }
    #endregion

    #region Operaciones CRUD - AJAX
    /// <summary>
    /// Crear servicio vía AJAX
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearServicioAjax(Servicio servicio, string? EtapasJson)
    {
        try
        {
            // Parsear y asignar etapas si existen
            if (!string.IsNullOrEmpty(EtapasJson))
            {
                servicio.Etapas = System.Text.Json.JsonSerializer.Deserialize<List<Etapa>>(EtapasJson) ?? new List<Etapa>();
            }

            var resultado = await ProcesarCreacionServicio(servicio);
            return await PrepararRespuestaAjax(resultado, servicio, "Creacion (AJAX) de servicio");
        }
        catch (Exception ex)
        {
            return await ManejiarExcepcionAjax(ex, servicio);
        }
    }

    /// <summary>
    /// Actualizar servicio vía AJAX
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActualizarServicioAjax(Servicio servicio, string? EtapasJson)
    {
        try
        {
            // Parsear y asignar etapas si existen
            if (!string.IsNullOrEmpty(EtapasJson))
            {
                servicio.Etapas = System.Text.Json.JsonSerializer.Deserialize<List<Etapa>>(EtapasJson) ?? new List<Etapa>();
            }

            var resultado = await ProcesarActualizacionServicio(servicio);
            return await PrepararRespuestaAjax(resultado, servicio, "Actualizacion (AJAX) de servicio");
        }
        catch (Exception ex)
        {
            return await ManejiarExcepcionAjax(ex, servicio);
        }
    }
    #endregion

    #region Cambio de Estado
    /// <summary>
    /// Desactivar un servicio
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> DeactivateServicio(string id)
    {
        return await CambiarEstadoServicio(id, "Inactivo", "Desactivacion de servicio");
    }

    /// <summary>
    /// Reactivar un servicio
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ReactivateServicio(string id)
    {
        return await CambiarEstadoServicio(id, "Activo", "Reactivacion de servicio");
    }
    #endregion

    #region Gestión de Tipos (Servicio y Vehículo)

    /// <summary>
    /// Crear nuevo tipo de servicio con respuesta AJAX
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearTipoServicio(string nombreTipo)
    {
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            // Respuesta AJAX
            try
            {
                if (string.IsNullOrWhiteSpace(nombreTipo))
                {
                    return Json(new { success = false, message = "El nombre del tipo de servicio es obligatorio." });
                }

                if (await _tipoServicioService.ExisteTipoServicio(nombreTipo))
                {
                    return Json(new { success = false, message = "Ya existe un tipo de servicio con el mismo nombre." });
                }

                // Crear tipo de servicio (AJAX)
                var docId = await _tipoServicioService.CrearTipoServicio(nombreTipo);
                await RegistrarEvento("Creacion de tipo de servicio", docId, "TipoServicio");

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

        // Fallback Crear Tipo Servicio
        return await GestionarTipoConId(
            nombreTipo,
            () => _tipoServicioService.ExisteTipoServicio(nombreTipo),
            () => _tipoServicioService.CrearTipoServicio(nombreTipo),
            "TipoServicio",
            "Creacion de tipo de servicio"
        );
    }

    /// <summary>
    /// Eliminar tipo de servicio con respuesta AJAX
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarTipoServicio(string nombreTipo)
    {
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            // Respuesta AJAX
            try
            {
                if (string.IsNullOrWhiteSpace(nombreTipo))
                {
                    return Json(new { success = false, message = "Debe seleccionar un tipo de servicio." });
                }

                var serviciosUsandoTipo = await _servicioService.ObtenerServiciosPorTipo(nombreTipo);
                if (serviciosUsandoTipo.Any())
                {
                    return Json(new
                    {
                        success = false,
                        message = "No se puede eliminar el tipo de servicio porque hay servicios que lo utilizan."
                    });
                }

                // Eliminar tipo de servicio (AJAX)
                var idsEliminados = await _tipoServicioService.EliminarTipoServicio(nombreTipo);
                foreach (var id in idsEliminados)
                {
                    await RegistrarEvento("Eliminacion de tipo de servicio", id, "TipoServicio");
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

        // Fallback Eliminar Tipo Servicio
        return await EliminarTipoConIds(
            nombreTipo,
            () => _servicioService.ObtenerServiciosPorTipo(nombreTipo),
            () => _tipoServicioService.EliminarTipoServicio(nombreTipo),
            "TipoServicio",
            "Eliminacion de tipo de servicio"
        );
    }

    /// <summary>
    /// Crear nuevo tipo de vehículo con respuesta AJAX
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearTipoVehiculo(string nombreTipo)
    {
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            // Respuesta AJAX
            try
            {
                if (string.IsNullOrWhiteSpace(nombreTipo))
                {
                    return Json(new { success = false, message = "El nombre del tipo de vehículo es obligatorio." });
                }

                if (await _tipoVehiculoService.ExisteTipoVehiculo(nombreTipo))
                {
                    return Json(new { success = false, message = "Ya existe un tipo de vehículo con el mismo nombre." });
                }

                // Crear tipo de vehículo (AJAX)
                var vehDocId = await _tipoVehiculoService.CrearTipoVehiculo(nombreTipo);
                await RegistrarEvento("Creacion de tipo de vehiculo", vehDocId, "TipoVehiculo");

                var tiposActualizados = await _tipoVehiculoService.ObtenerTiposVehiculos();

                return Json(new
                {
                    success = true,
                    message = "Tipo de vehículo creado correctamente.",
                    tipos = tiposActualizados
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al crear el tipo de vehículo: {ex.Message}" });
            }
        }

        // Fallback Crear Tipo Vehículo
        return await GestionarTipoConId(
            nombreTipo,
            () => _tipoVehiculoService.ExisteTipoVehiculo(nombreTipo),
            () => _tipoVehiculoService.CrearTipoVehiculo(nombreTipo),
            "TipoVehiculo",
            "Creacion de tipo de vehiculo"
        );
    }

    /// <summary>
    /// Eliminar tipo de vehículo con respuesta AJAX
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarTipoVehiculo(string nombreTipo)
    {
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            // Respuesta AJAX
            try
            {
                if (string.IsNullOrWhiteSpace(nombreTipo))
                {
                    return Json(new { success = false, message = "Debe seleccionar un tipo de vehículo." });
                }

                var serviciosUsandoTipo = await _servicioService.ObtenerServiciosPorTipoVehiculo(nombreTipo);
                if (serviciosUsandoTipo.Any())
                {
                    return Json(new
                    {
                        success = false,
                        message = "No se puede eliminar el tipo de vehículo porque hay servicios que lo utilizan."
                    });
                }

                // Eliminar tipo de vehículo (AJAX)
                var vehIdsEliminados = await _tipoVehiculoService.EliminarTipoVehiculo(nombreTipo);
                foreach (var id in vehIdsEliminados)
                {
                    await RegistrarEvento("Eliminacion de tipo de vehiculo", id, "TipoVehiculo");
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

        // Fallback Eliminar Tipo Vehículo
        return await EliminarTipoConIds(
            nombreTipo,
            () => _servicioService.ObtenerServiciosPorTipoVehiculo(nombreTipo),
            () => _tipoVehiculoService.EliminarTipoVehiculo(nombreTipo),
            "TipoVehiculo",
            "Eliminacion de tipo de vehiculo"
        );
    }

    #endregion

    #region Vistas Parciales
    /// <summary>
    /// Obtener formulario parcial para AJAX
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> FormPartial(string id)
    {
        await CargarListasForm();
        Servicio servicio = null;
        if (!string.IsNullOrEmpty(id))
            servicio = await _servicioService.ObtenerServicio(id);

        return PartialView("_ServicioForm", servicio);
    }

    /// <summary>
    /// Obtener tabla parcial para AJAX
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> TablePartial(
        List<string> estados,
        List<string> tipos,
        List<string> tiposVehiculo,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = null,
        string sortOrder = null)
    {
        estados = ConfigurarEstadosDefecto(estados);
        
        // Configurar ordenamiento por defecto
        sortBy ??= "Nombre";
        sortOrder ??= "asc";

        var servicios = await _servicioService.ObtenerServicios(estados, tipos, tiposVehiculo, pageNumber, pageSize, sortBy, sortOrder);
        var totalPages = await _servicioService.ObtenerTotalPaginas(estados, tipos, tiposVehiculo, pageSize);
        totalPages = Math.Max(totalPages, 1);

        ViewBag.CurrentPage = pageNumber;
        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = GetVisiblePages(pageNumber, totalPages);
        ViewBag.Estados = estados;
        ViewBag.Tipos = tipos;
        ViewBag.TiposVehiculo = tiposVehiculo;
        ViewBag.SortBy = sortBy;
        ViewBag.SortOrder = sortOrder;

        return PartialView("_ServicioTable", servicios);
    }
    #endregion

    #region Métodos Privados - Procesamiento de Negocio

    /// <summary>
    /// Configura los estados por defecto si no se especificaron
    /// </summary>
    private static List<string> ConfigurarEstadosDefecto(List<string> estados)
    {
        estados ??= new List<string>();
        if (!estados.Any())
            estados.Add("Activo");
        return estados;
    }

    /// <summary>
    /// Obtiene los datos de servicios con paginación y ordenamiento
    /// </summary>
    private async Task<(List<Servicio> servicios, int currentPage, int totalPages, List<int> visiblePages)>
        ObtenerDatosServicios(List<string> estados, List<string> tipos, List<string> tiposVehiculo, 
        int pageNumber, int pageSize, string sortBy, string sortOrder)
    {
        var servicios = await _servicioService.ObtenerServicios(estados, tipos, tiposVehiculo, pageNumber, pageSize, sortBy, sortOrder);
        var totalPages = Math.Max(await _servicioService.ObtenerTotalPaginas(estados, tipos, tiposVehiculo, pageSize), 1);
        var currentPage = Math.Clamp(pageNumber, 1, totalPages);
        var visiblePages = GetVisiblePages(currentPage, totalPages);

        return (servicios, currentPage, totalPages, visiblePages);
    }

    /// <summary>
    /// Carga las listas para los dropdowns
    /// </summary>
    private async Task<(List<string> tiposServicio, List<string> tiposVehiculo)> CargarListasDropdown()
    {
        var tiposServicio = await _tipoServicioService.ObtenerTiposServicio() ?? new List<string>();
        var tiposVehiculo = await _tipoVehiculoService.ObtenerTiposVehiculos() ?? new List<string>();
        return (tiposServicio, tiposVehiculo);
    }

    /// <summary>
    /// Configura el formulario según si es creación o edición
    /// </summary>
    private async Task ConfigurarFormulario(string editId)
    {
        if (!string.IsNullOrEmpty(editId))
        {
            var servicio = await _servicioService.ObtenerServicio(editId);
            ViewBag.EditServicio = servicio;
            ViewBag.FormTitle = "Editando un Servicio";
            ViewBag.SubmitButtonText = "Guardar";
            ViewBag.ClearButtonText = "Cancelar";
            ViewBag.FormAction = "ActualizarServicio";
        }
        else
        {
            ViewBag.FormTitle = "Registrando un Servicio";
            ViewBag.SubmitButtonText = "Registrar";
            ViewBag.ClearButtonText = "Limpiar Campos";
            ViewBag.FormAction = "CrearServicio";
        }
    }

    /// <summary>
    /// Procesa la creación de un servicio con validaciones
    /// </summary>
    private async Task<ResultadoOperacion> ProcesarCreacionServicio(Servicio servicio)
    {
        // Asignar ID temporal si no lo tiene
        if (string.IsNullOrEmpty(servicio.Id))
        {
            servicio.Id = "temp-" + Guid.NewGuid().ToString();
            ModelState.Clear();
            TryValidateModel(servicio);
        }

        // Validaciones
        ValidateServicio(servicio);
        if (!ModelState.IsValid)
        {
            return ResultadoOperacion.CrearError("Por favor, complete todos los campos obligatorios correctamente.");
        }

        // Verificar duplicados
        if (await _servicioService.ExisteServicioConNombreTipoVehiculo(servicio.Nombre, servicio.TipoVehiculo))
        {
            var mensaje = $"Ya existe un servicio con el nombre '{servicio.Nombre}' para vehículos tipo '{servicio.TipoVehiculo}'.";
            ModelState.AddModelError("Nombre", $"Ya existe un servicio con este nombre para vehículos tipo '{servicio.TipoVehiculo}'.");
            return ResultadoOperacion.CrearError(mensaje);
        }

        // Crear servicio
        servicio.Estado = "Activo";
        await _servicioService.CrearServicio(servicio);
        return ResultadoOperacion.CrearExito("Servicio creado correctamente.");
    }

    /// <summary>
    /// Procesa la actualización de un servicio con validaciones
    /// </summary>
    private async Task<ResultadoOperacion> ProcesarActualizacionServicio(Servicio servicio)
    {
        // Validaciones
        ValidateServicio(servicio);
        if (!ModelState.IsValid)
        {
            return ResultadoOperacion.CrearError("Por favor, complete todos los campos obligatorios correctamente.");
        }

        // Verificar que el servicio existe
        var servicioActual = await _servicioService.ObtenerServicio(servicio.Id);
        if (servicioActual == null)
        {
            ModelState.AddModelError("", "No se pudo encontrar el servicio a actualizar.");
            return ResultadoOperacion.CrearError("No se pudo encontrar el servicio a actualizar.");
        }

        // Verificar duplicados (excluyendo el actual)
        if (await _servicioService.ExisteServicioConNombreTipoVehiculo(servicio.Nombre, servicio.TipoVehiculo, servicio.Id))
        {
            var mensaje = $"Ya existe un servicio con el nombre '{servicio.Nombre}' para vehículos tipo '{servicio.TipoVehiculo}'.";
            ModelState.AddModelError("Nombre", $"Ya existe un servicio con este nombre para vehículos tipo '{servicio.TipoVehiculo}'.");
            return ResultadoOperacion.CrearError(mensaje);
        }

        // Actualizar servicio
        servicio.Estado = servicioActual.Estado;
        await _servicioService.ActualizarServicio(servicio);
        return ResultadoOperacion.CrearExito("Servicio actualizado correctamente.");
    }

    /// <summary>
    /// Cambia el estado de un servicio
    /// </summary>
    private async Task<IActionResult> CambiarEstadoServicio(string id, string nuevoEstado, string accionAuditoria)
    {
        await _servicioService.CambiarEstadoServicio(id, nuevoEstado);
        TempData["StateChangeEvent_UserId"] = id;
        TempData["StateChangeEvent_NewState"] = nuevoEstado;
        await RegistrarEvento(accionAuditoria, id, "Servicio");
        return RedirectToAction("Index");
    }

    /// <summary>
    /// Gestiona la creación de tipos (servicio/vehículo)
    /// </summary>
    private async Task<IActionResult> GestionarTipo(
        string nombreTipo,
        Func<Task<bool>> verificarExistencia,
        Func<Task> crear,
        string tipoDescripcion,
        string accionAuditoria)
    {
        if (!string.IsNullOrWhiteSpace(nombreTipo))
        {
            if (await verificarExistencia())
            {
                TempData["Error"] = $"Ya existe un {tipoDescripcion} con el mismo nombre.";
            }
            else
            {
                await crear();
                await RegistrarEvento(accionAuditoria, nombreTipo, tipoDescripcion.Contains("servicio") ? "TipoServicio" : "TipoVehiculo");
                TempData["Success"] = $"{char.ToUpper(tipoDescripcion[0])}{tipoDescripcion[1..]} creado correctamente.";
            }
        }
        return RedirectToAction("Index");
    }

    /// <summary>
    /// Gestiona la eliminación de tipos con verificación de uso
    /// </summary>
    private async Task<IActionResult> EliminarTipo(
        string nombreTipo,
        Func<Task<List<Servicio>>> obtenerServiciosUsandoTipo,
        Func<Task> eliminar,
        string tipoDescripcion,
        string accionAuditoria)
    {
        if (!string.IsNullOrWhiteSpace(nombreTipo))
        {
            var serviciosUsandoTipo = await obtenerServiciosUsandoTipo();
            if (serviciosUsandoTipo.Any())
            {
                TempData["Error"] = $"No se puede eliminar el {tipoDescripcion} porque hay servicios que lo utilizan.";
            }
            else
            {
                await eliminar();
                await RegistrarEvento(accionAuditoria, nombreTipo, tipoDescripcion.Contains("servicio") ? "TipoServicio" : "TipoVehiculo");
                TempData["Success"] = $"{char.ToUpper(tipoDescripcion[0])}{tipoDescripcion[1..]} eliminado correctamente.";
            }
        }
        return RedirectToAction("Index");
    }
    // Nuevo: crear tipo registrando ID real
    private async Task<IActionResult> GestionarTipoConId(
        string nombreTipo,
        Func<Task<bool>> verificarExistencia,
        Func<Task<string>> crearConId,
        string entidad,
        string accionAuditoria)
    {
        if (!string.IsNullOrWhiteSpace(nombreTipo))
        {
            if (await verificarExistencia())
            {
                TempData["Error"] = $"Ya existe un {entidad} con el mismo nombre.";
            }
            else
            {
                var id = await crearConId();
                await RegistrarEvento(accionAuditoria, id, entidad);
                TempData["Success"] = $"{entidad} creado correctamente.";
            }
        }
        return RedirectToAction("Index");
    }

    // Nuevo: eliminar tipo registrando todos los IDs eliminados
    private async Task<IActionResult> EliminarTipoConIds(
        string nombreTipo,
        Func<Task<List<Servicio>>> obtenerServiciosUsandoTipo,
        Func<Task<List<string>>> eliminarConIds,
        string entidad,
        string accionAuditoria)
    {
        if (!string.IsNullOrWhiteSpace(nombreTipo))
        {
            var serviciosUsandoTipo = await obtenerServiciosUsandoTipo();
            if (serviciosUsandoTipo.Any())
            {
                TempData["Error"] = $"No se puede eliminar {entidad} porque hay servicios que lo utilizan.";
            }
            else
            {
                var ids = await eliminarConIds();
                foreach (var id in ids)
                    await RegistrarEvento(accionAuditoria, id, entidad);

                TempData["Success"] = $"{entidad} eliminado correctamente.";
            }
        }
        return RedirectToAction("Index");
    }
    #endregion

    #region Métodos Privados - Respuestas y Manejo de Errores

    /// <summary>
    /// Prepara respuesta AJAX según el resultado de la operación
    /// </summary>
    private async Task<IActionResult> PrepararRespuestaAjax(ResultadoOperacion resultado, Servicio servicio, string accionAuditoria)
    {
        if (!resultado.EsExitoso)
        {
            Response.Headers["X-Form-Valid"] = "false";
            await CargarListasForm();
            return PartialView("_ServicioForm", servicio);
        }

        await RegistrarEvento(accionAuditoria, servicio.Id, "Servicio");
        Response.Headers["X-Form-Valid"] = "true";
        Response.Headers["X-Form-Message"] = resultado.MensajeExito;
        await CargarListasForm();
        return PartialView("_ServicioForm", null); // Formulario limpio
    }

    /// <summary>
    /// Maneja excepciones en operaciones tradicionales
    /// </summary>
    private async Task<IActionResult> ManejiarExcepcion(Exception ex, Servicio servicio)
    {
        var mensaje = ex is ArgumentException
            ? ex.Message
            : $"Error al procesar servicio: {ex.Message}";

        TempData["Error"] = mensaje;
        ModelState.AddModelError("", mensaje);
        return await PrepararVistaConError(servicio, mensaje);
    }

    /// <summary>
    /// Maneja excepciones en operaciones AJAX
    /// </summary>
    private async Task<IActionResult> ManejiarExcepcionAjax(Exception ex, Servicio servicio)
    {
        ModelState.AddModelError("", ex.Message);
        Response.Headers["X-Form-Valid"] = "false";
        await CargarListasForm();
        return PartialView("_ServicioForm", servicio);
    }
    #endregion

    #region Métodos Privados - Utilidades

    private List<int> GetVisiblePages(int currentPage, int totalPages, int range = 2)
    {
        var start = Math.Max(1, currentPage - range);
        var end = Math.Min(totalPages, currentPage + range);
        return Enumerable.Range(start, end - start + 1).ToList();
    }

    private void ValidateServicio(Servicio servicio)
    {
        if (!string.IsNullOrEmpty(servicio.Nombre) && !System.Text.RegularExpressions.Regex.IsMatch(servicio.Nombre, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$"))
        {
            ModelState.AddModelError("Nombre", "El nombre solo puede contener letras y espacios.");
        }

        if (servicio.Precio < 0)
        {
            ModelState.AddModelError("Precio", "El precio debe ser igual o mayor a 0.");
        }

        if (servicio.TiempoEstimado <= 0)
        {
            ModelState.AddModelError("TiempoEstimado", "El tiempo estimado debe ser mayor a 0.");
        }
    }

    private async Task<IActionResult> PrepararVistaConError(Servicio servicio, string mensaje = null)
    {
        var (tiposServicio, tiposVehiculoList) = await CargarListasDropdown();
        ViewBag.TiposServicio = tiposServicio;
        ViewBag.TodosLosTipos = tiposServicio;
        ViewBag.TodosLosTiposVehiculo = tiposVehiculoList;
        ViewBag.EditServicio = servicio;

        bool esCreacion = servicio.Id.StartsWith("temp-");
        ViewBag.FormTitle = esCreacion ? "Registrando un Servicio" : "Editando un Servicio";
        ViewBag.SubmitButtonText = esCreacion ? "Registrar" : "Guardar";
        ViewBag.ClearButtonText = esCreacion ? "Limpiar Campos" : "Cancelar";
        ViewBag.FormAction = esCreacion ? "CrearServicio" : "ActualizarServicio";

        var servicios = await _servicioService.ObtenerServicios(
            new List<string> { "Activo" }, null, null, null, null, 1, 10);
        var totalPages = Math.Max(await _servicioService.ObtenerTotalPaginas(
            new List<string> { "Activo" }, null, null, 10), 1);

        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = GetVisiblePages(1, totalPages);
        ViewBag.CurrentPage = 1;

        return View("Index", servicios);
    }

    private void ConfigurarViewBag(
        List<string> estados, List<string> tipos, List<string> tiposVehiculo,
        List<string> tiposServicio, List<string> tiposVehiculoList,
        int pageSize, int currentPage, int totalPages, List<int> visiblePages,
        string firstDocId, string lastDocId, string sortBy, string sortOrder)
    {
        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = visiblePages;
        ViewBag.CurrentPage = currentPage;
        ViewBag.Estados = estados;
        ViewBag.Tipos = tipos;
        ViewBag.TiposVehiculo = tiposVehiculo;
        ViewBag.TodosLosTiposVehiculo = tiposVehiculoList;
        ViewBag.PageSize = pageSize;
        ViewBag.FirstDocId = firstDocId;
        ViewBag.LastDocId = lastDocId;
        ViewBag.TiposServicio = tiposServicio;
        ViewBag.TodosLosTipos = tiposServicio;
        ViewBag.SortBy = sortBy;
        ViewBag.SortOrder = sortOrder;
    }

    #endregion

    #region Métodos Privados - Auditoría y Carga de Datos

    private async Task RegistrarEvento(string accion, string targetId, string entidad)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        await _auditService.LogEvent(userId, userEmail, accion, targetId, entidad);
    }

    private async Task CargarListasForm()
    {
        ViewBag.TiposServicio = await _tipoServicioService.ObtenerTiposServicio() ?? new List<string>();
        ViewBag.TodosLosTiposVehiculo = await _tipoVehiculoService.ObtenerTiposVehiculos() ?? new List<string>();
    }
    #endregion
}

/// <summary>
/// Clase auxiliar para representar el resultado de una operación
/// </summary>
public class ResultadoOperacion
{
    /// <summary>
    /// Indica si la operación fue exitosa
    /// </summary>
    public bool EsExitoso { get; private set; }

    /// <summary>
    /// Mensaje de éxito si la operación fue exitosa
    /// </summary>
    public string MensajeExito { get; private set; }

    /// <summary>
    /// Mensaje de error si la operación falló
    /// </summary>
    public string MensajeError { get; private set; }

    /// <summary>
    /// Constructor privado para forzar el uso de métodos factory
    /// </summary>
    private ResultadoOperacion() { }

    /// <summary>
    /// Crea un resultado exitoso
    /// </summary>
    public static ResultadoOperacion CrearExito(string mensaje)
    {
        return new ResultadoOperacion
        {
            EsExitoso = true,
            MensajeExito = mensaje
        };
    }

    /// <summary>
    /// Crea un resultado de error
    /// </summary>
    public static ResultadoOperacion CrearError(string mensaje)
    {
        return new ResultadoOperacion
        {
            EsExitoso = false,
            MensajeError = mensaje
        };
    }
}
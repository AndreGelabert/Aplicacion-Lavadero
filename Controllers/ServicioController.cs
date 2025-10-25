using Firebase.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

/// <summary>
/// Controlador para la gestión de servicios del lavadero.
/// Incluye filtros, paginación, búsqueda, acciones AJAX de creación/actualización,
/// y administración de tipos (servicio/vehículo).
/// </summary>
[Authorize(Roles = "Administrador")]
public class ServicioController : Controller
{
    #region Dependencias
    private readonly ServicioService _servicioService;
    private readonly AuditService _auditService;
    private readonly TipoServicioService _tipoServicioService;
    private readonly TipoVehiculoService _tipoVehiculoService;

    /// <summary>
    /// Crea una nueva instancia del controlador de servicios.
    /// </summary>
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
    /// Página principal de servicios con filtros, orden y paginación.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        List<string> estados,
        List<string> tipos,
        List<string> tiposVehiculo,
        int pageNumber = 1,
        int pageSize = 10,
        string editId = null,
        string sortBy = null,
        string sortOrder = null)
    {
        estados = ConfigurarEstadosDefecto(estados);

        sortBy ??= "Nombre";
        sortOrder ??= "asc";

        var (servicios, currentPage, totalPages, visiblePages) = await ObtenerDatosServicios(
            estados, tipos, tiposVehiculo, pageNumber, pageSize, sortBy, sortOrder);

        var (tiposServicio, tiposVehiculoList) = await CargarListasDropdown();

        ConfigurarViewBag(estados, tipos, tiposVehiculo, tiposServicio, tiposVehiculoList,
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
    public async Task<IActionResult> CrearServicioAjax(Servicio servicio, string? EtapasJson)
    {
        try
        {
            if (!string.IsNullOrEmpty(EtapasJson))
            {
                servicio.Etapas = System.Text.Json.JsonSerializer.Deserialize<List<Etapa>>(EtapasJson) ?? new List<Etapa>();
            }

            var resultado = await ProcesarCreacionServicio(servicio);
            return await PrepararRespuestaAjax(resultado, servicio, "Creacion de servicio");
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
    public async Task<IActionResult> ActualizarServicioAjax(Servicio servicio, string? EtapasJson)
    {
        try
        {
            if (!string.IsNullOrEmpty(EtapasJson))
            {
                servicio.Etapas = System.Text.Json.JsonSerializer.Deserialize<List<Etapa>>(EtapasJson) ?? new List<Etapa>();
            }

            var resultado = await ProcesarActualizacionServicio(servicio);
            return await PrepararRespuestaAjax(resultado, servicio, "Actualizacion de servicio");
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

    /// <summary>
    /// Devuelve la tabla parcial (sin búsqueda) con filtros y orden.
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

    #region Cambio de Estado

    /// <summary>
    /// Desactiva un servicio.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> DeactivateServicio(string id)
        => await CambiarEstadoServicio(id, "Inactivo", "Desactivacion de servicio");

    /// <summary>
    /// Reactiva un servicio.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ReactivateServicio(string id)
        => await CambiarEstadoServicio(id, "Activo", "Reactivacion de servicio");
    #endregion

    #region Gestión de Tipos (Servicio y Vehículo)

    /// <summary>
    /// Crear nuevo tipo de servicio con respuesta AJAX (incluye fallback).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearTipoServicio(string nombreTipo)
    {
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
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

        // Fallback (mantener por si se usa fuera de AJAX)
        return await GestionarTipoConId(
            nombreTipo,
            () => _tipoServicioService.ExisteTipoServicio(nombreTipo),
            () => _tipoServicioService.CrearTipoServicio(nombreTipo),
            "TipoServicio",
            "Creacion de tipo de servicio"
        );
    }

    /// <summary>
    /// Eliminar tipo de servicio con respuesta AJAX (incluye fallback).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarTipoServicio(string nombreTipo)
    {
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
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

        // Fallback
        return await EliminarTipoConIds(
            nombreTipo,
            () => _servicioService.ObtenerServiciosPorTipo(nombreTipo),
            () => _tipoServicioService.EliminarTipoServicio(nombreTipo),
            "TipoServicio",
            "Eliminacion de tipo de servicio"
        );
    }

    /// <summary>
    /// Crear nuevo tipo de vehículo con respuesta AJAX (incluye fallback).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearTipoVehiculo(string nombreTipo)
    {
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
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

        // Fallback
        return await GestionarTipoConId(
            nombreTipo,
            () => _tipoVehiculoService.ExisteTipoVehiculo(nombreTipo),
            () => _tipoVehiculoService.CrearTipoVehiculo(nombreTipo),
            "TipoVehiculo",
            "Creacion de tipo de vehiculo"
        );
    }

    /// <summary>
    /// Eliminar tipo de vehículo con respuesta AJAX (incluye fallback).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarTipoVehiculo(string nombreTipo)
    {
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
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

        // Fallback
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
    /// Devuelve el formulario parcial (usado por AJAX para editar/crear).
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
    /// <param name="pageNumber">Número de página actual (1-based).</param>
    /// <param name="pageSize">Cantidad de elementos por página.</param>
    /// <param name="sortBy">Campo por el cual ordenar.</param>
    /// <param name="sortOrder">Dirección del ordenamiento (asc/desc).</param>
    /// <returns>Tupla con la lista de servicios, página actual, total de páginas y páginas visibles.</returns>
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

        if (await _servicioService.ExisteServicioConNombreTipoVehiculo(servicio.Nombre, servicio.TipoVehiculo))
        {
            var mensaje = $"Ya existe un servicio con el nombre '{servicio.Nombre}' para vehículos tipo '{servicio.TipoVehiculo}'.";
            ModelState.AddModelError("Nombre", $"Ya existe un servicio con este nombre para vehículos tipo '{servicio.TipoVehiculo}'.");
            return ResultadoOperacion.CrearError(mensaje);
        }

        servicio.Estado = "Activo";
        await _servicioService.CrearServicio(servicio);
        return ResultadoOperacion.CrearExito("Servicio creado correctamente.");
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

        var servicioActual = await _servicioService.ObtenerServicio(servicio.Id);
        if (servicioActual == null)
        {
            ModelState.AddModelError("", "No se pudo encontrar el servicio a actualizar.");
            return ResultadoOperacion.CrearError("No se pudo encontrar el servicio a actualizar.");
        }

        if (await _servicioService.ExisteServicioConNombreTipoVehiculo(servicio.Nombre, servicio.TipoVehiculo, servicio.Id))
        {
            var mensaje = $"Ya existe un servicio con el nombre '{servicio.Nombre}' para vehículos tipo '{servicio.TipoVehiculo}'.";
            ModelState.AddModelError("Nombre", $"Ya existe un servicio con este nombre para vehículos tipo '{servicio.TipoVehiculo}'.");
            return ResultadoOperacion.CrearError(mensaje);
        }

        servicio.Estado = servicioActual.Estado;
        await _servicioService.ActualizarServicio(servicio);
        return ResultadoOperacion.CrearExito("Servicio actualizado correctamente.");
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
        await _servicioService.CambiarEstadoServicio(id, nuevoEstado);
        TempData["StateChangeEvent_UserId"] = id;
        TempData["StateChangeEvent_NewState"] = nuevoEstado;
        await RegistrarEvento(accionAuditoria, id, "Servicio");
        return RedirectToAction("Index");
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
            new List<string> { "Activo" }, null, null, 1, 10, null, null);

        var totalPages = Math.Max(await _servicioService.ObtenerTotalPaginas(
            new List<string> { "Activo" }, null, null, 10), 1);

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
    /// <param name="tiposServicio">Lista completa para el dropdown de tipos de servicio.</param>
    /// <param name="tiposVehiculoList">Lista completa para el dropdown de tipos de vehículo.</param>
    /// <param name="pageSize">Tamaño de página actual.</param>
    /// <param name="currentPage">Página actual.</param>
    /// <param name="totalPages">Total de páginas.</param>
    /// <param name="visiblePages">Rango de páginas visibles.</param>
    /// <param name="sortBy">Campo de ordenamiento.</param>
    /// <param name="sortOrder">Dirección del ordenamiento.</param>
    private void ConfigurarViewBag(
        List<string> estados, List<string> tipos, List<string> tiposVehiculo,
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
        ViewBag.TodosLosTiposVehiculo = tiposVehiculoList;
        ViewBag.PageSize = pageSize;
        ViewBag.TiposServicio = tiposServicio;
        ViewBag.TodosLosTipos = tiposServicio;
        ViewBag.SortBy = sortBy;
        ViewBag.SortOrder = sortOrder;
    }

    /// <summary>
    /// Registra un evento de auditoría asociado al usuario actual.
    /// </summary>
    /// <param name="accion">Nombre de la acción ejecutada (por ejemplo, "Creacion de servicio").</param>
    /// <param name="targetId">ID de la entidad objetivo sobre la que se actuó.</param>
    /// <param name="entidad">Tipo de entidad (por ejemplo, "Servicio").</param>
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
        ViewBag.TiposServicio = await _tipoServicioService.ObtenerTiposServicio() ?? new List<string>();
        ViewBag.TodosLosTiposVehiculo = await _tipoVehiculoService.ObtenerTiposVehiculos() ?? new List<string>();
    }

    /// <summary>
    /// Fallback no-AJAX: crea un tipo (servicio o vehículo), registra auditoría y redirige a Index.
    /// </summary>
    /// <param name="nombreTipo">Nombre del tipo a crear.</param>
    /// <param name="verificarExistencia">Función que verifica existencia previa del tipo.</param>
    /// <param name="crearConId">Función que crea el tipo y devuelve el ID creado.</param>
    /// <param name="entidad">Etiqueta de entidad para auditoría (por ejemplo, "TipoServicio").</param>
    /// <param name="accionAuditoria">Acción a registrar en auditoría.</param>
    /// <returns>Redirección a <c>Index</c>.</returns>
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

    /// <summary>
    /// Fallback no-AJAX: elimina un tipo (servicio o vehículo), registra auditoría y redirige a Index.
    /// </summary>
    /// <param name="nombreTipo">Nombre del tipo a eliminar.</param>
    /// <param name="obtenerServiciosUsandoTipo">Función que obtiene servicios que usan el tipo (para impedir eliminación si hay uso).</param>
    /// <param name="eliminarConIds">Función que elimina el tipo y devuelve los IDs eliminados.</param>
    /// <param name="entidad">Etiqueta de entidad para auditoría (por ejemplo, "TipoVehiculo").</param>
    /// <param name="accionAuditoria">Acción a registrar en auditoría.</param>
    /// <returns>Redirección a <c>Index</c>.</returns>
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
                {
                    await RegistrarEvento(accionAuditoria, id, entidad);
                }
                TempData["Success"] = $"{entidad} eliminado correctamente.";
            }
        }
        return RedirectToAction("Index");
    }
    #endregion
}


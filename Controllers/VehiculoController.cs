using Firebase.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

/// <summary>
/// Controlador para la gestión de vehículos del lavadero.
/// Incluye filtros, paginación, búsqueda, acciones AJAX de creación/actualización.
/// </summary>
[Authorize]
public class VehiculoController : Controller
{
    #region Dependencias
    private readonly VehiculoService _vehiculoService;
    private readonly ClienteService _clienteService;
    private readonly TipoVehiculoService _tipoVehiculoService;
    private readonly AuditService _auditService;

    public VehiculoController(
        VehiculoService vehiculoService,
        ClienteService clienteService,
        TipoVehiculoService tipoVehiculoService,
        AuditService auditService)
    {
        _vehiculoService = vehiculoService;
        _clienteService = clienteService;
        _tipoVehiculoService = tipoVehiculoService;
        _auditService = auditService;
    }
    #endregion

    #region Vistas Principales

    /// <summary>
    /// Página principal de vehículos con filtros, orden y paginación.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        List<string> estados,
        List<string> tiposVehiculo,
        string clienteId = null,
        int pageNumber = 1,
        int pageSize = 10,
        string editId = null,
        string sortBy = null,
        string sortOrder = null)
    {
        estados = ConfigurarEstadosDefecto(estados);

        sortBy ??= "Placa";
        sortOrder ??= "asc";

        var (vehiculos, currentPage, totalPages, visiblePages) = await ObtenerDatosVehiculos(
            estados, tiposVehiculo, clienteId, pageNumber, pageSize, sortBy, sortOrder);

        var tiposVehiculoList = await CargarListaTiposVehiculo();

        ConfigurarViewBag(estados, tiposVehiculo, tiposVehiculoList,
            pageSize, currentPage, totalPages, visiblePages, sortBy, sortOrder, clienteId);

        await ConfigurarFormulario(editId, clienteId);

        return View(vehiculos);
    }
    #endregion

    #region Operaciones CRUD - AJAX

    /// <summary>
    /// Crear un vehículo vía AJAX.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearVehiculoAjax(Vehiculo vehiculo)
    {
        try
        {
            var resultado = await ProcesarCreacionVehiculo(vehiculo);
            return await PrepararRespuestaAjax(resultado, vehiculo, "Creacion de vehiculo");
        }
        catch (Exception ex)
        {
            return await ManejiarExcepcionAjax(ex, vehiculo);
        }
    }

    /// <summary>
    /// Actualizar vehículo vía AJAX.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActualizarVehiculoAjax(Vehiculo vehiculo)
    {
        try
        {
            var resultado = await ProcesarActualizacionVehiculo(vehiculo);
            return await PrepararRespuestaAjax(resultado, vehiculo, "Actualizacion de vehiculo");
        }
        catch (Exception ex)
        {
            return await ManejiarExcepcionAjax(ex, vehiculo);
        }
    }
    #endregion

    #region Búsqueda y Tabla Parcial

    /// <summary>
    /// Busca vehículos por término de búsqueda (parcial para actualización dinámica).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> SearchPartial(
        string searchTerm,
        List<string> estados,
        List<string> tiposVehiculo,
        string clienteId = null,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = null,
        string sortOrder = null)
    {
        estados = ConfigurarEstadosDefecto(estados);

        sortBy ??= "Placa";
        sortOrder ??= "asc";

        var vehiculos = await _vehiculoService.BuscarVehiculos(
            searchTerm, estados, tiposVehiculo, clienteId, pageNumber, pageSize, sortBy, sortOrder);

        var totalVehiculos = await _vehiculoService.ObtenerTotalVehiculosBusqueda(
            searchTerm, estados, tiposVehiculo, clienteId);

        var totalPages = Math.Max((int)Math.Ceiling(totalVehiculos / (double)pageSize), 1);

        ViewBag.CurrentPage = pageNumber;
        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = GetVisiblePages(pageNumber, totalPages);
        ViewBag.Estados = estados;
        ViewBag.TiposVehiculo = tiposVehiculo;
        ViewBag.ClienteId = clienteId;
        ViewBag.SortBy = sortBy;
        ViewBag.SortOrder = sortOrder;
        ViewBag.SearchTerm = searchTerm;

        return PartialView("_VehiculoTable", vehiculos);
    }

    /// <summary>
    /// Devuelve la tabla parcial (sin búsqueda) con filtros y orden.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> TablePartial(
        List<string> estados,
        List<string> tiposVehiculo,
        string clienteId = null,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = null,
        string sortOrder = null)
    {
        estados = ConfigurarEstadosDefecto(estados);

        sortBy ??= "Placa";
        sortOrder ??= "asc";

        var vehiculos = await _vehiculoService.ObtenerVehiculos(estados, tiposVehiculo, clienteId, pageNumber, pageSize, sortBy, sortOrder);
        var totalPages = await _vehiculoService.ObtenerTotalPaginas(estados, tiposVehiculo, clienteId, pageSize);
        totalPages = Math.Max(totalPages, 1);

        ViewBag.CurrentPage = pageNumber;
        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = GetVisiblePages(pageNumber, totalPages);
        ViewBag.Estados = estados;
        ViewBag.TiposVehiculo = tiposVehiculo;
        ViewBag.ClienteId = clienteId;
        ViewBag.SortBy = sortBy;
        ViewBag.SortOrder = sortOrder;

        return PartialView("_VehiculoTable", vehiculos);
    }
    #endregion

    #region Cambio de Estado

    /// <summary>
    /// Desactiva un vehículo.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> DeactivateVehiculo(string id)
        => await CambiarEstadoVehiculo(id, "Inactivo", "Desactivacion de vehiculo");

    /// <summary>
    /// Reactiva un vehículo.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ReactivateVehiculo(string id)
        => await CambiarEstadoVehiculo(id, "Activo", "Reactivacion de vehiculo");
    #endregion

    #region Gestión de Tipos de Vehículo

    /// <summary>
    /// Crear nuevo tipo de vehículo con respuesta AJAX.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador")]
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

        return RedirectToAction("Index");
    }

    /// <summary>
    /// Eliminar tipo de vehículo con respuesta AJAX.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador")]
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

                var vehiculosUsandoTipo = await _vehiculoService.ObtenerVehiculosPorTipo(nombreTipo);
                if (vehiculosUsandoTipo.Any())
                {
                    return Json(new
                    {
                        success = false,
                        message = "No se puede eliminar el tipo de vehículo porque hay vehículos que lo utilizan."
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

        return RedirectToAction("Index");
    }
    #endregion

    #region Vistas Parciales y Modales

    /// <summary>
    /// Devuelve el formulario parcial (usado por AJAX para editar/crear).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> FormPartial(string id, string clienteId = null)
    {
        await CargarListasForm();

        if (!string.IsNullOrEmpty(id))
        {
            var vehiculo = await _vehiculoService.ObtenerVehiculo(id);

            Response.Headers["X-Form-Title"] = "Editando un Vehículo";

            ViewBag.FormTitle = "Editando un Vehículo";
            ViewBag.SubmitButtonText = "Guardar";
            ViewBag.ClearButtonText = "Cancelar";
            ViewBag.FormAction = "ActualizarVehiculoAjax";
            return PartialView("_VehiculoForm", vehiculo);
        }

        Response.Headers["X-Form-Title"] = "Registrando un Vehículo";

        ViewBag.FormTitle = "Registrando un Vehículo";
        ViewBag.SubmitButtonText = "Registrar";
        ViewBag.ClearButtonText = "Limpiar Campos";
        ViewBag.FormAction = "CrearVehiculoAjax";
        ViewBag.PreselectedClienteId = clienteId;
        return PartialView("_VehiculoForm", null);
    }

    /// <summary>
    /// Devuelve un modal con la información del cliente (read-only).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> VerCliente(string clienteId)
    {
        var cliente = await _clienteService.ObtenerCliente(clienteId);
        if (cliente == null)
        {
            return NotFound();
        }

        return PartialView("_VerClienteModal", cliente);
    }
    #endregion

    #region Métodos Privados - Procesamiento de Negocio

    private static List<string> ConfigurarEstadosDefecto(List<string> estados)
    {
        estados ??= new List<string>();
        if (!estados.Any())
            estados.Add("Activo");
        return estados;
    }

    private async Task<(List<Vehiculo> vehiculos, int currentPage, int totalPages, List<int> visiblePages)>
        ObtenerDatosVehiculos(List<string> estados, List<string> tiposVehiculo, string clienteId,
        int pageNumber, int pageSize, string sortBy, string sortOrder)
    {
        var vehiculos = await _vehiculoService.ObtenerVehiculos(estados, tiposVehiculo, clienteId, pageNumber, pageSize, sortBy, sortOrder);
        var totalPages = Math.Max(await _vehiculoService.ObtenerTotalPaginas(estados, tiposVehiculo, clienteId, pageSize), 1);
        var currentPage = Math.Clamp(pageNumber, 1, totalPages);
        var visiblePages = GetVisiblePages(currentPage, totalPages);

        return (vehiculos, currentPage, totalPages, visiblePages);
    }

    private async Task<List<string>> CargarListaTiposVehiculo()
    {
        return await _tipoVehiculoService.ObtenerTiposVehiculos() ?? new List<string>();
    }

    private async Task ConfigurarFormulario(string editId, string clienteId)
    {
        if (!string.IsNullOrEmpty(editId))
        {
            var vehiculo = await _vehiculoService.ObtenerVehiculo(editId);
            ViewBag.EditVehiculo = vehiculo;
            ViewBag.FormTitle = "Editando un Vehículo";
            ViewBag.SubmitButtonText = "Guardar";
            ViewBag.ClearButtonText = "Cancelar";
            ViewBag.FormAction = "ActualizarVehiculoAjax";
        }
        else
        {
            ViewBag.FormTitle = "Registrando un Vehículo";
            ViewBag.SubmitButtonText = "Registrar";
            ViewBag.ClearButtonText = "Limpiar Campos";
            ViewBag.FormAction = "CrearVehiculoAjax";
            ViewBag.PreselectedClienteId = clienteId;
        }
    }

    private async Task<ResultadoOperacion> ProcesarCreacionVehiculo(Vehiculo vehiculo)
    {
        if (string.IsNullOrEmpty(vehiculo.Id))
        {
            vehiculo.Id = "temp-" + Guid.NewGuid().ToString();
            ModelState.Clear();
            TryValidateModel(vehiculo);
        }

        ValidateVehiculo(vehiculo);
        if (!ModelState.IsValid)
        {
            return ResultadoOperacion.CrearError("Por favor, complete todos los campos obligatorios correctamente.");
        }

        if (await _vehiculoService.ExisteVehiculoConPlaca(vehiculo.Placa))
        {
            var mensaje = $"Ya existe un vehículo con la placa {vehiculo.Placa}.";
            ModelState.AddModelError("Placa", "Ya existe un vehículo con esta placa.");
            return ResultadoOperacion.CrearError(mensaje);
        }

        vehiculo.Estado = "Activo";
        vehiculo.Placa = vehiculo.Placa.ToUpperInvariant().Trim();
        await _vehiculoService.CrearVehiculo(vehiculo);

        // Agregar vehículo a la lista del cliente
        if (!string.IsNullOrEmpty(vehiculo.ClienteId))
        {
            await _clienteService.AgregarVehiculoACliente(vehiculo.ClienteId, vehiculo.Id);
        }

        return ResultadoOperacion.CrearExito("Vehículo creado correctamente.");
    }

    private async Task<ResultadoOperacion> ProcesarActualizacionVehiculo(Vehiculo vehiculo)
    {
        ValidateVehiculo(vehiculo);
        if (!ModelState.IsValid)
        {
            return ResultadoOperacion.CrearError("Por favor, complete todos los campos obligatorios correctamente.");
        }

        var vehiculoActual = await _vehiculoService.ObtenerVehiculo(vehiculo.Id);
        if (vehiculoActual == null)
        {
            ModelState.AddModelError("", "No se pudo encontrar el vehículo a actualizar.");
            return ResultadoOperacion.CrearError("No se pudo encontrar el vehículo a actualizar.");
        }

        // VALIDACIÓN: Prevenir cambio de placa
        if (!string.Equals(vehiculoActual.Placa, vehiculo.Placa, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError("Placa", "No se puede cambiar la placa de un vehículo existente.");
            return ResultadoOperacion.CrearError("No se puede cambiar la placa de un vehículo existente.");
        }

        // VALIDACIÓN: Prevenir cambio de tipo de vehículo
        if (!string.Equals(vehiculoActual.TipoVehiculo, vehiculo.TipoVehiculo, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError("TipoVehiculo", "No se puede cambiar el tipo de vehículo de un vehículo existente.");
            return ResultadoOperacion.CrearError("No se puede cambiar el tipo de vehículo de un vehículo existente.");
        }

        // VALIDACIÓN: Prevenir cambio de marca
        if (!string.Equals(vehiculoActual.Marca, vehiculo.Marca, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError("Marca", "No se puede cambiar la marca de un vehículo existente.");
            return ResultadoOperacion.CrearError("No se puede cambiar la marca de un vehículo existente.");
        }

        vehiculo.Estado = vehiculoActual.Estado;
        vehiculo.Placa = vehiculoActual.Placa; // Mantener la placa original
        await _vehiculoService.ActualizarVehiculo(vehiculo);

        return ResultadoOperacion.CrearExito("Vehículo actualizado correctamente.");
    }

    private async Task<IActionResult> CambiarEstadoVehiculo(string id, string nuevoEstado, string accionAuditoria)
    {
        await _vehiculoService.CambiarEstadoVehiculo(id, nuevoEstado);
        TempData["StateChangeEvent_UserId"] = id;
        TempData["StateChangeEvent_NewState"] = nuevoEstado;
        await RegistrarEvento(accionAuditoria, id, "Vehiculo");
        return RedirectToAction("Index");
    }
    #endregion

    #region Métodos Privados - Respuestas y Manejo de Errores

    private async Task<IActionResult> PrepararRespuestaAjax(ResultadoOperacion resultado, Vehiculo vehiculo, string accionAuditoria)
    {
        if (!resultado.EsExitoso)
        {
            Response.Headers["X-Form-Valid"] = "false";
            await CargarListasForm();

            var title = string.IsNullOrEmpty(vehiculo?.Id) || vehiculo.Id.StartsWith("temp-")
                ? "Registrando un Vehículo"
                : "Editando un Vehículo";

            Response.Headers["X-Form-Title"] = title;

            ViewBag.FormTitle = title;
            ViewBag.FormAction = string.IsNullOrEmpty(vehiculo?.Id) || vehiculo.Id.StartsWith("temp-") ? "CrearVehiculoAjax" : "ActualizarVehiculoAjax";

            return PartialView("_VehiculoForm", vehiculo);
        }

        await RegistrarEvento(accionAuditoria, vehiculo.Id, "Vehiculo");

        Response.Headers["X-Form-Valid"] = "true";
        Response.Headers["X-Form-Message"] = resultado.MensajeExito;

        Response.Headers["X-Form-Title"] = "Registrando un Vehículo";

        await CargarListasForm();
        ViewBag.FormTitle = "Registrando un Vehículo";
        ViewBag.SubmitButtonText = "Registrar";
        ViewBag.ClearButtonText = "Limpiar Campos";
        ViewBag.FormAction = "CrearVehiculoAjax";

        return PartialView("_VehiculoForm", null);
    }

    private async Task<IActionResult> ManejiarExcepcionAjax(Exception ex, Vehiculo vehiculo)
    {
        ModelState.AddModelError("", ex.Message);
        Response.Headers["X-Form-Valid"] = "false";
        await CargarListasForm();
        return PartialView("_VehiculoForm", vehiculo);
    }
    #endregion

    #region Métodos Privados - Utilidades

    private List<int> GetVisiblePages(int currentPage, int totalPages, int range = 2)
    {
        var start = Math.Max(1, currentPage - range);
        var end = Math.Min(totalPages, currentPage + range);
        return Enumerable.Range(start, end - start + 1).ToList();
    }

    private void ValidateVehiculo(Vehiculo vehiculo)
    {
        if (string.IsNullOrWhiteSpace(vehiculo.Placa))
        {
            ModelState.AddModelError("Placa", "La placa es obligatoria.");
        }

        if (string.IsNullOrWhiteSpace(vehiculo.ClienteId))
        {
            ModelState.AddModelError("ClienteId", "El dueño es obligatorio.");
        }
    }

    private void ConfigurarViewBag(
        List<string> estados, List<string> tiposVehiculo,
        List<string> tiposVehiculoList,
        int pageSize, int currentPage, int totalPages, List<int> visiblePages,
        string sortBy, string sortOrder, string clienteId)
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
        ViewBag.ClienteId = clienteId;
    }

    private async Task RegistrarEvento(string accion, string targetId, string entidad)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        await _auditService.LogEvent(userId, userEmail, accion, targetId, entidad);
    }

    private async Task CargarListasForm()
    {
        ViewBag.TodosLosTiposVehiculo = await _tipoVehiculoService.ObtenerTiposVehiculos() ?? new List<string>();
        var clientes = await _clienteService.ObtenerClientes(new List<string> { "Activo" }, null, 1, 1000, "NombreCompleto", "asc");
        ViewBag.TodosLosClientes = clientes;
    }
    #endregion
}

using Firebase.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

/// <summary>
/// Controlador para la gestión de clientes del lavadero.
/// Incluye filtros, paginación, búsqueda, acciones AJAX de creación/actualización.
/// </summary>
[Authorize]
public class ClienteController : Controller
{
    #region Dependencias
    private readonly ClienteService _clienteService;
    private readonly VehiculoService _vehiculoService;
    private readonly TipoDocumentoService _tipoDocumentoService;
    private readonly AuditService _auditService;

    public ClienteController(
        ClienteService clienteService,
        VehiculoService vehiculoService,
        TipoDocumentoService tipoDocumentoService,
        AuditService auditService)
    {
        _clienteService = clienteService;
        _vehiculoService = vehiculoService;
        _tipoDocumentoService = tipoDocumentoService;
        _auditService = auditService;
    }
    #endregion

    #region Vistas Principales

    /// <summary>
    /// Página principal de clientes con filtros, orden y paginación.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        List<string> estados,
        List<string> tiposDocumento,
        int pageNumber = 1,
        int pageSize = 10,
        string editId = null,
        string sortBy = null,
        string sortOrder = null)
    {
        estados = ConfigurarEstadosDefecto(estados);

        sortBy ??= "NombreCompleto";
        sortOrder ??= "asc";

        var (clientes, currentPage, totalPages, visiblePages) = await ObtenerDatosClientes(
            estados, tiposDocumento, pageNumber, pageSize, sortBy, sortOrder);

        var tiposDocumentoList = await CargarListaTiposDocumento();

        ConfigurarViewBag(estados, tiposDocumento, tiposDocumentoList,
            pageSize, currentPage, totalPages, visiblePages, sortBy, sortOrder);

        await ConfigurarFormulario(editId);

        return View(clientes);
    }
    #endregion

    #region Operaciones CRUD - AJAX

    /// <summary>
    /// Crear un cliente vía AJAX.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearClienteAjax(Cliente cliente, string? VehiculosIdsJson, string? PrimerNombre, string? PrimerApellido)
    {
        try
        {
            // Combinar nombres
            if (!string.IsNullOrEmpty(PrimerNombre) && !string.IsNullOrEmpty(PrimerApellido))
            {
                cliente.NombreCompleto = $"{PrimerNombre.Trim()} {PrimerApellido.Trim()}";
            }

            if (!string.IsNullOrEmpty(VehiculosIdsJson))
            {
                cliente.VehiculosIds = System.Text.Json.JsonSerializer.Deserialize<List<string>>(VehiculosIdsJson) ?? new List<string>();
            }

            var resultado = await ProcesarCreacionCliente(cliente);
            return await PrepararRespuestaAjax(resultado, cliente, "Creacion de cliente");
        }
        catch (Exception ex)
        {
            return await ManejiarExcepcionAjax(ex, cliente);
        }
    }

    /// <summary>
    /// Actualizar cliente vía AJAX.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActualizarClienteAjax(Cliente cliente, string? VehiculosIdsJson, string? PrimerNombre, string? PrimerApellido)
    {
        try
        {
            // Combinar nombres
            if (!string.IsNullOrEmpty(PrimerNombre) && !string.IsNullOrEmpty(PrimerApellido))
            {
                cliente.NombreCompleto = $"{PrimerNombre.Trim()} {PrimerApellido.Trim()}";
            }

            if (!string.IsNullOrEmpty(VehiculosIdsJson))
            {
                cliente.VehiculosIds = System.Text.Json.JsonSerializer.Deserialize<List<string>>(VehiculosIdsJson) ?? new List<string>();
            }

            var resultado = await ProcesarActualizacionCliente(cliente);
            return await PrepararRespuestaAjax(resultado, cliente, "Actualizacion de cliente");
        }
        catch (Exception ex)
        {
            return await ManejiarExcepcionAjax(ex, cliente);
        }
    }
    #endregion

    #region Búsqueda y Tabla Parcial

    /// <summary>
    /// Busca clientes por término de búsqueda (parcial para actualización dinámica).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> SearchPartial(
        string searchTerm,
        List<string> estados,
        List<string> tiposDocumento,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = null,
        string sortOrder = null)
    {
        estados = ConfigurarEstadosDefecto(estados);

        sortBy ??= "NombreCompleto";
        sortOrder ??= "asc";

        var clientes = await _clienteService.BuscarClientes(
            searchTerm, estados, tiposDocumento, pageNumber, pageSize, sortBy, sortOrder);

        var totalClientes = await _clienteService.ObtenerTotalClientesBusqueda(
            searchTerm, estados, tiposDocumento);

        var totalPages = Math.Max((int)Math.Ceiling(totalClientes / (double)pageSize), 1);

        ViewBag.CurrentPage = pageNumber;
        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = GetVisiblePages(pageNumber, totalPages);
        ViewBag.Estados = estados;
        ViewBag.TiposDocumento = tiposDocumento;
        ViewBag.SortBy = sortBy;
        ViewBag.SortOrder = sortOrder;
        ViewBag.SearchTerm = searchTerm;

        return PartialView("_ClienteTable", clientes);
    }

    /// <summary>
    /// Devuelve la tabla parcial (sin búsqueda) con filtros y orden.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> TablePartial(
        List<string> estados,
        List<string> tiposDocumento,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = null,
        string sortOrder = null)
    {
        estados = ConfigurarEstadosDefecto(estados);

        sortBy ??= "NombreCompleto";
        sortOrder ??= "asc";

        var clientes = await _clienteService.ObtenerClientes(estados, tiposDocumento, pageNumber, pageSize, sortBy, sortOrder);
        var totalPages = await _clienteService.ObtenerTotalPaginas(estados, tiposDocumento, pageSize);
        totalPages = Math.Max(totalPages, 1);

        ViewBag.CurrentPage = pageNumber;
        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = GetVisiblePages(pageNumber, totalPages);
        ViewBag.Estados = estados;
        ViewBag.TiposDocumento = tiposDocumento;
        ViewBag.SortBy = sortBy;
        ViewBag.SortOrder = sortOrder;

        return PartialView("_ClienteTable", clientes);
    }
    #endregion

    #region Cambio de Estado

    /// <summary>
    /// Desactiva un cliente.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> DeactivateCliente(string id, bool desactivarVehiculos = false)
    {
        if (desactivarVehiculos)
        {
            var vehiculos = await _vehiculoService.ObtenerVehiculosPorCliente(id);
            foreach (var vehiculo in vehiculos)
            {
                await _vehiculoService.CambiarEstadoVehiculo(vehiculo.Id, "Inactivo");
                await RegistrarEvento("Desactivacion de vehiculo", vehiculo.Id, "Vehiculo");
            }
        }
        return await CambiarEstadoCliente(id, "Inactivo", "Desactivacion de cliente");
    }

    /// <summary>
    /// Reactiva un cliente.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ReactivateCliente(string id)
        => await CambiarEstadoCliente(id, "Activo", "Reactivacion de cliente");
    #endregion

    #region Gestión de Tipos de Documento

    /// <summary>
    /// Crear nuevo tipo de documento con respuesta AJAX.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearTipoDocumento(string nombreTipo)
    {
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nombreTipo))
                {
                    return Json(new { success = false, message = "El nombre del tipo de documento es obligatorio." });
                }

                if (await _tipoDocumentoService.ExisteTipoDocumento(nombreTipo))
                {
                    return Json(new { success = false, message = "Ya existe un tipo de documento con el mismo nombre." });
                }

                var docId = await _tipoDocumentoService.CrearTipoDocumento(nombreTipo);
                await RegistrarEvento("Creacion de tipo de documento", docId, "TipoDocumento");

                var tiposActualizados = await _tipoDocumentoService.ObtenerTiposDocumento();

                return Json(new
                {
                    success = true,
                    message = "Tipo de documento creado correctamente.",
                    tipos = tiposActualizados
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al crear el tipo de documento: {ex.Message}" });
            }
        }

        return RedirectToAction("Index");
    }

    /// <summary>
    /// Eliminar tipo de documento con respuesta AJAX.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarTipoDocumento(string nombreTipo)
    {
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nombreTipo))
                {
                    return Json(new { success = false, message = "Debe seleccionar un tipo de documento." });
                }

                var clientesUsandoTipo = await _clienteService.ObtenerClientesPorTipoDocumento(nombreTipo);
                if (clientesUsandoTipo.Any())
                {
                    return Json(new
                    {
                        success = false,
                        message = "No se puede eliminar el tipo de documento porque hay clientes que lo utilizan."
                    });
                }

                var idsEliminados = await _tipoDocumentoService.EliminarTipoDocumento(nombreTipo);
                foreach (var id in idsEliminados)
                {
                    await RegistrarEvento("Eliminacion de tipo de documento", id, "TipoDocumento");
                }

                var tiposActualizados = await _tipoDocumentoService.ObtenerTiposDocumento();

                return Json(new
                {
                    success = true,
                    message = "Tipo de documento eliminado correctamente.",
                    tipos = tiposActualizados
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al eliminar el tipo de documento: {ex.Message}" });
            }
        }

        return RedirectToAction("Index");
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

        if (!string.IsNullOrEmpty(id))
        {
            var cliente = await _clienteService.ObtenerCliente(id);

            Response.Headers["X-Form-Title"] = "Editando un Cliente";

            ViewBag.FormTitle = "Editando un Cliente";
            ViewBag.SubmitButtonText = "Guardar";
            ViewBag.ClearButtonText = "Cancelar";
            ViewBag.FormAction = "ActualizarClienteAjax";
            return PartialView("_ClienteForm", cliente);
        }

        Response.Headers["X-Form-Title"] = "Registrando un Cliente";

        ViewBag.FormTitle = "Registrando un Cliente";
        ViewBag.SubmitButtonText = "Registrar";
        ViewBag.ClearButtonText = "Limpiar Campos";
        ViewBag.FormAction = "CrearClienteAjax";
        return PartialView("_ClienteForm", null);
    }

    /// <summary>
    /// Obtiene los vehículos activos para el dropdown.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ObtenerVehiculosActivos()
    {
        var vehiculos = await _vehiculoService.ObtenerVehiculosActivos();

        var vehiculosData = vehiculos.Select(v => new
        {
            id = v.Id,
            placa = v.Placa,
            descripcion = $"{v.Placa} - {v.Marca} {v.Modelo}"
        }).ToList();

        return Json(new { success = true, vehiculos = vehiculosData });
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

    private async Task<(List<Cliente> clientes, int currentPage, int totalPages, List<int> visiblePages)>
        ObtenerDatosClientes(List<string> estados, List<string> tiposDocumento,
        int pageNumber, int pageSize, string sortBy, string sortOrder)
    {
        var clientes = await _clienteService.ObtenerClientes(estados, tiposDocumento, pageNumber, pageSize, sortBy, sortOrder);
        var totalPages = Math.Max(await _clienteService.ObtenerTotalPaginas(estados, tiposDocumento, pageSize), 1);
        var currentPage = Math.Clamp(pageNumber, 1, totalPages);
        var visiblePages = GetVisiblePages(currentPage, totalPages);

        return (clientes, currentPage, totalPages, visiblePages);
    }

    private async Task<List<string>> CargarListaTiposDocumento()
    {
        return await _tipoDocumentoService.ObtenerTiposDocumento() ?? new List<string>();
    }

    private async Task ConfigurarFormulario(string editId)
    {
        if (!string.IsNullOrEmpty(editId))
        {
            var cliente = await _clienteService.ObtenerCliente(editId);
            ViewBag.EditCliente = cliente;
            ViewBag.FormTitle = "Editando un Cliente";
            ViewBag.SubmitButtonText = "Guardar";
            ViewBag.ClearButtonText = "Cancelar";
            ViewBag.FormAction = "ActualizarClienteAjax";
        }
        else
        {
            ViewBag.FormTitle = "Registrando un Cliente";
            ViewBag.SubmitButtonText = "Registrar";
            ViewBag.ClearButtonText = "Limpiar Campos";
            ViewBag.FormAction = "CrearClienteAjax";
        }
    }

    private async Task<ResultadoOperacion> ProcesarCreacionCliente(Cliente cliente)
    {
        if (string.IsNullOrEmpty(cliente.Id))
        {
            cliente.Id = "temp-" + Guid.NewGuid().ToString();
            ModelState.Clear();
            TryValidateModel(cliente);
        }

        ValidateCliente(cliente);
        if (!ModelState.IsValid)
        {
            return ResultadoOperacion.CrearError("Por favor, complete todos los campos obligatorios correctamente.");
        }

        if (await _clienteService.ExisteClienteConDocumento(cliente.TipoDocumento, cliente.NumeroDocumento))
        {
            var mensaje = $"Ya existe un cliente con el documento {cliente.TipoDocumento}: {cliente.NumeroDocumento}.";
            ModelState.AddModelError("NumeroDocumento", $"Ya existe un cliente con este documento.");
            return ResultadoOperacion.CrearError(mensaje);
        }

        cliente.Estado = "Activo";
        await _clienteService.CrearCliente(cliente);

        // Actualizar la relación con vehículos
        if (cliente.VehiculosIds != null && cliente.VehiculosIds.Any())
        {
            foreach (var vehiculoId in cliente.VehiculosIds)
            {
                var vehiculo = await _vehiculoService.ObtenerVehiculo(vehiculoId);
                if (vehiculo != null && vehiculo.ClienteId != cliente.Id)
                {
                    // Remover del cliente anterior si existe
                    if (!string.IsNullOrEmpty(vehiculo.ClienteId))
                    {
                        await _clienteService.RemoverVehiculoDeCliente(vehiculo.ClienteId, vehiculoId);
                    }
                    vehiculo.ClienteId = cliente.Id;
                    await _vehiculoService.ActualizarVehiculo(vehiculo);
                }
            }
        }

        return ResultadoOperacion.CrearExito("Cliente creado correctamente.");
    }

    private async Task<ResultadoOperacion> ProcesarActualizacionCliente(Cliente cliente)
    {
        ValidateCliente(cliente);
        if (!ModelState.IsValid)
        {
            return ResultadoOperacion.CrearError("Por favor, complete todos los campos obligatorios correctamente.");
        }

        var clienteActual = await _clienteService.ObtenerCliente(cliente.Id);
        if (clienteActual == null)
        {
            ModelState.AddModelError("", "No se pudo encontrar el cliente a actualizar.");
            return ResultadoOperacion.CrearError("No se pudo encontrar el cliente a actualizar.");
        }

        cliente.Estado = clienteActual.Estado;
        await _clienteService.ActualizarCliente(cliente);

        // Actualizar la relación con vehículos
        var vehiculosActuales = await _vehiculoService.ObtenerVehiculosPorCliente(cliente.Id);
        var vehiculosActualesIds = vehiculosActuales.Select(v => v.Id).ToList();
        var vehiculosNuevosIds = cliente.VehiculosIds ?? new List<string>();

        // Remover vehículos que ya no pertenecen a este cliente
        foreach (var vehiculoId in vehiculosActualesIds.Except(vehiculosNuevosIds))
        {
            var vehiculo = await _vehiculoService.ObtenerVehiculo(vehiculoId);
            if (vehiculo != null)
            {
                vehiculo.ClienteId = string.Empty; // Sin dueño
                await _vehiculoService.ActualizarVehiculo(vehiculo);
            }
        }

        // Agregar nuevos vehículos
        foreach (var vehiculoId in vehiculosNuevosIds.Except(vehiculosActualesIds))
        {
            var vehiculo = await _vehiculoService.ObtenerVehiculo(vehiculoId);
            if (vehiculo != null && vehiculo.ClienteId != cliente.Id)
            {
                // Remover del cliente anterior si existe
                if (!string.IsNullOrEmpty(vehiculo.ClienteId))
                {
                    await _clienteService.RemoverVehiculoDeCliente(vehiculo.ClienteId, vehiculoId);
                }
                vehiculo.ClienteId = cliente.Id;
                await _vehiculoService.ActualizarVehiculo(vehiculo);
            }
        }

        return ResultadoOperacion.CrearExito("Cliente actualizado correctamente.");
    }

    private async Task<IActionResult> CambiarEstadoCliente(string id, string nuevoEstado, string accionAuditoria)
    {
        await _clienteService.CambiarEstadoCliente(id, nuevoEstado);
        TempData["StateChangeEvent_UserId"] = id;
        TempData["StateChangeEvent_NewState"] = nuevoEstado;
        await RegistrarEvento(accionAuditoria, id, "Cliente");
        return RedirectToAction("Index");
    }
    #endregion

    #region Métodos Privados - Respuestas y Manejo de Errores

    private async Task<IActionResult> PrepararRespuestaAjax(ResultadoOperacion resultado, Cliente cliente, string accionAuditoria)
    {
        if (!resultado.EsExitoso)
        {
            Response.Headers["X-Form-Valid"] = "false";
            await CargarListasForm();

            var title = string.IsNullOrEmpty(cliente?.Id) || cliente.Id.StartsWith("temp-")
                ? "Registrando un Cliente"
                : "Editando un Cliente";

            Response.Headers["X-Form-Title"] = title;

            ViewBag.FormTitle = title;
            ViewBag.FormAction = string.IsNullOrEmpty(cliente?.Id) || cliente.Id.StartsWith("temp-") ? "CrearClienteAjax" : "ActualizarClienteAjax";

            return PartialView("_ClienteForm", cliente);
        }

        await RegistrarEvento(accionAuditoria, cliente.Id, "Cliente");

        Response.Headers["X-Form-Valid"] = "true";
        Response.Headers["X-Form-Message"] = resultado.MensajeExito;

        Response.Headers["X-Form-Title"] = "Registrando un Cliente";

        await CargarListasForm();
        ViewBag.FormTitle = "Registrando un Cliente";
        ViewBag.SubmitButtonText = "Registrar";
        ViewBag.ClearButtonText = "Limpiar Campos";
        ViewBag.FormAction = "CrearClienteAjax";

        return PartialView("_ClienteForm", null);
    }

    private async Task<IActionResult> ManejiarExcepcionAjax(Exception ex, Cliente cliente)
    {
        ModelState.AddModelError("", ex.Message);
        Response.Headers["X-Form-Valid"] = "false";
        await CargarListasForm();
        return PartialView("_ClienteForm", cliente);
    }
    #endregion

    #region Métodos Privados - Utilidades

    private List<int> GetVisiblePages(int currentPage, int totalPages, int range = 2)
    {
        var start = Math.Max(1, currentPage - range);
        var end = Math.Min(totalPages, currentPage + range);
        return Enumerable.Range(start, end - start + 1).ToList();
    }

    private void ValidateCliente(Cliente cliente)
    {
        if (!string.IsNullOrEmpty(cliente.NombreCompleto) && !System.Text.RegularExpressions.Regex.IsMatch(cliente.NombreCompleto, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$"))
        {
            ModelState.AddModelError("NombreCompleto", "El nombre solo puede contener letras y espacios.");
        }

        if (!string.IsNullOrEmpty(cliente.Email))
        {
            var emailRegex = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            if (!System.Text.RegularExpressions.Regex.IsMatch(cliente.Email, emailRegex))
            {
                ModelState.AddModelError("Email", "El formato del email no es válido.");
            }
        }
    }

    private void ConfigurarViewBag(
        List<string> estados, List<string> tiposDocumento,
        List<string> tiposDocumentoList,
        int pageSize, int currentPage, int totalPages, List<int> visiblePages,
        string sortBy, string sortOrder)
    {
        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = visiblePages;
        ViewBag.CurrentPage = currentPage;
        ViewBag.Estados = estados;
        ViewBag.TiposDocumento = tiposDocumento;
        ViewBag.TodosLosTiposDocumento = tiposDocumentoList;
        ViewBag.PageSize = pageSize;
        ViewBag.SortBy = sortBy;
        ViewBag.SortOrder = sortOrder;
    }

    private async Task RegistrarEvento(string accion, string targetId, string entidad)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        await _auditService.LogEvent(userId, userEmail, accion, targetId, entidad);
    }

    private async Task CargarListasForm()
    {
        ViewBag.TodosLosTiposDocumento = await _tipoDocumentoService.ObtenerTiposDocumento() ?? new List<string>();
        var vehiculos = await _vehiculoService.ObtenerVehiculosActivos();
        ViewBag.TodosLosVehiculos = vehiculos;
    }
    #endregion
}

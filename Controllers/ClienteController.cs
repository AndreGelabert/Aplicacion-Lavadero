using Firebase.Models;
using Firebase.Security;
using Firebase.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

[Authorize]
public class ClienteController : Controller
{
    private readonly ClienteService _clienteService;
    private readonly TipoDocumentoService _tipoDocumentoService;
    private readonly VehiculoService _vehiculoService;
    private readonly IAuditWriter _auditService;
    private readonly ILogger<ClienteController> _logger;
    private readonly ClienteLifecycleService _lifecycle;
    private readonly ClienteVehicleService _clienteVehicles;

    public ClienteController(
        ClienteService clienteService,
        TipoDocumentoService tipoDocumentoService,
        VehiculoService vehiculoService,
        IAuditWriter auditService,
        ILogger<ClienteController> logger,
        ClienteLifecycleService lifecycle, ClienteVehicleService clienteVehicles)
    {
        _clienteService = clienteService;
        _tipoDocumentoService = tipoDocumentoService;
        _vehiculoService = vehiculoService;
        _auditService = auditService;
        _logger = logger;
        _lifecycle = lifecycle;
        _clienteVehicles = clienteVehicles;
    }

    [HttpGet]
    [Permiso("Clientes.Consultar", "Clientes.Crear")]
    public async Task<IActionResult> Index(
        string searchTerm,
        List<string> estados,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = "Nombre",
        string sortOrder = "asc",
        string editId = null)
    {
        var puedeConsultar = User.TienePermiso("Clientes.Consultar");

        var clientes = await PrepararListado(searchTerm, estados, pageNumber, pageSize, sortBy, sortOrder, puedeConsultar);
        ViewBag.PageSize = pageSize;

        await CargarListasForm();
        await ConfigurarFormulario(puedeConsultar && User.TienePermiso("Clientes.Modificar") ? editId : string.Empty);

        return View(clientes);
    }

    [HttpGet]
    [Permiso("Clientes.Consultar")]
    public async Task<IActionResult> SearchPartial(
        string searchTerm,
        List<string> estados,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = "Nombre",
        string sortOrder = "asc")
    {
        var clientes = await PrepararListado(searchTerm, estados, pageNumber, pageSize, sortBy, sortOrder);

        return PartialView("_ClienteTable", clientes);
    }

    [HttpGet]
    [Permiso("Clientes.Consultar")]
    public async Task<IActionResult> TablePartial(
        List<string> estados,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = "Nombre",
        string sortOrder = "asc")
    {
        return await SearchPartial(null, estados, pageNumber, pageSize, sortBy, sortOrder);
    }

    [HttpGet]
    [Permiso("Clientes.Consultar")]
    public async Task<IActionResult> FormPartial(string id)
    {
        if (!string.IsNullOrEmpty(id) && !User.TienePermiso("Clientes.Modificar"))
            return Forbid();
        await CargarListasForm();
        Cliente cliente = null;
        if (!string.IsNullOrEmpty(id))
            cliente = await _clienteService.ObtenerCliente(id);

        return PartialView("_ClienteForm", cliente);
    }

    /// <summary>
    /// Obtiene el detalle de un cliente para mostrar en modal.
    /// </summary>
    [HttpGet]
    [Permiso("Clientes.Consultar", "Vehiculos.Consultar")]
    public async Task<IActionResult> DetailPartial(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return BadRequest("ID de cliente no válido");
        }

        var cliente = await _clienteService.ObtenerCliente(id);
        if (cliente == null)
        {
            return NotFound("Cliente no encontrado");
        }

        // Obtener vehículos del cliente
        var vehiculos = await _vehiculoService.ObtenerVehiculosPorCliente(id);
        ViewBag.Vehiculos = vehiculos;

        return PartialView("_ClienteDetail", cliente);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Permiso("Clientes.Crear")]
    public async Task<IActionResult> CrearClienteAjax(Cliente cliente, string? vehiculosDataJson = null)
    {
        if (!PuedeGestionarVehiculosDesdeCliente(User, "Clientes.Crear"))
            return Forbid();

        try
        {
            ModelState.Remove("Id");
            ModelState.Remove("VehiculosDataJson");
            
            if (!ModelState.IsValid)
            {
                return await PrepararRespuestaAjax(false, cliente, null);
            }

            // Validar duplicados (TipoDoc + NumDoc)
            var existente = await _clienteService.ObtenerClientePorDocumento(cliente.TipoDocumento, cliente.NumeroDocumento);
            if (existente != null)
            {
                ModelState.AddModelError("NumeroDocumento", "Ya existe un cliente con este documento.");
                return await PrepararRespuestaAjax(false, cliente, null);
            }

            var vehiculosData = DeserializarVehiculos(vehiculosDataJson);
            var validacion = await _clienteVehicles.Validar(cliente, vehiculosData, true);
            if (validacion.ErrorMessage != null)
            {
                ModelState.AddModelError(validacion.ErrorKey!, validacion.ErrorMessage);
                return await PrepararRespuestaAjax(false, cliente, null);
            }

            return await GuardarAtomico(cliente, vehiculosData!, true, new());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear cliente y asociar sus vehículos.");
            ModelState.AddModelError("", "No se pudo guardar el cliente. Intente nuevamente.");
            return await PrepararRespuestaAjax(false, cliente, null);
        }
    }

    private async Task<IActionResult> GuardarAtomico(Cliente cliente, List<ClienteVehiculoInput> datos, bool crear, Dictionary<string, string> antes)
    {
        var resultado = await _clienteVehicles.Guardar(cliente, datos, crear,
            User.FindFirstValue(ClaimTypes.NameIdentifier), User.FindFirstValue(ClaimTypes.Email));
        if (resultado.ErrorMessage != null)
        {
            ModelState.AddModelError(resultado.ErrorKey!, resultado.ErrorMessage);
            return await PrepararRespuestaAjax(false, cliente, null);
        }
        if (resultado.Claves!.Any())
        {
            ViewBag.AssociationKeys = System.Text.Json.JsonSerializer.Serialize(resultado.Claves);
            Response.Headers.CacheControl = "no-cache, no-store";
            Response.Headers.Pragma = "no-cache";
        }
        return await PrepararRespuestaAjax(true, cliente, crear ? "Creacion de cliente" : "Actualizacion de cliente", antes, auditoriaRegistrada: true);
    }

    private static List<ClienteVehiculoInput>? DeserializarVehiculos(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<ClienteVehiculoInput>>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (System.Text.Json.JsonException)
        {
            return new List<ClienteVehiculoInput>();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Permiso("Clientes.Modificar")]
    public async Task<IActionResult> ActualizarClienteAjax(Cliente cliente, string? vehiculosDataJson = null)
    {
        if (!PuedeGestionarVehiculosDesdeCliente(User, "Clientes.Modificar"))
            return Forbid();

        try
        {
            ModelState.Remove("VehiculosDataJson"); // No es parte del modelo Cliente
            
            if (!ModelState.IsValid) return await PrepararRespuestaAjax(false, cliente, null);

            var clienteActual = await _clienteService.ObtenerCliente(cliente.Id);
            if (clienteActual == null)
            {
                ModelState.AddModelError("", "El cliente no existe.");
                return await PrepararRespuestaAjax(false, cliente, null);
            }

            var vehiculosData = DeserializarVehiculos(vehiculosDataJson);
            var validacion = await _clienteVehicles.Validar(cliente, vehiculosData, false);
            if (validacion.ErrorMessage != null)
            {
                ModelState.AddModelError(validacion.ErrorKey!, validacion.ErrorMessage);
                return await PrepararRespuestaAjax(false, cliente, null);
            }

            var antesCliente = AuditCampos.CapturarCliente(clienteActual);
            return await GuardarAtomico(cliente, vehiculosData!, false, antesCliente);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar cliente y asociar sus vehículos.");
            ModelState.AddModelError("", "No se pudo guardar el cliente. Intente nuevamente.");
            return await PrepararRespuestaAjax(false, cliente, null);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Permiso("Clientes.Eliminar")]
    public async Task<IActionResult> DeactivateCliente(string id)
    {
        try
        {
            var message = await _lifecycle.Desactivar(id, User.FindFirstValue(ClaimTypes.NameIdentifier), User.FindFirstValue(ClaimTypes.Email));
            return Json(new { success = true, message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al desactivar el cliente {ClienteId}.", id);
            return Json(new { success = false, message = "No se pudo desactivar el cliente. Intente nuevamente." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Permiso("Clientes.Modificar")]
    public async Task<IActionResult> ReactivateCliente(string id)
    {
        try
        {
            var message = await _lifecycle.Reactivar(id, User.FindFirstValue(ClaimTypes.NameIdentifier), User.FindFirstValue(ClaimTypes.Email));
            return Json(new { success = true, message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al reactivar el cliente {ClienteId}.", id);
            return Json(new { success = false, message = "No se pudo reactivar el cliente. Intente nuevamente." });
        }
    }

    [HttpGet]
    [Permiso("Clientes.Consultar")]
    public async Task<IActionResult> GetVehiculosCliente(string clienteId)
    {
        if (!PuedeConsultarVehiculosCliente(User))
            return Forbid();

        try
        {
            var vehiculosConInfo = await _clienteVehicles.ObtenerVehiculos(clienteId);
            if (vehiculosConInfo == null)
                return Json(new { success = false, message = "Cliente no encontrado" });
            
            return Json(new
            {
                success = true,
                vehiculos = vehiculosConInfo
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener vehículos del cliente {ClienteId}.", clienteId);
            return Json(new { success = false, message = "No se pudieron obtener los vehículos del cliente." });
        }
    }

    public static bool PuedeConsultarVehiculosCliente(ClaimsPrincipal user) =>
        user.TienePermiso("Clientes.Consultar") && user.TienePermiso("Vehiculos.Consultar");

    public static bool PuedeGestionarVehiculosDesdeCliente(ClaimsPrincipal user, string permisoCliente) =>
        user.TienePermiso(permisoCliente) && user.TienePermiso("Vehiculos.Crear");

    [HttpPost]
    [Permiso("Clientes.Eliminar")]
    public async Task<IActionResult> EliminarCliente(string id)
    {
        try
        {
            var cliente = await _clienteService.ObtenerCliente(id);
            if (cliente != null && cliente.VehiculosIds != null && cliente.VehiculosIds.Any())
            {
                return Json(new { success = false, message = "No se puede eliminar el cliente porque tiene vehículos asociados." });
            }

            var antes = cliente == null ? null : AuditCampos.CapturarCliente(cliente);
            await _clienteService.EliminarCliente(id);
            await RegistrarEvento("Eliminacion fisica de cliente", id, "Cliente", antes, new(), cliente?.NombreCompleto);
            return Json(new { success = true, message = "Cliente eliminado correctamente." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar el cliente {ClienteId}.", id);
            return Json(new { success = false, message = "No se pudo eliminar el cliente. Intente nuevamente." });
        }
    }

    private async Task CargarListasForm()
    {
        ViewBag.TiposDocumento = await _tipoDocumentoService.ObtenerTiposDocumento();
        // Para el dropdown de vehículos, podríamos cargar todos los vehículos disponibles (sin dueño)
        // O todos los vehículos para asociar.
        // El requerimiento dice: "vehículos (desplegable con los vehiculos registrados en el lavadero, se pueden seleccionar mas de uno...)"
        // Esto implica que un cliente puede tener N vehículos.
        // Y "un vehículo solo puede tener un dueño".
        // Entonces en el dropdown solo deberían aparecer vehículos SIN DUEÑO o los que ya son de ESTE cliente.
        var todosVehiculos = await _vehiculoService.ObtenerTodosVehiculos();
        ViewBag.VehiculosDisponibles = todosVehiculos; // La vista filtrará o mostrará apropiadamente
    }

    private async Task ConfigurarFormulario(string editId)
    {
        if (!string.IsNullOrEmpty(editId))
        {
            var cliente = await _clienteService.ObtenerCliente(editId);
            ViewBag.EditCliente = cliente;
            ViewBag.FormTitle = "Editando Cliente";
            ViewBag.SubmitButtonText = "Guardar";
            ViewBag.ClearButtonText = "Cancelar";
            ViewBag.FormAction = "ActualizarClienteAjax";
        }
        else
        {
            ViewBag.FormTitle = "Registrando Cliente";
            ViewBag.SubmitButtonText = "Registrar";
            ViewBag.ClearButtonText = "Limpiar Campos";
            ViewBag.FormAction = "CrearClienteAjax";
        }
    }

    private async Task<IActionResult> PrepararRespuestaAjax(bool success, Cliente cliente, string accionAuditoria, Dictionary<string, string> antes = null, bool auditoriaRegistrada = false)
    {
        if (!success)
        {
            Response.Headers["X-Form-Valid"] = "false";
            await CargarListasForm();
            return PartialView("_ClienteForm", cliente);
        }

        if (!auditoriaRegistrada) await RegistrarCliente(accionAuditoria, cliente.Id, antes ?? new());
        Response.Headers["X-Form-Valid"] = "true";
        Response.Headers["X-Form-Message"] = accionAuditoria.Contains("Creacion") ? "Cliente creado correctamente." : "Cliente actualizado correctamente.";
        await CargarListasForm();
        return PartialView("_ClienteForm", null);
    }

    private async Task<List<Cliente>> PrepararListado(
        string searchTerm, List<string> estados, int pageNumber, int pageSize,
        string sortBy, string sortOrder, bool puedeConsultar = true)
    {
        // Si no se especifican estados, por defecto mostrar solo Activos
        if (estados == null || !estados.Any())
        {
            estados = new List<string> { "Activo" };
        }

        var clientes = puedeConsultar
            ? await _clienteService.ObtenerClientes(searchTerm, pageNumber, pageSize, sortBy, sortOrder, estados)
            : new List<Cliente>();
        var totalClientes = puedeConsultar
            ? await _clienteService.ObtenerTotalClientes(searchTerm, estados)
            : 0;
        var totalPages = Math.Max((int)Math.Ceiling(totalClientes / (double)pageSize), 1);

        ViewBag.CurrentPage = pageNumber;
        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = GetVisiblePages(pageNumber, totalPages);
        ViewBag.SortBy = sortBy;
        ViewBag.SortOrder = sortOrder;
        ViewBag.SearchTerm = searchTerm;
        ViewBag.Estados = estados;
        return clientes;
    }

    private List<int> GetVisiblePages(int currentPage, int totalPages, int range = 2)
    {
        var start = Math.Max(1, currentPage - range);
        var end = Math.Min(totalPages, currentPage + range);
        return Enumerable.Range(start, end - start + 1).ToList();
    }

    private async Task RegistrarCliente(string accion, string id, Dictionary<string, string> antes)
    {
        var cliente = await _clienteService.ObtenerCliente(id) ?? throw new InvalidOperationException("Cliente no encontrado después de guardar.");
        await RegistrarEvento(accion, id, "Cliente", antes, AuditCampos.CapturarCliente(cliente), cliente.NombreCompleto);
    }

    private async Task RegistrarVehiculo(string accion, string id, Dictionary<string, string> antes)
    {
        var vehiculo = await _vehiculoService.ObtenerVehiculo(id) ?? throw new InvalidOperationException("Vehículo no encontrado después de guardar.");
        await RegistrarEvento(accion, id, "Vehiculo", antes, AuditCampos.CapturarVehiculo(vehiculo), vehiculo.Patente);
    }

    private async Task RegistrarEvento(string accion, string targetId, string entidad, Dictionary<string, string> antes, Dictionary<string, string> despues, string nombre)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        await _auditService.LogEvent(userId, userEmail, accion, targetId, entidad, antes, despues, nombre);
    }

    private async Task RegistrarEvento(string accion, string targetId, string entidad)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        await _auditService.LogEvent(userId, userEmail, accion, targetId, entidad);
    }

}

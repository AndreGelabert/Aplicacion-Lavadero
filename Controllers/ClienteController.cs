using Firebase.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[Authorize(Roles = "Administrador,Empleado")]
public class ClienteController : Controller
{
    private readonly ClienteService _clienteService;
    private readonly TipoDocumentoService _tipoDocumentoService;
    private readonly VehiculoService _vehiculoService;
    private readonly AuditService _auditService;

    public ClienteController(
        ClienteService clienteService,
        TipoDocumentoService tipoDocumentoService,
        VehiculoService vehiculoService,
        AuditService auditService)
    {
        _clienteService = clienteService;
        _tipoDocumentoService = tipoDocumentoService;
        _vehiculoService = vehiculoService;
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string searchTerm,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = "Nombre",
        string sortOrder = "asc",
        string editId = null)
    {
        var clientes = await _clienteService.ObtenerClientes(searchTerm, pageNumber, pageSize, sortBy, sortOrder);
        var totalClientes = await _clienteService.ObtenerTotalClientes(searchTerm);
        var totalPages = Math.Max((int)Math.Ceiling(totalClientes / (double)pageSize), 1);

        ViewBag.CurrentPage = pageNumber;
        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = GetVisiblePages(pageNumber, totalPages);
        ViewBag.SortBy = sortBy;
        ViewBag.SortOrder = sortOrder;
        ViewBag.SearchTerm = searchTerm;
        ViewBag.PageSize = pageSize;

        await CargarListasForm();
        await ConfigurarFormulario(editId);

        return View(clientes);
    }

    [HttpGet]
    public async Task<IActionResult> SearchPartial(
        string searchTerm,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = "Nombre",
        string sortOrder = "asc")
    {
        var clientes = await _clienteService.ObtenerClientes(searchTerm, pageNumber, pageSize, sortBy, sortOrder);
        var totalClientes = await _clienteService.ObtenerTotalClientes(searchTerm);
        var totalPages = Math.Max((int)Math.Ceiling(totalClientes / (double)pageSize), 1);

        ViewBag.CurrentPage = pageNumber;
        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = GetVisiblePages(pageNumber, totalPages);
        ViewBag.SortBy = sortBy;
        ViewBag.SortOrder = sortOrder;
        ViewBag.SearchTerm = searchTerm;

        return PartialView("_ClienteTable", clientes);
    }

    [HttpGet]
    public async Task<IActionResult> TablePartial(
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = "Nombre",
        string sortOrder = "asc")
    {
        return await SearchPartial(null, pageNumber, pageSize, sortBy, sortOrder);
    }

    [HttpGet]
    public async Task<IActionResult> FormPartial(string id)
    {
        await CargarListasForm();
        Cliente cliente = null;
        if (!string.IsNullOrEmpty(id))
            cliente = await _clienteService.ObtenerCliente(id);

        return PartialView("_ClienteForm", cliente);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearClienteAjax(Cliente cliente)
    {
        try
        {
            ModelState.Remove("Id"); // Id es generado
            if (!ModelState.IsValid) return await PrepararRespuestaAjax(false, cliente, null);

            // Validar duplicados (TipoDoc + NumDoc)
            var existente = await _clienteService.ObtenerClientePorDocumento(cliente.TipoDocumento, cliente.NumeroDocumento);
            if (existente != null)
            {
                ModelState.AddModelError("NumeroDocumento", "Ya existe un cliente con este documento.");
                return await PrepararRespuestaAjax(false, cliente, null);
            }

            await _clienteService.CrearCliente(cliente);
            return await PrepararRespuestaAjax(true, cliente, "Creacion de cliente");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
            return await PrepararRespuestaAjax(false, cliente, null);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActualizarClienteAjax(Cliente cliente)
    {
        try
        {
            if (!ModelState.IsValid) return await PrepararRespuestaAjax(false, cliente, null);

            var clienteActual = await _clienteService.ObtenerCliente(cliente.Id);
            if (clienteActual == null)
            {
                ModelState.AddModelError("", "El cliente no existe.");
                return await PrepararRespuestaAjax(false, cliente, null);
            }

            // Mantener VehiculosIds si no vienen en el form (aunque deberían venir si se manejan bien)
            // En este caso, asumimos que el form no edita VehiculosIds directamente, sino que se agregan por otro lado.
            // Pero si el modelo binder los pierde, hay que recuperarlos.
            cliente.VehiculosIds = clienteActual.VehiculosIds;

            await _clienteService.ActualizarCliente(cliente);
            return await PrepararRespuestaAjax(true, cliente, "Actualizacion de cliente");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
            return await PrepararRespuestaAjax(false, cliente, null);
        }
    }

    [HttpPost]
    public async Task<IActionResult> EliminarCliente(string id)
    {
        try
        {
            // Validar si tiene vehículos? El requerimiento dice "un vehículo solo puede tener un dueño".
            // Si borramos el cliente, los vehículos quedarían huérfanos o deberían borrarse.
            // Por seguridad, impedimos borrar si tiene vehículos, o los desvinculamos.
            // Asumiremos desvinculación o restricción. Restricción es más segura.
            var cliente = await _clienteService.ObtenerCliente(id);
            if (cliente != null && cliente.VehiculosIds != null && cliente.VehiculosIds.Any())
            {
                return Json(new { success = false, message = "No se puede eliminar el cliente porque tiene vehículos asociados." });
            }

            await _clienteService.EliminarCliente(id);
            await RegistrarEvento("Eliminacion de cliente", id, "Cliente");
            return Json(new { success = true, message = "Cliente eliminado correctamente." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Error al eliminar cliente: {ex.Message}" });
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

    private async Task<IActionResult> PrepararRespuestaAjax(bool success, Cliente cliente, string accionAuditoria)
    {
        if (!success)
        {
            Response.Headers["X-Form-Valid"] = "false";
            await CargarListasForm();
            return PartialView("_ClienteForm", cliente);
        }

        await RegistrarEvento(accionAuditoria, cliente.Id, "Cliente");
        Response.Headers["X-Form-Valid"] = "true";
        Response.Headers["X-Form-Message"] = accionAuditoria.Contains("Creacion") ? "Cliente creado correctamente." : "Cliente actualizado correctamente.";
        await CargarListasForm();
        return PartialView("_ClienteForm", null);
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
        await _auditService.LogEvent(userId, userEmail, accion, targetId, entidad);
    }
}

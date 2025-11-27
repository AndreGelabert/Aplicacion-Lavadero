using Firebase.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[Authorize(Roles = "Administrador,Empleado")]
public class VehiculoController : Controller
{
    private readonly VehiculoService _vehiculoService;
    private readonly TipoVehiculoService _tipoVehiculoService;
    private readonly ClienteService _clienteService; // Para obtener info del dueño
    private readonly AuditService _auditService;

    public VehiculoController(
        VehiculoService vehiculoService,
        TipoVehiculoService tipoVehiculoService,
        ClienteService clienteService,
        AuditService auditService)
    {
        _vehiculoService = vehiculoService;
        _tipoVehiculoService = tipoVehiculoService;
        _clienteService = clienteService;
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string searchTerm,
        string tipoVehiculo,
        List<string> estados,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = "Patente",
        string sortOrder = "asc",
        string editId = null)
    {
        // Si no se especifican estados, por defecto mostrar solo Activos
        if (estados == null || !estados.Any())
        {
            estados = new List<string> { "Activo" };
        }

        var vehiculos = await _vehiculoService.ObtenerVehiculos(searchTerm, tipoVehiculo, pageNumber, pageSize, sortBy, sortOrder, estados);
        var totalVehiculos = await _vehiculoService.ObtenerTotalVehiculos(searchTerm, tipoVehiculo, estados);
        var totalPages = Math.Max((int)Math.Ceiling(totalVehiculos / (double)pageSize), 1);

        ViewBag.CurrentPage = pageNumber;
        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = GetVisiblePages(pageNumber, totalPages);
        ViewBag.SortBy = sortBy;
        ViewBag.SortOrder = sortOrder;
        ViewBag.SearchTerm = searchTerm;
        ViewBag.TipoVehiculo = tipoVehiculo;
        ViewBag.Estados = estados;
        ViewBag.PageSize = pageSize;

        await CargarListasForm();
        
        // Solo configurar formulario si se está editando
        if (!string.IsNullOrEmpty(editId))
        {
            await ConfigurarFormulario(editId);
        }
        else
        {
            ViewBag.FormTitle = "Registrando Vehículo";
            ViewBag.EditVehiculo = null;
        }

        return View(vehiculos);
    }

    [HttpGet]
    public async Task<IActionResult> SearchPartial(
        string searchTerm,
        string tipoVehiculo,
        List<string> estados,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = "Patente",
        string sortOrder = "asc")
    {
        if (estados == null || !estados.Any())
        {
            estados = new List<string> { "Activo" };
        }

        var vehiculos = await _vehiculoService.ObtenerVehiculos(searchTerm, tipoVehiculo, pageNumber, pageSize, sortBy, sortOrder, estados);
        var totalVehiculos = await _vehiculoService.ObtenerTotalVehiculos(searchTerm, tipoVehiculo, estados);
        var totalPages = Math.Max((int)Math.Ceiling(totalVehiculos / (double)pageSize), 1);

        ViewBag.CurrentPage = pageNumber;
        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = GetVisiblePages(pageNumber, totalPages);
        ViewBag.SortBy = sortBy;
        ViewBag.SortOrder = sortOrder;
        ViewBag.SearchTerm = searchTerm;
        ViewBag.TipoVehiculo = tipoVehiculo;
        ViewBag.Estados = estados;

        return PartialView("_VehiculoTable", vehiculos);
    }

    [HttpGet]
    public async Task<IActionResult> TablePartial(
        string tipoVehiculo,
        List<string> estados,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = "Patente",
        string sortOrder = "asc")
    {
        return await SearchPartial(null, tipoVehiculo, estados, pageNumber, pageSize, sortBy, sortOrder);
    }

    [HttpGet]
    public async Task<IActionResult> FormPartial(string id)
    {
        await CargarListasForm();
        Vehiculo vehiculo = null;
        if (!string.IsNullOrEmpty(id))
            vehiculo = await _vehiculoService.ObtenerVehiculo(id);

        return PartialView("_VehiculoForm", vehiculo);
    }

    [HttpGet]
    public async Task<IActionResult> GetClienteInfo(string clienteId)
    {
        if (string.IsNullOrEmpty(clienteId)) return NotFound();
        var cliente = await _clienteService.ObtenerCliente(clienteId);
        if (cliente == null) return NotFound();

        // Retornamos un partial o un JSON para mostrar en el modal
        return Json(new
        {
            nombreCompleto = cliente.NombreCompleto,
            documento = $"{cliente.TipoDocumento} {cliente.NumeroDocumento}",
            telefono = cliente.Telefono,
            email = cliente.Email
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearVehiculoAjax(Vehiculo vehiculo)
    {
        try
        {
            ModelState.Remove("Id");
            ModelState.Remove("ClienteNombreCompleto"); // No viene del form
            if (!ModelState.IsValid) return await PrepararRespuestaAjax(false, vehiculo, null);

            var existente = await _vehiculoService.ObtenerVehiculoPorPatente(vehiculo.Patente);
            if (existente != null)
            {
                ModelState.AddModelError("Patente", "Ya existe un vehículo con esta patente.");
                return await PrepararRespuestaAjax(false, vehiculo, null);
            }

            // Si se asignó un cliente (aunque el form principal de vehículo no suele tener selector de cliente según requerimiento inicial, 
            // pero "un vehículo solo puede tener un dueño", así que podría asignarse después o desde la vista de cliente).
            // El requerimiento dice: "campo llamado titular, pero que sea de solo lectura... si todavia no se le asigno un dueño, quedará vacio".
            // Esto implica que la asignación se hace desde Cliente -> Vehículo, o quizás aquí si agregamos lógica extra.
            // Por ahora respetamos el modelo.

            await _vehiculoService.CrearVehiculo(vehiculo);
            return await PrepararRespuestaAjax(true, vehiculo, "Creacion de vehiculo");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
            return await PrepararRespuestaAjax(false, vehiculo, null);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActualizarVehiculoAjax(Vehiculo vehiculo)
    {
        try
        {
            // Validaciones de solo lectura (Patente, Tipo, Marca no editables)
            // Debemos obtener el original para comparar o restaurar
            var original = await _vehiculoService.ObtenerVehiculo(vehiculo.Id);
            if (original == null) return NotFound();

            // Restaurar campos no editables para asegurar integridad si el binder los trajo modificados (o validar que sean iguales)
            vehiculo.Patente = original.Patente;
            vehiculo.TipoVehiculo = original.TipoVehiculo;
            vehiculo.Marca = original.Marca;
            vehiculo.ClienteId = original.ClienteId; // El dueño no se cambia desde aquí según requerimiento implícito de "solo lectura"
            vehiculo.ClienteNombreCompleto = original.ClienteNombreCompleto;

            // Solo Modelo y Color son editables según "Una vez registrado el vehículo, no se puede editar patente, tipo vehículo y marca"
            // El usuario no mencionó Modelo y Color como NO editables, así que asumimos que sí.

            ModelState.Remove("ClienteNombreCompleto");
            if (!ModelState.IsValid) return await PrepararRespuestaAjax(false, vehiculo, null);

            await _vehiculoService.ActualizarVehiculo(vehiculo);
            return await PrepararRespuestaAjax(true, vehiculo, "Actualizacion de vehiculo");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
            return await PrepararRespuestaAjax(false, vehiculo, null);
        }
    }

    [HttpPost]
    public async Task<IActionResult> DeactivateVehiculo(string id)
    {
        try
        {
            await _vehiculoService.CambiarEstadoVehiculo(id, "Inactivo");
            await RegistrarEvento("Desactivacion de vehiculo", id, "Vehiculo");
            return Json(new { success = true, message = "Vehículo desactivado correctamente." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Error al desactivar vehículo: {ex.Message}" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ReactivateVehiculo(string id)
    {
        try
        {
            await _vehiculoService.CambiarEstadoVehiculo(id, "Activo");
            await RegistrarEvento("Reactivacion de vehiculo", id, "Vehiculo");
            return Json(new { success = true, message = "Vehículo reactivado correctamente." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Error al reactivar vehículo: {ex.Message}" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> EliminarVehiculo(string id)
    {
        try
        {
            // Borrado físico - solo si realmente es necesario
            await _vehiculoService.EliminarVehiculo(id);
            await RegistrarEvento("Eliminacion fisica de vehiculo", id, "Vehiculo");
            return Json(new { success = true, message = "Vehículo eliminado correctamente." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Error al eliminar vehículo: {ex.Message}" });
        }
    }

    private async Task CargarListasForm()
    {
        ViewBag.TiposVehiculo = await _tipoVehiculoService.ObtenerTiposVehiculos();
    }

    private async Task ConfigurarFormulario(string editId)
    {
        if (!string.IsNullOrEmpty(editId))
        {
            var vehiculo = await _vehiculoService.ObtenerVehiculo(editId);
            ViewBag.EditVehiculo = vehiculo;
            ViewBag.FormTitle = "Editando Vehículo";
            ViewBag.SubmitButtonText = "Guardar";
            ViewBag.ClearButtonText = "Cancelar";
            ViewBag.FormAction = "ActualizarVehiculoAjax";
        }
        else
        {
            ViewBag.FormTitle = "Registrando Vehículo";
            ViewBag.SubmitButtonText = "Registrar";
            ViewBag.ClearButtonText = "Limpiar Campos";
            ViewBag.FormAction = "CrearVehiculoAjax";
        }
    }

    private async Task<IActionResult> PrepararRespuestaAjax(bool success, Vehiculo vehiculo, string accionAuditoria)
    {
        if (!success)
        {
            Response.Headers["X-Form-Valid"] = "false";
            await CargarListasForm();
            return PartialView("_VehiculoForm", vehiculo);
        }

        await RegistrarEvento(accionAuditoria, vehiculo.Id, "Vehiculo");
        Response.Headers["X-Form-Valid"] = "true";
        Response.Headers["X-Form-Message"] = accionAuditoria.Contains("Creacion") ? "Vehículo creado correctamente." : "Vehículo actualizado correctamente.";
        await CargarListasForm();
        return PartialView("_VehiculoForm", null);
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

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
        List<string> estados,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = "Nombre",
        string sortOrder = "asc",
        string editId = null)
    {
        // Si no se especifican estados, por defecto mostrar solo Activos
        if (estados == null || !estados.Any())
        {
            estados = new List<string> { "Activo" };
        }

        var clientes = await _clienteService.ObtenerClientes(searchTerm, pageNumber, pageSize, sortBy, sortOrder, estados);
        var totalClientes = await _clienteService.ObtenerTotalClientes(searchTerm, estados);
        var totalPages = Math.Max((int)Math.Ceiling(totalClientes / (double)pageSize), 1);

        ViewBag.CurrentPage = pageNumber;
        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = GetVisiblePages(pageNumber, totalPages);
        ViewBag.SortBy = sortBy;
        ViewBag.SortOrder = sortOrder;
        ViewBag.SearchTerm = searchTerm;
        ViewBag.Estados = estados;
        ViewBag.PageSize = pageSize;

        await CargarListasForm();
        await ConfigurarFormulario(editId);

        return View(clientes);
    }

    [HttpGet]
    public async Task<IActionResult> SearchPartial(
        string searchTerm,
        List<string> estados,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = "Nombre",
        string sortOrder = "asc")
    {
        if (estados == null || !estados.Any())
        {
            estados = new List<string> { "Activo" };
        }

        var clientes = await _clienteService.ObtenerClientes(searchTerm, pageNumber, pageSize, sortBy, sortOrder, estados);
        var totalClientes = await _clienteService.ObtenerTotalClientes(searchTerm, estados);
        var totalPages = Math.Max((int)Math.Ceiling(totalClientes / (double)pageSize), 1);

        ViewBag.CurrentPage = pageNumber;
        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = GetVisiblePages(pageNumber, totalPages);
        ViewBag.SortBy = sortBy;
        ViewBag.SortOrder = sortOrder;
        ViewBag.SearchTerm = searchTerm;
        ViewBag.Estados = estados;

        return PartialView("_ClienteTable", clientes);
    }

    [HttpGet]
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
    public async Task<IActionResult> CrearClienteAjax(Cliente cliente, string? vehiculosDataJson = null)
    {
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

            List<VehiculoData>? vehiculosData = null;
            
            if (!string.IsNullOrWhiteSpace(vehiculosDataJson))
            {
                try
                {
                    var options = new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    
                    vehiculosData = System.Text.Json.JsonSerializer.Deserialize<List<VehiculoData>>(vehiculosDataJson, options);
                }
                catch (System.Text.Json.JsonException)
                {
                    vehiculosData = new List<VehiculoData>();
                }
            }

            if (vehiculosData == null || !vehiculosData.Any())
            {
                ModelState.AddModelError("", "Debe agregar al menos un vehículo.");
                return await PrepararRespuestaAjax(false, cliente, null);
            }

            await _clienteService.CrearCliente(cliente);

            var vehiculosIds = new List<string>();

            foreach (var vehiculoData in vehiculosData)
            {
                if (vehiculoData.EsNuevo)
                {
                    var nuevoVehiculo = new Vehiculo
                    {
                        Id = "",
                        Patente = vehiculoData.Patente.ToUpper(), // Convertir a mayúsculas
                        Marca = vehiculoData.Marca,
                        Modelo = vehiculoData.Modelo,
                        Color = vehiculoData.Color,
                        TipoVehiculo = vehiculoData.TipoVehiculo,
                        ClienteId = cliente.Id,
                        ClienteNombreCompleto = cliente.NombreCompleto,
                        Estado = "Activo"
                    };

                    await _vehiculoService.CrearVehiculo(nuevoVehiculo);
                    vehiculosIds.Add(nuevoVehiculo.Id);
                    
                    // Registrar evento de creación de vehículo
                    await RegistrarEvento("Creacion de vehiculo", nuevoVehiculo.Id, "Vehiculo");
                }
                else if (!string.IsNullOrEmpty(vehiculoData.Id))
                {
                    var vehiculo = await _vehiculoService.ObtenerVehiculo(vehiculoData.Id);
                    if (vehiculo != null)
                    {
                        // Actualizar datos del vehículo (reasignación o actualización normal)
                        vehiculo.ClienteId = cliente.Id;
                        vehiculo.ClienteNombreCompleto = cliente.NombreCompleto;
                        vehiculo.Color = vehiculoData.Color; // Actualizar color si cambió
                        vehiculo.TipoVehiculo = vehiculoData.TipoVehiculo; // Actualizar tipo si cambió
                        vehiculo.Estado = "Activo"; // Reactivar si estaba inactivo
                        
                        await _vehiculoService.ActualizarVehiculo(vehiculo);
                        vehiculosIds.Add(vehiculo.Id);
                        
                        // Registrar evento de reasignación si corresponde
                        if (vehiculoData.EsReasignacion)
                        {
                            await RegistrarEvento("Reasignacion de vehiculo sin dueno a nuevo cliente", vehiculo.Id, "Vehiculo");
                        }
                    }
                }
            }

            cliente.VehiculosIds = vehiculosIds;
            await _clienteService.ActualizarCliente(cliente);

            return await PrepararRespuestaAjax(true, cliente, "Creacion de cliente");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
            return await PrepararRespuestaAjax(false, cliente, null);
        }
    }

    // Clase auxiliar para deserializar datos de vehículos
    private class VehiculoData
    {
        public string? Id { get; set; }
        public string Patente { get; set; } = "";
        public string Marca { get; set; } = "";
        public string Modelo { get; set; } = "";
        public string Color { get; set; } = "";
        public string TipoVehiculo { get; set; } = "";
        public bool EsNuevo { get; set; }
        public bool EsReasignacion { get; set; } // Para indicar reasignación de vehículo sin dueño
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActualizarClienteAjax(Cliente cliente, string? vehiculosDataJson = null)
    {
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

            List<VehiculoData>? vehiculosData = null;
            
            if (!string.IsNullOrWhiteSpace(vehiculosDataJson))
            {
                try
                {
                    var options = new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    
                    vehiculosData = System.Text.Json.JsonSerializer.Deserialize<List<VehiculoData>>(vehiculosDataJson, options);
                }
                catch (System.Text.Json.JsonException)
                {
                    vehiculosData = new List<VehiculoData>();
                }
            }

            if (vehiculosData == null || !vehiculosData.Any())
            {
                ModelState.AddModelError("", "Debe tener al menos un vehículo.");
                return await PrepararRespuestaAjax(false, cliente, null);
            }

            var vehiculosIds = new List<string>();
            var vehiculosAnteriores = clienteActual.VehiculosIds ?? new List<string>();

            foreach (var vehiculoData in vehiculosData)
            {
                if (vehiculoData.EsNuevo)
                {
                    var nuevoVehiculo = new Vehiculo
                    {
                        Id = "",
                        Patente = vehiculoData.Patente.ToUpper(),
                        Marca = vehiculoData.Marca,
                        Modelo = vehiculoData.Modelo,
                        Color = vehiculoData.Color,
                        TipoVehiculo = vehiculoData.TipoVehiculo,
                        ClienteId = cliente.Id,
                        ClienteNombreCompleto = cliente.NombreCompleto,
                        Estado = "Activo"
                    };

                    await _vehiculoService.CrearVehiculo(nuevoVehiculo);
                    vehiculosIds.Add(nuevoVehiculo.Id);
                    
                    // Registrar evento de creación de vehículo
                    await RegistrarEvento("Creacion de vehiculo", nuevoVehiculo.Id, "Vehiculo");
                }
                else if (!string.IsNullOrEmpty(vehiculoData.Id))
                {
                    var vehiculo = await _vehiculoService.ObtenerVehiculo(vehiculoData.Id);
                    if (vehiculo != null)
                    {
                        // Actualizar datos del vehículo (reasignación o actualización normal)
                        vehiculo.ClienteId = cliente.Id;
                        vehiculo.ClienteNombreCompleto = cliente.NombreCompleto;
                        vehiculo.Color = vehiculoData.Color; // Actualizar color si cambió
                        vehiculo.TipoVehiculo = vehiculoData.TipoVehiculo; // Actualizar tipo si cambió
                        vehiculo.Estado = "Activo"; // Reactivar si estaba inactivo
                        
                        await _vehiculoService.ActualizarVehiculo(vehiculo);
                        vehiculosIds.Add(vehiculo.Id);
                        
                        // Registrar evento de reasignación si corresponde
                        if (vehiculoData.EsReasignacion)
                        {
                            await RegistrarEvento("Reasignacion de vehiculo sin dueno a cliente existente", vehiculo.Id, "Vehiculo");
                        }
                    }
                }
            }

            // Detectar vehículos removidos (que estaban antes pero ya no están)
            var vehiculosRemovidos = vehiculosAnteriores.Except(vehiculosIds).ToList();
            
            // Desvincular y desactivar vehículos removidos
            foreach (var vehiculoRemovidoId in vehiculosRemovidos)
            {
                var vehiculoRemovido = await _vehiculoService.ObtenerVehiculo(vehiculoRemovidoId);
                if (vehiculoRemovido != null)
                {
                    vehiculoRemovido.ClienteId = "";
                    vehiculoRemovido.ClienteNombreCompleto = null;
                    vehiculoRemovido.Estado = "Inactivo";
                    await _vehiculoService.ActualizarVehiculo(vehiculoRemovido);
                    await RegistrarEvento("Vehiculo desvinculado y desactivado (removido de cliente)", vehiculoRemovidoId, "Vehiculo");
                }
            }

            cliente.VehiculosIds = vehiculosIds;
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
    public async Task<IActionResult> DeactivateCliente(string id)
    {
        try
        {
            // Obtener vehículos del cliente
            var vehiculos = await _vehiculoService.ObtenerVehiculosPorCliente(id);
            
            // Desactivar todos los vehículos activos del cliente
            foreach (var vehiculo in vehiculos.Where(v => v.Estado == "Activo"))
            {
                await _vehiculoService.CambiarEstadoVehiculo(vehiculo.Id, "Inactivo");
                await RegistrarEvento("Desactivacion de vehiculo (por desactivacion de cliente)", vehiculo.Id, "Vehiculo");
            }
            
            // Desactivar el cliente
            await _clienteService.CambiarEstadoCliente(id, "Inactivo");
            await RegistrarEvento("Desactivacion de cliente", id, "Cliente");
            
            var cantidadVehiculos = vehiculos.Count(v => v.Estado == "Activo");
            var mensaje = cantidadVehiculos > 0 
                ? $"Cliente desactivado correctamente. Se desactivaron {cantidadVehiculos} vehículo(s) asociado(s)."
                : "Cliente desactivado correctamente.";
            
            return Json(new { success = true, message = mensaje });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Error al desactivar cliente: {ex.Message}" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ReactivateCliente(string id)
    {
        try
        {
            // Obtener vehículos del cliente
            var vehiculos = await _vehiculoService.ObtenerVehiculosPorCliente(id);
            
            // Contar vehículos inactivos ANTES de reactivarlos
            var cantidadVehiculosInactivos = vehiculos.Count(v => v.Estado == "Inactivo");
            
            // Reactivar todos los vehículos inactivos del cliente (solo si tienen dueño)
            foreach (var vehiculo in vehiculos.Where(v => v.Estado == "Inactivo" && !string.IsNullOrEmpty(v.ClienteId)))
            {
                await _vehiculoService.CambiarEstadoVehiculo(vehiculo.Id, "Activo");
                await RegistrarEvento("Reactivacion de vehiculo (por reactivacion de cliente)", vehiculo.Id, "Vehiculo");
            }
            
            // Reactivar el cliente
            await _clienteService.CambiarEstadoCliente(id, "Activo");
            await RegistrarEvento("Reactivacion de cliente", id, "Cliente");
            
            var mensaje = cantidadVehiculosInactivos > 0 
                ? $"Cliente reactivado correctamente. Se reactivaron {cantidadVehiculosInactivos} vehículo(s) asociado(s)."
                : "Cliente reactivado correctamente.";
            
            return Json(new { success = true, message = mensaje });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Error al reactivar cliente: {ex.Message}" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetVehiculosCliente(string clienteId)
    {
        try
        {
            var cliente = await _clienteService.ObtenerCliente(clienteId);
            if (cliente == null)
            {
                return Json(new { success = false, message = "Cliente no encontrado" });
            }

            var vehiculos = await _vehiculoService.ObtenerVehiculosPorCliente(clienteId);
            
            return Json(new
            {
                success = true,
                vehiculos = vehiculos.Select(v => new
                {
                    id = v.Id,
                    patente = v.Patente,
                    marca = v.Marca,
                    modelo = v.Modelo,
                    color = v.Color,
                    tipoVehiculo = v.TipoVehiculo,
                    estado = v.Estado
                })
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> EliminarCliente(string id)
    {
        try
        {
            var cliente = await _clienteService.ObtenerCliente(id);
            if (cliente != null && cliente.VehiculosIds != null && cliente.VehiculosIds.Any())
            {
                return Json(new { success = false, message = "No se puede eliminar el cliente porque tiene vehículos asociados." });
            }

            await _clienteService.EliminarCliente(id);
            await RegistrarEvento("Eliminacion fisica de cliente", id, "Cliente");
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

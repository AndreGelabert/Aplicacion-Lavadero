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

    /// <summary>
    /// Obtiene el detalle de un cliente para mostrar en modal.
    /// </summary>
    [HttpGet]
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
            var clavesAsociacionGeneradas = new List<string>(); // Para mostrar al usuario

            foreach (var vehiculoData in vehiculosData)
            {
                if (vehiculoData.EsAsociacion)
                {
                    // CASO: Asociación a vehículo existente mediante clave
                    var vehiculoExistente = await _vehiculoService.ObtenerVehiculoPorPatente(vehiculoData.Patente.ToUpper());
                    if (vehiculoExistente != null)
                    {
                        // Agregar cliente a la lista de clientes asociados del vehículo
                        if (vehiculoExistente.ClientesIds == null)
                        {
                            vehiculoExistente.ClientesIds = new List<string>();
                        }
                        
                        if (!vehiculoExistente.ClientesIds.Contains(cliente.Id))
                        {
                            vehiculoExistente.ClientesIds.Add(cliente.Id);
                            await _vehiculoService.ActualizarVehiculo(vehiculoExistente);
                        }
                        
                        vehiculosIds.Add(vehiculoExistente.Id);
                        await RegistrarEvento("Asociacion de cliente a vehiculo compartido", vehiculoExistente.Id, "Vehiculo");
                    }
                }
                else if (vehiculoData.EsNuevo)
                {
                    // CASO: Nuevo vehículo - Generar clave de asociación
                    var claveTextoPlano = VehiculoService.GenerarClaveAsociacion();
                    var claveHash = VehiculoService.HashClaveAsociacion(claveTextoPlano);
                    
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
                        ClientesIds = new List<string> { cliente.Id }, // Agregar también a la lista
                        ClaveAsociacionHash = claveHash,
                        Estado = "Activo"
                    };

                    await _vehiculoService.CrearVehiculo(nuevoVehiculo);
                    vehiculosIds.Add(nuevoVehiculo.Id);
                    
                    // Guardar la clave para mostrarla al usuario
                    clavesAsociacionGeneradas.Add($"{nuevoVehiculo.Patente}: {claveTextoPlano}");
                    
                    await RegistrarEvento("Creacion de vehiculo", nuevoVehiculo.Id, "Vehiculo");
                }
                else if (!string.IsNullOrEmpty(vehiculoData.Id))
                {
                    // CASO: Reasignación de vehículo existente sin dueño
                    var vehiculo = await _vehiculoService.ObtenerVehiculo(vehiculoData.Id);
                    if (vehiculo != null)
                    {
                        // Generar clave de asociación si no tiene
                        if (string.IsNullOrEmpty(vehiculo.ClaveAsociacionHash))
                        {
                            var claveTextoPlano = VehiculoService.GenerarClaveAsociacion();
                            vehiculo.ClaveAsociacionHash = VehiculoService.HashClaveAsociacion(claveTextoPlano);
                            clavesAsociacionGeneradas.Add($"{vehiculo.Patente}: {claveTextoPlano}");
                        }
                        
                        vehiculo.ClienteId = cliente.Id;
                        vehiculo.ClienteNombreCompleto = cliente.NombreCompleto;
                        vehiculo.Color = vehiculoData.Color;
                        vehiculo.TipoVehiculo = vehiculoData.TipoVehiculo;
                        vehiculo.Estado = "Activo";
                        
                        // Inicializar o actualizar lista de clientes
                        if (vehiculo.ClientesIds == null)
                        {
                            vehiculo.ClientesIds = new List<string>();
                        }
                        if (!vehiculo.ClientesIds.Contains(cliente.Id))
                        {
                            vehiculo.ClientesIds.Add(cliente.Id);
                        }
                        
                        await _vehiculoService.ActualizarVehiculo(vehiculo);
                        vehiculosIds.Add(vehiculo.Id);
                        
                        if (vehiculoData.EsReasignacion)
                        {
                            await RegistrarEvento("Reasignacion de vehiculo sin dueno a nuevo cliente", vehiculo.Id, "Vehiculo");
                        }
                    }
                }
            }

            cliente.VehiculosIds = vehiculosIds;
            await _clienteService.ActualizarCliente(cliente);

            // Si se generaron claves, incluirlas en el mensaje de respuesta
            if (clavesAsociacionGeneradas.Any())
            {
                var clavesMsg = string.Join(", ", clavesAsociacionGeneradas);
                Response.Headers["X-Association-Keys"] = System.Text.Json.JsonSerializer.Serialize(clavesAsociacionGeneradas);
            }

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
        public bool EsAsociacion { get; set; } // Para indicar asociación a vehículo existente con clave
        public string? ClaveAsociacion { get; set; } // Clave de asociación para vehículos compartidos
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
            var clavesAsociacionGeneradas = new List<string>();

            foreach (var vehiculoData in vehiculosData)
            {
                if (vehiculoData.EsAsociacion)
                {
                    // CASO: Asociación a vehículo existente mediante clave
                    var vehiculoExistente = await _vehiculoService.ObtenerVehiculoPorPatente(vehiculoData.Patente.ToUpper());
                    if (vehiculoExistente != null)
                    {
                        // Agregar cliente a la lista de clientes asociados del vehículo
                        if (vehiculoExistente.ClientesIds == null)
                        {
                            vehiculoExistente.ClientesIds = new List<string>();
                        }
                        
                        if (!vehiculoExistente.ClientesIds.Contains(cliente.Id))
                        {
                            vehiculoExistente.ClientesIds.Add(cliente.Id);
                            await _vehiculoService.ActualizarVehiculo(vehiculoExistente);
                        }
                        
                        vehiculosIds.Add(vehiculoExistente.Id);
                        await RegistrarEvento("Asociacion de cliente a vehiculo compartido", vehiculoExistente.Id, "Vehiculo");
                    }
                }
                else if (vehiculoData.EsNuevo)
                {
                    // CASO: Nuevo vehículo - Generar clave de asociación
                    var claveTextoPlano = VehiculoService.GenerarClaveAsociacion();
                    var claveHash = VehiculoService.HashClaveAsociacion(claveTextoPlano);
                    
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
                        ClientesIds = new List<string> { cliente.Id },
                        ClaveAsociacionHash = claveHash,
                        Estado = "Activo"
                    };

                    await _vehiculoService.CrearVehiculo(nuevoVehiculo);
                    vehiculosIds.Add(nuevoVehiculo.Id);
                    
                    // Guardar la clave para mostrarla al usuario
                    clavesAsociacionGeneradas.Add($"{nuevoVehiculo.Patente}: {claveTextoPlano}");
                    
                    // Registrar evento de creación de vehículo
                    await RegistrarEvento("Creacion de vehiculo", nuevoVehiculo.Id, "Vehiculo");
                }
                else if (!string.IsNullOrEmpty(vehiculoData.Id))
                {
                    var vehiculo = await _vehiculoService.ObtenerVehiculo(vehiculoData.Id);
                    if (vehiculo != null)
                    {
                        // Generar clave de asociación si no tiene
                        if (string.IsNullOrEmpty(vehiculo.ClaveAsociacionHash))
                        {
                            var claveTextoPlano = VehiculoService.GenerarClaveAsociacion();
                            vehiculo.ClaveAsociacionHash = VehiculoService.HashClaveAsociacion(claveTextoPlano);
                            clavesAsociacionGeneradas.Add($"{vehiculo.Patente}: {claveTextoPlano}");
                        }
                        
                        // Actualizar datos del vehículo
                        vehiculo.Color = vehiculoData.Color;
                        vehiculo.TipoVehiculo = vehiculoData.TipoVehiculo;
                        vehiculo.Estado = "Activo";
                        
                        // Asegurar que el cliente esté en la lista de clientes
                        if (vehiculo.ClientesIds == null)
                        {
                            vehiculo.ClientesIds = new List<string>();
                        }
                        if (!vehiculo.ClientesIds.Contains(cliente.Id))
                        {
                            vehiculo.ClientesIds.Add(cliente.Id);
                        }
                        
                        // Solo actualizar ClienteId y ClienteNombreCompleto si era vacío (reasignación)
                        if (string.IsNullOrEmpty(vehiculo.ClienteId))
                        {
                            vehiculo.ClienteId = cliente.Id;
                            vehiculo.ClienteNombreCompleto = cliente.NombreCompleto;
                        }
                        
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
            
            // Desvincular vehículos removidos
            foreach (var vehiculoRemovidoId in vehiculosRemovidos)
            {
                var vehiculoRemovido = await _vehiculoService.ObtenerVehiculo(vehiculoRemovidoId);
                if (vehiculoRemovido != null)
                {
                    // Remover de ClientesIds
                    if (vehiculoRemovido.ClientesIds != null && vehiculoRemovido.ClientesIds.Contains(cliente.Id))
                    {
                        vehiculoRemovido.ClientesIds.Remove(cliente.Id);
                    }
                    
                    // Si era el cliente principal, limpiar ClienteId
                    if (vehiculoRemovido.ClienteId == cliente.Id)
                    {
                        vehiculoRemovido.ClienteId = "";
                        vehiculoRemovido.ClienteNombreCompleto = null;
                    }
                    
                    // Si no quedan clientes, desactivar el vehículo
                    bool tieneClientes = !string.IsNullOrEmpty(vehiculoRemovido.ClienteId) || 
                                        (vehiculoRemovido.ClientesIds != null && vehiculoRemovido.ClientesIds.Any());
                    
                    if (!tieneClientes)
                    {
                        vehiculoRemovido.Estado = "Inactivo";
                    }
                    
                    await _vehiculoService.ActualizarVehiculo(vehiculoRemovido);
                    await RegistrarEvento("Vehiculo desvinculado de cliente", vehiculoRemovidoId, "Vehiculo");
                }
            }

            cliente.VehiculosIds = vehiculosIds;
            await _clienteService.ActualizarCliente(cliente);

            // Si se generaron claves, incluirlas en el mensaje de respuesta
            if (clavesAsociacionGeneradas.Any())
            {
                Response.Headers["X-Association-Keys"] = System.Text.Json.JsonSerializer.Serialize(clavesAsociacionGeneradas);
            }

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
            
            int vehiculosDesactivados = 0;
            int vehiculosCompartidosNoDesactivados = 0;
            
            // Procesar cada vehículo activo
            foreach (var vehiculo in vehiculos.Where(v => v.Estado == "Activo"))
            {
                // Verificar si el vehículo es compartido (tiene otros clientes activos)
                bool tieneOtrosClientesActivos = false;
                
                if (vehiculo.ClientesIds != null && vehiculo.ClientesIds.Any())
                {
                    // Verificar si hay otros clientes activos que usan este vehículo
                    foreach (var clienteId in vehiculo.ClientesIds.Where(c => c != id))
                    {
                        var otroCliente = await _clienteService.ObtenerCliente(clienteId);
                        if (otroCliente != null && otroCliente.Estado == "Activo")
                        {
                            tieneOtrosClientesActivos = true;
                            break;
                        }
                    }
                }
                
                // También verificar ClienteId principal si es diferente
                if (!tieneOtrosClientesActivos && !string.IsNullOrEmpty(vehiculo.ClienteId) && vehiculo.ClienteId != id)
                {
                    var clientePrincipal = await _clienteService.ObtenerCliente(vehiculo.ClienteId);
                    if (clientePrincipal != null && clientePrincipal.Estado == "Activo")
                    {
                        tieneOtrosClientesActivos = true;
                    }
                }
                
                if (tieneOtrosClientesActivos)
                {
                    // No desactivar el vehículo porque otros clientes activos lo usan
                    vehiculosCompartidosNoDesactivados++;
                }
                else
                {
                    // Desactivar el vehículo porque es exclusivo de este cliente (o compartido solo con clientes inactivos)
                    await _vehiculoService.CambiarEstadoVehiculo(vehiculo.Id, "Inactivo");
                    await RegistrarEvento("Desactivacion de vehiculo (por desactivacion de cliente)", vehiculo.Id, "Vehiculo");
                    vehiculosDesactivados++;
                }
            }
            
            // Desactivar el cliente
            await _clienteService.CambiarEstadoCliente(id, "Inactivo");
            await RegistrarEvento("Desactivacion de cliente", id, "Cliente");
            
            // Construir mensaje informativo
            var mensajeParts = new List<string> { "Cliente desactivado correctamente." };
            
            if (vehiculosDesactivados > 0)
            {
                mensajeParts.Add($"Se desactivaron {vehiculosDesactivados} vehículo(s) exclusivo(s).");
            }
            
            if (vehiculosCompartidosNoDesactivados > 0)
            {
                mensajeParts.Add($"{vehiculosCompartidosNoDesactivados} vehículo(s) compartido(s) permanecen activos (usados por otros clientes).");
            }
            
            return Json(new { success = true, message = string.Join(" ", mensajeParts) });
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
            
            int vehiculosReactivados = 0;
            int vehiculosYaActivos = 0;
            
            // Procesar cada vehículo inactivo
            foreach (var vehiculo in vehiculos.Where(v => v.Estado == "Inactivo"))
            {
                // Solo reactivar si el vehículo tenía algún dueño (no quedó huérfano)
                bool tieneAlgunCliente = !string.IsNullOrEmpty(vehiculo.ClienteId) || 
                                         (vehiculo.ClientesIds != null && vehiculo.ClientesIds.Any());
                
                if (tieneAlgunCliente)
                {
                    // Verificar si algún otro cliente activo ya está usando este vehículo
                    bool tieneOtroClienteActivo = false;
                    
                    if (vehiculo.ClientesIds != null)
                    {
                        foreach (var clienteId in vehiculo.ClientesIds.Where(c => c != id))
                        {
                            var otroCliente = await _clienteService.ObtenerCliente(clienteId);
                            if (otroCliente != null && otroCliente.Estado == "Activo")
                            {
                                tieneOtroClienteActivo = true;
                                break;
                            }
                        }
                    }
                    
                    if (!tieneOtroClienteActivo && !string.IsNullOrEmpty(vehiculo.ClienteId) && vehiculo.ClienteId != id)
                    {
                        var clientePrincipal = await _clienteService.ObtenerCliente(vehiculo.ClienteId);
                        if (clientePrincipal != null && clientePrincipal.Estado == "Activo")
                        {
                            tieneOtroClienteActivo = true;
                        }
                    }
                    
                    // Reactivar el vehículo (ya sea porque es exclusivo de este cliente o compartido)
                    await _vehiculoService.CambiarEstadoVehiculo(vehiculo.Id, "Activo");
                    await RegistrarEvento("Reactivacion de vehiculo (por reactivacion de cliente)", vehiculo.Id, "Vehiculo");
                    vehiculosReactivados++;
                }
            }
            
            // Contar vehículos que ya estaban activos (compartidos con otros clientes activos)
            vehiculosYaActivos = vehiculos.Count(v => v.Estado == "Activo");
            
            // Reactivar el cliente
            await _clienteService.CambiarEstadoCliente(id, "Activo");
            await RegistrarEvento("Reactivacion de cliente", id, "Cliente");
            
            // Construir mensaje informativo
            var mensajeParts = new List<string> { "Cliente reactivado correctamente." };
            
            if (vehiculosReactivados > 0)
            {
                mensajeParts.Add($"Se reactivaron {vehiculosReactivados} vehículo(s).");
            }
            
            if (vehiculosYaActivos > 0)
            {
                mensajeParts.Add($"{vehiculosYaActivos} vehículo(s) ya estaban activos (compartidos con otros clientes).");
            }
            
            return Json(new { success = true, message = string.Join(" ", mensajeParts) });
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

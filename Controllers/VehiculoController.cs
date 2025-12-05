using Firebase.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Firebase.Services;

[Authorize(Roles = "Administrador,Empleado")]
public class VehiculoController : Controller
{
    private readonly VehiculoService _vehiculoService;
    private readonly TipoVehiculoService _tipoVehiculoService;
    private readonly ClienteService _clienteService;
    private readonly AuditService _auditService;
    private readonly ICarQueryService _carQueryService;
    private readonly ILogger<VehiculoController> _logger; // Agregado el logger

    public VehiculoController(
        VehiculoService vehiculoService,
        TipoVehiculoService tipoVehiculoService,
        ClienteService clienteService,
        AuditService auditService,
        ICarQueryService carQueryService,
        ILogger<VehiculoController> logger) // Inyectado el logger
    {
        _vehiculoService = vehiculoService;
        _tipoVehiculoService = tipoVehiculoService;
        _clienteService = clienteService;
        _auditService = auditService;
        _carQueryService = carQueryService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string searchTerm,
        List<string> tiposVehiculo,
        List<string> marcas,
        List<string> colores,
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

        var vehiculos = await _vehiculoService.ObtenerVehiculos(searchTerm, tiposVehiculo, marcas, colores, pageNumber, pageSize, sortBy, sortOrder, estados);
        var totalVehiculos = await _vehiculoService.ObtenerTotalVehiculos(searchTerm, tiposVehiculo, marcas, colores, estados);
        var totalPages = Math.Max((int)Math.Ceiling(totalVehiculos / (double)pageSize), 1);

        ViewBag.CurrentPage = pageNumber;
        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = GetVisiblePages(pageNumber, totalPages);
        ViewBag.SortBy = sortBy;
        ViewBag.SortOrder = sortOrder;
        ViewBag.SearchTerm = searchTerm;
        ViewBag.TiposVehiculoFiltrados = tiposVehiculo;
        ViewBag.MarcasFiltradas = marcas;
        ViewBag.ColoresFiltrados = colores;
        ViewBag.Estados = estados;
        ViewBag.PageSize = pageSize;

        await CargarListasForm();
        
        // Obtener listas únicas para filtros
        ViewBag.MarcasDisponibles = await _vehiculoService.ObtenerMarcasUnicas();
        ViewBag.ColoresDisponibles = await _vehiculoService.ObtenerColoresUnicos();
        
        // Solo configurar formulario si se está editando
        if (!string.IsNullOrEmpty(editId))
        {
            await ConfigurarFormulario(editId);
        }
        else
        {
            ViewBag.FormTitle = "Editar Vehículo";
            ViewBag.EditVehiculo = null;
        }

        return View(vehiculos);
    }

    [HttpGet]
    public async Task<IActionResult> SearchPartial(
        string searchTerm,
        List<string> tiposVehiculo,
        List<string> marcas,
        List<string> colores,
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

        var vehiculos = await _vehiculoService.ObtenerVehiculos(searchTerm, tiposVehiculo, marcas, colores, pageNumber, pageSize, sortBy, sortOrder, estados);
        var totalVehiculos = await _vehiculoService.ObtenerTotalVehiculos(searchTerm, tiposVehiculo, marcas, colores, estados);
        var totalPages = Math.Max((int)Math.Ceiling(totalVehiculos / (double)pageSize), 1);

        ViewBag.CurrentPage = pageNumber;
        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = GetVisiblePages(pageNumber, totalPages);
        ViewBag.SortBy = sortBy;
        ViewBag.SortOrder = sortOrder;
        ViewBag.SearchTerm = searchTerm;
        ViewBag.TiposVehiculoFiltrados = tiposVehiculo;
        ViewBag.MarcasFiltradas = marcas;
        ViewBag.ColoresFiltrados = colores;
        ViewBag.Estados = estados;

        return PartialView("_VehiculoTable", vehiculos);
    }

    [HttpGet]
    public async Task<IActionResult> TablePartial(
        List<string> tiposVehiculo,
        List<string> marcas,
        List<string> colores,
        List<string> estados,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = "Patente",
        string sortOrder = "asc")
    {
        return await SearchPartial(null, tiposVehiculo, marcas, colores, estados, pageNumber, pageSize, sortBy, sortOrder);
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

        return Json(new
        {
            nombreCompleto = cliente.NombreCompleto,
            documento = $"{cliente.TipoDocumento} {cliente.NumeroDocumento}",
            telefono = cliente.Telefono,
            email = cliente.Email
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetMarcasYColores()
    {
        var marcas = await _vehiculoService.ObtenerMarcasUnicas();
        var colores = await _vehiculoService.ObtenerColoresUnicos();
        
        return Json(new
        {
            marcas = marcas,
            colores = colores
        });
    }

    /// <summary>
    /// Verifica si existe un vehículo con la patente especificada y determina si puede ser reasignado
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> VerificarVehiculoSinDueno(string patente, string marca, string modelo)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(patente) || string.IsNullOrWhiteSpace(marca) || string.IsNullOrWhiteSpace(modelo))
            {
                return Json(new { existe = false });
            }

            var vehiculoExistente = await _vehiculoService.ObtenerVehiculoPorPatente(patente.ToUpper());
            
            // CASO 1: No existe ningún vehículo con esa patente → OK, puede crear
            if (vehiculoExistente == null)
            {
                return Json(new { existe = false });
            }
            
            // CASO 2: Existe vehículo ACTIVO → ERROR, patente duplicada
            if (vehiculoExistente.Estado == "Activo")
            {
                return Json(new 
                { 
                    existe = false, 
                    error = $"Ya existe un vehículo activo con la patente {vehiculoExistente.Patente} ({vehiculoExistente.TipoVehiculo} - {vehiculoExistente.Marca} {vehiculoExistente.Modelo}). No se pueden registrar patentes duplicadas."
                });
            }
            
            // CASO 3: Existe vehículo INACTIVO pero CON DUEÑO → ERROR, no se puede reasignar
            if (!string.IsNullOrEmpty(vehiculoExistente.ClienteId))
            {
                return Json(new 
                { 
                    existe = false, 
                    error = $"Ya existe un vehículo con la patente {vehiculoExistente.Patente} asignado al cliente {vehiculoExistente.ClienteNombreCompleto}. No se pueden registrar patentes duplicadas."
                });
            }
            
            // CASO 4: Existe vehículo INACTIVO, SIN DUEÑO, con misma marca y modelo → REASIGNACIÓN
            if (vehiculoExistente.Marca.Equals(marca, StringComparison.OrdinalIgnoreCase) &&
                vehiculoExistente.Modelo.Equals(modelo, StringComparison.OrdinalIgnoreCase))
            {
                return Json(new
                {
                    existe = true,
                    vehiculo = new
                    {
                        id = vehiculoExistente.Id,
                        patente = vehiculoExistente.Patente,
                        marca = vehiculoExistente.Marca,
                        modelo = vehiculoExistente.Modelo,
                        color = vehiculoExistente.Color,
                        tipoVehiculo = vehiculoExistente.TipoVehiculo
                    }
                });
            }
            
            // CASO 5: Existe vehículo INACTIVO, SIN DUEÑO, pero DIFERENTE marca/modelo → ERROR
            // Esto indica que la misma patente perteneció a un vehículo distinto
            return Json(new 
            { 
                existe = false, 
                error = $"Ya existe un vehículo registrado con la patente {vehiculoExistente.Patente}, pero es un {vehiculoExistente.TipoVehiculo} {vehiculoExistente.Marca} {vehiculoExistente.Modelo}. Está ingresando datos de un {marca} {modelo}. Verifique la patente."
            });
        }
        catch (Exception ex)
        {
            return Json(new { existe = false, error = $"Error al verificar vehículo: {ex.Message}" });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearVehiculoAjax(Vehiculo vehiculo)
    {
        try
        {
            // IMPORTANTE: Remover validaciones de campos generados
            ModelState.Remove("Id");
            ModelState.Remove("ClienteNombreCompleto"); // No viene del form
            
            // NUEVO: ClienteId es obligatorio ahora
            if (string.IsNullOrWhiteSpace(vehiculo.ClienteId))
            {
                ModelState.AddModelError("ClienteId", "El vehículo debe tener un dueño asignado.");
            }
            
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x => x.Value.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                    );
                
                Console.WriteLine($"❌ ModelState inválido:");
                foreach (var error in errors)
                {
                    Console.WriteLine($"  Campo: {error.Key}");
                    foreach (var msg in error.Value)
                    {
                        Console.WriteLine($"    - {msg}");
                    }
                }
                
                return await PrepararRespuestaAjax(false, vehiculo, null);
            }

            var existente = await _vehiculoService.ObtenerVehiculoPorPatente(vehiculo.Patente);
            if (existente != null)
            {
                ModelState.AddModelError("Patente", "Ya existe un vehículo con esta patente.");
                return await PrepararRespuestaAjax(false, vehiculo, null);
            }

            // Obtener nombre completo del cliente
            var cliente = await _clienteService.ObtenerCliente(vehiculo.ClienteId);
            if (cliente == null)
            {
                ModelState.AddModelError("ClienteId", "El cliente especificado no existe.");
                return await PrepararRespuestaAjax(false, vehiculo, null);
            }

            vehiculo.ClienteNombreCompleto = cliente.NombreCompleto;
            vehiculo.Estado = "Activo";
            
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
            // Verificar si el vehículo tiene dueño
            var vehiculo = await _vehiculoService.ObtenerVehiculo(id);
            if (vehiculo == null)
            {
                return Json(new { success = false, message = "Vehículo no encontrado." });
            }

            if (!string.IsNullOrEmpty(vehiculo.ClienteId))
            {
                return Json(new { 
                    success = false, 
                    message = $"No se puede desactivar el vehículo porque está asignado al cliente: {vehiculo.ClienteNombreCompleto}. Para desactivarlo, primero remuévalo del cliente desde la edición del cliente." 
                });
            }

            await _vehiculoService.CambiarEstadoVehiculo(id, "Inactivo");
            await RegistrarEvento("Desactivacion de vehiculo", id, "Vehiculo");
            return Json(new { success = true, message = "Vehículo desactivado correctamente." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Error al desactivar vehículo: {ex.Message}" });
        }
    }

    // NOTA: La reactivación de vehículos solo se permite de dos formas:
    // 1. Al reactivar el cliente (reactivación en cascada)
    // 2. Al reasignar el vehículo a un nuevo cliente
    // Por lo tanto, este endpoint ha sido removido para mantener consistencia

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
        
        // Usar solo caracteres ASCII en headers HTTP
        var mensaje = accionAuditoria.Contains("Creacion") 
            ? "Vehiculo creado correctamente." 
            : "Vehiculo actualizado correctamente.";
        Response.Headers["X-Form-Message"] = mensaje;
        
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

    /// <summary>
    /// GET: /Vehiculo/GetMarcas
    /// Retorna lista de marcas de vehículos desde CarQuery API
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMarcas()
    {
        try
        {
            _logger.LogInformation("🎯 Endpoint GetMarcas llamado");
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var marcas = await _carQueryService.GetMarcasAsync();
            stopwatch.Stop();
            
            _logger.LogInformation($"✅ GetMarcas completado en {stopwatch.ElapsedMilliseconds}ms - Retornando {marcas.Count} marcas");
            
            return Json(marcas);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"💥 ERROR en endpoint GetMarcas: {ex.Message}");
            return StatusCode(500, new { error = "Error al obtener marcas", details = ex.Message });
        }
    }

    /// <summary>
    /// GET: /Vehiculo/GetMarcasPorTipo?tipoVehiculo=Automóvil
    /// Retorna marcas filtradas por tipo de vehículo
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMarcasPorTipo(string tipoVehiculo)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(tipoVehiculo))
            {
                return BadRequest(new { error = "El parámetro tipoVehiculo es requerido" });
            }

            _logger.LogInformation("🎯 Endpoint GetMarcasPorTipo llamado - Tipo: {Tipo}", tipoVehiculo);
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var marcas = await _carQueryService.GetMarcasPorTipoAsync(tipoVehiculo);
            stopwatch.Stop();
            
            _logger.LogInformation("✅ GetMarcasPorTipo completado en {Elapsed}ms - Retornando {Count} marcas para tipo '{Tipo}'", 
                stopwatch.ElapsedMilliseconds, marcas.Count, tipoVehiculo);
            
            return Json(marcas);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 ERROR en endpoint GetMarcasPorTipo: {Message}", ex.Message);
            return StatusCode(500, new { error = "Error al obtener marcas", details = ex.Message });
        }
    }

    /// <summary>
    /// GET: /Vehiculo/GetModelos?marcaId=toyota&year=2020
    /// Retorna modelos de una marca (opcionalmente filtrado por año)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetModelos(string marcaId, int? year = null)
    {
        if (string.IsNullOrWhiteSpace(marcaId))
        {
            return BadRequest(new { error = "marcaId es requerido" });
        }

        try
        {
            var modelos = await _carQueryService.GetModelosAsync(marcaId, year);
            return Json(modelos);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Error al obtener modelos", details = ex.Message });
        }
    }

    /// <summary>
    /// GET: /Vehiculo/GetColores
    /// Retorna lista de colores comunes
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetColores()
    {
        try
        {
            var colores = await _carQueryService.GetColoresComunes();
            return Json(colores);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Error al obtener colores", details = ex.Message });
        }
    }

    /// <summary>
    /// GET: /Vehiculo/GetYears?marcaId=toyota
    /// Retorna rango de años disponibles para una marca
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetYears(string marcaId)
    {
        if (string.IsNullOrWhiteSpace(marcaId))
        {
            return BadRequest(new { error = "marcaId es requerido" });
        }

        try
        {
            var (minYear, maxYear) = await _carQueryService.GetYearsAsync(marcaId);
            return Json(new { minYear, maxYear });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Error al obtener años", details = ex.Message });
        }
    }

    /// <summary>
    /// GET: /Vehiculo/GetVehiculosParaAsociacion
    /// Retorna vehículos activos que pueden ser asociados a otros clientes (tienen clave de asociación).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetVehiculosParaAsociacion()
    {
        try
        {
            var vehiculos = await _vehiculoService.ObtenerVehiculosParaAsociacion();
            
            return Json(vehiculos.Select(v => new
            {
                id = v.Id,
                patente = v.Patente,
                marca = v.Marca,
                modelo = v.Modelo,
                color = v.Color,
                tipoVehiculo = v.TipoVehiculo,
                estado = v.Estado
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener vehículos para asociación");
            return StatusCode(500, new { error = "Error al obtener vehículos", details = ex.Message });
        }
    }

    /// <summary>
    /// POST: /Vehiculo/ValidarClaveAsociacion
    /// Valida que la clave de asociación sea correcta para un vehículo específico.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ValidarClaveAsociacion([FromBody] ValidarClaveRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request?.Patente) || string.IsNullOrWhiteSpace(request?.ClaveAsociacion))
            {
                return Json(new { 
                    valida = false, 
                    error = "La patente y la clave de asociación son obligatorias." 
                });
            }

            var vehiculo = await _vehiculoService.ValidarClaveYObtenerVehiculo(
                request.Patente.ToUpper(), 
                request.ClaveAsociacion
            );

            if (vehiculo == null)
            {
                return Json(new { 
                    valida = false, 
                    error = "La clave de asociación no es válida para este vehículo." 
                });
            }

            return Json(new
            {
                valida = true,
                vehiculo = new
                {
                    id = vehiculo.Id,
                    patente = vehiculo.Patente,
                    marca = vehiculo.Marca,
                    modelo = vehiculo.Modelo,
                    color = vehiculo.Color,
                    tipoVehiculo = vehiculo.TipoVehiculo,
                    clienteNombreCompleto = vehiculo.ClienteNombreCompleto
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al validar clave de asociación");
            return Json(new { valida = false, error = "Error al validar la clave de asociación." });
        }
    }

    /// <summary>
    /// Clase para recibir la solicitud de validación de clave.
    /// </summary>
    public class ValidarClaveRequest
    {
        public string Patente { get; set; } = "";
        public string ClaveAsociacion { get; set; } = "";
    }
}

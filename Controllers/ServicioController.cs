using Firebase.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[Authorize(Roles = "Administrador")]
public class ServicioController : Controller
{
    private readonly ServicioService _servicioService;
    private readonly AuditService _auditService;
    private readonly TipoServicioService _tipoServicioService;
    private readonly TipoVehiculoService _tipoVehiculoService; // Nuevo servicio para tipos de vehículo

    public ServicioController(
        ServicioService servicioService,
        AuditService auditService,
        TipoServicioService tipoServicioService,
        TipoVehiculoService tipoVehiculoService) // Añadir inyección del nuevo servicio
    {
        _servicioService = servicioService;
        _auditService = auditService;
        _tipoServicioService = tipoServicioService;
        _tipoVehiculoService = tipoVehiculoService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
    List<string> estados,
    List<string> tipos,
    List<string> tiposVehiculo, // Añadimos este parámetro
    string firstDocId = null,
    string lastDocId = null,
    int pageNumber = 1,
    int pageSize = 10,
    string editId = null)
    {
        // Si no hay estados seleccionados, usar "Activo" por defecto
        if (estados == null || !estados.Any())
        {
            estados = new List<string> { "Activo" };
        }

        var servicios = await _servicioService.ObtenerServicios(estados, tipos, tiposVehiculo, firstDocId, lastDocId, pageNumber, pageSize);
        var totalPages = await _servicioService.ObtenerTotalPaginas(estados, tipos, tiposVehiculo, pageSize);
        totalPages = Math.Max(totalPages, 1);
        var currentPage = Math.Clamp(pageNumber, 1, totalPages);
        var visiblePages = GetVisiblePages(currentPage, totalPages);

        // Obtener tipos de servicio para el desplegable
        var tiposServicio = await _tipoServicioService.ObtenerTiposServicio() ?? new List<string>();
        // Obtener tipos de vehículo para el desplegable
        var tiposVehiculoList = await _tipoVehiculoService.ObtenerTiposVehiculos() ?? new List<string>();

        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = visiblePages;
        ViewBag.CurrentPage = currentPage;
        ViewBag.Estados = estados;
        ViewBag.Tipos = tipos;
        ViewBag.TiposVehiculo = tiposVehiculo; // Agregar a ViewBag los tipos de vehículo seleccionados
        ViewBag.TodosLosTiposVehiculo = tiposVehiculoList; // Para el filtro de todos los tipos de vehículo
        ViewBag.PageSize = pageSize;
        ViewBag.FirstDocId = servicios.FirstOrDefault()?.Id;
        ViewBag.LastDocId = servicios.LastOrDefault()?.Id;
        ViewBag.TiposServicio = tiposServicio;
        ViewBag.TodosLosTipos = tiposServicio; // Para el filtro

        if (!string.IsNullOrEmpty(editId))
        {
            var servicio = servicios.FirstOrDefault(s => s.Id == editId);
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

        return View(servicios);
    }

    [HttpPost]
    public async Task<IActionResult> CrearServicio(Servicio servicio)
    {
        try
        {
            // Asignar un ID temporal para satisfacer la validación de modelo
            if (string.IsNullOrEmpty(servicio.Id))
            {
                servicio.Id = "temp-" + Guid.NewGuid().ToString();
                // Limpiar el ModelState y volver a validar con el ID asignado
                ModelState.Clear();
                TryValidateModel(servicio);
            }

            // Validaciones personalizadas adicionales
            ValidateServicio(servicio);

            if (!ModelState.IsValid)
            {
                // Registra errores de validación
                foreach (var state in ModelState)
                {
                    foreach (var error in state.Value.Errors)
                    {
                        Console.WriteLine($"Error en {state.Key}: {error.ErrorMessage}");
                    }
                }

                TempData["Error"] = "Por favor, complete todos los campos obligatorios correctamente.";
            }
            else
            {
                // Verificar si ya existe un servicio con el mismo nombre para el mismo tipo de vehículo
                bool existeServicio = await _servicioService.ExisteServicioConNombreTipoVehiculo(servicio.Nombre, servicio.TipoVehiculo);
                if (existeServicio)
                {
                    TempData["Error"] = $"Ya existe un servicio con el nombre '{servicio.Nombre}' para vehículos tipo '{servicio.TipoVehiculo}'.";
                    ModelState.AddModelError("Nombre", $"Ya existe un servicio con este nombre para vehículos tipo '{servicio.TipoVehiculo}'.");
                }
                else
                {
                    servicio.Estado = "Activo"; // Aseguramos que el servicio se crea activo
                    await _servicioService.CrearServicio(servicio);
                    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    var userEmail = User.FindFirstValue(ClaimTypes.Email);
                    await _auditService.LogEvent(userId, userEmail, "Creación de servicio", servicio.Id, "Servicio");
                    TempData["Success"] = "Servicio creado correctamente.";
                    return RedirectToAction("Index");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al crear servicio: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            TempData["Error"] = $"Error al crear servicio: {ex.Message}";
            ModelState.AddModelError("", $"Error al crear servicio: {ex.Message}");
        }

        // Si llegamos aquí, hubo un error - preparar la vista con los datos necesarios
        var tiposServicio = await _tipoServicioService.ObtenerTiposServicio() ?? new List<string>();
        var tiposVehiculo = await _tipoVehiculoService.ObtenerTiposVehiculos() ?? new List<string>();

        ViewBag.TiposServicio = tiposServicio;
        ViewBag.TiposVehiculo = tiposVehiculo;
        ViewBag.EditServicio = servicio; // Mantener los valores introducidos
        ViewBag.FormTitle = "Registrando un Servicio";
        ViewBag.SubmitButtonText = "Registrar";
        ViewBag.ClearButtonText = "Limpiar Campos";
        ViewBag.FormAction = "CrearServicio";

        // Configuración de la paginación
        var servicios = await _servicioService.ObtenerServicios(new List<string> { "Activo" }, null, null, null, null, 1, 10);
        var totalPages = await _servicioService.ObtenerTotalPaginas(new List<string> { "Activo" }, null, null, 10);
        totalPages = Math.Max(totalPages, 1);
        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = GetVisiblePages(1, totalPages);
        ViewBag.CurrentPage = 1;

        return View("Index", servicios);
    }

    [HttpPost]
    public async Task<IActionResult> ActualizarServicio(Servicio servicio)
    {
        try
        {
            // Validaciones personalizadas adicionales
            ValidateServicio(servicio);

            if (!ModelState.IsValid)
            {
                foreach (var state in ModelState)
                {
                    foreach (var error in state.Value.Errors)
                    {
                        Console.WriteLine($"Error en {state.Key}: {error.ErrorMessage}");
                    }
                }

                TempData["Error"] = "Por favor, complete todos los campos obligatorios correctamente.";
            }
            else
            {
                var servicioActual = await _servicioService.ObtenerServicio(servicio.Id);
                if (servicioActual != null)
                {
                    // Verificar si ya existe otro servicio con el mismo nombre para el mismo tipo de vehículo
                    bool existeServicio = await _servicioService.ExisteServicioConNombreTipoVehiculo(servicio.Nombre, servicio.TipoVehiculo, servicio.Id);
                    if (existeServicio)
                    {
                        TempData["Error"] = $"Ya existe un servicio con el nombre '{servicio.Nombre}' para vehículos tipo '{servicio.TipoVehiculo}'.";
                        ModelState.AddModelError("Nombre", $"Ya existe un servicio con este nombre para vehículos tipo '{servicio.TipoVehiculo}'.");
                    }
                    else
                    {
                        servicio.Estado = servicioActual.Estado;
                        await _servicioService.ActualizarServicio(servicio);
                        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                        var userEmail = User.FindFirstValue(ClaimTypes.Email);
                        await _auditService.LogEvent(userId, userEmail, "Actualización de servicio", servicio.Id, "Servicio");
                        TempData["Success"] = "Servicio actualizado correctamente.";
                        return RedirectToAction("Index");
                    }
                }
                else
                {
                    TempData["Error"] = "No se pudo encontrar el servicio a actualizar.";
                    ModelState.AddModelError("", "No se pudo encontrar el servicio a actualizar.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al actualizar servicio: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            TempData["Error"] = $"Error al actualizar servicio: {ex.Message}";
            ModelState.AddModelError("", $"Error al actualizar servicio: {ex.Message}");
        }

        // Si llegamos aquí, hubo un error
        var tiposServicio = await _tipoServicioService.ObtenerTiposServicio() ?? new List<string>();
        var tiposVehiculo = await _tipoVehiculoService.ObtenerTiposVehiculos() ?? new List<string>();

        ViewBag.TiposServicio = tiposServicio;
        ViewBag.TiposVehiculo = tiposVehiculo;
        ViewBag.EditServicio = servicio;
        ViewBag.FormTitle = "Editando un Servicio";
        ViewBag.SubmitButtonText = "Guardar";
        ViewBag.ClearButtonText = "Cancelar";
        ViewBag.FormAction = "ActualizarServicio";

        return View("Index", await _servicioService.ObtenerServicios(new List<string> { "Activo" }, new List<string>(), null, null, null, 1, 10));
    }

    [HttpPost]
    public async Task<IActionResult> DeactivateServicio(string id)
    {
        await _servicioService.CambiarEstadoServicio(id, "Inactivo");
        TempData["StateChangeEvent_UserId"] = id;
        TempData["StateChangeEvent_NewState"] = "Inactivo";
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        await _auditService.LogEvent(userId, userEmail, "Desactivación de servicio", id, "Servicio");
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> ReactivateServicio(string id)
    {
        await _servicioService.CambiarEstadoServicio(id, "Activo");
        TempData["StateChangeEvent_UserId"] = id;
        TempData["StateChangeEvent_NewState"] = "Activo";
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        await _auditService.LogEvent(userId, userEmail, "Reactivación de servicio", id, "Servicio");
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> CrearTipoServicio(string nombreTipo)
    {
        if (!string.IsNullOrWhiteSpace(nombreTipo))
        {
            // Verificar si ya existe un tipo con el mismo nombre (normalizado)
            bool existeTipo = await _tipoServicioService.ExisteTipoServicio(nombreTipo);

            if (existeTipo)
            {
                // Si existe, añadir mensaje de error
                TempData["Error"] = "Ya existe un tipo de servicio con el mismo nombre.";
            }
            else
            {
                // Si no existe, crear el nuevo tipo
                await _tipoServicioService.CrearTipoServicio(nombreTipo);
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userEmail = User.FindFirstValue(ClaimTypes.Email);
                await _auditService.LogEvent(userId, userEmail, "Creación de tipo de servicio", nombreTipo, "TipoServicio");
                TempData["Success"] = "Tipo de servicio creado correctamente.";
            }
        }
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> EliminarTipoServicio(string nombreTipo)
    {
        if (!string.IsNullOrWhiteSpace(nombreTipo))
        {
            // Verificar si hay servicios usando este tipo
            var serviciosConTipo = await _servicioService.ObtenerServiciosPorTipo(nombreTipo);

            if (serviciosConTipo.Any())
            {
                TempData["Error"] = "No se puede eliminar el tipo de servicio porque hay servicios que lo utilizan.";
            }
            else
            {
                // Si no hay servicios que lo usen, eliminar el tipo
                await _tipoServicioService.EliminarTipoServicio(nombreTipo);
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userEmail = User.FindFirstValue(ClaimTypes.Email);
                await _auditService.LogEvent(userId, userEmail, "Eliminación de tipo de servicio", nombreTipo, "TipoServicio");
                TempData["Success"] = "Tipo de servicio eliminado correctamente.";
            }
        }
        return RedirectToAction("Index");
    }

    // Nuevos métodos para gestionar tipos de vehículos
    [HttpPost]
    public async Task<IActionResult> CrearTipoVehiculo(string nombreTipo)
    {
        if (!string.IsNullOrWhiteSpace(nombreTipo))
        {
            bool existeTipo = await _tipoVehiculoService.ExisteTipoVehiculo(nombreTipo);

            if (existeTipo)
            {
                TempData["Error"] = "Ya existe un tipo de vehículo con el mismo nombre.";
            }
            else
            {
                await _tipoVehiculoService.CrearTipoVehiculo(nombreTipo);
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userEmail = User.FindFirstValue(ClaimTypes.Email);
                await _auditService.LogEvent(userId, userEmail, "Creación de tipo de vehículo", nombreTipo, "TipoVehiculo");
                TempData["Success"] = "Tipo de vehículo creado correctamente.";
            }
        }
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> EliminarTipoVehiculo(string nombreTipo)
    {
        if (!string.IsNullOrWhiteSpace(nombreTipo))
        {
            // Verificar si hay servicios usando este tipo de vehículo
            var serviciosConTipo = await _servicioService.ObtenerServiciosPorTipoVehiculo(nombreTipo);

            if (serviciosConTipo.Any())
            {
                TempData["Error"] = "No se puede eliminar el tipo de vehículo porque hay servicios que lo utilizan.";
            }
            else
            {
                await _tipoVehiculoService.EliminarTipoVehiculo(nombreTipo);
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userEmail = User.FindFirstValue(ClaimTypes.Email);
                await _auditService.LogEvent(userId, userEmail, "Eliminación de tipo de vehículo", nombreTipo, "TipoVehiculo");
                TempData["Success"] = "Tipo de vehículo eliminado correctamente.";
            }
        }
        return RedirectToAction("Index");
    }

    private List<int> GetVisiblePages(int currentPage, int totalPages, int range = 2)
    {
        var start = Math.Max(1, currentPage - range);
        var end = Math.Min(totalPages, currentPage + range);
        return Enumerable.Range(start, end - start + 1).ToList();
    }

    // Método para validar las reglas específicas del servicio
    private void ValidateServicio(Servicio servicio)
    {
        // Validación para el nombre (solo letras y espacios)
        if (!string.IsNullOrEmpty(servicio.Nombre) && !System.Text.RegularExpressions.Regex.IsMatch(servicio.Nombre, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$"))
        {
            ModelState.AddModelError("Nombre", "El nombre solo puede contener letras y espacios.");
        }

        // Validación para el precio (no negativo)
        if (servicio.Precio < 0)
        {
            ModelState.AddModelError("Precio", "El precio debe ser igual o mayor a 0.");
        }

        // Validación para el tiempo estimado (mayor a 0)
        if (servicio.TiempoEstimado <= 0)
        {
            ModelState.AddModelError("TiempoEstimado", "El tiempo estimado debe ser mayor a 0.");
        }
    }
}

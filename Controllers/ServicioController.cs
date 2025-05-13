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
    string firstDocId = null,
    string lastDocId = null,
    int pageNumber = 1,
    int pageSize = 10,
    string editId = null)
    {
        var servicios = await _servicioService.ObtenerServicios(estados, tipos, firstDocId, lastDocId, pageNumber, pageSize);
        var totalPages = await _servicioService.ObtenerTotalPaginas(estados, tipos, pageSize);
        totalPages = Math.Max(totalPages, 1);
        var currentPage = Math.Clamp(pageNumber, 1, totalPages);
        var visiblePages = GetVisiblePages(currentPage, totalPages);

        // Obtener tipos de servicio para el desplegable
        var tiposServicio = await _tipoServicioService.ObtenerTiposServicio() ?? new List<string>();
        // Obtener tipos de vehículo para el desplegable
        var tiposVehiculo = await _tipoVehiculoService.ObtenerTiposVehiculos() ?? new List<string>();

        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = visiblePages;
        ViewBag.CurrentPage = currentPage;
        ViewBag.Estados = estados;
        ViewBag.Tipos = tipos;
        ViewBag.PageSize = pageSize;
        ViewBag.FirstDocId = servicios.FirstOrDefault()?.Id;
        ViewBag.LastDocId = servicios.LastOrDefault()?.Id;
        ViewBag.TiposServicio = tiposServicio;
        ViewBag.TiposVehiculo = tiposVehiculo; // Nuevo para el desplegable de tipos de vehículo
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
        if (ModelState.IsValid)
        {
            servicio.Estado = "Activo"; // Aseguramos que el servicio se crea activo
            await _servicioService.CrearServicio(servicio);
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            await _auditService.LogEvent(userId, userEmail, "Creación de servicio", servicio.Id, "Servicio");
            return RedirectToAction("Index");
        }

        // Si hay errores, necesitamos cargar los tipos de servicio para el formulario
        var tiposServicio = await _tipoServicioService.ObtenerTiposServicio() ?? new List<string>();
        var tiposVehiculo = await _tipoVehiculoService.ObtenerTiposVehiculos() ?? new List<string>();

        ViewBag.TiposServicio = tiposServicio;
        ViewBag.TiposVehiculo = tiposVehiculo; // Nuevo para el desplegable

        // También necesitamos configurar otras variables del ViewBag que utiliza la vista
        ViewBag.FormTitle = "Registrando un Servicio";
        ViewBag.SubmitButtonText = "Registrar";
        ViewBag.ClearButtonText = "Limpiar Campos";
        ViewBag.FormAction = "CrearServicio";

        // Configuración de la paginación
        var servicios = await _servicioService.ObtenerServicios(new List<string> { "Activo" }, null, null, null, 1, 10);
        var totalPages = await _servicioService.ObtenerTotalPaginas(new List<string> { "Activo" }, null, 10);
        totalPages = Math.Max(totalPages, 1);
        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = GetVisiblePages(1, totalPages);
        ViewBag.CurrentPage = 1;

        return View("Index", servicios);
    }

    [HttpPost]
    public async Task<IActionResult> ActualizarServicio(Servicio servicio)
    {
        if (ModelState.IsValid)
        {
            // Obtener el estado actual del servicio para mantenerlo
            var servicioActual = await _servicioService.ObtenerServicio(servicio.Id);
            if (servicioActual != null)
            {
                servicio.Estado = servicioActual.Estado;

                await _servicioService.ActualizarServicio(servicio);
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userEmail = User.FindFirstValue(ClaimTypes.Email);
                await _auditService.LogEvent(userId, userEmail, "Actualización de servicio", servicio.Id, "Servicio");
                return RedirectToAction("Index");
            }
            else
            {
                ModelState.AddModelError("", "No se pudo encontrar el servicio a actualizar.");
            }
        }

        // Si hay errores, volver a cargar la página con los mismos datos
        var tiposServicio = await _tipoServicioService.ObtenerTiposServicio() ?? new List<string>();
        var tiposVehiculo = await _tipoVehiculoService.ObtenerTiposVehiculos() ?? new List<string>();

        ViewBag.TiposServicio = tiposServicio;
        ViewBag.TiposVehiculo = tiposVehiculo; // Nuevo para el desplegable

        ViewBag.EditServicio = servicio;
        ViewBag.FormTitle = "Editando un Servicio";
        ViewBag.SubmitButtonText = "Guardar";
        ViewBag.ClearButtonText = "Cancelar";
        ViewBag.FormAction = "ActualizarServicio";

        return View("Index", await _servicioService.ObtenerServicios(new List<string> { "Activo" }, new List<string>(), null, null, 1, 10));
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
}

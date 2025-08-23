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
    private readonly TipoVehiculoService _tipoVehiculoService;

    public ServicioController(
        ServicioService servicioService,
        AuditService auditService,
        TipoServicioService tipoServicioService,
        TipoVehiculoService tipoVehiculoService)
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
        List<string> tiposVehiculo,
        string firstDocId = null,
        string lastDocId = null,
        int pageNumber = 1,
        int pageSize = 10,
        string editId = null)
    {
        // Usar "Activo" por defecto si no se especifica ningun estado
        estados ??= new List<string>();
        if (!estados.Any())
        {
            estados.Add("Activo");
        }

        var servicios = await _servicioService.ObtenerServicios(estados, tipos, tiposVehiculo, firstDocId, lastDocId, pageNumber, pageSize);
        var totalPages = await _servicioService.ObtenerTotalPaginas(estados, tipos, tiposVehiculo, pageSize);
        totalPages = Math.Max(totalPages, 1);
        var currentPage = Math.Clamp(pageNumber, 1, totalPages);
        var visiblePages = GetVisiblePages(currentPage, totalPages);

        // Obtener listas para dropdowns
        var tiposServicio = await _tipoServicioService.ObtenerTiposServicio() ?? new List<string>();
        var tiposVehiculoList = await _tipoVehiculoService.ObtenerTiposVehiculos() ?? new List<string>();

        // Configurar ViewBag
        ConfigurarViewBag(
            estados, tipos, tiposVehiculo,
            tiposServicio, tiposVehiculoList,
            pageSize, currentPage, totalPages, visiblePages,
            servicios.FirstOrDefault()?.Id, servicios.LastOrDefault()?.Id);
        // --- CAMBIO PRINCIPAL ---
        // Si hay editId, cargar el servicio directamente
        if (!string.IsNullOrEmpty(editId))
        {
            var servicio = await _servicioService.ObtenerServicio(editId);
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
            // Asignar un ID temporal para la validacion
            if (string.IsNullOrEmpty(servicio.Id))
            {
                servicio.Id = "temp-" + Guid.NewGuid().ToString();
                ModelState.Clear();
                TryValidateModel(servicio);
            }

            // Validacion personalizada
            ValidateServicio(servicio);

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Por favor, complete todos los campos obligatorios correctamente.";
                return await PrepararVistaConError(servicio);
            }

            // Verificar si ya existe servicio con mismo nombre y tipo
            if (await _servicioService.ExisteServicioConNombreTipoVehiculo(servicio.Nombre, servicio.TipoVehiculo))
            {
                TempData["Error"] = $"Ya existe un servicio con el nombre '{servicio.Nombre}' para vehiculos tipo '{servicio.TipoVehiculo}'.";
                ModelState.AddModelError("Nombre", $"Ya existe un servicio con este nombre para vehiculos tipo '{servicio.TipoVehiculo}'.");
                return await PrepararVistaConError(servicio);
            }

            // Crear el servicio
            servicio.Estado = "Activo";
            await _servicioService.CrearServicio(servicio);

            // Registrar evento de auditoria
            await RegistrarEvento("Creacion de servicio", servicio.Id, "Servicio");

            TempData["Success"] = "Servicio creado correctamente.";
            return RedirectToAction("Index");
        }
        catch (ArgumentException ex)
        {
            TempData["Error"] = ex.Message;
            ModelState.AddModelError("", ex.Message);
            return await PrepararVistaConError(servicio);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error al crear servicio: {ex.Message}";
            ModelState.AddModelError("", $"Error al crear servicio: {ex.Message}");
            return await PrepararVistaConError(servicio);
        }
    }

    [HttpPost]
    public async Task<IActionResult> ActualizarServicio(Servicio servicio)
    {
        try
        {
            // Validacion personalizada
            ValidateServicio(servicio);

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Por favor, complete todos los campos obligatorios correctamente.";
                return await PrepararVistaConError(servicio);
            }

            var servicioActual = await _servicioService.ObtenerServicio(servicio.Id);
            if (servicioActual == null)
            {
                TempData["Error"] = "No se pudo encontrar el servicio a actualizar.";
                ModelState.AddModelError("", "No se pudo encontrar el servicio a actualizar.");
                return await PrepararVistaConError(servicio);
            }

            // Verificar si ya existe otro servicio con el mismo nombre para el mismo tipo de vehiculo
            if (await _servicioService.ExisteServicioConNombreTipoVehiculo(servicio.Nombre, servicio.TipoVehiculo, servicio.Id))
            {
                TempData["Error"] = $"Ya existe un servicio con el nombre '{servicio.Nombre}' para vehiculos tipo '{servicio.TipoVehiculo}'.";
                ModelState.AddModelError("Nombre", $"Ya existe un servicio con este nombre para vehiculos tipo '{servicio.TipoVehiculo}'.");
                return await PrepararVistaConError(servicio);
            }

            // Mantener el estado actual
            servicio.Estado = servicioActual.Estado;

            // Actualizar el servicio
            await _servicioService.ActualizarServicio(servicio);

            // Registrar evento de auditoria
            await RegistrarEvento("Actualizacion de servicio", servicio.Id, "Servicio");

            TempData["Success"] = "Servicio actualizado correctamente.";
            return RedirectToAction("Index");
        }
        catch (ArgumentException ex)
        {
            TempData["Error"] = ex.Message;
            ModelState.AddModelError("", ex.Message);
            return await PrepararVistaConError(servicio);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error al actualizar servicio: {ex.Message}";
            ModelState.AddModelError("", $"Error al actualizar servicio: {ex.Message}");
            return await PrepararVistaConError(servicio);
        }
    }

    [HttpPost]
    public async Task<IActionResult> DeactivateServicio(string id)
    {
        await _servicioService.CambiarEstadoServicio(id, "Inactivo");
        TempData["StateChangeEvent_UserId"] = id;
        TempData["StateChangeEvent_NewState"] = "Inactivo";
        await RegistrarEvento("Desactivacion de servicio", id, "Servicio");
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> ReactivateServicio(string id)
    {
        await _servicioService.CambiarEstadoServicio(id, "Activo");
        TempData["StateChangeEvent_UserId"] = id;
        TempData["StateChangeEvent_NewState"] = "Activo";
        await RegistrarEvento("Reactivacion de servicio", id, "Servicio");
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> CrearTipoServicio(string nombreTipo)
    {
        if (!string.IsNullOrWhiteSpace(nombreTipo))
        {
            // Verificar si ya existe un tipo con el mismo nombre
            bool existeTipo = await _tipoServicioService.ExisteTipoServicio(nombreTipo);

            if (existeTipo)
            {
                TempData["Error"] = "Ya existe un tipo de servicio con el mismo nombre.";
            }
            else
            {
                await _tipoServicioService.CrearTipoServicio(nombreTipo);
                await RegistrarEvento("Creacion de tipo de servicio", nombreTipo, "TipoServicio");
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
                await _tipoServicioService.EliminarTipoServicio(nombreTipo);
                await RegistrarEvento("Eliminacion de tipo de servicio", nombreTipo, "TipoServicio");
                TempData["Success"] = "Tipo de servicio eliminado correctamente.";
            }
        }
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> CrearTipoVehiculo(string nombreTipo)
    {
        if (!string.IsNullOrWhiteSpace(nombreTipo))
        {
            bool existeTipo = await _tipoVehiculoService.ExisteTipoVehiculo(nombreTipo);

            if (existeTipo)
            {
                TempData["Error"] = "Ya existe un tipo de vehiculo con el mismo nombre.";
            }
            else
            {
                await _tipoVehiculoService.CrearTipoVehiculo(nombreTipo);
                await RegistrarEvento("Creacion de tipo de vehiculo", nombreTipo, "TipoVehiculo");
                TempData["Success"] = "Tipo de vehiculo creado correctamente.";
            }
        }
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> EliminarTipoVehiculo(string nombreTipo)
    {
        if (!string.IsNullOrWhiteSpace(nombreTipo))
        {
            // Verificar si hay servicios usando este tipo de vehiculo
            var serviciosConTipo = await _servicioService.ObtenerServiciosPorTipoVehiculo(nombreTipo);

            if (serviciosConTipo.Any())
            {
                TempData["Error"] = "No se puede eliminar el tipo de vehiculo porque hay servicios que lo utilizan.";
            }
            else
            {
                await _tipoVehiculoService.EliminarTipoVehiculo(nombreTipo);
                await RegistrarEvento("Eliminacion de tipo de vehiculo", nombreTipo, "TipoVehiculo");
                TempData["Success"] = "Tipo de vehiculo eliminado correctamente.";
            }
        }
        return RedirectToAction("Index");
    }

    #region Metodos privados

    private List<int> GetVisiblePages(int currentPage, int totalPages, int range = 2)
    {
        var start = Math.Max(1, currentPage - range);
        var end = Math.Min(totalPages, currentPage + range);
        return Enumerable.Range(start, end - start + 1).ToList();
    }

    private void ValidateServicio(Servicio servicio)
    {
        if (!string.IsNullOrEmpty(servicio.Nombre) && !System.Text.RegularExpressions.Regex.IsMatch(servicio.Nombre, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$"))
        {
            ModelState.AddModelError("Nombre", "El nombre solo puede contener letras y espacios.");
        }

        if (servicio.Precio < 0)
        {
            ModelState.AddModelError("Precio", "El precio debe ser igual o mayor a 0.");
        }

        if (servicio.TiempoEstimado <= 0)
        {
            ModelState.AddModelError("TiempoEstimado", "El tiempo estimado debe ser mayor a 0.");
        }
    }

    private async Task<IActionResult> PrepararVistaConError(Servicio servicio)
    {
        var tiposServicio = await _tipoServicioService.ObtenerTiposServicio() ?? new List<string>();
        var tiposVehiculoList = await _tipoVehiculoService.ObtenerTiposVehiculos() ?? new List<string>();
        ViewBag.TiposServicio = tiposServicio;
        ViewBag.TodosLosTipos = tiposServicio;
        ViewBag.TodosLosTiposVehiculo = tiposVehiculoList;
        ViewBag.EditServicio = servicio;

        bool esCreacion = servicio.Id.StartsWith("temp-");
        ViewBag.FormTitle = esCreacion ? "Registrando un Servicio" : "Editando un Servicio";
        ViewBag.SubmitButtonText = esCreacion ? "Registrar" : "Guardar";
        ViewBag.ClearButtonText = esCreacion ? "Limpiar Campos" : "Cancelar";
        ViewBag.FormAction = esCreacion ? "CrearServicio" : "ActualizarServicio";

        // Configuracion de la paginacion
        var servicios = await _servicioService.ObtenerServicios(
            new List<string> { "Activo" }, null, null, null, null, 1, 10);
        var totalPages = await _servicioService.ObtenerTotalPaginas(
            new List<string> { "Activo" }, null, null, 10);

        totalPages = Math.Max(totalPages, 1);
        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = GetVisiblePages(1, totalPages);
        ViewBag.CurrentPage = 1;

        return View("Index", servicios);
    }

    private void ConfigurarViewBag(
        List<string> estados, List<string> tipos, List<string> tiposVehiculo,
        List<string> tiposServicio, List<string> tiposVehiculoList,
        int pageSize, int currentPage, int totalPages, List<int> visiblePages,
        string firstDocId, string lastDocId)
    {
        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = visiblePages;
        ViewBag.CurrentPage = currentPage;
        ViewBag.Estados = estados;
        ViewBag.Tipos = tipos;
        ViewBag.TiposVehiculo = tiposVehiculo;
        ViewBag.TodosLosTiposVehiculo = tiposVehiculoList;
        ViewBag.PageSize = pageSize;
        ViewBag.FirstDocId = firstDocId;
        ViewBag.LastDocId = lastDocId;
        ViewBag.TiposServicio = tiposServicio;
        ViewBag.TodosLosTipos = tiposServicio;
    }

    private async Task RegistrarEvento(string accion, string targetId, string entidad)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        await _auditService.LogEvent(userId, userEmail, accion, targetId, entidad);
    }

    #endregion

    [HttpGet]
    public async Task<IActionResult> FormPartial(string id)
    {
        await CargarListasForm();
        Servicio servicio = null;
        if (!string.IsNullOrEmpty(id))
            servicio = await _servicioService.ObtenerServicio(id);

        return PartialView("_ServicioForm", servicio);
    }

    [HttpGet]
    public async Task<IActionResult> TablePartial(
        List<string> estados,
        List<string> tipos,
        List<string> tiposVehiculo,
        int pageNumber = 1,
        int pageSize = 10)
    {
        estados ??= new List<string>();
        if (!estados.Any()) estados.Add("Activo");

        var servicios = await _servicioService.ObtenerServicios(estados, tipos, tiposVehiculo, null, null, pageNumber, pageSize);
        var totalPages = await _servicioService.ObtenerTotalPaginas(estados, tipos, tiposVehiculo, pageSize);
        totalPages = Math.Max(totalPages, 1);
        ViewBag.CurrentPage = pageNumber;
        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = GetVisiblePages(pageNumber, totalPages);
        ViewBag.Estados = estados;
        ViewBag.Tipos = tipos;
        ViewBag.TiposVehiculo = tiposVehiculo;
        return PartialView("_ServicioTable", servicios);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearServicioAjax(Servicio servicio)
    {
        try
        {
            // Asignar un ID temporal para la validación si no lo tiene
            if (string.IsNullOrEmpty(servicio.Id))
            {
                servicio.Id = "temp-" + Guid.NewGuid().ToString();
                ModelState.Clear();
                TryValidateModel(servicio);
            }

            ValidateServicio(servicio);
            if (!ModelState.IsValid)
            {
                Response.Headers["X-Form-Valid"] = "false";
                await CargarListasForm();
                return PartialView("_ServicioForm", servicio);
            }

            if (await _servicioService.ExisteServicioConNombreTipoVehiculo(servicio.Nombre, servicio.TipoVehiculo))
            {
                ModelState.AddModelError("Nombre", "Ya existe un servicio con ese nombre para ese tipo de vehiculo.");
                Response.Headers["X-Form-Valid"] = "false";
                await CargarListasForm();
                return PartialView("_ServicioForm", servicio);
            }

            servicio.Estado = "Activo";
            await _servicioService.CrearServicio(servicio);
            await RegistrarEvento("Creacion (AJAX) de servicio", servicio.Id, "Servicio");

            Response.Headers["X-Form-Valid"] = "true";
            Response.Headers["X-Form-Message"] = "Servicio creado correctamente.";
            await CargarListasForm();
            return PartialView("_ServicioForm", null); // Limpio
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
            Response.Headers["X-Form-Valid"] = "false";
            await CargarListasForm();
            return PartialView("_ServicioForm", servicio);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActualizarServicioAjax(Servicio servicio)
    {
        try
        {
            ValidateServicio(servicio);
            if (!ModelState.IsValid)
            {
                Response.Headers["X-Form-Valid"] = "false";
                await CargarListasForm();
                return PartialView("_ServicioForm", servicio);
            }

            var actual = await _servicioService.ObtenerServicio(servicio.Id);
            if (actual == null)
            {
                ModelState.AddModelError("", "Servicio no encontrado.");
                Response.Headers["X-Form-Valid"] = "false";
                await CargarListasForm();
                return PartialView("_ServicioForm", servicio);
            }

            if (await _servicioService.ExisteServicioConNombreTipoVehiculo(servicio.Nombre, servicio.TipoVehiculo, servicio.Id))
            {
                ModelState.AddModelError("Nombre", "Ya existe otro servicio con ese nombre para ese tipo de vehiculo.");
                Response.Headers["X-Form-Valid"] = "false";
                await CargarListasForm();
                return PartialView("_ServicioForm", servicio);
            }

            servicio.Estado = actual.Estado;
            await _servicioService.ActualizarServicio(servicio);
            await RegistrarEvento("Actualizacion (AJAX) de servicio", servicio.Id, "Servicio");

            Response.Headers["X-Form-Valid"] = "true";
            Response.Headers["X-Form-Message"] = "Servicio actualizado correctamente.";
            await CargarListasForm();
            return PartialView("_ServicioForm", null);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
            Response.Headers["X-Form-Valid"] = "false";
            await CargarListasForm();
            return PartialView("_ServicioForm", servicio);
        }
    }

    private async Task CargarListasForm()
    {
        ViewBag.TiposServicio = await _tipoServicioService.ObtenerTiposServicio() ?? new List<string>();
        ViewBag.TodosLosTiposVehiculo = await _tipoVehiculoService.ObtenerTiposVehiculos() ?? new List<string>();
    }
}
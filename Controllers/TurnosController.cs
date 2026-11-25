using Firebase.Models;
using Firebase.Security;
using Firebase.Services;
using Microsoft.AspNetCore.Mvc;

namespace FirebaseLoginCustom.Controllers;

public class TurnosController(ITurnoConsultaService consulta) : Controller
{
    [HttpGet]
    [Permiso("Turnos.Consultar")]
    public async Task<IActionResult> Index(DateTime? fechaDesde = null, DateTime? fechaHasta = null, decimal? precioDesde = null, decimal? precioHasta = null, string? sortBy = null, string? sortOrder = null, [FromQuery(Name = "filtroEstado")] List<string>? filtroEstado = null, int pageNumber = 1, int pageSize = 10)
    {
        if (!ModelState.IsValid ||
            (fechaDesde.HasValue && fechaHasta.HasValue && fechaDesde.Value.Date > fechaHasta.Value.Date) ||
            precioDesde < 0 || precioHasta < 0 ||
            (precioDesde.HasValue && precioHasta.HasValue && precioDesde > precioHasta))
            return BadRequest("Los filtros de fecha o precio son inválidos.");

        if (ControllerContext?.HttpContext != null && User.TienePermiso("Turnos.Crear"))
        {
            var identity = HttpContext.RequestServices.GetService<TurnoFormIdentity>();
            if (identity != null)
            {
                ViewBag.FormIdentity = identity.Issue(User);
            }
            if (User.TienePermiso("Clientes.Crear"))
            {
                var tipoDocService = HttpContext.RequestServices.GetService<TipoDocumentoService>();
                if (tipoDocService != null)
                {
                    ViewBag.TiposDocumento = await tipoDocService.ObtenerTiposDocumentoCompletos();
                }
            }
        }
        var items = await consulta.Listar();
        var zona = Firebase.Utils.TimeZoneHelper.ArgentinaZone;
        var estadosValidos = (filtroEstado != null && filtroEstado.Count > 0)
            ? filtroEstado
            : new List<string> { "Pendiente", "EnProceso", "Completado", "Cancelado", "Ausente" };
        var filtrados = items.Where(t =>
        {
            var fecha = TimeZoneInfo.ConvertTimeFromUtc(t.InicioUtc, zona).Date;
            return (!fechaDesde.HasValue || fecha >= fechaDesde.Value.Date) &&
                   (!fechaHasta.HasValue || fecha <= fechaHasta.Value.Date) &&
                   (!precioDesde.HasValue || t.PrecioAcordado >= precioDesde.Value) &&
                   (!precioHasta.HasValue || t.PrecioAcordado <= precioHasta.Value) &&
                   (filtroEstado == null || filtroEstado.Count == 0 || filtroEstado.Contains(t.Estado));
        });
        bool desc = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);
        Func<TurnoConsultaViewModel, object>? clave = sortBy switch
        {
            "Cliente" => t => t.Cliente,
            "Vehiculo" => t => string.Join(", ", t.Vehiculos.Select(v => v.Patente)),
            "Fecha" => t => t.InicioUtc,
            "Precio" => t => t.PrecioAcordado,
            "Estado" => t => t.Estado,
            _ => null
        };
        if (clave != null) filtrados = desc ? filtrados.OrderByDescending(clave) : filtrados.OrderBy(clave);
        var listaFiltrada = filtrados.ToList();
        pageSize = pageSize <= 0 ? 10 : pageSize;
        int totalItems = listaFiltrada.Count;
        int totalPages = Math.Max((int)Math.Ceiling(totalItems / (double)pageSize), 1);
        int currentPage = Math.Clamp(pageNumber <= 0 ? 1 : pageNumber, 1, totalPages);
        var paginados = listaFiltrada.Skip((currentPage - 1) * pageSize).Take(pageSize).ToList();

        ViewBag.FechaDesde = fechaDesde;
        ViewBag.FechaHasta = fechaHasta;
        ViewBag.PrecioDesde = precioDesde;
        ViewBag.PrecioHasta = precioHasta;
        ViewBag.FiltroEstado = estadosValidos;
        ViewBag.SortBy = sortBy;
        ViewBag.SortOrder = sortOrder;
        ViewBag.CurrentPage = currentPage;
        ViewBag.TotalPages = totalPages;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalItems = totalItems;
        ViewBag.VisiblePages = GetVisiblePages(currentPage, totalPages);
        return View(paginados);
    }

    [HttpGet]
    [Permiso("Turnos.Consultar")]
    public async Task<IActionResult> TablePartial(
        List<string>? filtroEstado,
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        decimal? precioDesde,
        decimal? precioHasta,
        string? searchTerm,
        int pageNumber = 1,
        int pageSize = 10,
        string? sortBy = null,
        string? sortOrder = null)
    {
        var items = await consulta.Listar();
        var zona = Firebase.Utils.TimeZoneHelper.ArgentinaZone;
        var estadosValidos = (filtroEstado != null && filtroEstado.Count > 0)
            ? filtroEstado
            : new List<string> { "Pendiente", "EnProceso", "Completado", "Cancelado", "Ausente" };

        var query = searchTerm?.Trim().ToLowerInvariant();

        var filtrados = items.Where(t =>
        {
            var fecha = TimeZoneInfo.ConvertTimeFromUtc(t.InicioUtc, zona).Date;
            var matchFecha = (!fechaDesde.HasValue || fecha >= fechaDesde.Value.Date) &&
                             (!fechaHasta.HasValue || fecha <= fechaHasta.Value.Date);
            var matchPrecio = (!precioDesde.HasValue || t.PrecioAcordado >= precioDesde.Value) &&
                              (!precioHasta.HasValue || t.PrecioAcordado <= precioHasta.Value);
            var matchEstado = filtroEstado == null || filtroEstado.Count == 0 || filtroEstado.Contains(t.Estado);

            bool matchSearch = true;
            if (!string.IsNullOrEmpty(query))
            {
                var cliente = t.Cliente?.ToLowerInvariant() ?? "";
                var patentes = string.Join(" ", t.Vehiculos.Select(v => v.Patente)).ToLowerInvariant();
                var estado = t.Estado?.ToLowerInvariant() ?? "";
                var servicios = string.Join(" ", t.Vehiculos.SelectMany(v => v.Servicios.Select(s => s.Nombre))).ToLowerInvariant();
                matchSearch = cliente.Contains(query) || patentes.Contains(query) || estado.Contains(query) || servicios.Contains(query);
            }

            return matchFecha && matchPrecio && matchEstado && matchSearch;
        });

        bool desc = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);
        Func<TurnoConsultaViewModel, object>? clave = sortBy switch
        {
            "Cliente" => t => t.Cliente,
            "Vehiculo" => t => string.Join(", ", t.Vehiculos.Select(v => v.Patente)),
            "Fecha" => t => t.InicioUtc,
            "Precio" => t => t.PrecioAcordado,
            "Estado" => t => t.Estado,
            _ => null
        };
        if (clave != null) filtrados = desc ? filtrados.OrderByDescending(clave) : filtrados.OrderBy(clave);
        else filtrados = filtrados.OrderByDescending(t => t.InicioUtc);

        var listaFiltrada = filtrados.ToList();
        pageSize = pageSize <= 0 ? 10 : pageSize;
        int totalItems = listaFiltrada.Count;
        int totalPages = Math.Max((int)Math.Ceiling(totalItems / (double)pageSize), 1);
        int currentPage = Math.Clamp(pageNumber <= 0 ? 1 : pageNumber, 1, totalPages);
        var paginados = listaFiltrada.Skip((currentPage - 1) * pageSize).Take(pageSize).ToList();

        ViewBag.FechaDesde = fechaDesde;
        ViewBag.FechaHasta = fechaHasta;
        ViewBag.PrecioDesde = precioDesde;
        ViewBag.PrecioHasta = precioHasta;
        ViewBag.FiltroEstado = estadosValidos;
        ViewBag.SortBy = sortBy;
        ViewBag.SortOrder = sortOrder;
        ViewBag.CurrentPage = currentPage;
        ViewBag.TotalPages = totalPages;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalItems = totalItems;
        ViewBag.VisiblePages = GetVisiblePages(currentPage, totalPages);

        return PartialView("_TurnoTable", paginados);
    }

    private static List<int> GetVisiblePages(int currentPage, int totalPages, int range = 2)
    {
        var start = Math.Max(1, currentPage - range);
        var end = Math.Min(totalPages, currentPage + range);
        return Enumerable.Range(start, end - start + 1).ToList();
    }

    [HttpGet]
    [Permiso("Turnos.Consultar")]
    public async Task<IActionResult> Detalle(string id)
    {
        var turno = await consulta.Obtener(id);
        if (turno == null) return NotFound();
        var configService = HttpContext?.RequestServices?.GetService<ConfiguracionService>();
        if (configService != null)
        {
            var config = await configService.ObtenerConfiguracion();
            if (config != null)
            {
                var estaEnVentana = config.EstaEnVentanaInicioTurno(turno.InicioUtc, DateTime.UtcNow, out var motivo);
                ViewBag.EstaEnVentanaInicioTurno = estaEnVentana;
                ViewBag.MotivoVentanaInicioTurno = motivo;
            }
        }
        return View(turno);
    }

    [HttpGet]
    [Permiso("Turnos.Crear")]
    public async Task<IActionResult> Crear()
    {
        if (Request != null && Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            if (User.TienePermiso("Clientes.Crear"))
                ViewBag.TiposDocumento = await HttpContext.RequestServices.GetRequiredService<TipoDocumentoService>().ObtenerTiposDocumentoCompletos();
            var identity = HttpContext.RequestServices.GetRequiredService<TurnoFormIdentity>();
            return PartialView("_TurnoForm", new TurnoWebForm { FormIdentity = identity.Issue(User) });
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ActionName("Crear")]
    [Permiso("Turnos.Crear")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearPost(TurnoWebForm form)
    {
        var identity = HttpContext.RequestServices.GetRequiredService<TurnoFormIdentity>();
        if (!ModelState.IsValid || form == null || form.Validar().Count != 0 ||
            !identity.TryRead(User, form.FormIdentity, out var id))
            return BadRequest(new { success = false, editable = true, message = "Datos de turno inválidos. Corrija los datos e intente nuevamente." });

        var todayAr = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3));
        if (form.Fecha.HasValue && form.Fecha.Value < todayAr)
            return BadRequest(new { success = false, editable = true, message = "La fecha del turno no puede ser anterior a hoy." });

        try
        {
            var adapter = HttpContext.RequestServices.GetRequiredService<TurnoWebBookingAdapter>();
            var request = new TurnoWebBookingRequest(id, id, form.ClienteId, form.VehiculoId,
                form.Fecha!.Value.ToDateTime(form.Hora!.Value), form.AceptaAdelanto,
                form.Servicios.Select(s => new TurnoWebBookingSelection(s.ServicioId, s.PaqueteId)).ToArray(),
                form.ClienteTrajoId, form.ClienteRetiraId, form.Descuento, form.Notas, form.CuponId);
            var bookingResult = await adapter.ReservarConDetalle(request);
            return bookingResult.Success
                ? Ok(new { success = true, message = "Turno registrado correctamente." })
                : Conflict(new { success = false, editable = true, message = bookingResult.ErrorMessage ?? "No hay disponibilidad para el turno." });
        }
        catch (ArgumentException)
        {
            return BadRequest(new { success = false, message = "Datos de turno inválidos." });
        }
        catch (InvalidOperationException)
        {
            return Conflict(new { success = false, message = "No se pudo registrar el turno." });
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { success = false, message = "No se pudo registrar el turno." });
        }
    }

    private static bool ValidEditId(string? id) => !string.IsNullOrWhiteSpace(id) && !id.Contains('/') && id is not "." and not ".." &&
        !(id.StartsWith("__", StringComparison.Ordinal) && id.EndsWith("__", StringComparison.Ordinal)) && System.Text.Encoding.UTF8.GetByteCount(id) <= 1500;

    [HttpGet]
    [Permiso("Turnos.Modificar")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "Permiso:Turnos.Consultar")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> DatosEdicion(string? id)
    {
        if (!ValidEditId(id)) return BadRequest(new { success = false, message = "Datos de turno inválidos." });
        var datos = await consulta.ObtenerDatosEdicion(id!);
        return datos == null ? NotFound() : Ok(datos);
    }

    [HttpGet]
    [Permiso("Turnos.Modificar")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "Permiso:Turnos.Consultar")]
    public IActionResult EditarToken(string? id)
    {
        if (!ValidEditId(id)) return BadRequest(new { success = false, message = "Datos de turno inválidos." });
        var identity = new TurnoEditFormIdentity(HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>());
        try { return Ok(new { identity = identity.Issue(User, id!) }); }
        catch (InvalidOperationException) { return BadRequest(new { success = false, message = "Datos de turno inválidos." }); }
    }

    [HttpPost]
    [Permiso("Turnos.Modificar")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "Permiso:Turnos.Consultar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(TurnoEditWebForm form)
    {
        var provider = HttpContext.RequestServices;
        var identity = new TurnoEditFormIdentity(provider.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>());
        if (!ModelState.IsValid || form == null || !ValidEditId(form.Id) || !identity.TryRead(User, form.Id, form.FormIdentity, out var key))
            return BadRequest(new { success = false, message = "Datos de turno inválidos." });
        try
        {
            var adapter = new TurnoWebEditAdapter(provider.GetRequiredService<Google.Cloud.Firestore.FirestoreDb>(), provider.GetRequiredService<TurnoService>());
            return await adapter.Editar(form, key)
                ? Ok(new { success = true, message = "Turno modificado correctamente." })
                : Conflict(new { success = false, message = "No hay disponibilidad para el turno." });
        }
        catch (ArgumentException) { return BadRequest(new { success = false, message = "Datos de turno inválidos." }); }
        catch (InvalidOperationException) { return Conflict(new { success = false, message = "No se pudo modificar el turno." }); }
        catch (Exception) { return StatusCode(StatusCodes.Status503ServiceUnavailable, new { success = false, message = "No se pudo modificar el turno." }); }
    }

    [HttpGet]
    [Permiso("Turnos.Modificar")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "Permiso:Turnos.Consultar")]
    public async Task<IActionResult> RecursosInicio(string? id)
    {
        if (!User.TienePermiso("Lavados.Crear") || !User.TienePermiso("Lavados.Modificar")) return Forbid();
        if (!new TurnoInicioRequest { TurnoId = id }.EsValido()) return BadRequest(new { success = false, message = "Datos de inicio inválidos." });
        try
        {
            var items = await HttpContext.RequestServices.GetRequiredService<TurnoInicioRecursosService>().Obtener(id!);
            return Ok(new { success = true, items });
        }
        catch (ArgumentException) { return Conflict(new { success = false, message = "No se pudo consultar el turno." }); }
        catch (InvalidOperationException) { return Conflict(new { success = false, message = "No se pudo consultar el turno." }); }
        catch (Exception) { return StatusCode(StatusCodes.Status503ServiceUnavailable, new { success = false, message = "No se pudo consultar el turno." }); }
    }

    [HttpPost]
    [Permiso("Turnos.Modificar")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "Permiso:Turnos.Consultar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Iniciar([FromBody] TurnoInicioRequest? request)
    {
        if (!User.TienePermiso("Lavados.Crear") || !User.TienePermiso("Lavados.Modificar")) return Forbid();
        if (!ModelState.IsValid || request == null || !request.EsValido())
            return BadRequest(new { success = false, message = "Datos de inicio inválidos." });

        var configService = HttpContext?.RequestServices?.GetService<ConfiguracionService>();
        if (configService != null)
        {
            var config = await configService.ObtenerConfiguracion();
            if (config != null)
            {
                var turno = await consulta.Obtener(request.TurnoId!);
                if (turno != null)
                {
                    if (!config.EstaEnVentanaInicioTurno(turno.InicioUtc, DateTime.UtcNow, out var motivoVentana))
                    {
                        return BadRequest(new { success = false, message = motivoVentana });
                    }
                    var duracionTotal = turno.Vehiculos.SelectMany(v => v.Servicios).Sum(s => s.TiempoEstimado);
                    if (config.ExcedeHorarioDeCierre(DateTime.UtcNow, duracionTotal, out var mensaje))
                    {
                        return BadRequest(new { success = false, message = mensaje });
                    }
                }
            }
        }

        try
        {
            var service = HttpContext.RequestServices.GetRequiredService<LavadoService>();
            var id = await service.ConvertirTurnoEnLavado(request.TurnoId!, request.Seleccion);
            return Ok(new { success = true, lavadoId = id });
        }
        catch (ArgumentException)
        {
            return BadRequest(new { success = false, message = "Datos de inicio inválidos." });
        }
        catch (InvalidOperationException)
        {
            return Conflict(new { success = false, message = "No se pudo iniciar el turno." });
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { success = false, message = "No se pudo iniciar el turno." });
        }
    }

    [HttpPost]
    [Permiso("Turnos.Cancelar")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "Permiso:Turnos.Consultar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancelar([FromForm] string? id, [FromForm] string? motivo = null)
    {
        if (!ModelState.IsValid || !ValidEditId(id) || !new TurnoInicioRequest { TurnoId = id }.EsValido())
            return BadRequest(new { success = false, message = "Datos de turno inválidos." });
        if (string.IsNullOrWhiteSpace(motivo))
            return BadRequest(new { success = false, message = "Debe ingresar un motivo de cancelación." });
        TurnoService service;
        try
        {
            service = HttpContext.RequestServices.GetRequiredService<TurnoService>();
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { success = false, message = "No se pudo cancelar el turno." });
        }
        try
        {
            await service.CancelarWeb(id!, motivo);
            return Ok(new { success = true, message = "Turno cancelado correctamente." });
        }
        catch (ArgumentException)
        {
            return BadRequest(new { success = false, message = "Datos de turno inválidos." });
        }
        catch (InvalidOperationException)
        {
            return Conflict(new { success = false, message = "No se pudo cancelar el turno." });
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { success = false, message = "No se pudo cancelar el turno." });
        }
    }

    [HttpGet]
    [Permiso("Turnos.Crear", "Turnos.Consultar", "Turnos.Modificar")]
    public async Task<IActionResult> BuscarVehiculosPorPatente([FromServices] ITurnoSelectorService selector, string? search) =>
        Json(await selector.BuscarVehiculos(search, User.TienePermiso("Turnos.Consultar")));

    [HttpGet]
    [Permiso("Turnos.Crear", "Turnos.Consultar", "Turnos.Modificar")]
    public async Task<IActionResult> ObtenerClientesVehiculo([FromServices] ITurnoSelectorService selector, string? vehiculoId, string? patente) =>
        Json(await selector.ClientesVehiculo(vehiculoId, patente, User.TienePermiso("Turnos.Consultar")));

    [HttpGet]
    [Permiso("Turnos.Crear", "Turnos.Consultar", "Turnos.Modificar")]
    public async Task<IActionResult> ObtenerServiciosPorTipoVehiculo([FromServices] ITurnoSelectorService selector, string? tipoVehiculo) =>
        Json(await selector.Servicios(tipoVehiculo));

    [HttpGet]
    [Permiso("Turnos.Crear", "Turnos.Consultar", "Turnos.Modificar")]
    public async Task<IActionResult> ObtenerPaquetesPorTipoVehiculo([FromServices] ITurnoSelectorService selector, string? tipoVehiculo) =>
        Json(await selector.Paquetes(tipoVehiculo));
}

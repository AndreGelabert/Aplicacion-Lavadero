using System.Security.Claims;
using Firebase.Models;
using Firebase.Security;
using Firebase.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Firebase.Controllers;

[Authorize]
public sealed class InsumosController(
    InsumoService insumos,
    ProveedorService proveedores,
    StockService stock,
    ComprasService compras,
    IAuditWriter audit,
    IEmailSender emailSender,
    ConfiguracionService configuracion,
    TipoInsumoService? tipos = null) : Controller
{
    [HttpGet]
    [Permiso("Insumos.Consultar", "Insumos.Crear")]
    public async Task<IActionResult> Index(string? search, string? tipo, bool soloBajoMinimo = false, int page = 1)
    {
        var lista = await insumos.ObtenerTodos();
        ViewBag.PorcentajeAlertaStock = (await configuracion.ObtenerConfiguracion()).PorcentajeAlertaStock;
        ViewBag.Criticos = lista.Count(x => x.Estado == "Activo" && x.StockActual <= x.StockMinimo);
        ViewBag.AlertasStock = await stock.ObtenerAlertasPendientes();
        ViewBag.Proveedores = await proveedores.ObtenerTodos(false);
        ViewBag.PuedeAdministrar = User.TieneAlgunPermiso("Insumos.Crear", "Insumos.Modificar");
        var todosTipos = tipos == null ? new List<TipoInsumo>() : await tipos.ObtenerTodos();
        if (tipos != null) ViewBag.Tipos = todosTipos.Where(x => x.Estado == "Activo").ToList();
        var preliminar = ProyectarInventario(lista, todosTipos, new Dictionary<string, decimal?>(), search, tipo, soloBajoMinimo, page);
        var reservas = await stock.ObtenerReservasInventario(preliminar.Pagina.Select(x => x.Id));
        var resultado = ProyectarInventario(lista, todosTipos, reservas, search, tipo, soloBajoMinimo, page);
        ViewBag.Page = resultado.Page;
        ViewBag.PageSize = 20;
        ViewBag.TotalPages = resultado.TotalPages;
        ViewBag.Grupos = resultado.Grupos;
        return View(resultado.Pagina);
    }

    public sealed record ProyeccionInventario(List<Insumo> Pagina, int Page, int TotalPages, List<InventarioAgrupadoViewModel> Grupos);

    [NonAction]
    public static ProyeccionInventario ProyectarInventario(List<Insumo> todos, IEnumerable<TipoInsumo> tipos,
        IReadOnlyDictionary<string, decimal?> reservas, string? search, string? tipo, bool soloBajoMinimo, int page)
    {
        var listaTipos = tipos.ToList();
        var identidad = new InventarioAgrupadoViewModel.IdentidadTipos(listaTipos);
        IEnumerable<Insumo> filtrados = todos;
        if (!string.IsNullOrWhiteSpace(search))
            filtrados = filtrados.Where(x => x.Nombre.Contains(search, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(tipo))
        {
            var tipoFiltro = tipo.Trim();
            filtrados = filtrados.Where(x => identidad.CoincideFiltro(x, tipoFiltro));
        }
        if (soloBajoMinimo) filtrados = filtrados.Where(x => x.StockActual <= x.StockMinimo);
        var lista = filtrados.ToList();
        const int pageSize = 20;
        var gruposFiltrados = lista.GroupBy(identidad.Clave, StringComparer.OrdinalIgnoreCase).ToList();
        var totalPages = Math.Max(1, (int)Math.Ceiling(gruposFiltrados.Count / (double)pageSize));
        page = Math.Min(Math.Max(1, page), totalPages);
        var pagina = gruposFiltrados.Skip((page - 1) * pageSize).Take(pageSize).SelectMany(g => g).ToList();
        return new(pagina, page, totalPages, InventarioAgrupadoViewModel.Crear(todos, listaTipos, reservas, pagina));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Permiso("Insumos.Crear", "Insumos.Modificar")]
    public async Task<IActionResult> Guardar(Insumo insumo, bool crear)
    {
        if (!User.TienePermiso(crear ? "Insumos.Crear" : "Insumos.Modificar")) return Forbid();
        insumo.Nombre ??= string.Empty;
        insumo.Tipo ??= string.Empty;
        insumo.UnidadMedida ??= string.Empty;
        if (!crear && string.IsNullOrWhiteSpace(insumo.Id))
        {
            TempData["Error"] = "El insumo no existe.";
            return RedirectToAction(nameof(Index));
        }

        // Si es edición y el formulario no contenía TipoId (p. ej. formularios Razor legacy),
        // preservamos el TipoId persistido del insumo para no alterarlo inadvertidamente.
        if (!crear && !Request.Form.ContainsKey("TipoId"))
        {
            var actual = await insumos.Obtener(insumo.Id);
            if (actual != null)
            {
                insumo.TipoId = actual.TipoId;
            }
        }

        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var actorEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        var auditoria = await insumos.GuardarConAuditoria(insumo, crear, actorId, actorEmail);
        var resultado = auditoria.Resultado;
        TempData[resultado.EsExitoso ? "Success" : "Error"] = resultado.EsExitoso ? resultado.MensajeExito : resultado.MensajeError;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Permiso("Insumos.Modificar")]
    public async Task<IActionResult> CambiarEstado(string id, string estado)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var actorEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        var auditoria = await insumos.CambiarEstadoConAuditoria(id, estado, actorId, actorEmail);
        var resultado = auditoria.Resultado;
        TempData[resultado.EsExitoso ? "Success" : "Error"] = resultado.EsExitoso ? resultado.MensajeExito : resultado.MensajeError;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Permiso("Insumos.AjustarStock")]
    public async Task<IActionResult> Ajustar(string insumoId, decimal cantidad, bool ingreso, string justificacion)
    {
        try
        {
            var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var actorEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
            var resultado = await stock.Ajustar(insumoId, cantidad, ingreso, justificacion, actorId, actorEmail);
            TempData[resultado.EsExitoso ? "Success" : "Error"] = resultado.EsExitoso ? resultado.MensajeExito : resultado.MensajeError;
        }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Permiso("Insumos.Kardex")]
    public async Task<IActionResult> Kardex(string? insumoId, string? tipo, DateTime? desde, DateTime? hasta, int page = 1, string? sortOrder = null, string? cursor = null)
    {
        if (!ModelState.IsValid || (desde.HasValue && hasta.HasValue && desde.Value.Date > hasta.Value.Date))
            return BadRequest("Rango de fechas no válido.");

        if (!string.IsNullOrWhiteSpace(cursor))
        {
            if (!KardexCursor.TratarDeDecodificar(cursor, out _, out var errorToken))
                return BadRequest("Cursor no válido: " + errorToken);
        }

        ViewBag.Insumos = await insumos.ObtenerTodos();
        ViewBag.InsumoId = insumoId;
        ViewBag.Tipo = tipo;
        ViewBag.Desde = desde;
        ViewBag.Hasta = hasta;
        sortOrder = sortOrder == "asc" ? "asc" : "desc";
        const int pageSize = 20;
        var resultado = await stock.ObtenerKardexPaginado(insumoId, tipo, desde, hasta, page, pageSize, sortOrder, cursor);
        ViewBag.Page = resultado.PaginaActual;
        ViewBag.TotalPages = resultado.TotalPaginas;
        ViewBag.SortOrder = sortOrder;
        ViewBag.HasPrevious = resultado.TienePaginaAnterior;
        ViewBag.HasNext = resultado.TienePaginaSiguiente;
        ViewBag.PrevCursor = resultado.CursorAnterior;
        ViewBag.NextCursor = resultado.CursorSiguiente;
        ViewBag.Cursor = cursor;
        return View(resultado.Movimientos);
    }

    [HttpGet]
    [Permiso("Insumos.Crear", "Insumos.Modificar")]
    public async Task<IActionResult> Proveedores()
    {
        ViewBag.PuedeAdministrar = User.TienePermiso("Insumos.Crear") || User.TienePermiso("Insumos.Modificar");
        return View(await proveedores.ObtenerTodos());
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Permiso("Insumos.Crear", "Insumos.Modificar")]
    public async Task<IActionResult> GuardarProveedor(Proveedor proveedor, bool crear)
    {
        if (!User.TienePermiso(crear ? "Insumos.Crear" : "Insumos.Modificar")) return Forbid();
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var actorEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        var auditoria = await proveedores.GuardarConAuditoria(proveedor, crear, actorId, actorEmail);
        var resultado = auditoria.Resultado;
        TempData[resultado.EsExitoso ? "Success" : "Error"] = resultado.EsExitoso ? resultado.MensajeExito : resultado.MensajeError;
        return RedirectToAction(nameof(Proveedores));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Permiso("Insumos.Modificar")]
    public async Task<IActionResult> EstadoProveedor(string id, string estado)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var actorEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        var auditoria = await proveedores.CambiarEstadoConAuditoria(id, estado, actorId, actorEmail);
        var resultado = auditoria.Resultado;
        TempData[resultado.EsExitoso ? "Success" : "Error"] = resultado.EsExitoso ? resultado.MensajeExito : resultado.MensajeError;
        return RedirectToAction(nameof(Proveedores));
    }

    [HttpGet]
    [Permiso("Insumos.Modificar", "Insumos.Recepcionar")]
    public async Task<IActionResult> Ordenes()
    {
        ViewBag.Proveedores = await proveedores.ObtenerTodos(false);
        ViewBag.Insumos = await insumos.ObtenerTodos(false);
        return View(await compras.ObtenerOrdenes());
    }

    [HttpGet]
    [Permiso("Insumos.Modificar")]
    public async Task<IActionResult> Configuracion() => View(await compras.ObtenerDirectiva());

    [HttpPost, ValidateAntiForgeryToken]
    [Permiso("Insumos.Modificar")]
    public async Task<IActionResult> GuardarConfiguracion(DirectivaReabastecimiento directiva)
    {
        var resultado = await compras.GuardarDirectiva(directiva);
        TempData[resultado.EsExitoso ? "Success" : "Error"] = resultado.EsExitoso ? resultado.MensajeExito : resultado.MensajeError;
        return RedirectToAction(nameof(Configuracion));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Permiso("Insumos.Modificar")]
    public async Task<IActionResult> AprobarOrden(string id)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var actorEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        var auditoria = await compras.AprobarOrdenConAuditoria(id, emailSender, actorId, actorEmail);
        var resultado = auditoria.Resultado;
        TempData[resultado.EsExitoso ? "Success" : "Error"] = resultado.EsExitoso ? resultado.MensajeExito : resultado.MensajeError;
        return RedirectToAction(nameof(Ordenes));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Permiso("Insumos.Modificar")]
    public async Task<IActionResult> EditarOrdenPendiente(string id, string insumoId, decimal? cantidad, decimal? precio)
    {
        var entradaValida = ModelState.IsValid && cantidad.HasValue && precio.HasValue;
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var actorEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        var auditoria = !entradaValida
            ? new ComprasService.ResultadoAuditoriaOrden(ResultadoOperacion.CrearError("Ingrese una cantidad y un precio válidos."))
            : await compras.EditarOrdenPendienteConAuditoria(id, insumoId, cantidad!.Value, precio!.Value, actorId, actorEmail);
        var resultado = auditoria.Resultado;
        TempData[resultado.EsExitoso ? "Success" : "Error"] = resultado.EsExitoso ? resultado.MensajeExito : resultado.MensajeError;
        return RedirectToAction(nameof(Ordenes));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Permiso("Insumos.Modificar")]
    public async Task<IActionResult> CambiarEstadoOrden(string id, string estado, string? motivo)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var actorEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        var auditoria = await compras.CambiarEstadoOrdenConAuditoria(id, estado, motivo, actorId, actorEmail);
        var resultado = auditoria.Resultado;
        TempData[resultado.EsExitoso ? "Success" : "Error"] = resultado.EsExitoso ? resultado.MensajeExito : resultado.MensajeError;
        return RedirectToAction(nameof(Ordenes));
    }

    [HttpGet]
    [Permiso("Insumos.Modificar")]
    public async Task<IActionResult> Cotizaciones(string? grupoId)
    {
        var solicitudes = await compras.ObtenerSolicitudes();
        ViewBag.GrupoId = grupoId;
        ViewBag.Proveedores = await proveedores.ObtenerTodos(false);
        ViewBag.Insumos = await insumos.ObtenerTodos(false);
        ViewBag.PuedeCrear = User.TienePermiso("Insumos.Crear");
        ViewBag.Comparacion = string.IsNullOrWhiteSpace(grupoId)
            ? new List<ComparacionCotizacion>()
            : await compras.ObtenerComparacion(grupoId);
        return View(solicitudes);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Permiso("Insumos.Modificar")]
    public async Task<IActionResult> AprobarSolicitud(string id)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var actorEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        var resultado = await compras.AprobarSolicitud(id, actorId, actorEmail);
        TempData[resultado.EsExitoso ? "Success" : "Error"] = resultado.EsExitoso ? resultado.MensajeExito : resultado.MensajeError;
        return RedirectToAction(nameof(Cotizaciones));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Permiso("Insumos.Modificar")]
    public async Task<IActionResult> RegistrarRespuesta(CotizacionRespuesta respuesta)
    {
        var resultado = await compras.RegistrarRespuesta(respuesta, User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty, User.FindFirstValue(ClaimTypes.Email) ?? string.Empty);
        TempData[resultado.EsExitoso ? "Success" : "Error"] = resultado.EsExitoso ? resultado.MensajeExito : resultado.MensajeError;
        return RedirectToAction(nameof(Cotizaciones));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Permiso("Insumos.Modificar")]
    public async Task<IActionResult> EnviarSolicitudEmail(string id)
    {
        var solicitud = (await compras.ObtenerSolicitudes()).FirstOrDefault(x => x.Id == id);
        var proveedor = solicitud == null ? null : await proveedores.Obtener(solicitud.ProveedorId);
        if (solicitud == null || proveedor == null || string.IsNullOrWhiteSpace(proveedor.EmailOrdenes))
        {
            TempData["Error"] = "La solicitud o el email del proveedor no están disponibles.";
            return RedirectToAction(nameof(Cotizaciones));
        }

        var contenido = string.Join("<br>", solicitud.Items.Select(x =>
            $"{System.Net.WebUtility.HtmlEncode(x.NombreInsumo)}: {x.Cantidad}"));
        var resultado = await emailSender.EnviarAsync(new EmailMessage(
            proveedor.EmailOrdenes,
            "Solicitud de cotización de insumos",
            $"<p>Solicitamos cotización para los siguientes insumos:</p><p>{contenido}</p>"));
        TempData[resultado.Enviado ? "Success" : "Error"] = resultado.Mensaje;
        return RedirectToAction(nameof(Cotizaciones));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Permiso("Insumos.Crear")]
    public async Task<IActionResult> CrearSolicitudes(List<string> proveedorIds, List<OrdenCompraItem> items, string? notas)
    {
        var resultado = await compras.CrearSolicitudes(proveedorIds, items, notas, User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty, User.FindFirstValue(ClaimTypes.Email) ?? string.Empty);
        if (!resultado.EsExitoso)
        {
            TempData["Error"] = resultado.MensajeError;
            return RedirectToAction(nameof(Cotizaciones));
        }
        var fallidos = new List<string>();
        var contenido = string.Join("<br>", items.Where(x => x.Cantidad > 0).Select(x =>
            $"{System.Net.WebUtility.HtmlEncode(x.NombreInsumo)}: {x.Cantidad}"));
        foreach (var id in proveedorIds.Distinct(StringComparer.Ordinal))
        {
            var proveedor = await proveedores.Obtener(id);
            if (proveedor == null || string.IsNullOrWhiteSpace(proveedor.EmailOrdenes)) { fallidos.Add(id); continue; }
            var envio = await emailSender.EnviarAsync(new EmailMessage(proveedor.EmailOrdenes,
                "Solicitud de cotización de insumos", $"<p>Solicitamos cotización para los siguientes insumos:</p><p>{contenido}</p>"));
            if (!envio.Enviado) fallidos.Add(proveedor.RazonSocial);
        }
        if (fallidos.Count > 0) TempData["Error"] = "Solicitudes creadas, pero no se pudo enviar el correo a: " + string.Join(", ", fallidos);
        else TempData["Success"] = resultado.MensajeExito;
        return RedirectToAction(nameof(Cotizaciones));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Permiso("Insumos.Crear")]
    public async Task<IActionResult> CrearSolicitud(SolicitudCotizacion solicitud)
    {
        var resultado = await compras.CrearSolicitud(solicitud);
        TempData[resultado.EsExitoso ? "Success" : "Error"] = resultado.EsExitoso ? resultado.MensajeExito : resultado.MensajeError;
        return RedirectToAction(nameof(Cotizaciones));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Permiso("Insumos.Modificar")]
    public async Task<IActionResult> Adjudicar(string id, string? respuestaId)
    {
        try
        {
            var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var actorEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
            var resultado = await compras.Adjudicar(id, respuestaId, emailSender, actorId, actorEmail);
            TempData[resultado.EsExitoso ? "Success" : "Error"] = resultado.EsExitoso ? resultado.MensajeExito : resultado.MensajeError;
        }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Ordenes));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Permiso("Insumos.Crear")]
    public async Task<IActionResult> CrearOrdenManual(OrdenCompra orden)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Datos de la orden inválidos.";
            return RedirectToAction(nameof(Ordenes));
        }
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var actorEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        var auditoria = await compras.CrearOrdenManualConAuditoria(orden, emailSender, actorId, actorEmail);
        var resultado = auditoria.Resultado;
        if (!resultado.EsExitoso)
        {
            TempData["Error"] = resultado.MensajeError;
            return RedirectToAction(nameof(Ordenes));
        }
        TempData["Success"] = resultado.MensajeExito;
        return RedirectToAction(nameof(Ordenes));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Permiso("Insumos.Recepcionar")]
    public async Task<IActionResult> RecibirOrden(string id, List<string>? insumoIds, List<decimal>? cantidades, bool cancelarConcurrentes = false)
    {
        try
        {
            if (!ModelState.IsValid || insumoIds == null || cantidades == null || insumoIds.Count == 0 || insumoIds.Count != cantidades.Count || insumoIds.Any(string.IsNullOrWhiteSpace) || insumoIds.Distinct(StringComparer.Ordinal).Count() != insumoIds.Count)
                throw new InvalidOperationException("Las cantidades de recepción deben ser válidas y mayores a cero.");
            var recepcion = insumoIds.Zip(cantidades).ToDictionary(x => x.First, x => x.Second, StringComparer.Ordinal);
            var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var actorEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
            var auditoria = await compras.RecibirConAuditoria(id, recepcion, cancelarConcurrentes, actorId, actorEmail);
            var resultado = auditoria.Resultado;
            TempData[resultado.EsExitoso ? "Success" : "Error"] = resultado.EsExitoso ? resultado.MensajeExito : resultado.MensajeError;
        }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Ordenes));
    }
}

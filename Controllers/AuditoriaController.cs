using Firebase.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Controlador para la visualización de registros de auditoría.
/// Solo accesible para administradores.
/// </summary>
[Authorize(Roles = "Administrador")]
public class AuditoriaController : Controller
{
    #region Dependencias
    private readonly AuditService _auditService;
    private readonly PersonalService _personalService;
    private readonly ServicioService _servicioService;
    private readonly TipoServicioService _tipoServicioService;
    private readonly TipoVehiculoService _tipoVehiculoService;
    private readonly PaqueteServicioService _paqueteServicioService; // NUEVO

    public AuditoriaController(
        AuditService auditService,
        PersonalService personalService,
        ServicioService servicioService,
        TipoServicioService tipoServicioService,
        TipoVehiculoService tipoVehiculoService,
        PaqueteServicioService paqueteServicioService) // NUEVO
    {
        _auditService = auditService;
        _personalService = personalService;
        _servicioService = servicioService;
        _tipoServicioService = tipoServicioService;
        _tipoVehiculoService = tipoVehiculoService;
        _paqueteServicioService = paqueteServicioService; // NUEVO
    }
    #endregion

    #region Vistas Principales

    /// <summary>
    /// Página principal de auditoría con filtros y paginación
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null,
        List<string> acciones = null,
        List<string> tiposObjetivo = null,
        int pageNumber = 1,
        int pageSize = 20,
        string sortBy = null,
        string sortOrder = null)
    {
        // Configurar ordenamiento por defecto
        sortBy ??= "Timestamp";
        sortOrder ??= "desc";

        // Caso especial: ordenar por Objeto (TargetName) requiere resolver nombres primero y luego paginar
        if (string.Equals(sortBy, "TargetName", StringComparison.OrdinalIgnoreCase))
        {
            var todos = await _auditService.ObtenerRegistros(
                fechaInicio, fechaFin, acciones, tiposObjetivo,
                pageNumber: 1, pageSize: int.MaxValue, sortBy: "Timestamp", sortOrder: "desc");

            var conNombres = await MapearNombresUsuarios(todos);

            var totalRegistros = conNombres.Count;
            var totalPages = Math.Max((int)Math.Ceiling(totalRegistros / (double)pageSize), 1);
            var currentPage = Math.Clamp(pageNumber, 1, totalPages);
            var visiblePages = GetVisiblePages(currentPage, totalPages);

            var ordenados = OrdenarRegistrosConNombre(conNombres, sortBy, sortOrder);
            var pagina = ordenados.Skip((currentPage - 1) * pageSize).Take(pageSize).ToList();

            var (accionesUnicas, tiposObjetivoUnicos) = await CargarListasFiltros();

            ConfigurarViewBag(fechaInicio, fechaFin, acciones, tiposObjetivo,
                accionesUnicas, tiposObjetivoUnicos,
                pageSize, currentPage, totalPages, visiblePages, sortBy, sortOrder);

            return View(pagina);
        }

        // Flujo estándar (ordenamientos por campos del modelo base)
        var (registros, currentPageStd, totalPagesStd, visiblePagesStd) = await ObtenerDatosAuditoria(
            fechaInicio, fechaFin, acciones, tiposObjetivo, pageNumber, pageSize, sortBy, sortOrder);

        var registrosConNombresStd = await MapearNombresUsuarios(registros);

        var (accionesUnicasStd, tiposObjetivoUnicosStd) = await CargarListasFiltros();

        ConfigurarViewBag(fechaInicio, fechaFin, acciones, tiposObjetivo, accionesUnicasStd, tiposObjetivoUnicosStd,
            pageSize, currentPageStd, totalPagesStd, visiblePagesStd, sortBy, sortOrder);

        return View(registrosConNombresStd);
    }
    #endregion

    #region Vistas Parciales

    /// <summary>
    /// Obtener tabla parcial para AJAX
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> TablePartial(
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null,
        List<string> acciones = null,
        List<string> tiposObjetivo = null,
        int pageNumber = 1,
        int pageSize = 20,
        string sortBy = null,
        string sortOrder = null)
    {
        sortBy ??= "Timestamp";
        sortOrder ??= "desc";

        // Caso especial: ordenar por TargetName requiere resolver nombres y paginar aquí
        if (string.Equals(sortBy, "TargetName", StringComparison.OrdinalIgnoreCase))
        {
            var todos = await _auditService.ObtenerRegistros(
                fechaInicio, fechaFin, acciones, tiposObjetivo,
                pageNumber: 1, pageSize: int.MaxValue, sortBy: "Timestamp", sortOrder: "desc");

            var conNombres = await MapearNombresUsuarios(todos);

            var totalRegistros = conNombres.Count;
            var totalPages = Math.Max((int)Math.Ceiling(totalRegistros / (double)pageSize), 1);
            var currentPage = Math.Clamp(pageNumber, 1, totalPages);
            var visiblePages = GetVisiblePages(currentPage, totalPages);

            var ordenados = OrdenarRegistrosConNombre(conNombres, sortBy, sortOrder);
            var pagina = ordenados.Skip((currentPage - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.CurrentPage = currentPage;
            ViewBag.TotalPages = totalPages;
            ViewBag.VisiblePages = visiblePages;
            ViewBag.FechaInicio = fechaInicio;
            ViewBag.FechaFin = fechaFin;
            ViewBag.Acciones = acciones;
            ViewBag.TiposObjetivo = tiposObjetivo;
            ViewBag.SortBy = sortBy;
            ViewBag.SortOrder = sortOrder;

            return PartialView("_AuditoriaTable", pagina);
        }

        // Flujo estándar (servicio ya pagina y ordena por campos base)
        var registros = await _auditService.ObtenerRegistros(
            fechaInicio, fechaFin, acciones, tiposObjetivo, pageNumber, pageSize, sortBy, sortOrder);

        var registrosConNombres = await MapearNombresUsuarios(registros);

        var totalPagesStd = await _auditService.ObtenerTotalPaginas(fechaInicio, fechaFin, acciones, tiposObjetivo, pageSize);
        totalPagesStd = Math.Max(totalPagesStd, 1);

        ViewBag.CurrentPage = pageNumber;
        ViewBag.TotalPages = totalPagesStd;
        ViewBag.VisiblePages = GetVisiblePages(pageNumber, totalPagesStd);
        ViewBag.FechaInicio = fechaInicio;
        ViewBag.FechaFin = fechaFin;
        ViewBag.Acciones = acciones;
        ViewBag.TiposObjetivo = tiposObjetivo;
        ViewBag.SortBy = sortBy;
        ViewBag.SortOrder = sortOrder;

        return PartialView("_AuditoriaTable", registrosConNombres);
    }

    /// <summary>
    /// Búsqueda de registros (mejorada con búsqueda por nombres)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> SearchPartial(
        string searchTerm,
        DateTime? fechaInicio,
        DateTime? fechaFin,
        List<string> acciones,
        List<string> tiposObjetivo,
        int pageNumber = 1,
        int pageSize = 20,
        string sortBy = null,
        string sortOrder = null)
    {
        sortBy ??= "Timestamp";
        sortOrder ??= "desc";

        // 1) Coincidencias por texto básico (correo/acción/tipo/id/fecha)
        var porTexto = await _auditService.BuscarRegistros(
            searchTerm, fechaInicio, fechaFin, acciones, tiposObjetivo,
            pageNumber: 1, pageSize: int.MaxValue, sortBy, sortOrder);

        // 2) Coincidencias por NOMBRE del usuario actor (no por correo)
        var empleados = await _personalService.ObtenerEmpleados(
            new List<string> { "Activo", "Inactivo" }, null, null, 1, int.MaxValue);

        var idsPorNombre = new HashSet<string>(
            empleados
                .Where(e => CoincideTexto(e.NombreCompleto, searchTerm))
                .Select(e => e.Id)
        );

        var porFiltros = await _auditService.ObtenerRegistros(
            fechaInicio, fechaFin, acciones, tiposObjetivo,
            pageNumber: 1, pageSize: int.MaxValue, sortBy, sortOrder);

        var porNombreActor = porFiltros
            .Where(r => !string.IsNullOrWhiteSpace(r.UserId) && idsPorNombre.Contains(r.UserId))
            .ToList();

        // 3) Coincidencias por NOMBRE DEL OBJETO (TargetName) mostrado en la vista
        var porFiltrosConNombres = await MapearNombresUsuarios(porFiltros);
        var porNombreObjeto = porFiltrosConNombres
            .Where(r => !string.IsNullOrWhiteSpace(r.TargetName) && CoincideTexto(r.TargetName, searchTerm))
            .Select(r => new AuditLog
            {
                UserId = r.UserId,
                UserEmail = r.UserEmail,
                Action = r.Action,
                TargetId = r.TargetId,
                TargetType = r.TargetType,
                Timestamp = r.Timestamp
            })
            .ToList();

        // 4) Unificar, quitar duplicados
        var unificados = porTexto
            .Concat(porNombreActor)
            .Concat(porNombreObjeto)
            .GroupBy(r => new { r.UserId, r.UserEmail, r.Action, r.TargetId, r.TargetType, r.Timestamp })
            .Select(g => g.First())
            .ToList();

        var totalRegistros = unificados.Count;

        // Orden especial por TargetName (requiere resolver nombres antes de ordenar/paginar)
        if (string.Equals(sortBy, "TargetName", StringComparison.OrdinalIgnoreCase))
        {
            var conNombresTodos = await MapearNombresUsuarios(unificados);

            var totalPagesTN = Math.Max((int)Math.Ceiling(totalRegistros / (double)pageSize), 1);
            var currentPageTN = Math.Clamp(pageNumber, 1, totalPagesTN);
            var visiblePagesTN = GetVisiblePages(currentPageTN, totalPagesTN);

            var ordenadosTN = OrdenarRegistrosConNombre(conNombresTodos, sortBy, sortOrder);
            var paginaTN = ordenadosTN.Skip((currentPageTN - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.CurrentPage = currentPageTN;
            ViewBag.TotalPages = totalPagesTN;
            ViewBag.VisiblePages = visiblePagesTN;
            ViewBag.FechaInicio = fechaInicio?.ToString("yyyy-MM-dd");
            ViewBag.FechaFin = fechaFin?.ToString("yyyy-MM-dd");
            ViewBag.Acciones = acciones ?? new List<string>();
            ViewBag.TiposObjetivo = tiposObjetivo ?? new List<string>();
            ViewBag.SortBy = sortBy;
            ViewBag.SortOrder = sortOrder;
            ViewBag.SearchTerm = searchTerm;

            return PartialView("_AuditoriaTable", paginaTN);
        }

        // Flujo estándar: ordenar/paginar con el modelo base
        var ordenados = OrdenarRegistros(unificados, sortBy, sortOrder);
        var pagina = ordenados
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // 5) Mapear nombres para la vista
        var registrosConNombres = await MapearNombresUsuarios(pagina);

        // 6) Paginación
        var totalPages = Math.Max((int)Math.Ceiling(totalRegistros / (double)pageSize), 1);

        ViewBag.CurrentPage = pageNumber;
        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = GetVisiblePages(pageNumber, totalPages);
        ViewBag.FechaInicio = fechaInicio?.ToString("yyyy-MM-dd");
        ViewBag.FechaFin = fechaFin?.ToString("yyyy-MM-dd");
        ViewBag.Acciones = acciones ?? new List<string>();
        ViewBag.TiposObjetivo = tiposObjetivo ?? new List<string>();
        ViewBag.SortBy = sortBy;
        ViewBag.SortOrder = sortOrder;
        ViewBag.SearchTerm = searchTerm;

        return PartialView("_AuditoriaTable", registrosConNombres);
    }
    #endregion

    #region Métodos Privados

    /// <summary>
    /// Obtiene los datos de auditoría con paginación
    /// </summary>
    private async Task<(List<AuditLog> registros, int currentPage, int totalPages, List<int> visiblePages)>
        ObtenerDatosAuditoria(DateTime? fechaInicio, DateTime? fechaFin, List<string> acciones,
        List<string> tiposObjetivo, int pageNumber, int pageSize, string sortBy, string sortOrder)
    {
        var registros = await _auditService.ObtenerRegistros(
            fechaInicio, fechaFin, acciones, tiposObjetivo, pageNumber, pageSize, sortBy, sortOrder);

        var totalPages = Math.Max(await _auditService.ObtenerTotalPaginas(
            fechaInicio, fechaFin, acciones, tiposObjetivo, pageSize), 1);

        var currentPage = Math.Clamp(pageNumber, 1, totalPages);
        var visiblePages = GetVisiblePages(currentPage, totalPages);

        return (registros, currentPage, totalPages, visiblePages);
    }

    /// <summary>
    /// Mapea UserIds a nombres de usuarios y resuelve TargetName por tipo/ID
    /// </summary>
    private async Task<List<AuditLogConNombre>> MapearNombresUsuarios(List<AuditLog> registros)
    {
        // Empleados: obtener todos una vez
        var empleados = await _personalService.ObtenerEmpleados(new List<string>(), null, null, 1, int.MaxValue);
        var empleadosDict = empleados.ToDictionary(e => e.Id, e => e.NombreCompleto);

        // IDs por tipo para resolver nombres
        var servicioIds = registros.Where(r => r.TargetType == "Servicio" && !string.IsNullOrWhiteSpace(r.TargetId))
                                   .Select(r => r.TargetId).Distinct().ToList();

        var tipoServIds = registros.Where(r => r.TargetType == "TipoServicio" && !string.IsNullOrWhiteSpace(r.TargetId))
                                   .Select(r => r.TargetId).Distinct().ToList();

        var tipoVehIds = registros.Where(r => r.TargetType == "TipoVehiculo" && !string.IsNullOrWhiteSpace(r.TargetId))
                                  .Select(r => r.TargetId).Distinct().ToList();

        var paqueteIds = registros.Where(r => r.TargetType == "PaqueteServicio" && !string.IsNullOrWhiteSpace(r.TargetId))
                                  .Select(r => r.TargetId).Distinct().ToList(); // NUEVO

        // Resolver nombres de Servicio (llamadas individuales; pageSize pequeño)
        var servDict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in servicioIds)
        {
            try
            {
                var s = await _servicioService.ObtenerServicio(id);
                servDict[id] = s?.Nombre;
            }
            catch { servDict[id] = null; }
        }

        // Resolver nombres de TipoServicio
        var tipoServDict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in tipoServIds)
        {
            try { tipoServDict[id] = await _tipoServicioService.ObtenerNombrePorId(id); }
            catch { tipoServDict[id] = null; }
        }

        // Resolver nombres de TipoVehiculo
        var tipoVehDict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in tipoVehIds)
        {
            try { tipoVehDict[id] = await _tipoVehiculoService.ObtenerNombrePorId(id); }
            catch { tipoVehDict[id] = null; }
        }

        // Resolver nombres de PaqueteServicio // NUEVO
        var paqueteDict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in paqueteIds)
        {
            try
            {
                var p = await _paqueteServicioService.ObtenerPaquete(id);
                paqueteDict[id] = p?.Nombre;
            }
            catch { paqueteDict[id] = null; }
        }

        // Construir modelo de vista
        return registros.Select(r =>
        {
            var userName = empleadosDict.TryGetValue(r.UserId ?? string.Empty, out var nombre)
                ? nombre
                : (r.UserEmail ?? "Usuario desconocido");

            string? targetName = null;
            if (!string.IsNullOrWhiteSpace(r.TargetId))
            {
                switch (r.TargetType)
                {
                    case "Servicio":
                        servDict.TryGetValue(r.TargetId, out targetName);
                        break;
                    case "TipoServicio":
                        tipoServDict.TryGetValue(r.TargetId, out targetName);
                        break;
                    case "TipoVehiculo":
                        tipoVehDict.TryGetValue(r.TargetId, out targetName);
                        break;
                    case "PaqueteServicio": // NUEVO
                        paqueteDict.TryGetValue(r.TargetId, out targetName);
                        break;
                    case "Empleado":
                    case "Usuario":
                        targetName = empleadosDict.TryGetValue(r.TargetId, out var empNombre) ? empNombre : null;
                        break;
                }
            }

            return new AuditLogConNombre
            {
                UserId = r.UserId,
                UserEmail = r.UserEmail,
                UserName = userName,
                Action = r.Action,
                TargetId = r.TargetId,
                TargetType = r.TargetType,
                TargetName = targetName,
                Timestamp = r.Timestamp
            };
        }).ToList();
    }

    /// <summary>
    /// Carga las listas para los filtros
    /// </summary>
    private async Task<(List<string> acciones, List<string> tiposObjetivo)> CargarListasFiltros()
    {
        var acciones = await _auditService.ObtenerAccionesUnicas();
        var tiposObjetivo = await _auditService.ObtenerTiposObjetivoUnicos();
        return (acciones, tiposObjetivo);
    }

    /// <summary>
    /// Configura el ViewBag con todos los datos necesarios
    /// </summary>
    private void ConfigurarViewBag(
        DateTime? fechaInicio, DateTime? fechaFin, List<string> acciones, List<string> tiposObjetivo,
        List<string> accionesUnicas, List<string> tiposObjetivoUnicos,
        int pageSize, int currentPage, int totalPages, List<int> visiblePages,
        string sortBy, string sortOrder)
    {
        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = visiblePages;
        ViewBag.CurrentPage = currentPage;
        ViewBag.FechaInicio = fechaInicio;
        ViewBag.FechaFin = fechaFin;
        ViewBag.Acciones = acciones ?? new List<string>();
        ViewBag.TiposObjetivo = tiposObjetivo ?? new List<string>();
        ViewBag.TodasLasAcciones = accionesUnicas;
        ViewBag.TodosLosTiposObjetivo = tiposObjetivoUnicos;
        ViewBag.PageSize = pageSize;
        ViewBag.SortBy = sortBy;
        ViewBag.SortOrder = sortOrder;
    }

    /// <summary>
    /// Obtiene las páginas visibles para la paginación
    /// </summary>
    private List<int> GetVisiblePages(int currentPage, int totalPages, int range = 2)
    {
        var start = Math.Max(1, currentPage - range);
        var end = Math.Min(totalPages, currentPage + range);
        return Enumerable.Range(start, end - start + 1).ToList();
    }

    /// <summary>
    /// Busca si el término está contenido en la fecha local del timestamp.
    /// </summary>
    private bool BuscarEnFechaLocal(DateTime timestamp, string searchTerm)
    {
        var fechaLocal = timestamp.ToLocalTime().ToString("g"); // "g" = fecha y hora corta
        return fechaLocal.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);
    }
    // Helpers: mismo criterio de normalización y coincidencia que el servicio
    private static bool CoincideTexto(string texto, string termino)
    {
        if (string.IsNullOrWhiteSpace(texto) || string.IsNullOrWhiteSpace(termino))
            return false;

        if (texto.Equals(termino, StringComparison.OrdinalIgnoreCase))
            return true;

        var tNorm = NormalizarTexto(texto);
        var qNorm = NormalizarTexto(termino);

        if (tNorm.Equals(qNorm, StringComparison.OrdinalIgnoreCase))
            return true;

        if (tNorm.Contains(qNorm, StringComparison.OrdinalIgnoreCase))
            return true;

        if (termino.Contains(' '))
        {
            var palabrasQ = qNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var palabrasT = tNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            return palabrasQ.All(pq =>
                palabrasT.Any(pt =>
                    pt.Contains(pq, StringComparison.OrdinalIgnoreCase) ||
                    pq.Contains(pt, StringComparison.OrdinalIgnoreCase)));
        }

        return false;
    }

    private static string NormalizarTexto(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        var descomp = texto.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(descomp.Length);
        foreach (var c in descomp)
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }

    private static List<AuditLog> OrdenarRegistros(List<AuditLog> registros, string sortBy, string sortOrder)
    {
        var descending = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);
        switch (sortBy?.ToLowerInvariant())
        {
            case "timestamp":
            case "fecha":
                return descending ? registros.OrderByDescending(r => r.Timestamp).ToList()
                                  : registros.OrderBy(r => r.Timestamp).ToList();
            case "useremail":
            case "usuario":
                return descending ? registros.OrderByDescending(r => r.UserEmail).ToList()
                                  : registros.OrderBy(r => r.UserEmail).ToList();
            case "action":
            case "accion":
                return descending ? registros.OrderByDescending(r => r.Action).ToList()
                                  : registros.OrderBy(r => r.Action).ToList();
            case "targettype":
            case "tipo":
                return descending ? registros.OrderByDescending(r => r.TargetType).ToList()
                                  : registros.OrderBy(r => r.TargetType).ToList();
            case "targetid":
            case "objeto":
                return descending ? registros.OrderByDescending(r => r.TargetId).ToList()
                                  : registros.OrderBy(r => r.TargetId).ToList();
            default:
                return registros.OrderByDescending(r => r.Timestamp).ToList();
        }
    }

    /// <summary>
    /// Ordena una lista de registros ya enriquecidos con TargetName y UserName.
    /// Soporta ordenamiento por TargetName ("Objeto") además de los campos estándar.
    /// </summary>
    private static List<AuditLogConNombre> OrdenarRegistrosConNombre(List<AuditLogConNombre> registros, string sortBy, string sortOrder)
    {
        var descending = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);
        switch (sortBy?.ToLowerInvariant())
        {
            case "targetname":
            case "objeto":
                return descending ? registros.OrderByDescending(r => r.TargetName).ToList()
                                  : registros.OrderBy(r => r.TargetName).ToList();
            case "timestamp":
            case "fecha":
                return descending ? registros.OrderByDescending(r => r.Timestamp).ToList()
                                  : registros.OrderBy(r => r.Timestamp).ToList();
            case "useremail":
            case "usuario":
                return descending ? registros.OrderByDescending(r => r.UserEmail).ToList()
                                  : registros.OrderBy(r => r.UserEmail).ToList();
            case "action":
            case "accion":
                return descending ? registros.OrderByDescending(r => r.Action).ToList()
                                  : registros.OrderBy(r => r.Action).ToList();
            case "targettype":
            case "tipo":
                return descending ? registros.OrderByDescending(r => r.TargetType).ToList()
                                  : registros.OrderBy(r => r.TargetType).ToList();
            default:
                return registros.OrderByDescending(r => r.Timestamp).ToList();
        }
    }
    #endregion
}
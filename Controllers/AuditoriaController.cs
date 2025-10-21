using Firebase.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Controlador para la visualizaci�n de registros de auditor�a.
/// Solo accesible para administradores.
/// </summary>
[Authorize(Roles = "Administrador")]
public class AuditoriaController : Controller
{
    #region Dependencias
    private readonly AuditService _auditService;
    private readonly PersonalService _personalService;

    public AuditoriaController(
        AuditService auditService,
        PersonalService personalService)
    {
        _auditService = auditService;
        _personalService = personalService;
    }
    #endregion

    #region Vistas Principales

    /// <summary>
    /// P�gina principal de auditor�a con filtros y paginaci�n
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

        // Obtener datos de auditor�a
        var (registros, currentPage, totalPages, visiblePages) = await ObtenerDatosAuditoria(
            fechaInicio, fechaFin, acciones, tiposObjetivo, pageNumber, pageSize, sortBy, sortOrder);

        // Mapear usuarios a los registros
        var registrosConNombres = await MapearNombresUsuarios(registros);

        // Cargar listas para filtros
        var (accionesUnicas, tiposObjetivoUnicos) = await CargarListasFiltros();

        // Configurar ViewBag
        ConfigurarViewBag(fechaInicio, fechaFin, acciones, tiposObjetivo, accionesUnicas, tiposObjetivoUnicos,
            pageSize, currentPage, totalPages, visiblePages, sortBy, sortOrder);

        return View(registrosConNombres);
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

        var registros = await _auditService.ObtenerRegistros(
            fechaInicio, fechaFin, acciones, tiposObjetivo, pageNumber, pageSize, sortBy, sortOrder);
        
        var registrosConNombres = await MapearNombresUsuarios(registros);
        
        var totalPages = await _auditService.ObtenerTotalPaginas(fechaInicio, fechaFin, acciones, tiposObjetivo, pageSize);
        totalPages = Math.Max(totalPages, 1);

        ViewBag.CurrentPage = pageNumber;
        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = GetVisiblePages(pageNumber, totalPages);
        ViewBag.FechaInicio = fechaInicio;
        ViewBag.FechaFin = fechaFin;
        ViewBag.Acciones = acciones;
        ViewBag.TiposObjetivo = tiposObjetivo;
        ViewBag.SortBy = sortBy;
        ViewBag.SortOrder = sortOrder;

        return PartialView("_AuditoriaTable", registrosConNombres);
    }

    /// <summary>
    /// B�squeda de registros (mejorada con b�squeda por nombres)
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

        // 1) Buscar por correo/acci�n/tipo/id/fecha (todos los match de texto)
        //    Traemos todos para poder unificar y paginar correctamente
        var porTexto = await _auditService.BuscarRegistros(
            searchTerm, fechaInicio, fechaFin, acciones, tiposObjetivo,
            pageNumber: 1, pageSize: int.MaxValue, sortBy, sortOrder);

        // 2) Resolver coincidencias por NOMBRE de empleado (no por correo)
        var empleados = await _personalService.ObtenerEmpleados(
            new List<string> { "Activo", "Inactivo" }, null, null, 1, int.MaxValue);

        var idsPorNombre = new HashSet<string>(
            empleados
                .Where(e => CoincideTexto(e.NombreCompleto, searchTerm))
                .Select(e => e.Id)
        );

        // 3) Traer todos los registros seg�n filtros (sin t�rmino) para poder cruzar por UserId
        var porFiltros = await _auditService.ObtenerRegistros(
            fechaInicio, fechaFin, acciones, tiposObjetivo,
            pageNumber: 1, pageSize: int.MaxValue, sortBy, sortOrder);

        var porNombre = porFiltros
            .Where(r => !string.IsNullOrWhiteSpace(r.UserId) && idsPorNombre.Contains(r.UserId))
            .ToList();

        // 4) Unificar, quitar duplicados, ordenar y paginar
        var unificados = porTexto
            .Concat(porNombre)
            .GroupBy(r => new { r.UserId, r.UserEmail, r.Action, r.TargetId, r.TargetType, r.Timestamp })
            .Select(g => g.First())
            .ToList();

        var totalRegistros = unificados.Count;

        var ordenados = OrdenarRegistros(unificados, sortBy, sortOrder);
        var pagina = ordenados
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // 5) Mapear nombres para la vista
        var registrosConNombres = await MapearNombresUsuarios(pagina);

        // 6) Paginaci�n
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

    #region M�todos Privados

    /// <summary>
    /// Obtiene los datos de auditor�a con paginaci�n
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
    /// Mapea UserIds a nombres de usuarios
    /// </summary>
    private async Task<List<AuditLogConNombre>> MapearNombresUsuarios(List<AuditLog> registros)
    {
        // Obtener todos los empleados una sola vez
        var empleados = await _personalService.ObtenerEmpleados(new List<string>(), null, null, 1, int.MaxValue);
        var empleadosDict = empleados.ToDictionary(e => e.Id, e => e.NombreCompleto);

        return registros.Select(r => new AuditLogConNombre
        {
            UserId = r.UserId,
            UserEmail = r.UserEmail,
            UserName = empleadosDict.ContainsKey(r.UserId ?? string.Empty) 
                ? empleadosDict[r.UserId] 
                : r.UserEmail ?? "Usuario desconocido",
            Action = r.Action,
            TargetId = r.TargetId,
            TargetType = r.TargetType,
            Timestamp = r.Timestamp
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
    /// Obtiene las p�ginas visibles para la paginaci�n
    /// </summary>
    private List<int> GetVisiblePages(int currentPage, int totalPages, int range = 2)
    {
        var start = Math.Max(1, currentPage - range);
        var end = Math.Min(totalPages, currentPage + range);
        return Enumerable.Range(start, end - start + 1).ToList();
    }
    
    /// <summary>
    /// Busca si el t�rmino est� contenido en la fecha local del timestamp.
    /// </summary>
    private bool BuscarEnFechaLocal(DateTime timestamp, string searchTerm)
    {
        var fechaLocal = timestamp.ToLocalTime().ToString("g"); // "g" = fecha y hora corta
        return fechaLocal.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);
    }
    // Helpers: mismo criterio de normalizaci�n y coincidencia que el servicio
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
            default:
                return registros.OrderByDescending(r => r.Timestamp).ToList();
        }
    }
    #endregion
}

/// <summary>
/// Modelo auxiliar para auditor�a con nombre de usuario
/// </summary>
public class AuditLogConNombre
{
    public string UserId { get; set; }
    public string UserEmail { get; set; }
    public string UserName { get; set; }
    public string Action { get; set; }
    public string TargetId { get; set; }
    public string TargetType { get; set; }
    public DateTime Timestamp { get; set; }
}
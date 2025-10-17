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

        // Obtener datos de auditoría
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
    /// Búsqueda de registros (mejorada con búsqueda por nombres)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> SearchPartial(
        string searchTerm,
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

        var registros = await _auditService.BuscarRegistros(
            searchTerm, fechaInicio, fechaFin, acciones, tiposObjetivo, pageNumber, pageSize, sortBy, sortOrder);
        
        var registrosConNombres = await MapearNombresUsuarios(registros);
        
        // Filtrado adicional por nombre de usuario (después del mapeo)
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var searchTermTrimmed = searchTerm.Trim();
            registrosConNombres = registrosConNombres.Where(r =>
                // Ya viene filtrado del servicio, pero agregamos búsqueda por UserName mapeado
                (r.UserName?.Contains(searchTermTrimmed, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.UserEmail?.Contains(searchTermTrimmed, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.Action?.Contains(searchTermTrimmed, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.TargetType?.Contains(searchTermTrimmed, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.TargetId?.Contains(searchTermTrimmed, StringComparison.OrdinalIgnoreCase) ?? false) ||
                BuscarEnFechaLocal(r.Timestamp, searchTermTrimmed)
            ).ToList();
        }
        
        var totalRegistros = registrosConNombres.Count;
        var totalPages = Math.Max((int)Math.Ceiling(totalRegistros / (double)pageSize), 1);

        ViewBag.CurrentPage = pageNumber;
        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = GetVisiblePages(pageNumber, totalPages);
        ViewBag.FechaInicio = fechaInicio;
        ViewBag.FechaFin = fechaFin;
        ViewBag.Acciones = acciones;
        ViewBag.TiposObjetivo = tiposObjetivo;
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
    #endregion
}

/// <summary>
/// Modelo auxiliar para auditoría con nombre de usuario
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
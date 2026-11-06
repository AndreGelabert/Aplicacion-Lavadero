using Firebase.Models;

namespace Firebase.Services;

/// <summary>Coordinates audit read queries and name resolution independently of HTTP delivery.</summary>
public sealed class AuditQueryService
{
    private readonly AuditService _auditService;
    private readonly PersonalService _personalService;
    private readonly ServicioService _servicioService;
    private readonly TipoServicioService _tipoServicioService;
    private readonly TipoVehiculoService _tipoVehiculoService;
    private readonly PaqueteServicioService _paqueteServicioService;
    private readonly ClienteService _clienteService; // NUEVO
    private readonly VehiculoService _vehiculoService; // NUEVO
    private readonly LavadoService _lavadoService; // NUEVO

    /// <summary>
    /// Crea el servicio de consultas y resolución de nombres de auditoría.
    /// </summary>
    /// <param name="auditService">Servicio de auditoría.</param>
    /// <param name="personalService">Servicio de personal.</param>
    /// <param name="servicioService">Servicio de servicios.</param>
    /// <param name="tipoServicioService">Servicio de tipos de servicio.</param>
    /// <param name="tipoVehiculoService">Servicio de tipos de vehículo.</param>
    /// <param name="paqueteServicioService">Servicio de paquetes de servicio.</param>
    /// <param name="clienteService">Servicio de clientes.</param>
    /// <param name="vehiculoService">Servicio de vehículos.</param>
    /// <param name="lavadoService">Servicio de lavados.</param>
    public AuditQueryService(
        AuditService auditService,
        PersonalService personalService,
        ServicioService servicioService,
        TipoServicioService tipoServicioService,
        TipoVehiculoService tipoVehiculoService,
        PaqueteServicioService paqueteServicioService,
        ClienteService clienteService, // NUEVO
        VehiculoService vehiculoService, // NUEVO
        LavadoService lavadoService) // NUEVO
    {
        _auditService = auditService;
        _personalService = personalService;
        _servicioService = servicioService;
        _tipoServicioService = tipoServicioService;
        _tipoVehiculoService = tipoVehiculoService;
        _paqueteServicioService = paqueteServicioService;
        _clienteService = clienteService; // NUEVO
        _vehiculoService = vehiculoService; // NUEVO
        _lavadoService = lavadoService; // NUEVO
    }
    public async Task<AuditoriaViewModel> ObtenerPantalla(AuditoriaFilter filters)
    {
        // Configurar ordenamiento por defecto
        filters.SortBy ??= "Timestamp";
        filters.SortOrder ??= "desc";

        // Caso especial: ordenar por Objeto (TargetName) requiere resolver nombres primero y luego paginar
        if (string.Equals(filters.SortBy, "TargetName", StringComparison.OrdinalIgnoreCase))
        {
            var model = await ConsultarPorNombre(filters);
            var (accionesUnicas, tiposObjetivoUnicos) = await CargarListasFiltros();

            model.TodasLasAcciones = accionesUnicas;
            model.TodosLosTiposObjetivo = tiposObjetivoUnicos;
            return model;
        }

        // Flujo estándar (ordenamientos por campos del modelo base)
        var (registros, currentPageStd, totalPagesStd, visiblePagesStd) = await ObtenerDatosAuditoria(
            filters.FechaInicio, filters.FechaFin, filters.Acciones, filters.TiposObjetivo, filters.PageNumber, filters.PageSize, filters.SortBy, filters.SortOrder);

        var registrosConNombresStd = await MapearNombresUsuarios(registros);

        var (accionesUnicasStd, tiposObjetivoUnicosStd) = await CargarListasFiltros();

        return CrearModelo(filters, registrosConNombresStd, currentPageStd, totalPagesStd, visiblePagesStd, accionesUnicasStd, tiposObjetivoUnicosStd);
    }

    public async Task<AuditoriaViewModel> ObtenerTabla(AuditoriaFilter filters)
    {
        filters.SortBy ??= "Timestamp";
        filters.SortOrder ??= "desc";

        // Caso especial: ordenar por TargetName requiere resolver nombres y paginar aquí
        if (string.Equals(filters.SortBy, "TargetName", StringComparison.OrdinalIgnoreCase))
        {
            return await ConsultarPorNombre(filters);
        }

        // Flujo estándar (servicio ya pagina y ordena por campos base)
        var registros = await _auditService.ObtenerRegistros(
            filters.FechaInicio, filters.FechaFin, filters.Acciones, filters.TiposObjetivo, filters.PageNumber, filters.PageSize, filters.SortBy, filters.SortOrder);

        var registrosConNombres = await MapearNombresUsuarios(registros);

        var totalPagesStd = await _auditService.ObtenerTotalPaginas(filters.FechaInicio, filters.FechaFin, filters.Acciones, filters.TiposObjetivo, filters.PageSize);
        totalPagesStd = Math.Max(totalPagesStd, 1);

        return CrearModelo(filters, registrosConNombres, filters.PageNumber, totalPagesStd, GetVisiblePages(filters.PageNumber, totalPagesStd));
    }

    /// <summary>
    /// Búsqueda de registros (mejorada con búsqueda por nombres).
    /// </summary>
    /// <param name="filters">Filtros, orden y paginación de la consulta.</param>
    /// <returns>Registros enriquecidos y metadatos de paginación.</returns>
    public async Task<AuditoriaViewModel> Buscar(AuditoriaFilter filters)
    {
        filters.SortBy ??= "Timestamp";
        filters.SortOrder ??= "desc";

        // 1) Coincidencias por texto básico (correo/acción/tipo/id/fecha)
        var porTexto = await _auditService.BuscarRegistros(
            filters.SearchTerm, filters.FechaInicio, filters.FechaFin, filters.Acciones, filters.TiposObjetivo,
            pageNumber: 1, pageSize: int.MaxValue, filters.SortBy, filters.SortOrder);

        // 2) Coincidencias por NOMBRE del usuario actor (no por correo)
        // Usar método sin filtro para incluir empleados inactivos/eliminados
        var empleados = await _personalService.ObtenerEmpleadosSinFiltroEstado();

        var idsPorNombre = new HashSet<string>(
            empleados
                .Where(e => AuditTextMatcher.Matches(e.NombreCompleto, filters.SearchTerm))
                .Select(e => e.Id)
        );

        var porFiltros = await _auditService.ObtenerRegistros(
            filters.FechaInicio, filters.FechaFin, filters.Acciones, filters.TiposObjetivo,
            pageNumber: 1, pageSize: int.MaxValue, filters.SortBy, filters.SortOrder);

        var porNombreActor = porFiltros
            .Where(r => !string.IsNullOrWhiteSpace(r.UserId) && idsPorNombre.Contains(r.UserId))
            .ToList();

        // 3) Coincidencias por NOMBRE DEL OBJETO (TargetName) mostrado en la vista
        var porFiltrosConNombres = await MapearNombresUsuarios(porFiltros);
        var porNombreObjeto = porFiltrosConNombres
            .Where(r => !string.IsNullOrWhiteSpace(r.TargetName) && AuditTextMatcher.Matches(r.TargetName, filters.SearchTerm))
            .Select(r => new AuditLog
            {
                DocId = r.DocId,
                UserId = r.UserId,
                UserEmail = r.UserEmail,
                Action = r.Action,
                TargetId = r.TargetId,
                TargetType = r.TargetType,
                NombreHistorico = r.NombreHistorico,
                Antes = r.Antes,
                Despues = r.Despues,
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
        if (string.Equals(filters.SortBy, "TargetName", StringComparison.OrdinalIgnoreCase))
        {
            return await PaginarPorNombre(filters, unificados);
        }

        // Flujo estándar: ordenar/paginar con el modelo base
        var ordenados = OrdenarRegistros(unificados, filters.SortBy, filters.SortOrder);
        var pagina = ordenados
            .Skip((filters.PageNumber - 1) * filters.PageSize)
            .Take(filters.PageSize)
            .ToList();

        // 5) Mapear nombres para la vista
        var registrosConNombres = await MapearNombresUsuarios(pagina);

        // 6) Paginación
        var totalPages = Math.Max((int)Math.Ceiling(totalRegistros / (double)filters.PageSize), 1);

        return CrearModelo(filters, registrosConNombres, filters.PageNumber, totalPages, GetVisiblePages(filters.PageNumber, totalPages));
    }

    /// <summary>
    /// Obtiene los datos de auditoría con paginación.
    /// </summary>
    /// <param name="fechaInicio">Fecha de inicio del filtro.</param>
    /// <param name="fechaFin">Fecha de fin del filtro.</param>
    /// <param name="acciones">Lista de acciones a filtrar.</param>
    /// <param name="tiposObjetivo">Lista de tipos objetivo a filtrar.</param>
    /// <param name="pageNumber">Número de página actual.</param>
    /// <param name="pageSize">Tamaño de página.</param>
    /// <param name="sortBy">Campo por el cual ordenar.</param>
    /// <param name="sortOrder">Dirección del ordenamiento.</param>
    /// <returns>Tupla con la lista de registros, página actual, total de páginas y páginas visibles.</returns>
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
    /// Mapea UserIds a nombres de usuarios y resuelve TargetName por tipo/ID.
    /// </summary>
    /// <param name="registros">Lista de registros de auditoría.</param>
    /// <returns>Lista de registros con nombres mapeados.</returns>
    private async Task<List<AuditLogConNombre>> MapearNombresUsuarios(List<AuditLog> registros)
    {
        // CRÍTICO: Pasar NULL como estados para obtener TODOS los empleados
        // NULL indica "sin filtro", lista vacía agregaría "Activo" por defecto
        // Esto permite mapear nombres incluso de empleados dados de baja o eliminados
        var empleados = await _personalService.ObtenerEmpleadosSinFiltroEstado();

        var empleadosDict = empleados.ToDictionary(e => e.Id, e => e.NombreCompleto);

        // IDs por tipo para resolver nombres
        var servicioIds = registros.Where(r => r.TargetType == "Servicio" && !string.IsNullOrWhiteSpace(r.TargetId))
            .Select(r => r.TargetId).Distinct().ToList();

        var tipoServIds = registros.Where(r => r.TargetType == "TipoServicio" && !string.IsNullOrWhiteSpace(r.TargetId))
            .Select(r => r.TargetId).Distinct().ToList();

        var tipoVehIds = registros.Where(r => r.TargetType == "TipoVehiculo" && !string.IsNullOrWhiteSpace(r.TargetId))
            .Select(r => r.TargetId).Distinct().ToList();

        var paqueteIds = registros.Where(r => r.TargetType == "PaqueteServicio" && !string.IsNullOrWhiteSpace(r.TargetId))
            .Select(r => r.TargetId).Distinct().ToList();

        // NUEVO: Resolver nombres de Cliente
        var clienteIds = registros.Where(r => r.TargetType == "Cliente" && !string.IsNullOrWhiteSpace(r.TargetId))
            .Select(r => r.TargetId).Distinct().ToList();

        // NUEVO: Resolver nombres de Vehículo
        var vehiculoIds = registros.Where(r => r.TargetType == "Vehiculo" && !string.IsNullOrWhiteSpace(r.TargetId))
            .Select(r => r.TargetId).Distinct().ToList();

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

        // Resolver nombres de PaqueteServicio
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

        // NUEVO: Resolver nombres de Cliente
        var clienteDict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in clienteIds)
        {
            try
            {
                var c = await _clienteService.ObtenerCliente(id);
                clienteDict[id] = c?.NombreCompleto;
            }
            catch { clienteDict[id] = null; }
        }

        // NUEVO: Resolver nombres de Vehículo (mostrar patente - marca modelo)
        var vehiculoDict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in vehiculoIds)
        {
            try
            {
                var v = await _vehiculoService.ObtenerVehiculo(id);
                vehiculoDict[id] = v != null ? $"{v.Patente} - {v.Marca} {v.Modelo}" : null;
            }
            catch { vehiculoDict[id] = null; }
        }

        // NUEVO: Resolver nombres de Lavado
        var lavadoIds = registros.Where(r => r.TargetType == "Lavado" && !string.IsNullOrWhiteSpace(r.TargetId))
            .Select(r => r.TargetId).Distinct().ToList();

        var lavadoDict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in lavadoIds)
        {
            try
            {
                var l = await _lavadoService.ObtenerLavado(id);
                lavadoDict[id] = l != null ? $"Lavado #{l.Id.Substring(0, 8)} - {l.VehiculoPatente ?? "Sin patente"}" : null;
            }
            catch { lavadoDict[id] = null; }
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
                    case "PaqueteServicio":
                        paqueteDict.TryGetValue(r.TargetId, out targetName);
                        break;
                    case "Cliente":
                        clienteDict.TryGetValue(r.TargetId, out targetName);
                        break;
                    case "Vehiculo":
                        vehiculoDict.TryGetValue(r.TargetId, out targetName);
                        break;
                    case "Lavado":
                        lavadoDict.TryGetValue(r.TargetId, out targetName);
                        break;
                    case "Empleado":
                    case "Usuario":
                        targetName = empleadosDict.TryGetValue(r.TargetId, out var empNombre) ? empNombre : null;
                        break;
                }
            }

            return new AuditLogConNombre
            {
                DocId = r.DocId,
                UserId = r.UserId,
                UserEmail = r.UserEmail,
                UserName = userName,
                Action = r.Action,
                TargetId = r.TargetId,
                TargetType = r.TargetType,
                TargetName = string.IsNullOrWhiteSpace(r.NombreHistorico) ? targetName : r.NombreHistorico,
                NombreHistorico = r.NombreHistorico,
                Antes = r.Antes,
                Despues = r.Despues,
                Timestamp = r.Timestamp
            };
        }).ToList();
    }

    /// <summary>
    /// Carga las listas para los filtros.
    /// </summary>
    /// <returns>Tupla con listas de acciones y tipos objetivo.</returns>
    private async Task<(List<string> acciones, List<string> tiposObjetivo)> CargarListasFiltros()
    {
        var acciones = await _auditService.ObtenerAccionesUnicas();
        var tiposObjetivo = await _auditService.ObtenerTiposObjetivoUnicos();
        return (acciones, tiposObjetivo);
    }

    // Resolve display names before pagination; standard field ordering keeps its existing path.
    private async Task<AuditoriaViewModel> ConsultarPorNombre(AuditoriaFilter filters)
    {
        var registros = await _auditService.ObtenerRegistros(
            filters.FechaInicio, filters.FechaFin, filters.Acciones, filters.TiposObjetivo,
            pageNumber: 1, pageSize: int.MaxValue, sortBy: "Timestamp", sortOrder: "desc");
        return await PaginarPorNombre(filters, registros);
    }

    private async Task<AuditoriaViewModel> PaginarPorNombre(AuditoriaFilter filters, List<AuditLog> registros)
    {
        var conNombres = await MapearNombresUsuarios(registros);
        var totalPages = Math.Max((int)Math.Ceiling(registros.Count / (double)filters.PageSize), 1);
        var currentPage = Math.Clamp(filters.PageNumber, 1, totalPages);
        var pagina = OrdenarRegistrosConNombre(conNombres, filters.SortBy, filters.SortOrder)
            .Skip((currentPage - 1) * filters.PageSize).Take(filters.PageSize).ToList();
        return CrearModelo(filters, pagina, currentPage, totalPages, GetVisiblePages(currentPage, totalPages));
    }

    private static AuditoriaViewModel CrearModelo(
        AuditoriaFilter filters, List<AuditLogConNombre> registros,
        int currentPage, int totalPages, List<int> visiblePages,
        List<string>? acciones = null, List<string>? tiposObjetivo = null) => new()
    {
        Filters = filters,
        Registros = registros,
        CurrentPage = currentPage,
        TotalPages = totalPages,
        VisiblePages = visiblePages,
        TodasLasAcciones = acciones ?? new(),
        TodosLosTiposObjetivo = tiposObjetivo ?? new()
    };

    /// <summary>
    /// Obtiene las páginas visibles para la paginación.
    /// </summary>
    /// <param name="currentPage">Página actual.</param>
    /// <param name="totalPages">Total de páginas.</param>
    /// <param name="range">Rango de páginas.</param>
    /// <returns>Lista de páginas visibles.</returns>
    private List<int> GetVisiblePages(int currentPage, int totalPages, int range = 2)
    {
        var start = Math.Max(1, currentPage - range);
        var end = Math.Min(totalPages, currentPage + range);
        return Enumerable.Range(start, end - start + 1).ToList();
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
    /// <param name="registros">Lista de registros con nombres.</param>
    /// <param name="sortBy">Campo de ordenamiento.</param>
    /// <param name="sortOrder">Dirección del ordenamiento.</param>
    /// <returns>Lista ordenada.</returns>
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
}

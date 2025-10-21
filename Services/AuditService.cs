using Firebase.Models;
using Google.Cloud.Firestore;

/// <summary>
/// Servicio para la gestión de registros de auditoría en Firestore.
/// Proporciona operaciones de registro, consulta, filtrado y paginación.
/// </summary>
public class AuditService
{
    #region Constantes
    private const string COLLECTION_NAME = "registros_auditoria";
    private const string ORDEN_DEFECTO = "Timestamp";
    private const string DIRECCION_DEFECTO = "desc";
    #endregion

    #region Dependencias
    private readonly FirestoreDb _firestore;

    public AuditService(FirestoreDb firestore)
    {
        _firestore = firestore ?? throw new ArgumentNullException(nameof(firestore));
    }
    #endregion

    #region Operaciones de Registro

    /// <summary>
    /// Registra un evento de auditoría en la base de datos
    /// </summary>
    public async Task LogEvent(string userId, string userEmail, string action, string targetId, string targetType)
    {
        var auditLog = new AuditLog
        {
            UserId = userId,
            UserEmail = userEmail,
            Action = action,
            TargetId = targetId,
            TargetType = targetType,
            Timestamp = DateTime.UtcNow
        };

        await _firestore.Collection(COLLECTION_NAME).AddAsync(auditLog);
    }
    #endregion

    #region Operaciones de Consulta

    /// <summary>
    /// Obtiene una lista paginada de registros de auditoría con filtros
    /// </summary>
    public async Task<List<AuditLog>> ObtenerRegistros(
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null,
        List<string> acciones = null,
        List<string> tiposObjetivo = null,
        int pageNumber = 1,
        int pageSize = 20,
        string sortBy = null,
        string sortOrder = null)
    {
        // Validar parámetros de paginación
        ValidarParametrosPaginacion(pageNumber, pageSize);

        // Configurar ordenamiento por defecto
        sortBy ??= ORDEN_DEFECTO;
        sortOrder ??= DIRECCION_DEFECTO;

        // Obtener registros aplicando filtros
        var registros = await ObtenerRegistrosFiltrados(fechaInicio, fechaFin, acciones, tiposObjetivo, sortBy, sortOrder);

        // Aplicar paginación
        return registros
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();
    }

    /// <summary>
    /// Busca registros de auditoría por término de búsqueda
    /// Soporta búsqueda por fecha, usuario (con acentos), acción, tipo y ID
    /// </summary>
    public async Task<List<AuditLog>> BuscarRegistros(
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
        // Validar parámetros
        ValidarParametrosPaginacion(pageNumber, pageSize);
        sortBy ??= ORDEN_DEFECTO;
        sortOrder ??= DIRECCION_DEFECTO;

        // Obtener registros filtrados
        var registros = await ObtenerRegistrosFiltrados(fechaInicio, fechaFin, acciones, tiposObjetivo, sortBy, sortOrder);

        // Aplicar búsqueda si hay término
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var searchTermTrimmed = searchTerm.Trim();
            
            // Intentar parsear como fecha
            bool isFechaBusqueda = TryParseFecha(searchTermTrimmed, out DateTime fechaBuscada);

            registros = registros.Where(r =>
                // Búsqueda por fecha (formato dd/MM/yyyy o dd/MM/yyyy HH:mm)
                (isFechaBusqueda && r.Timestamp.Date == fechaBuscada.Date) ||
                BuscarEnFecha(r.Timestamp, searchTermTrimmed) ||
                
                // Búsqueda por email (sin normalizar para mantener exactitud)
                BuscarEnTexto(r.UserEmail, searchTermTrimmed) ||
                
                // Búsqueda por acción
                BuscarEnTexto(r.Action, searchTermTrimmed) ||
                
                // Búsqueda por tipo de objetivo
                BuscarEnTexto(r.TargetType, searchTermTrimmed) ||
                
                // Búsqueda por ID de objetivo
                BuscarEnTexto(r.TargetId, searchTermTrimmed)
            ).ToList();
        }

        // Aplicar paginación
        return registros
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();
    }

    /// <summary>
    /// Obtiene el total de páginas para los registros filtrados
    /// </summary>
    public async Task<int> ObtenerTotalPaginas(
        DateTime? fechaInicio,
        DateTime? fechaFin,
        List<string> acciones,
        List<string> tiposObjetivo,
        int pageSize)
    {
        if (pageSize <= 0)
            throw new ArgumentException("El tamaño de página debe ser mayor a 0", nameof(pageSize));

        var totalRegistros = await ObtenerTotalRegistros(fechaInicio, fechaFin, acciones, tiposObjetivo);
        return (int)Math.Ceiling(totalRegistros / (double)pageSize);
    }

    /// <summary>
    /// Obtiene el total de registros que coinciden con la búsqueda
    /// </summary>
    public async Task<int> ObtenerTotalRegistrosBusqueda(
        string searchTerm,
        DateTime? fechaInicio,
        DateTime? fechaFin,
        List<string> acciones,
        List<string> tiposObjetivo)
    {
        var registros = await ObtenerRegistrosFiltrados(fechaInicio, fechaFin, acciones, tiposObjetivo, ORDEN_DEFECTO, DIRECCION_DEFECTO);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var searchTermTrimmed = searchTerm.Trim();
            bool isFechaBusqueda = TryParseFecha(searchTermTrimmed, out DateTime fechaBuscada);

            registros = registros.Where(r =>
                (isFechaBusqueda && r.Timestamp.Date == fechaBuscada.Date) ||
                BuscarEnFecha(r.Timestamp, searchTermTrimmed) ||
                BuscarEnTexto(r.UserEmail, searchTermTrimmed) ||
                BuscarEnTexto(r.Action, searchTermTrimmed) ||
                BuscarEnTexto(r.TargetType, searchTermTrimmed) ||
                BuscarEnTexto(r.TargetId, searchTermTrimmed)
            ).ToList();
        }

        return registros.Count;
    }

    /// <summary>
    /// Obtiene todas las acciones únicas registradas
    /// </summary>
    public async Task<List<string>> ObtenerAccionesUnicas()
    {
        var snapshot = await _firestore.Collection(COLLECTION_NAME).GetSnapshotAsync();
        return snapshot.Documents
            .Select(doc => MapearDocumentoAAuditLog(doc))
            .Select(log => log.Action)
            .Where(action => !string.IsNullOrWhiteSpace(action))
            .Distinct()
            .OrderBy(a => a)
            .ToList();
    }

    /// <summary>
    /// Obtiene todos los tipos de objetivo únicos registrados
    /// </summary>
    public async Task<List<string>> ObtenerTiposObjetivoUnicos()
    {
        var snapshot = await _firestore.Collection(COLLECTION_NAME).GetSnapshotAsync();
        return snapshot.Documents
            .Select(doc => MapearDocumentoAAuditLog(doc))
            .Select(log => log.TargetType)
            .Where(type => !string.IsNullOrWhiteSpace(type))
            .Distinct()
            .OrderBy(t => t)
            .ToList();
    }
    #endregion

    #region Métodos Privados

    /// <summary>
    /// Obtiene todos los registros aplicando filtros y ordenamiento
    /// </summary>
    private async Task<List<AuditLog>> ObtenerRegistrosFiltrados(
        DateTime? fechaInicio,
        DateTime? fechaFin,
        List<string> acciones,
        List<string> tiposObjetivo,
        string sortBy,
        string sortOrder)
    {
        // Obtener todos los registros (Firestore tiene limitaciones para filtros complejos)
        var snapshot = await _firestore.Collection(COLLECTION_NAME).GetSnapshotAsync();
        var registros = snapshot.Documents
            .Select(MapearDocumentoAAuditLog)
            .ToList();

        // Aplicar filtro de fecha de inicio
        if (fechaInicio.HasValue)
        {
            registros = registros.Where(r => r.Timestamp >= fechaInicio.Value).ToList();
        }

        // Aplicar filtro de fecha fin
        if (fechaFin.HasValue)
        {
            var fechaFinConHora = fechaFin.Value.Date.AddDays(1).AddTicks(-1);
            registros = registros.Where(r => r.Timestamp <= fechaFinConHora).ToList();
        }

        // Aplicar filtro de acciones
        if (acciones?.Any() == true)
        {
            registros = registros.Where(r => acciones.Contains(r.Action)).ToList();
        }

        // Aplicar filtro de tipos de objetivo
        if (tiposObjetivo?.Any() == true)
        {
            registros = registros.Where(r => tiposObjetivo.Contains(r.TargetType)).ToList();
        }

        // Aplicar ordenamiento
        return AplicarOrdenamiento(registros, sortBy, sortOrder);
    }

    /// <summary>
    /// Obtiene el total de registros que cumplen con los filtros
    /// </summary>
    private async Task<int> ObtenerTotalRegistros(
        DateTime? fechaInicio,
        DateTime? fechaFin,
        List<string> acciones,
        List<string> tiposObjetivo)
    {
        var registros = await ObtenerRegistrosFiltrados(fechaInicio, fechaFin, acciones, tiposObjetivo, ORDEN_DEFECTO, DIRECCION_DEFECTO);
        return registros.Count;
    }

    /// <summary>
    /// Aplica ordenamiento a la lista de registros
    /// </summary>
    private static List<AuditLog> AplicarOrdenamiento(List<AuditLog> registros, string sortBy, string sortOrder)
    {
        var descending = sortOrder?.ToLower() == "desc";

        return sortBy?.ToLower() switch
        {
            "timestamp" or "fecha" => descending
                ? registros.OrderByDescending(r => r.Timestamp).ToList()
                : registros.OrderBy(r => r.Timestamp).ToList(),

            "useremail" or "usuario" => descending
                ? registros.OrderByDescending(r => r.UserEmail).ToList()
                : registros.OrderBy(r => r.UserEmail).ToList(),

            "action" or "accion" => descending
                ? registros.OrderByDescending(r => r.Action).ToList()
                : registros.OrderBy(r => r.Action).ToList(),

            "targettype" or "tipo" => descending
                ? registros.OrderByDescending(r => r.TargetType).ToList()
                : registros.OrderBy(r => r.TargetType).ToList(),

            _ => registros.OrderByDescending(r => r.Timestamp).ToList() // Default por fecha descendente
        };
    }

    /// <summary>
    /// Mapea un documento de Firestore a un objeto AuditLog
    /// </summary>
    private static AuditLog MapearDocumentoAAuditLog(DocumentSnapshot documento)
    {
        return new AuditLog
        {
            UserId = documento.ContainsField("UserId") ? documento.GetValue<string>("UserId") : null,
            UserEmail = documento.ContainsField("UserEmail") ? documento.GetValue<string>("UserEmail") : null,
            Action = documento.ContainsField("Action") ? documento.GetValue<string>("Action") : null,
            TargetId = documento.ContainsField("TargetId") ? documento.GetValue<string>("TargetId") : null,
            TargetType = documento.ContainsField("TargetType") ? documento.GetValue<string>("TargetType") : null,
            Timestamp = documento.ContainsField("Timestamp")
                ? documento.GetValue<Timestamp>("Timestamp").ToDateTime()
                : DateTime.MinValue
        };
    }

    /// <summary>
    /// Valida los parámetros de paginación
    /// </summary>
    private static void ValidarParametrosPaginacion(int pageNumber, int pageSize)
    {
        if (pageNumber <= 0)
            throw new ArgumentException("El número de página debe ser mayor a 0", nameof(pageNumber));

        if (pageSize <= 0)
            throw new ArgumentException("El tamaño de página debe ser mayor a 0", nameof(pageSize));
    }
    #endregion

    #region Métodos Auxiliares de Búsqueda

    /// <summary>
    /// Busca en un texto con soporte para búsqueda exacta y normalizada
    /// Soporta coincidencias exactas, parciales y búsqueda de palabras individuales
    /// </summary>
    private static bool BuscarEnTexto(string texto, string termino)
    {
        if (string.IsNullOrWhiteSpace(texto) || string.IsNullOrWhiteSpace(termino))
            return false;

        // 1. Búsqueda exacta (respetando acentos)
        if (texto.Equals(termino, StringComparison.OrdinalIgnoreCase))
            return true;

        // 2. Normalizar ambos textos
        var textoNormalizado = NormalizarTexto(texto);
        var terminoNormalizado = NormalizarTexto(termino);

        // 3. Búsqueda exacta normalizada
        if (textoNormalizado.Equals(terminoNormalizado, StringComparison.OrdinalIgnoreCase))
            return true;

        // 4. Búsqueda parcial (contiene el término completo)
        if (textoNormalizado.Contains(terminoNormalizado, StringComparison.OrdinalIgnoreCase))
            return true;

        // 5. NUEVO: Búsqueda por palabras individuales (para "Andre Gelabert")
        // Si el término tiene espacios, buscar cada palabra individualmente
        if (termino.Contains(' '))
        {
            var palabrasBusqueda = terminoNormalizado.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var palabrasTexto = textoNormalizado.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // Todas las palabras del término deben estar en el texto
            return palabrasBusqueda.All(palabraBusqueda =>
                palabrasTexto.Any(palabraTexto =>
                    palabraTexto.Contains(palabraBusqueda, StringComparison.OrdinalIgnoreCase) ||
                    palabraBusqueda.Contains(palabraTexto, StringComparison.OrdinalIgnoreCase)));
        }

        return false;
    }

    /// <summary>
    /// Normaliza texto removiendo acentos y caracteres especiales
    /// </summary>
    private static string NormalizarTexto(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        // Normalizar a forma de descomposición (separa caracteres base de diacríticos)
        var textoNormalizado = texto.Normalize(System.Text.NormalizationForm.FormD);

        // Filtrar solo caracteres que no sean marcas diacríticas
        var resultado = new System.Text.StringBuilder();
        foreach (var c in textoNormalizado)
        {
            var categoriaUnicode = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (categoriaUnicode != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                resultado.Append(c);
            }
        }

        return resultado.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }

    /// <summary>
    /// Busca en una fecha por diferentes formatos
    /// </summary>
    private static bool BuscarEnFecha(DateTime fecha, string termino)
    {
        if (string.IsNullOrWhiteSpace(termino))
            return false;

        var fechaLocal = fecha.ToLocalTime();
        
        // Formatos de búsqueda soportados
        var formatosFecha = new[]
        {
            fechaLocal.ToString("dd/MM/yyyy HH:mm:ss"),  // Formato completo
            fechaLocal.ToString("dd/MM/yyyy HH:mm"),     // Sin segundos
            fechaLocal.ToString("dd/MM/yyyy"),            // Solo fecha
            fechaLocal.ToString("dd/MM"),                 // Día y mes
            fechaLocal.ToString("MM/yyyy"),               // Mes y año
            fechaLocal.ToString("yyyy"),                  // Solo año
            fechaLocal.ToString("dd"),                    // Solo día
            fechaLocal.ToString("MM"),                    // Solo mes
            fechaLocal.ToString("HH:mm"),                 // Solo hora
            fechaLocal.ToString("HH")                     // Solo hora (sin minutos)
        };

        return formatosFecha.Any(f => f.Contains(termino, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Intenta parsear una fecha de diferentes formatos
    /// </summary>
    private static bool TryParseFecha(string texto, out DateTime fecha)
    {
        fecha = DateTime.MinValue;

        if (string.IsNullOrWhiteSpace(texto))
            return false;

        // Formatos de fecha a intentar
        var formatos = new[]
        {
            "dd/MM/yyyy",
            "dd/MM/yy",
            "dd-MM-yyyy",
            "dd-MM-yy",
            "yyyy-MM-dd",
            "yyyy/MM/dd"
        };

        foreach (var formato in formatos)
        {
            if (DateTime.TryParseExact(texto, formato, 
                System.Globalization.CultureInfo.InvariantCulture, 
                System.Globalization.DateTimeStyles.None, 
                out fecha))
            {
                return true;
            }
        }

        // Intento final con parse genérico
        return DateTime.TryParse(texto, out fecha);
    }

    #endregion
}
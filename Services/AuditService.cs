using Firebase.Models;
using Google.Cloud.Firestore;

/// <summary>
/// Servicio para la gesti�n de registros de auditor�a en Firestore.
/// Proporciona operaciones de registro, consulta, filtrado y paginaci�n.
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
    /// Registra un evento de auditor�a en la base de datos
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
    /// Obtiene una lista paginada de registros de auditor�a con filtros.
    /// - Si sortBy == "Timestamp" (por defecto): usa paginaci�n f�sica (Limit/Offset) en Firestore.
    /// - Para otros sortBy: mantiene l�gica previa (orden/paginaci�n en memoria).
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
        ValidarParametrosPaginacion(pageNumber, pageSize);

        sortBy ??= ORDEN_DEFECTO;
        sortOrder ??= DIRECCION_DEFECTO;

        // Paginaci�n f�sica soportada cuando ordenamos por Timestamp (caso por defecto y m�s com�n)
        var ordenEsTimestamp = string.Equals(sortBy, "Timestamp", StringComparison.OrdinalIgnoreCase);

        if (ordenEsTimestamp)
        {
            // Query en Firestore con filtros y orden por fecha
            var query = ConstruirQueryAuditoria(fechaInicio, fechaFin, acciones, tiposObjetivo, sortBy, sortOrder, forCounting: false);

            // Paginaci�n f�sica (Limit/Offset)
            var offset = (pageNumber - 1) * pageSize;
            if (offset > 0)
                query = query.Offset(offset);

            query = query.Limit(pageSize);

            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents.Select(MapearDocumentoAAuditLog).ToList();
        }

        // Fallback: otros ordenamientos (UserEmail/Action/TargetType...)
        var registros = await ObtenerRegistrosFiltrados(fechaInicio, fechaFin, acciones, tiposObjetivo, sortBy, sortOrder);

        return registros
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();
    }

    /// <summary>
    /// Busca registros por t�rmino (misma l�gica previa). Mantiene paginaci�n en memoria.
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
        ValidarParametrosPaginacion(pageNumber, pageSize);
        sortBy ??= ORDEN_DEFECTO;
        sortOrder ??= DIRECCION_DEFECTO;

        var registros = await ObtenerRegistrosFiltrados(fechaInicio, fechaFin, acciones, tiposObjetivo, sortBy, sortOrder);

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

        return registros
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();
    }

    /// <summary>
    /// Obtiene el total de p�ginas para los registros filtrados.
    /// Intenta usar agregaci�n Count() en Firestore; si no est� disponible, hace fallback.
    /// </summary>
    public async Task<int> ObtenerTotalPaginas(
        DateTime? fechaInicio,
        DateTime? fechaFin,
        List<string> acciones,
        List<string> tiposObjetivo,
        int pageSize)
    {
        if (pageSize <= 0)
            throw new ArgumentException("El tama�o de p�gina debe ser mayor a 0", nameof(pageSize));

        var totalRegistros = await ObtenerTotalRegistrosLigero(fechaInicio, fechaFin, acciones, tiposObjetivo);
        return (int)Math.Ceiling(totalRegistros / (double)pageSize);
    }

    /// <summary>
    /// Total de registros que coinciden con la b�squeda (mantiene enfoque previo).
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
    /// Obtiene todas las acciones �nicas registradas
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
    /// Obtiene todos los tipos de objetivo �nicos registrados
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

    #region M�todos Privados

    /// <summary>
    /// Construye un query de Firestore aplicando filtros y orden.
    /// - Si hay filtros por rango de fecha, el primer OrderBy debe ser por "Timestamp" (requisito de Firestore).
    /// - Para orden por Timestamp, se agrega tie-breaker por DocumentId para orden estable.
    /// - Para WhereIn (acciones, tipos), Firestore limita a 10 elementos; si hay m�s, filtra cliente.
    /// - Si forCounting=true, evita aplicar OrderBy para reducir requisitos de �ndice.
    /// </summary>
    private Query ConstruirQueryAuditoria(
        DateTime? fechaInicio,
        DateTime? fechaFin,
        List<string> acciones,
        List<string> tiposObjetivo,
        string sortBy,
        string sortOrder,
        bool forCounting)
    {
        var query = _firestore.Collection(COLLECTION_NAME) as Query;

        // Filtros por fecha (usar UTC)
        if (fechaInicio.HasValue)
        {
            var fiUtc = ToUtc(fechaInicio.Value);
            query = query.WhereGreaterThanOrEqualTo("Timestamp", fiUtc);
        }

        if (fechaFin.HasValue)
        {
            // Fin de d�a inclusivo
            var ffUtc = ToUtc(EndOfDay(fechaFin.Value));
            query = query.WhereLessThanOrEqualTo("Timestamp", ffUtc);
        }

        // Acciones
        if (acciones?.Any() == true)
        {
            if (acciones.Count == 1)
                query = query.WhereEqualTo("Action", acciones[0]);
            else if (acciones.Count <= 10)
                query = query.WhereIn("Action", acciones);
            // Si > 10, no se aplica filtro aqu�; se filtrar� en memoria cuando corresponda.
        }

        // Tipos objetivo
        if (tiposObjetivo?.Any() == true)
        {
            if (tiposObjetivo.Count == 1)
                query = query.WhereEqualTo("TargetType", tiposObjetivo[0]);
            else if (tiposObjetivo.Count <= 10)
                query = query.WhereIn("TargetType", tiposObjetivo);
            // Si > 10, no se aplica filtro aqu�; se filtrar� en memoria cuando corresponda.
        }

        // Para conteo, evitar orden innecesario
        if (forCounting)
        {
            return query;
        }

        var descending = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);
        var sortLower = sortBy?.ToLowerInvariant() ?? "timestamp";

        // Si hay rango de fecha y sortBy != Timestamp, Firestore exige que el primer OrderBy sea por Timestamp
        var rangoFechaActivo = fechaInicio.HasValue || fechaFin.HasValue;

        if (rangoFechaActivo && sortLower != "timestamp")
        {
            query = descending ? query.OrderByDescending("Timestamp") : query.OrderBy("Timestamp");
            // Segundo orden por el campo solicitado (puede requerir �ndice compuesto)
            query = descending ? query.OrderByDescending(sortBy) : query.OrderBy(sortBy);
        }
        else
        {
            // Orden normal
            query = descending ? query.OrderByDescending(sortBy) : query.OrderBy(sortBy);
        }

        // Tie-breaker por DocumentId para orden estable cuando ordenamos por Timestamp
        if (sortLower == "timestamp")
        {
            query = descending
                ? query.OrderByDescending(FieldPath.DocumentId)
                : query.OrderBy(FieldPath.DocumentId);
        }

        return query;
    }

    /// <summary>
    /// Obtiene registros aplicando filtros y ordenamiento en memoria (fallback).
    /// </summary>
    private async Task<List<AuditLog>> ObtenerRegistrosFiltrados(
        DateTime? fechaInicio,
        DateTime? fechaFin,
        List<string> acciones,
        List<string> tiposObjetivo,
        string sortBy,
        string sortOrder)
    {
        var snapshot = await _firestore.Collection(COLLECTION_NAME).GetSnapshotAsync();
        var registros = snapshot.Documents
            .Select(MapearDocumentoAAuditLog)
            .ToList();

        if (fechaInicio.HasValue)
        {
            registros = registros.Where(r => r.Timestamp >= fechaInicio.Value).ToList();
        }

        if (fechaFin.HasValue)
        {
            var fechaFinConHora = EndOfDay(fechaFin.Value);
            registros = registros.Where(r => r.Timestamp <= fechaFinConHora).ToList();
        }

        if (acciones?.Any() == true)
        {
            registros = registros.Where(r => acciones.Contains(r.Action)).ToList();
        }

        if (tiposObjetivo?.Any() == true)
        {
            registros = registros.Where(r => tiposObjetivo.Contains(r.TargetType)).ToList();
        }

        return AplicarOrdenamiento(registros, sortBy, sortOrder);
    }

    /// <summary>
    /// Intenta contar registros usando agregaci�n Count(); fallback a contar snapshot si no est� disponible.
    /// </summary>
    private async Task<long> ObtenerTotalRegistrosLigero(
        DateTime? fechaInicio,
        DateTime? fechaFin,
        List<string> acciones,
        List<string> tiposObjetivo)
    {
        // Construir query sin orden para minimizar requisitos de �ndice
        var query = ConstruirQueryAuditoria(fechaInicio, fechaFin, acciones, tiposObjetivo, ORDEN_DEFECTO, DIRECCION_DEFECTO, forCounting: true);

        try
        {
            var aggSnapshot = await query.Count().GetSnapshotAsync();
            return aggSnapshot.Count ?? 0;
        }
        catch
        {
            // Fallback: contar documentos del snapshot
            var snapshot = await query.GetSnapshotAsync();
            var total = snapshot.Documents.Count;

            // Si no aplicamos WhereIn por exceder 10, filtrar en memoria para un conteo correcto
            if (acciones?.Any() == true && acciones.Count > 10)
            {
                total = snapshot.Documents
                    .Select(MapearDocumentoAAuditLog)
                    .Count(r => acciones.Contains(r.Action));
            }

            if (tiposObjetivo?.Any() == true && tiposObjetivo.Count > 10)
            {
                total = snapshot.Documents
                    .Select(MapearDocumentoAAuditLog)
                    .Count(r => tiposObjetivo.Contains(r.TargetType));
            }

            return total;
        }
    }

    /// <summary>
    /// Aplica ordenamiento en memoria (fallback)
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

            _ => registros.OrderByDescending(r => r.Timestamp).ToList()
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
    /// Valida los par�metros de paginaci�n
    /// </summary>
    private static void ValidarParametrosPaginacion(int pageNumber, int pageSize)
    {
        if (pageNumber <= 0)
            throw new ArgumentException("El n�mero de p�gina debe ser mayor a 0", nameof(pageNumber));

        if (pageSize <= 0)
            throw new ArgumentException("El tama�o de p�gina debe ser mayor a 0", nameof(pageSize));
    }
    #endregion

    #region M�todos Auxiliares de B�squeda

    private static bool BuscarEnTexto(string texto, string termino)
    {
        if (string.IsNullOrWhiteSpace(texto) || string.IsNullOrWhiteSpace(termino))
            return false;

        if (texto.Equals(termino, StringComparison.OrdinalIgnoreCase))
            return true;

        var textoNormalizado = NormalizarTexto(texto);
        var terminoNormalizado = NormalizarTexto(termino);

        if (textoNormalizado.Equals(terminoNormalizado, StringComparison.OrdinalIgnoreCase))
            return true;

        if (textoNormalizado.Contains(terminoNormalizado, StringComparison.OrdinalIgnoreCase))
            return true;

        if (termino.Contains(' '))
        {
            var palabrasBusqueda = terminoNormalizado.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var palabrasTexto = textoNormalizado.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            return palabrasBusqueda.All(palabraBusqueda =>
                palabrasTexto.Any(palabraTexto =>
                    palabraTexto.Contains(palabraBusqueda, StringComparison.OrdinalIgnoreCase) ||
                    palabraBusqueda.Contains(palabraTexto, StringComparison.OrdinalIgnoreCase)));
        }

        return false;
    }

    private static string NormalizarTexto(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        var textoNormalizado = texto.Normalize(System.Text.NormalizationForm.FormD);
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

    private static bool BuscarEnFecha(DateTime fecha, string termino)
    {
        if (string.IsNullOrWhiteSpace(termino))
            return false;

        var fechaLocal = fecha.ToLocalTime();

        var formatosFecha = new[]
        {
            fechaLocal.ToString("dd/MM/yyyy HH:mm:ss"),
            fechaLocal.ToString("dd/MM/yyyy HH:mm"),
            fechaLocal.ToString("dd/MM/yyyy"),
            fechaLocal.ToString("dd/MM"),
            fechaLocal.ToString("MM/yyyy"),
            fechaLocal.ToString("yyyy"),
            fechaLocal.ToString("dd"),
            fechaLocal.ToString("MM"),
            fechaLocal.ToString("HH:mm"),
            fechaLocal.ToString("HH")
        };

        return formatosFecha.Any(f => f.Contains(termino, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryParseFecha(string texto, out DateTime fecha)
    {
        fecha = DateTime.MinValue;

        if (string.IsNullOrWhiteSpace(texto))
            return false;

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

        return DateTime.TryParse(texto, out fecha);
    }

    private static DateTime ToUtc(DateTime dt)
    {
        return dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
    }

    private static DateTime EndOfDay(DateTime dt)
    {
        var d = dt.Date.AddDays(1).AddTicks(-1);
        return d.Kind == DateTimeKind.Utc ? d : d.ToUniversalTime();
    }
    #endregion
}
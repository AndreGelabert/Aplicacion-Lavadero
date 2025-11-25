using Firebase.Models;
using Google.Cloud.Firestore;

/// <summary>
/// Servicio para la gestión de personal (empleados) en Firestore.
/// Proporciona operaciones de consulta, filtrado, paginación, búsqueda y ordenamiento.
/// </summary>
public class PersonalService
{
    #region Constantes
    private const string COLLECTION_NAME = "empleados";
    private const string ESTADO_DEFECTO = "Activo";
    private const string ORDEN_DEFECTO = "NombreCompleto";
    private const string DIRECCION_DEFECTO = "asc";
    #endregion

    #region Dependencias
    private readonly FirestoreDb _firestore;

    /// <summary>
    /// Inicializa una nueva instancia del servicio de personal.
    /// </summary>
    /// <param name="firestore">Instancia de la base de datos Firestore.</param>
    public PersonalService(FirestoreDb firestore)
    {
    _firestore = firestore ?? throw new ArgumentNullException(nameof(firestore));
    }
  #endregion

    #region Operaciones de Consulta

    /// <summary>
    /// Obtiene una lista paginada de empleados aplicando filtros y ordenamiento.
    /// </summary>
    /// <param name="estados">Lista de estados a filtrar (null para solo activos por defecto).</param>
    /// <param name="roles">Lista de roles a filtrar (null para todos).</param>
    /// <param name="pageNumber">Número de página (1-based).</param>
 /// <param name="pageSize">Cantidad de elementos por página.</param>
  /// <param name="sortBy">Campo por el cual ordenar.</param>
    /// <param name="sortOrder">Dirección del ordenamiento (asc/desc).</param>
    /// <returns>Lista de empleados filtrados, ordenados y paginados.</returns>
    public async Task<List<Empleado>> ObtenerEmpleados(
        List<string> estados = null,
        List<string> roles = null,
   int pageNumber = 1,
    int pageSize = 10,
        string sortBy = null,
        string sortOrder = null)
    {
        ValidarParametrosPaginacion(pageNumber, pageSize);

        sortBy ??= ORDEN_DEFECTO;
        sortOrder ??= DIRECCION_DEFECTO;

     var empleados = await ObtenerEmpleadosFiltrados(estados, roles, sortBy, sortOrder);

        return AplicarPaginacion(empleados, pageNumber, pageSize);
    }

    /// <summary>
    /// Calcula el número total de páginas para los empleados filtrados.
    /// </summary>
    /// <param name="estados">Lista de estados a filtrar.</param>
    /// <param name="roles">Lista de roles a filtrar.</param>
    /// <param name="pageSize">Cantidad de elementos por página.</param>
    /// <returns>Número total de páginas.</returns>
    public async Task<int> ObtenerTotalPaginas(
        List<string> estados,
        List<string> roles,
        int pageSize)
    {
        if (pageSize <= 0)
       throw new ArgumentException("El tamaño de página debe ser mayor a 0", nameof(pageSize));

        var totalEmpleados = await ObtenerTotalEmpleados(estados, roles);
 return (int)Math.Ceiling(totalEmpleados / (double)pageSize);
    }

    /// <summary>
    /// Obtiene un empleado específico por su ID.
    /// </summary>
    /// <param name="id">ID del empleado.</param>
    /// <returns>El empleado encontrado o null si no existe.</returns>
    public async Task<Empleado> ObtenerEmpleado(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
      throw new ArgumentException("El ID del empleado no puede estar vacío", nameof(id));

  var docRef = _firestore.Collection(COLLECTION_NAME).Document(id);
  var snapshot = await docRef.GetSnapshotAsync();

        return snapshot.Exists ? MapearDocumentoAEmpleado(snapshot) : null;
    }

    /// <summary>
    /// Obtiene TODOS los empleados sin aplicar filtro de estado.
    /// Útil para auditoría y reportes donde se necesitan empleados inactivos/eliminados.
    /// </summary>
 /// <returns>Lista completa de todos los empleados en la base de datos.</returns>
    public async Task<List<Empleado>> ObtenerEmpleadosSinFiltroEstado()
    {
        var query = _firestore.Collection(COLLECTION_NAME);
   var snapshot = await query.GetSnapshotAsync();
   
        return snapshot.Documents
     .Select(MapearDocumentoAEmpleado)
.OrderBy(e => e.NombreCompleto)
   .ToList();
    }

    /// <summary>
    /// Busca empleados por término de búsqueda (nombre, email, rol).
    /// </summary>
    public async Task<List<Empleado>> BuscarEmpleados(
        string searchTerm,
    List<string> estados = null,
        List<string> roles = null,
    int pageNumber = 1,
        int pageSize = 10,
        string sortBy = null,
        string sortOrder = null)
{
        var baseFiltrada = await ObtenerEmpleadosFiltrados(estados, roles, sortBy, sortOrder);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
     baseFiltrada = AplicarBusqueda(baseFiltrada, searchTerm);
        }

      var ordenados = AplicarOrdenamiento(baseFiltrada, sortBy, sortOrder);
        return AplicarPaginacion(ordenados, Math.Max(pageNumber, 1), Math.Max(pageSize, 1));
    }

    /// <summary>
    /// Obtiene el total de empleados que coinciden con la búsqueda.
    /// /// </summary>
    public async Task<int> ObtenerTotalEmpleadosBusqueda(
      string searchTerm,
        List<string> estados,
        List<string> roles)
    {
        estados = ConfigurarEstadosDefecto(estados);
      var baseFiltrada = await ObtenerEmpleadosFiltrados(estados, roles, ORDEN_DEFECTO, DIRECCION_DEFECTO);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
  baseFiltrada = AplicarBusqueda(baseFiltrada, searchTerm);
        }

        return baseFiltrada.Count;
    }

    /// <summary>
    /// Obtiene todos los roles únicos registrados.
    /// </summary>
    public async Task<List<string>> ObtenerRolesUnicos()
    {
        var snapshot = await _firestore.Collection(COLLECTION_NAME).GetSnapshotAsync();
        return snapshot.Documents
            .Select(doc => doc.ContainsField("Rol") ? doc.GetValue<string>("Rol") : "Empleado")
   .Where(rol => !string.IsNullOrWhiteSpace(rol))
            .Distinct()
        .OrderBy(rol => rol)
        .ToList();
    }
  #endregion

    #region Operaciones CRUD

    /// <summary>
    /// Actualiza el rol de un empleado.
    /// </summary>
    public async Task ActualizarRol(string id, string nuevoRol)
    {
        if (string.IsNullOrWhiteSpace(id))
  throw new ArgumentException("El ID del empleado no puede estar vacío", nameof(id));

    if (string.IsNullOrWhiteSpace(nuevoRol))
 throw new ArgumentException("El nuevo rol no puede estar vacío", nameof(nuevoRol));

        var employeeRef = _firestore.Collection(COLLECTION_NAME).Document(id);
        await employeeRef.UpdateAsync("Rol", nuevoRol);
    }

    /// <summary>
    /// Cambia el estado de un empleado.
    /// </summary>
    public async Task CambiarEstadoEmpleado(string id, string nuevoEstado)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("El ID del empleado no puede estar vacío", nameof(id));

  if (string.IsNullOrWhiteSpace(nuevoEstado))
        throw new ArgumentException("El nuevo estado no puede estar vacío", nameof(nuevoEstado));

     var employeeRef = _firestore.Collection(COLLECTION_NAME).Document(id);
    await employeeRef.UpdateAsync("Estado", nuevoEstado);
  }
    #endregion

    #region Métodos Privados - Consultas Base

    /// <summary>
    /// Obtiene todos los empleados aplicando filtros y ordenamiento.
    /// </summary>
    private async Task<List<Empleado>> ObtenerEmpleadosFiltrados(
        List<string> estados,
        List<string> roles,
     string sortBy,
   string sortOrder)
    {
      estados = ConfigurarEstadosDefecto(estados);

        var query = ConstruirQueryFiltros(estados, roles);

        var snapshot = await query.GetSnapshotAsync();
  var empleados = snapshot.Documents
       .Select(MapearDocumentoAEmpleado)
     .ToList();

        return AplicarOrdenamiento(empleados, sortBy, sortOrder);
    }

    /// <summary>
    /// Obtiene el total de empleados que cumplen con los filtros.
    /// </summary>
    private async Task<int> ObtenerTotalEmpleados(
     List<string> estados,
      List<string> roles)
 {
        estados = ConfigurarEstadosDefecto(estados);

    var query = ConstruirQueryFiltros(estados, roles);

        var snapshot = await query.GetSnapshotAsync();
        return snapshot.Count;
    }
    #endregion

    #region Métodos Privados - Utilidades

    /// <summary>
    /// Configura la lista de estados por defecto si no se especificó ninguno.
    /// </summary>
    private static List<string> ConfigurarEstadosDefecto(List<string> estados)
    {
      estados ??= new List<string>();
        if (!estados.Any())
        {
  estados.Add(ESTADO_DEFECTO);
        }
    return estados;
    }

    /// <summary>
    /// Construye un query de Firestore aplicando filtros de estado y rol.
    /// </summary>
    private Query ConstruirQueryFiltros(
        List<string> estados,
        List<string> roles)
    {
        Query query = _firestore.Collection(COLLECTION_NAME);

        if (estados?.Any() == true)
        {
            query = query.WhereIn("Estado", estados);
        }

 if (roles?.Any() == true)
        {
         query = query.WhereIn("Rol", roles);
        }

   return query;
    }

    /// <summary>
    /// Aplica ordenamiento a una lista de empleados.
    /// </summary>
    private static List<Empleado> AplicarOrdenamiento(List<Empleado> empleados, string sortBy, string sortOrder)
    {
  sortBy ??= ORDEN_DEFECTO;
        sortOrder = (sortOrder ?? DIRECCION_DEFECTO).Trim().ToLowerInvariant();

        Func<Empleado, object> keySelector = sortBy switch
   {
     "NombreCompleto" => e => e.NombreCompleto,
            "Email" => e => e.Email,
            "Rol" => e => e.Rol,
            "Estado" => e => e.Estado,
            _ => e => e.NombreCompleto
        };

        var ordered = sortOrder == "desc"
            ? empleados.OrderByDescending(keySelector)
     : empleados.OrderBy(keySelector);

        return ordered.ToList();
    }

    /// <summary>
    /// Aplica paginación a una lista en memoria.
 /// </summary>
    private static List<Empleado> AplicarPaginacion(List<Empleado> lista, int pageNumber, int pageSize)
        => lista.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

    /// <summary>
    /// Aplica la lógica de búsqueda sobre una lista previamente filtrada.
    /// </summary>
    private static List<Empleado> AplicarBusqueda(List<Empleado> baseFiltrada, string searchTerm)
    {
        var term = searchTerm?.Trim() ?? string.Empty;
 if (term.Length == 0) return baseFiltrada;

        var termUpper = term.ToUpperInvariant();

    return baseFiltrada.Where(e =>
        (e.NombreCompleto?.ToUpperInvariant().Contains(termUpper) ?? false) ||
          (e.Email?.ToUpperInvariant().Contains(termUpper) ?? false) ||
       (e.Rol?.ToUpperInvariant().Contains(termUpper) ?? false) ||
         (e.Estado?.ToUpperInvariant().Contains(termUpper) ?? false)
        ).ToList();
    }

    /// <summary>
    /// Mapea un documento de Firestore a un objeto Empleado.
    /// </summary>
 private static Empleado MapearDocumentoAEmpleado(DocumentSnapshot documento)
    {
        return new Empleado
        {
Id = documento.Id,
            NombreCompleto = documento.GetValue<string>("Nombre"),
            Email = documento.GetValue<string>("Email"),
       Rol = documento.ContainsField("Rol") ? documento.GetValue<string>("Rol") : "Empleado",
    Estado = documento.GetValue<string>("Estado")
        };
    }

    /// <summary>
    /// Valida los parámetros de paginación.
    /// </summary>
    private static void ValidarParametrosPaginacion(int pageNumber, int pageSize)
    {
      if (pageNumber <= 0)
     throw new ArgumentException("El número de página debe ser mayor a 0", nameof(pageNumber));

        if (pageSize <= 0)
            throw new ArgumentException("El tamaño de página debe ser mayor a 0", nameof(pageSize));
    }
 #endregion
}

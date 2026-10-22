using Firebase.Models;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Caching.Memory;

/// <summary>
/// Servicio para la gestión de roles y sus permisos en Firestore.
/// Proporciona operaciones CRUD, filtrado, búsqueda, paginación, conteo de empleados
/// asignados y el aseguramiento de los roles base del sistema.
/// </summary>
public class RolService
{
    #region Constantes
    public const string COLLECTION_NAME = "roles";

    private const string COLLECTION_EMPLEADOS = "empleados";
    private const string ESTADO_DEFECTO = "Activo";
    private const string ORDEN_DEFECTO = "Nombre";
    private const string DIRECCION_DEFECTO = "asc";
    private const string CACHE_KEY_ROLES_BASE = "roles_base_asegurados";
    private static readonly TimeSpan CACHE_DURACION_ROLES_BASE = TimeSpan.FromMinutes(30);
    #endregion

    #region Dependencias
    private readonly FirestoreDb _firestore;
    private readonly IMemoryCache _cache;

    /// <summary>
    /// Inicializa una nueva instancia del servicio de roles.
    /// </summary>
    /// <param name="firestore">Instancia de la base de datos Firestore.</param>
    /// <param name="cache">Caché en memoria para evitar re-sembrar los roles base en cada solicitud.</param>
    public RolService(FirestoreDb firestore, IMemoryCache cache)
    {
        _firestore = firestore ?? throw new ArgumentNullException(nameof(firestore));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }
    #endregion

    #region Operaciones de Consulta

    /// <summary>
    /// Obtiene una lista paginada de roles aplicando filtros y ordenamiento.
    /// </summary>
    /// <param name="estado">Estado a filtrar ("Activo" o "Inactivo"); null o vacío para todos.</param>
    /// <param name="pageNumber">Número de página (1-based).</param>
    /// <param name="pageSize">Cantidad de elementos por página.</param>
    /// <param name="sortBy">Campo por el cual ordenar.</param>
    /// <param name="sortOrder">Dirección del ordenamiento (asc/desc).</param>
    /// <returns>Lista de roles filtrados, ordenados y paginados.</returns>
    public async Task<List<Rol>> ObtenerRoles(
        string? estado = null,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = ORDEN_DEFECTO,
        string sortOrder = DIRECCION_DEFECTO)
    {
        var roles = await ObtenerRolesFiltrados(estado, sortBy, sortOrder);
        return AplicarPaginacion(roles, pageNumber, pageSize);
    }

    /// <summary>
    /// Busca roles por nombre o descripción, aplicando filtro de estado y paginación.
    /// </summary>
    /// <param name="searchTerm">Término de búsqueda.</param>
    /// <param name="estado">Estado a filtrar ("Activo" o "Inactivo"); null o vacío para todos.</param>
    /// <param name="pageNumber">Número de página actual.</param>
    /// <param name="pageSize">Tamaño de página.</param>
    /// <param name="sortBy">Campo por el cual ordenar.</param>
    /// <param name="sortOrder">Dirección del ordenamiento.</param>
    /// <returns>Lista de roles que coinciden con la búsqueda.</returns>
    public async Task<List<Rol>> BuscarRoles(
        string searchTerm,
        string? estado = null,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = ORDEN_DEFECTO,
        string sortOrder = DIRECCION_DEFECTO)
    {
        var roles = await ObtenerRolesFiltrados(estado, sortBy, sortOrder);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            roles = roles
                .Where(rol => RolReglas.ContieneTexto(rol.Nombre, searchTerm)
                    || RolReglas.ContieneTexto(rol.Descripcion, searchTerm))
                .ToList();
        }

        return AplicarPaginacion(roles, pageNumber, pageSize);
    }

    /// <summary>
    /// Calcula el número total de páginas para los roles filtrados por estado.
    /// </summary>
    /// <param name="estado">Estado a filtrar; null o vacío para todos.</param>
    /// <param name="pageSize">Tamaño de página.</param>
    /// <returns>Número total de páginas, con un mínimo de 1.</returns>
    public async Task<int> ObtenerTotalPaginas(string? estado, int pageSize)
    {
        var roles = await ObtenerRolesFiltrados(estado, ORDEN_DEFECTO, DIRECCION_DEFECTO);
        return CalcularTotalPaginas(roles.Count, pageSize);
    }

    /// <summary>
    /// Obtiene el total de roles que coinciden con la búsqueda y el estado.
    /// </summary>
    /// <param name="searchTerm">Término de búsqueda.</param>
    /// <param name="estado">Estado a filtrar; null o vacío para todos.</param>
    /// <returns>Número total de roles que coinciden.</returns>
    public async Task<int> ObtenerTotalRolesBusqueda(string searchTerm, string? estado = null)
    {
        var roles = await ObtenerRolesFiltrados(estado, ORDEN_DEFECTO, DIRECCION_DEFECTO);

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return roles.Count;
        }

        return roles.Count(rol => RolReglas.ContieneTexto(rol.Nombre, searchTerm)
            || RolReglas.ContieneTexto(rol.Descripcion, searchTerm));
    }

    /// <summary>
    /// Obtiene un rol específico por su ID.
    /// </summary>
    /// <param name="id">ID del rol.</param>
    /// <returns>El rol encontrado o null si no existe.</returns>
    public async Task<Rol?> ObtenerRol(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var docRef = _firestore.Collection(COLLECTION_NAME).Document(id);
        var snapshot = await docRef.GetSnapshotAsync();

        return snapshot.Exists ? MapearDocumentoARol(snapshot) : null;
    }

    /// <summary>
    /// Determina si existe un rol con el nombre indicado, ignorando mayúsculas y espacios.
    /// </summary>
    /// <param name="nombre">Nombre a verificar.</param>
    /// <param name="excluirId">ID de rol a excluir de la comparación (para ediciones).</param>
    /// <returns>True si ya existe otro rol con ese nombre.</returns>
    public async Task<bool> ExisteRolConNombre(string nombre, string? excluirId = null)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return false;
        }

        var roles = await ObtenerTodosLosRolesAsync();

        return roles.Any(rol =>
            !string.Equals(rol.Id, excluirId, StringComparison.Ordinal)
            && RolReglas.SonNombresIguales(rol.Nombre, nombre));
    }

    /// <summary>
    /// Determina si existe un rol activo con el nombre indicado.
    /// </summary>
    /// <param name="nombre">Nombre del rol a verificar.</param>
    /// <returns>True si existe un rol activo con ese nombre.</returns>
    public async Task<bool> ExisteRolActivo(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return false;
        }

        var roles = await ObtenerTodosLosRolesAsync();

        return roles.Any(rol =>
            RolReglas.SonNombresIguales(rol.Nombre, nombre)
            && string.Equals(rol.Estado, ESTADO_DEFECTO, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Obtiene el conteo de empleados asignados a cada rol.
    /// Incluye empleados activos e inactivos. Los empleados sin rol cuentan como "Empleado".
    /// </summary>
    /// <returns>Diccionario nombre de rol → cantidad de empleados, sin distinguir mayúsculas.</returns>
    public async Task<Dictionary<string, int>> ObtenerConteoUsuariosPorRol()
    {
        var snapshot = await _firestore.Collection(COLLECTION_EMPLEADOS).GetSnapshotAsync();
        var conteo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var documento in snapshot.Documents)
        {
            var rol = documento.ContainsField("Rol")
                ? documento.GetValue<string>("Rol")
                : "Empleado";

            if (string.IsNullOrWhiteSpace(rol))
            {
                rol = "Empleado";
            }

            conteo.TryGetValue(rol, out var cantidad);
            conteo[rol] = cantidad + 1;
        }

        return conteo;
    }
    #endregion

    #region Operaciones CRUD

    /// <summary>
    /// Crea un rol con un ID generado automáticamente.
    /// </summary>
    /// <param name="rol">Rol a crear.</param>
    /// <returns>ID del rol creado.</returns>
    public async Task<string> CrearRol(Rol rol)
    {
        if (rol == null)
        {
            throw new ArgumentNullException(nameof(rol));
        }

        var docRef = _firestore.Collection(COLLECTION_NAME).Document();
        rol.Id = docRef.Id;

        await docRef.SetAsync(CrearDiccionarioRol(rol));
        return docRef.Id;
    }

    /// <summary>
    /// Actualiza todos los campos editables de un rol existente.
    /// </summary>
    /// <param name="rol">Rol con los datos actualizados.</param>
    public async Task ActualizarRol(Rol rol)
    {
        if (rol == null)
        {
            throw new ArgumentNullException(nameof(rol));
        }

        if (string.IsNullOrWhiteSpace(rol.Id))
        {
            throw new ArgumentException("El ID del rol no puede estar vacío.", nameof(rol));
        }

        var docRef = _firestore.Collection(COLLECTION_NAME).Document(rol.Id);
        await docRef.SetAsync(CrearDiccionarioRol(rol), SetOptions.Overwrite);
    }

    /// <summary>
    /// Elimina físicamente un rol por su ID.
    /// </summary>
    /// <param name="id">ID del rol a eliminar.</param>
    public async Task EliminarRol(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("El ID del rol no puede estar vacío.", nameof(id));
        }

        await _firestore.Collection(COLLECTION_NAME).Document(id).DeleteAsync();
    }

    /// <summary>
    /// Asegura la existencia de los roles base del sistema.
    /// Crea los roles faltantes y restaura los permisos críticos del Administrador.
    /// El resultado se cachea por 30 minutos para evitar consultas repetidas.
    /// </summary>
    public async Task AsegurarRolesBaseAsync()
    {
        if (_cache.TryGetValue(CACHE_KEY_ROLES_BASE, out _))
        {
            return;
        }

        var roles = await ObtenerTodosLosRolesAsync();

        foreach (var rolBase in RolCatalogo.RolesBase)
        {
            var existente = roles.FirstOrDefault(rol => RolReglas.SonNombresIguales(rol.Nombre, rolBase.Nombre));

            if (existente != null)
            {
                if (string.Equals(existente.Nombre, "Administrador", StringComparison.OrdinalIgnoreCase))
                {
                    var faltantes = RolCatalogo.PermisosCriticosAdministrador
                        .Where(critico => !existente.Permisos.Contains(critico))
                        .ToList();

                    if (faltantes.Count > 0)
                    {
                        existente.Permisos = RolCatalogo.NormalizarPermisos(
                            existente.Permisos.Concat(faltantes)).ToList();
                        await ActualizarRol(existente);
                    }
                }

                continue;
            }

            // Evita sobrescribir un documento que ya usa el ID fijo con otro nombre.
            if (roles.Any(rol => string.Equals(rol.Id, rolBase.Id, StringComparison.Ordinal)))
            {
                continue;
            }

            var nuevoRol = new Rol
            {
                Id = rolBase.Id,
                Nombre = rolBase.Nombre,
                Descripcion = rolBase.Descripcion,
                Permisos = RolCatalogo.NormalizarPermisos(rolBase.Permisos).ToList(),
                Estado = ESTADO_DEFECTO,
                FechaCreacion = DateTime.UtcNow
            };

            await _firestore.Collection(COLLECTION_NAME)
                .Document(rolBase.Id)
                .SetAsync(CrearDiccionarioRol(nuevoRol));
        }

        _cache.Set(CACHE_KEY_ROLES_BASE, true, CACHE_DURACION_ROLES_BASE);
    }
    #endregion

    #region Métodos Privados - Consultas Base

    /// <summary>
    /// Obtiene todos los roles de Firestore y los ordena según el campo solicitado.
    /// </summary>
    private async Task<List<Rol>> ObtenerRolesFiltrados(string? estado, string sortBy, string sortOrder)
    {
        var roles = await ObtenerTodosLosRolesAsync();

        if (string.Equals(estado, "Activo", StringComparison.OrdinalIgnoreCase)
            || string.Equals(estado, "Inactivo", StringComparison.OrdinalIgnoreCase))
        {
            roles = roles
                .Where(rol => string.Equals(rol.Estado, estado, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return AplicarOrdenamiento(roles, sortBy, sortOrder);
    }

    /// <summary>
    /// Lee la colección completa de roles y la mapea a modelos.
    /// </summary>
    private async Task<List<Rol>> ObtenerTodosLosRolesAsync()
    {
        var snapshot = await _firestore.Collection(COLLECTION_NAME).GetSnapshotAsync();
        return snapshot.Documents.Select(MapearDocumentoARol).ToList();
    }
    #endregion

    #region Métodos Privados - Utilidades

    /// <summary>
    /// Aplica ordenamiento a una lista de roles.
    /// </summary>
    private static List<Rol> AplicarOrdenamiento(List<Rol> roles, string sortBy, string sortOrder)
    {
        var campo = string.IsNullOrWhiteSpace(sortBy) ? ORDEN_DEFECTO : sortBy.Trim();
        var descendente = string.Equals(sortOrder?.Trim(), "desc", StringComparison.OrdinalIgnoreCase);

        return campo switch
        {
            "Descripcion" => (descendente
                    ? roles.OrderByDescending(rol => rol.Descripcion, StringComparer.OrdinalIgnoreCase)
                    : roles.OrderBy(rol => rol.Descripcion, StringComparer.OrdinalIgnoreCase))
                .ThenBy(rol => rol.Id, StringComparer.Ordinal)
                .ToList(),
            "Permisos" => (descendente
                    ? roles.OrderByDescending(rol => rol.Permisos.Count)
                    : roles.OrderBy(rol => rol.Permisos.Count))
                .ThenBy(rol => rol.Nombre, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            "Estado" => (descendente
                    ? roles.OrderByDescending(rol => rol.Estado, StringComparer.OrdinalIgnoreCase)
                    : roles.OrderBy(rol => rol.Estado, StringComparer.OrdinalIgnoreCase))
                .ThenBy(rol => rol.Nombre, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            "FechaCreacion" => (descendente
                    ? roles.OrderByDescending(rol => rol.FechaCreacion)
                    : roles.OrderBy(rol => rol.FechaCreacion))
                .ThenBy(rol => rol.Nombre, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            _ => (descendente
                    ? roles.OrderByDescending(rol => rol.Nombre, StringComparer.OrdinalIgnoreCase)
                    : roles.OrderBy(rol => rol.Nombre, StringComparer.OrdinalIgnoreCase))
                .ThenBy(rol => rol.Id, StringComparer.Ordinal)
                .ToList()
        };
    }

    /// <summary>
    /// Aplica paginación en memoria con valores mínimos seguros.
    /// </summary>
    private static List<Rol> AplicarPaginacion(List<Rol> roles, int pageNumber, int pageSize)
    {
        var pagina = Math.Max(1, pageNumber);
        var tamano = Math.Max(1, pageSize);

        return roles.Skip((pagina - 1) * tamano).Take(tamano).ToList();
    }

    /// <summary>
    /// Calcula el total de páginas con un mínimo de una página.
    /// </summary>
    private static int CalcularTotalPaginas(int total, int pageSize)
    {
        var tamano = Math.Max(1, pageSize);
        return Math.Max(1, (int)Math.Ceiling(total / (double)tamano));
    }

    /// <summary>
    /// Construye el diccionario de campos que se persiste en Firestore (sin el Id).
    /// </summary>
    private static Dictionary<string, object> CrearDiccionarioRol(Rol rol)
    {
        return new Dictionary<string, object>
        {
            ["Nombre"] = RolReglas.NormalizarNombre(rol.Nombre),
            ["Descripcion"] = (rol.Descripcion ?? string.Empty).Trim(),
            ["Permisos"] = RolCatalogo.NormalizarPermisos(rol.Permisos).ToList(),
            ["Estado"] = string.IsNullOrWhiteSpace(rol.Estado) ? ESTADO_DEFECTO : rol.Estado,
            ["FechaCreacion"] = rol.FechaCreacion == default ? DateTime.UtcNow : rol.FechaCreacion.ToUniversalTime()
        };
    }

    /// <summary>
    /// Mapea un documento de Firestore a un objeto Rol.
    /// </summary>
    private static Rol MapearDocumentoARol(DocumentSnapshot documento)
    {
        var permisos = documento.ContainsField("Permisos")
            ? documento.GetValue<List<object>>("Permisos")
                ?.Where(valor => valor != null)
                .Select(valor => valor!.ToString() ?? string.Empty)
                .Where(valor => !string.IsNullOrWhiteSpace(valor))
                .ToList() ?? new List<string>()
            : new List<string>();

        var estado = documento.ContainsField("Estado")
            ? documento.GetValue<string>("Estado")
            : null;

        return new Rol
        {
            Id = documento.Id,
            Nombre = documento.ContainsField("Nombre")
                ? documento.GetValue<string>("Nombre") ?? string.Empty
                : string.Empty,
            Descripcion = documento.ContainsField("Descripcion")
                ? documento.GetValue<string>("Descripcion") ?? string.Empty
                : string.Empty,
            Permisos = permisos,
            Estado = string.IsNullOrWhiteSpace(estado) ? ESTADO_DEFECTO : estado,
            FechaCreacion = documento.ContainsField("FechaCreacion")
                ? documento.GetValue<Timestamp>("FechaCreacion").ToDateTime()
                : DateTime.UtcNow
        };
    }
    #endregion
}

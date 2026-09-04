using Firebase.Models;
using Google.Cloud.Firestore;

/// <summary>
/// Servicio para la gestión de roles y sus permisos.
/// </summary>
public class RolService
{
    private const string COLLECTION_NAME = "roles";
    private readonly FirestoreDb _firestore;

    public RolService(FirestoreDb firestore)
    {
        _firestore = firestore ?? throw new ArgumentNullException(nameof(firestore));
    }

    public async Task<List<Rol>> ObtenerRoles(
        List<string>? estados = null,
        int pageNumber = 1,
        int pageSize = 10,
        string? sortBy = null,
        string? sortOrder = null)
    {
        await EnsureRolesBaseAsync();

        var roles = await ObtenerRolesFiltrados(estados, sortBy, sortOrder);
        pageNumber = Math.Max(pageNumber, 1);
        pageSize = Math.Max(pageSize, 1);

        return roles.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
    }

    public async Task<int> ObtenerTotalPaginas(List<string>? estados, int pageSize)
    {
        await EnsureRolesBaseAsync();

        if (pageSize <= 0)
            throw new ArgumentException("El tamaño de página debe ser mayor a 0", nameof(pageSize));

        var total = await ObtenerTotalRoles(estados);
        return (int)Math.Ceiling(total / (double)pageSize);
    }

    public async Task<List<Rol>> BuscarRoles(
        string? searchTerm,
        List<string>? estados = null,
        int pageNumber = 1,
        int pageSize = 10,
        string? sortBy = null,
        string? sortOrder = null)
    {
        await EnsureRolesBaseAsync();

        var roles = await ObtenerRolesFiltrados(estados, sortBy, sortOrder);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToUpperInvariant();
            roles = roles.Where(r =>
                (r.Nombre?.ToUpperInvariant().Contains(term) ?? false) ||
                (r.Descripcion?.ToUpperInvariant().Contains(term) ?? false)).ToList();
        }

        var ordenados = AplicarOrdenamiento(roles, sortBy, sortOrder);
        pageNumber = Math.Max(pageNumber, 1);
        pageSize = Math.Max(pageSize, 1);

        return ordenados.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
    }

    public async Task<int> ObtenerTotalRolesBusqueda(string? searchTerm, List<string>? estados)
    {
        await EnsureRolesBaseAsync();

        var roles = await ObtenerRolesFiltrados(estados, "Nombre", "asc");

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToUpperInvariant();
            roles = roles.Where(r =>
                (r.Nombre?.ToUpperInvariant().Contains(term) ?? false) ||
                (r.Descripcion?.ToUpperInvariant().Contains(term) ?? false)).ToList();
        }

        return roles.Count;
    }

    public async Task<Rol?> ObtenerRol(string id)
    {
        await EnsureRolesBaseAsync();

        if (string.IsNullOrWhiteSpace(id))
            return null;

        var doc = await _firestore.Collection(COLLECTION_NAME).Document(id).GetSnapshotAsync();
        return doc.Exists ? MapearDocumentoARol(doc) : null;
    }

    public async Task<Rol?> ObtenerRolPorNombre(string nombreRol)
    {
        await EnsureRolesBaseAsync();

        if (string.IsNullOrWhiteSpace(nombreRol))
            return null;

        var snapshot = await _firestore.Collection(COLLECTION_NAME)
            .WhereEqualTo("Nombre", nombreRol.Trim())
            .Limit(1)
            .GetSnapshotAsync();

        var doc = snapshot.Documents.FirstOrDefault();
        return doc is null ? null : MapearDocumentoARol(doc);
    }

    public async Task CrearRol(Rol rol)
    {
        await EnsureRolesBaseAsync();

        if (rol == null)
            throw new ArgumentNullException(nameof(rol));

        NormalizarRol(rol);
        ValidarRol(rol);

        if (await ExisteRolConNombre(rol.Nombre))
            throw new ArgumentException("Ya existe un rol con ese nombre.");

        var docRef = _firestore.Collection(COLLECTION_NAME).Document();
        rol.Id = docRef.Id;
        rol.FechaCreacion = DateTime.UtcNow;

        await docRef.SetAsync(CrearDiccionarioRol(rol));
    }

    public async Task ActualizarRol(Rol rol)
    {
        await EnsureRolesBaseAsync();

        if (rol == null)
            throw new ArgumentNullException(nameof(rol));

        if (string.IsNullOrWhiteSpace(rol.Id))
            throw new ArgumentException("El ID del rol es obligatorio para actualizar.", nameof(rol.Id));

        var actual = await ObtenerRol(rol.Id) ?? throw new ArgumentException("Rol no encontrado.");

        if (actual.EsProtegido)
            throw new InvalidOperationException("No se pueden modificar roles protegidos del sistema.");

        NormalizarRol(rol);
        ValidarRol(rol);

        if (!string.Equals(actual.Nombre, rol.Nombre, StringComparison.OrdinalIgnoreCase) &&
            await ExisteRolConNombre(rol.Nombre, rol.Id))
        {
            throw new ArgumentException("Ya existe un rol con ese nombre.");
        }

        rol.FechaCreacion = actual.FechaCreacion;

        await _firestore.Collection(COLLECTION_NAME)
            .Document(rol.Id)
            .SetAsync(CrearDiccionarioRol(rol), SetOptions.Overwrite);
    }

    public async Task EliminarRol(string id)
    {
        await EnsureRolesBaseAsync();

        var rol = await ObtenerRol(id) ?? throw new ArgumentException("Rol no encontrado.");

        if (rol.EsProtegido)
            throw new InvalidOperationException("No se puede eliminar un rol protegido del sistema.");

        var tieneEmpleados = await RolTieneEmpleadosAsignados(rol.Nombre);
        if (tieneEmpleados)
            throw new InvalidOperationException("No se puede eliminar el rol porque tiene empleados asignados.");

        await _firestore.Collection(COLLECTION_NAME).Document(id).DeleteAsync();
    }

    public async Task<List<string>> ObtenerRolesActivos()
    {
        await EnsureRolesBaseAsync();

        var snapshot = await _firestore.Collection(COLLECTION_NAME)
            .WhereEqualTo("Estado", "Activo")
            .GetSnapshotAsync();

        return snapshot.Documents
            .Select(MapearDocumentoARol)
            .Select(r => r.Nombre)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n)
            .ToList();
    }

    public async Task<List<string>> ObtenerPermisosRol(string nombreRol)
    {
        await EnsureRolesBaseAsync();

        if (string.IsNullOrWhiteSpace(nombreRol))
            return [];

        var rol = await ObtenerRolPorNombre(nombreRol);
        if (rol == null)
            return [];

        return rol.Permisos
            .Select(NormalizarClavePermiso)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<bool> TienePermiso(string nombreRol, string vista, string accion)
    {
        await EnsureRolesBaseAsync();

        if (string.IsNullOrWhiteSpace(nombreRol))
            return false;

        if (string.Equals(nombreRol, "Administrador", StringComparison.OrdinalIgnoreCase))
            return true;

        var rol = await ObtenerRolPorNombre(nombreRol);
        if (rol == null || !string.Equals(rol.Estado, "Activo", StringComparison.OrdinalIgnoreCase))
            return false;

        var permisoBuscado = PermisosSistema.CrearClave(vista, accion);

        return rol.Permisos
            .Select(NormalizarClavePermiso)
            .Any(p => string.Equals(p, permisoBuscado, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<Dictionary<string, int>> ObtenerCantidadEmpleadosPorRoles(IEnumerable<string> roles)
    {
        var listaRoles = roles
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var resultado = listaRoles.ToDictionary(r => r, _ => 0, StringComparer.OrdinalIgnoreCase);
        if (!listaRoles.Any()) return resultado;

        var snapshot = await _firestore.Collection("empleados").GetSnapshotAsync();

        foreach (var doc in snapshot.Documents)
        {
            var rolEmpleado = doc.ContainsField("Rol") ? doc.GetValue<string>("Rol") : string.Empty;
            if (string.IsNullOrWhiteSpace(rolEmpleado)) continue;

            var match = listaRoles.FirstOrDefault(r => string.Equals(r, rolEmpleado, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match))
            {
                resultado[match]++;
            }
        }

        return resultado;
    }

    public async Task<bool> RolTieneEmpleadosAsignados(string nombreRol)
    {
        if (string.IsNullOrWhiteSpace(nombreRol))
            return false;

        var snapshot = await _firestore.Collection("empleados")
            .WhereEqualTo("Rol", nombreRol.Trim())
            .Limit(1)
            .GetSnapshotAsync();

        return snapshot.Count > 0;
    }

    private async Task<List<Rol>> ObtenerRolesFiltrados(List<string>? estados, string? sortBy, string? sortOrder)
    {
        var roles = await ObtenerTodosLosRoles();

        if (estados?.Any() == true)
        {
            var setEstados = new HashSet<string>(estados, StringComparer.OrdinalIgnoreCase);
            roles = roles.Where(r => setEstados.Contains(r.Estado)).ToList();
        }

        return AplicarOrdenamiento(roles, sortBy, sortOrder);
    }

    private async Task<int> ObtenerTotalRoles(List<string>? estados)
    {
        var roles = await ObtenerTodosLosRoles();

        if (estados?.Any() == true)
        {
            var setEstados = new HashSet<string>(estados, StringComparer.OrdinalIgnoreCase);
            roles = roles.Where(r => setEstados.Contains(r.Estado)).ToList();
        }

        return roles.Count;
    }

    private async Task<List<Rol>> ObtenerTodosLosRoles()
    {
        var snapshot = await _firestore.Collection(COLLECTION_NAME).GetSnapshotAsync();

        return snapshot.Documents
            .Select(MapearDocumentoARol)
            .GroupBy(r => r.Nombre, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    private static List<Rol> AplicarOrdenamiento(List<Rol> roles, string? sortBy, string? sortOrder)
    {
        sortBy ??= "Nombre";
        sortOrder = (sortOrder ?? "asc").Trim().ToLowerInvariant();

        Func<Rol, object?> keySelector = sortBy switch
        {
            "Nombre" => r => r.Nombre,
            "Descripcion" => r => r.Descripcion,
            "Estado" => r => r.Estado,
            "CantidadPermisos" => r => r.Permisos?.Count ?? 0,
            "FechaCreacion" => r => r.FechaCreacion,
            _ => r => r.Nombre
        };

        return (sortOrder == "desc" ? roles.OrderByDescending(keySelector) : roles.OrderBy(keySelector)).ToList();
    }

    private async Task<bool> ExisteRolConNombre(string nombre, string? idActual = null)
    {
        var snapshot = await _firestore.Collection(COLLECTION_NAME)
            .WhereEqualTo("Nombre", nombre.Trim())
            .GetSnapshotAsync();

        return snapshot.Documents.Any(d => idActual == null || !string.Equals(d.Id, idActual, StringComparison.OrdinalIgnoreCase));
    }

    private static void ValidarRol(Rol rol)
    {
        if (string.IsNullOrWhiteSpace(rol.Nombre))
            throw new ArgumentException("El nombre del rol es obligatorio.");

        if (rol.Nombre.Trim().Length < 3)
            throw new ArgumentException("El nombre del rol debe tener al menos 3 caracteres.");

        if (string.IsNullOrWhiteSpace(rol.Descripcion))
            throw new ArgumentException("La descripción del rol es obligatoria.");

        if (rol.Descripcion.Trim().Length < 3)
            throw new ArgumentException("La descripción debe tener al menos 3 caracteres.");

        if (rol.Permisos == null || !rol.Permisos.Any())
            throw new ArgumentException("Debe seleccionar al menos un permiso.");
    }

    private static void NormalizarRol(Rol rol)
    {
        rol.Nombre = rol.Nombre?.Trim() ?? string.Empty;
        rol.Descripcion = rol.Descripcion?.Trim() ?? string.Empty;
        rol.Estado = string.IsNullOrWhiteSpace(rol.Estado) ? "Activo" : rol.Estado.Trim();

        rol.Permisos = (rol.Permisos ?? new List<string>())
            .Select(NormalizarClavePermiso)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizarClavePermiso(string? permiso)
    {
        if (string.IsNullOrWhiteSpace(permiso)) return string.Empty;

        var partes = permiso.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (partes.Length != 2) return string.Empty;

        return PermisosSistema.CrearClave(partes[0], partes[1]);
    }

    private static Dictionary<string, object> CrearDiccionarioRol(Rol rol)
    {
        return new Dictionary<string, object>
        {
            { "Nombre", rol.Nombre },
            { "Descripcion", rol.Descripcion },
            { "Estado", rol.Estado },
            { "Permisos", rol.Permisos },
            { "FechaCreacion", Timestamp.FromDateTime(rol.FechaCreacion.Kind == DateTimeKind.Utc ? rol.FechaCreacion : rol.FechaCreacion.ToUniversalTime()) }
        };
    }

    private static Rol MapearDocumentoARol(DocumentSnapshot doc)
    {
        var nombre = doc.ContainsField("Nombre") ? doc.GetValue<string>("Nombre") : doc.Id;
        var descripcion = doc.ContainsField("Descripcion") ? doc.GetValue<string>("Descripcion") : $"Rol {nombre}";
        var estado = doc.ContainsField("Estado") ? doc.GetValue<string>("Estado") : "Activo";

        var permisos = new List<string>();
        if (doc.ContainsField("Permisos"))
        {
            try
            {
                permisos = doc.GetValue<List<string>>("Permisos") ?? [];
            }
            catch
            {
                permisos = [];
            }
        }

        DateTime fechaCreacion;
        if (doc.ContainsField("FechaCreacion"))
        {
            var ts = doc.GetValue<Timestamp>("FechaCreacion");
            fechaCreacion = ts.ToDateTime();
        }
        else
        {
            fechaCreacion = DateTime.UtcNow;
        }

        return new Rol
        {
            Id = doc.Id,
            Nombre = nombre,
            Descripcion = descripcion,
            Estado = string.IsNullOrWhiteSpace(estado) ? "Activo" : estado,
            Permisos = permisos.Select(NormalizarClavePermiso).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            FechaCreacion = DateTime.SpecifyKind(fechaCreacion, DateTimeKind.Utc)
        };
    }

    public async Task EnsureRolesBaseAsync()
    {
        var snapshot = await _firestore.Collection(COLLECTION_NAME)
            .WhereIn("Nombre", ["Administrador", "Empleado"])
            .GetSnapshotAsync();

        var existentes = snapshot.Documents
            .Select(MapearDocumentoARol)
            .ToDictionary(r => r.Nombre, StringComparer.OrdinalIgnoreCase);

        if (!existentes.ContainsKey("Administrador"))
        {
            await CrearRolBase("rol_administrador", "Administrador", "Rol con acceso completo al sistema.", PermisosSistema.ObtenerPermisosBaseAdministrador());
        }

        if (!existentes.ContainsKey("Empleado"))
        {
            await CrearRolBase("rol_empleado", "Empleado", "Rol operativo para tareas diarias del lavadero.", PermisosSistema.ObtenerPermisosBaseEmpleado());
        }
    }

    private async Task CrearRolBase(string id, string nombre, string descripcion, List<string> permisos)
    {
        var docRef = _firestore.Collection(COLLECTION_NAME).Document(id);
        var doc = await docRef.GetSnapshotAsync();
        if (doc.Exists) return;

        var rol = new Rol
        {
            Id = id,
            Nombre = nombre,
            Descripcion = descripcion,
            Estado = "Activo",
            FechaCreacion = DateTime.UtcNow,
            Permisos = permisos
        };

        await docRef.SetAsync(CrearDiccionarioRol(rol));
    }
}

using System.Globalization;
using System.Text;

/// <summary>
/// Catálogo central de módulos, acciones y permisos disponibles para los roles.
/// </summary>
public sealed record ModuloPermisos(string Clave, string Nombre);

/// <summary>
/// Definición de un rol base protegido del sistema.
/// </summary>
public sealed record RolBase(string Id, string Nombre, string Descripcion, IReadOnlyList<string> Permisos);

/// <summary>
/// Expone el catálogo de permisos del sistema y las definiciones de los roles base.
/// </summary>
public static class RolCatalogo
{
    public const string Consultar = "Consultar";
    public const string Crear = "Crear";
    public const string Modificar = "Modificar";
    public const string Eliminar = "Eliminar";

    /// <summary>
    /// Acciones disponibles para cada módulo, en el orden en que se muestran.
    /// </summary>
    public static IReadOnlyList<string> Acciones { get; } = new[] { Consultar, Crear, Modificar, Eliminar };

    /// <summary>
    /// Módulos reales del sistema que participan en la matriz de permisos.
    /// </summary>
    public static IReadOnlyList<ModuloPermisos> Modulos { get; } = new[]
    {
        new ModuloPermisos("Clientes", "Clientes"),
        new ModuloPermisos("Vehiculos", "Vehículos"),
        new ModuloPermisos("Servicios", "Servicios"),
        new ModuloPermisos("Paquetes", "Paquetes"),
        new ModuloPermisos("Lavados", "Lavados"),
        new ModuloPermisos("Personal", "Personal"),
        new ModuloPermisos("Configuracion", "Configuración"),
        new ModuloPermisos("Auditoria", "Auditoría"),
        new ModuloPermisos("Roles", "Roles y Permisos")
    };

    /// <summary>
    /// Todas las claves de permiso válidas, en orden de módulo y acción.
    /// </summary>
    public static IReadOnlyList<string> TodosLosPermisos { get; } =
        Modulos.SelectMany(modulo => Acciones.Select(accion => Clave(modulo.Clave, accion)))
            .ToList();

    /// <summary>
    /// Permisos que el rol Administrador no puede perder.
    /// </summary>
    public static IReadOnlyList<string> PermisosCriticosAdministrador { get; } = new[]
    {
        "Roles.Consultar", "Roles.Crear", "Roles.Modificar", "Roles.Eliminar"
    };

    /// <summary>
    /// Nombres de los roles predeterminados que no pueden renombrarse ni eliminarse.
    /// </summary>
    public static IReadOnlyList<string> NombresRolesProtegidos { get; } = new[] { "Administrador", "Empleado" };

    /// <summary>
    /// Definiciones de los roles base que se aseguran al abrir el módulo.
    /// </summary>
    public static IReadOnlyList<RolBase> RolesBase { get; } = new[]
    {
        new RolBase("rol-administrador", "Administrador",
            "Rol predeterminado con acceso completo al sistema.", TodosLosPermisos),
        new RolBase("rol-empleado", "Empleado",
            "Rol operativo predeterminado para la atención de clientes, vehículos y lavados.",
            new[]
            {
                "Clientes.Consultar", "Clientes.Crear", "Clientes.Modificar",
                "Vehiculos.Consultar", "Vehiculos.Crear", "Vehiculos.Modificar",
                "Servicios.Consultar", "Paquetes.Consultar",
                "Lavados.Consultar", "Lavados.Crear", "Lavados.Modificar"
            })
    };

    /// <summary>
    /// Construye la clave de permiso con el formato "Modulo.Accion".
    /// </summary>
    public static string Clave(string modulo, string accion) => $"{modulo}.{accion}";

    /// <summary>
    /// Indica si la clave corresponde exactamente a un permiso del catálogo.
    /// </summary>
    public static bool EsPermisoValido(string? clave) =>
        !string.IsNullOrWhiteSpace(clave) && TodosLosPermisos.Contains(clave, StringComparer.Ordinal);

    /// <summary>
    /// Elimina permisos desconocidos y duplicados, conservando el orden del catálogo.
    /// </summary>
    public static IReadOnlyList<string> NormalizarPermisos(IEnumerable<string>? permisos)
    {
        var seleccionados = new HashSet<string>(
            (permisos ?? Array.Empty<string>()).Where(EsPermisoValido)!,
            StringComparer.Ordinal);

        return TodosLosPermisos.Where(seleccionados.Contains).ToList();
    }

    /// <summary>
    /// Indica si el nombre corresponde a un rol protegido, ignorando mayúsculas y espacios.
    /// </summary>
    public static bool EsRolProtegido(string? nombre)
    {
        var normalizado = (nombre ?? string.Empty).Trim();
        return NombresRolesProtegidos.Any(n => string.Equals(n, normalizado, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Devuelve una etiqueta legible para un permiso, por ejemplo "Clientes · Consultar".
    /// </summary>
    public static string EtiquetaPermiso(string? clave)
    {
        if (string.IsNullOrWhiteSpace(clave))
        {
            return string.Empty;
        }

        var separador = clave.IndexOf('.');
        if (separador <= 0)
        {
            return clave;
        }

        var moduloClave = clave[..separador];
        var accion = clave[(separador + 1)..];
        var modulo = Modulos.FirstOrDefault(m => string.Equals(m.Clave, moduloClave, StringComparison.Ordinal));

        return $"{modulo?.Nombre ?? moduloClave} · {accion}";
    }
}

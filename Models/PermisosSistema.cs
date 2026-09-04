namespace Firebase.Models;

/// <summary>
/// Catálogo centralizado de permisos por vista y acción.
/// </summary>
public static class PermisosSistema
{
    public const string Consultar = "Consultar";
    public const string Crear = "Crear";
    public const string Modificar = "Modificar";
    public const string Eliminar = "Eliminar";

    public const string VistaLavados = "Lavados";
    public const string VistaClientes = "Cliente";
    public const string VistaVehiculos = "Vehiculo";
    public const string VistaServicio = "Servicio";
    public const string VistaPaqueteServicio = "PaqueteServicio";
    public const string VistaPersonal = "Personal";
    public const string VistaAuditoria = "Auditoria";
    public const string VistaConfiguracion = "Configuracion";
    public const string VistaTipoDocumento = "TipoDocumento";
    public const string VistaRol = "Rol";

    public static readonly IReadOnlyList<string> AccionesComunes =
    [
        Consultar,
        Crear,
        Modificar,
        Eliminar
    ];

    public static readonly IReadOnlyList<string> Vistas =
    [
        VistaLavados,
        VistaClientes,
        VistaVehiculos,
        VistaServicio,
        VistaPaqueteServicio,
        VistaPersonal,
        VistaAuditoria,
        VistaConfiguracion,
        VistaTipoDocumento,
        VistaRol
    ];

    public static readonly ISet<string> RolesProtegidos =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Administrador",
            "Empleado"
        };

    public static string CrearClave(string vista, string accion)
        => $"{vista.Trim()}:{accion.Trim()}";

    public static List<string> ObtenerTodosLosPermisos()
    {
        var permisos = new List<string>();

        foreach (var vista in Vistas)
        {
            foreach (var accion in AccionesComunes)
            {
                permisos.Add(CrearClave(vista, accion));
            }
        }

        return permisos;
    }

    public static List<string> ObtenerPermisosBaseAdministrador()
        => ObtenerTodosLosPermisos();

    public static List<string> ObtenerPermisosBaseEmpleado()
    {
        var permisos = new List<string>();

        AgregarPermisosVista(permisos, VistaLavados, [Consultar, Crear, Modificar]);
        AgregarPermisosVista(permisos, VistaClientes, [Consultar, Crear, Modificar]);
        AgregarPermisosVista(permisos, VistaVehiculos, [Consultar, Crear, Modificar]);
        AgregarPermisosVista(permisos, VistaTipoDocumento, [Consultar, Crear, Modificar, Eliminar]);

        return permisos;
    }

    private static void AgregarPermisosVista(List<string> permisos, string vista, IEnumerable<string> acciones)
    {
        foreach (var accion in acciones)
        {
            permisos.Add(CrearClave(vista, accion));
        }
    }
}

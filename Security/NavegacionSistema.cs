using System.Security.Claims;

namespace Firebase.Security
{
    /// <summary>
    /// Entrada del menú lateral asociada al permiso de consulta de un módulo.
    /// </summary>
    public sealed record ModuloNavegacion(string Controlador, string Etiqueta, string Permiso)
    {
        /// <summary>
        /// Ruta del módulo.
        /// </summary>
        public string Ruta => "/" + Controlador;
    }

    /// <summary>
    /// Catálogo de navegación del sistema, ordenado como el menú lateral.
    /// </summary>
    public static class NavegacionSistema
    {
        /// <summary>
        /// Ruta de la página de acceso denegado.
        /// </summary>
        public const string RutaAccesoDenegado = "/Home/AccessDenied";

        /// <summary>
        /// Mensaje mostrado cuando el rol no permite abrir ningún módulo.
        /// </summary>
        public const string MensajeSinAcceso =
            "Su rol no tiene acceso a ningún módulo del sistema. Contacte al administrador.";

        /// <summary>
        /// Módulos navegables en el orden en que se muestran en el menú lateral.
        /// </summary>
        public static IReadOnlyList<ModuloNavegacion> Modulos { get; } = new[]
        {
            new ModuloNavegacion("Lavados", "Lavados", RolCatalogo.Clave("Lavados", RolCatalogo.Consultar)),
            new ModuloNavegacion("Cliente", "Clientes", RolCatalogo.Clave("Clientes", RolCatalogo.Consultar)),
            new ModuloNavegacion("Vehiculo", "Vehículos", RolCatalogo.Clave("Vehiculos", RolCatalogo.Consultar)),
            new ModuloNavegacion("Servicio", "Servicios", RolCatalogo.Clave("Servicios", RolCatalogo.Consultar)),
            new ModuloNavegacion("PaqueteServicio", "Paquetes", RolCatalogo.Clave("Paquetes", RolCatalogo.Consultar)),
            new ModuloNavegacion("Personal", "Personal", RolCatalogo.Clave("Personal", RolCatalogo.Consultar)),
            new ModuloNavegacion("Auditoria", "Auditoría", RolCatalogo.Clave("Auditoria", RolCatalogo.Consultar)),
            new ModuloNavegacion("Configuracion", "Configuración", RolCatalogo.Clave("Configuracion", RolCatalogo.Consultar)),
            new ModuloNavegacion("Rol", "Roles y Permisos", RolCatalogo.Clave("Roles", RolCatalogo.Consultar))
        };

        /// <summary>
        /// Obtiene los permisos del usuario autenticado.
        /// </summary>
        public static IReadOnlyList<string> PermisosDe(ClaimsPrincipal? user) => PermisoClaim.Obtener(user);

        /// <summary>
        /// Devuelve el primer módulo, en orden de menú, que el usuario puede abrir.
        /// </summary>
        public static ModuloNavegacion? PrimerModuloAccesible(IEnumerable<string>? permisos)
        {
            var disponibles = new HashSet<string>(
                permisos ?? Array.Empty<string>(),
                StringComparer.Ordinal);

            return Modulos.FirstOrDefault(modulo => disponibles.Contains(modulo.Permiso));
        }

        /// <summary>
        /// Devuelve los módulos visibles para el conjunto de permisos indicado.
        /// </summary>
        public static IReadOnlyList<ModuloNavegacion> ModulosVisibles(IEnumerable<string>? permisos)
        {
            var disponibles = new HashSet<string>(
                permisos ?? Array.Empty<string>(),
                StringComparer.Ordinal);

            return Modulos.Where(modulo => disponibles.Contains(modulo.Permiso)).ToList();
        }

        /// <summary>
        /// Devuelve la ruta del primer módulo accesible o la página de acceso denegado.
        /// </summary>
        public static string RutaLanding(IEnumerable<string>? permisos) =>
            PrimerModuloAccesible(permisos)?.Ruta ?? RutaAccesoDenegado;

        /// <summary>
        /// Devuelve la ruta inicial del usuario autenticado según sus claims de permiso.
        /// </summary>
        public static string RutaLanding(ClaimsPrincipal? user) => RutaLanding(PermisosDe(user));
    }
}

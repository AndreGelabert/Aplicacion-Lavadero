using System.Security.Claims;

namespace Firebase.Security
{
    /// <summary>
    /// Utilidades para leer los permisos emitidos como claims de autenticación.
    /// </summary>
    public static class PermisoClaim
    {
        /// <summary>
        /// Tipo de claim usado para cada permiso "Modulo.Accion".
        /// </summary>
        public const string Tipo = "permiso";

        /// <summary>
        /// Indica si el usuario tiene exactamente el permiso indicado.
        /// </summary>
        public static bool Tiene(ClaimsPrincipal? user, string? permiso)
        {
            if (user?.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(permiso))
            {
                return false;
            }

            return user.HasClaim(Tipo, permiso);
        }

        /// <summary>
        /// Indica si el usuario tiene al menos uno de los permisos indicados.
        /// </summary>
        public static bool TieneAlguno(ClaimsPrincipal? user, params string[]? permisos)
        {
            if (user?.Identity?.IsAuthenticated != true || permisos == null || permisos.Length == 0)
            {
                return false;
            }

            return permisos.Any(permiso => Tiene(user, permiso));
        }

        /// <summary>
        /// Devuelve todos los permisos emitidos para el usuario.
        /// </summary>
        public static IReadOnlyList<string> Obtener(ClaimsPrincipal? user)
        {
            if (user == null)
            {
                return Array.Empty<string>();
            }

            return user.FindAll(Tipo)
                .Select(claim => claim.Value)
                .Where(valor => !string.IsNullOrWhiteSpace(valor))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
    }

    /// <summary>
    /// Extensiones de ClaimsPrincipal para consultar permisos desde controladores y vistas.
    /// </summary>
    public static class PermisoPrincipalExtensions
    {
        /// <summary>
        /// Indica si el usuario tiene el permiso indicado.
        /// </summary>
        public static bool TienePermiso(this ClaimsPrincipal? user, string? permiso) =>
            PermisoClaim.Tiene(user, permiso);

        /// <summary>
        /// Indica si el usuario tiene al menos uno de los permisos indicados.
        /// </summary>
        public static bool TieneAlgunPermiso(this ClaimsPrincipal? user, params string[]? permisos) =>
            PermisoClaim.TieneAlguno(user, permisos);
    }
}

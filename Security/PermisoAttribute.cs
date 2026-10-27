using Microsoft.AspNetCore.Authorization;

namespace Firebase.Security
{
    /// <summary>
    /// Exige que el usuario autenticado tenga al menos uno de los permisos indicados.
    /// Los permisos múltiples se combinan con OR mediante la política "Permiso:A|B".
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class PermisoAttribute : AuthorizeAttribute
    {
        /// <summary>
        /// Crea el atributo con uno o más permisos "Modulo.Accion".
        /// </summary>
        public PermisoAttribute(params string[] permisos)
        {
            var validos = (permisos ?? Array.Empty<string>())
                .Where(permiso => !string.IsNullOrWhiteSpace(permiso))
                .Select(permiso => permiso.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (validos.Length == 0)
            {
                throw new ArgumentException("Debe indicar al menos un permiso.", nameof(permisos));
            }

            Permisos = validos;
            Policy = PermisoPolicyProvider.Prefijo + string.Join("|", validos);
        }

        /// <summary>
        /// Permisos exigidos por el atributo.
        /// </summary>
        public IReadOnlyList<string> Permisos { get; }
    }
}

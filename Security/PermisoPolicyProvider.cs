using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Firebase.Security
{
    /// <summary>
    /// Proveedor dinámico de políticas para los permisos "Modulo.Accion".
    /// Una política con varios permisos separados por "|" exige al menos uno de ellos (OR).
    /// </summary>
    public sealed class PermisoPolicyProvider : IAuthorizationPolicyProvider
    {
        /// <summary>
        /// Prefijo que identifica las políticas generadas a partir de permisos.
        /// </summary>
        public const string Prefijo = "Permiso:";

        private readonly DefaultAuthorizationPolicyProvider _proveedorPorDefecto;

        /// <summary>
        /// Crea el proveedor a partir de las opciones de autorización de la aplicación.
        /// </summary>
        public PermisoPolicyProvider(IOptions<AuthorizationOptions> options)
        {
            _proveedorPorDefecto = new DefaultAuthorizationPolicyProvider(options);
        }

        /// <inheritdoc />
        public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _proveedorPorDefecto.GetDefaultPolicyAsync();

        /// <inheritdoc />
        public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _proveedorPorDefecto.GetFallbackPolicyAsync();

        /// <inheritdoc />
        public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            if (!policyName.StartsWith(Prefijo, StringComparison.Ordinal))
            {
                return _proveedorPorDefecto.GetPolicyAsync(policyName);
            }

            var permisos = policyName[Prefijo.Length..]
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(permiso => !string.IsNullOrWhiteSpace(permiso))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (permisos.Length == 0)
            {
                return Task.FromResult<AuthorizationPolicy?>(null);
            }

            var politica = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireAssertion(context =>
                    permisos.Any(permiso => context.User.HasClaim(PermisoClaim.Tipo, permiso)))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(politica);
        }
    }
}

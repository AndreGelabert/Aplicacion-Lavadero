using Firebase.Security;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using Xunit;

namespace Firebase.Tests.Security
{
    /// <summary>
    /// Unit tests for the permission claim helper and the dynamic permission policy provider.
    /// </summary>
    public class PermisoTests
    {
        private static ClaimsPrincipal CrearUsuario(params string[] permisos)
        {
            var claims = permisos.Select(p => new Claim(PermisoClaim.Tipo, p));
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        }

        [Fact]
        public void TienePermiso_ShouldMatchTheExactClaim()
        {
            var user = CrearUsuario("Clientes.Consultar", "Auditoria.Consultar");

            Assert.True(user.TienePermiso("Auditoria.Consultar"));
            Assert.False(user.TienePermiso("Auditoria.Crear"));
        }

        [Fact]
        public void TieneAlgunPermiso_ShouldReturnTrueWhenAnyClaimMatches()
        {
            var user = CrearUsuario("Roles.Consultar");

            Assert.True(user.TieneAlgunPermiso("Roles.Crear", "Roles.Consultar"));
            Assert.False(user.TieneAlgunPermiso("Roles.Crear", "Roles.Eliminar"));
        }

        [Fact]
        public void UnauthenticatedUser_ShouldHaveNoPermission()
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity());

            Assert.False(user.TienePermiso("Clientes.Consultar"));
            Assert.False(user.TieneAlgunPermiso("Clientes.Consultar"));
        }

        [Fact]
        public async Task PolicyProvider_ShouldAcceptAnyOfTheEncodedPermissions()
        {
            var provider = new PermisoPolicyProvider(
                Microsoft.Extensions.Options.Options.Create(new Microsoft.AspNetCore.Authorization.AuthorizationOptions()));

            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            services.AddLogging();
            services.AddAuthorization();
            services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider>(provider);
            using var serviceProvider = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions
                .BuildServiceProvider(services);
            var authorization = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationService>(serviceProvider);

            var conCrear = await authorization.AuthorizeAsync(
                CrearUsuario("Clientes.Crear"), null, "Permiso:Clientes.Consultar|Clientes.Crear");
            Assert.True(conCrear.Succeeded);

            var sinPermiso = await authorization.AuthorizeAsync(
                CrearUsuario("Lavados.Consultar"), null, "Permiso:Clientes.Consultar|Clientes.Crear");
            Assert.False(sinPermiso.Succeeded);
        }

        [Fact]
        public async Task PolicyProvider_ShouldFallBackForPoliciesWithoutThePrefix()
        {
            var options = new Microsoft.AspNetCore.Authorization.AuthorizationOptions();
            options.AddPolicy("Otra", policy => policy.RequireAuthenticatedUser());
            var provider = new PermisoPolicyProvider(Microsoft.Extensions.Options.Options.Create(options));

            var policy = await provider.GetPolicyAsync("Otra");

            Assert.NotNull(policy);
        }

        [Fact]
        public async Task PolicyProvider_ShouldReturnNullForAnEmptyEncodedPermission()
        {
            var provider = new PermisoPolicyProvider(
                Microsoft.Extensions.Options.Options.Create(new Microsoft.AspNetCore.Authorization.AuthorizationOptions()));

            Assert.Null(await provider.GetPolicyAsync("Permiso:"));
        }
    }
}

using Xunit;

namespace Firebase.Tests.Services
{
    /// <summary>
    /// Unit tests for the role permission catalog.
    /// Covers the permission matrix, normalization, protected roles, and base role definitions.
    /// </summary>
    public class RolCatalogoTests
    {
        [Fact]
        public void TodosLosPermisos_ShouldHave36UniqueKeys()
        {
            Assert.Equal(36, RolCatalogo.TodosLosPermisos.Count);
            Assert.Equal(36, RolCatalogo.TodosLosPermisos.Distinct().Count());
        }

        [Fact]
        public void EsPermisoValido_ShouldRejectUnknownAction()
        {
            Assert.True(RolCatalogo.EsPermisoValido("Clientes.Consultar"));
            Assert.False(RolCatalogo.EsPermisoValido("Clientes.Borrar"));
            Assert.False(RolCatalogo.EsPermisoValido("Turnos.Consultar"));
        }

        [Fact]
        public void NormalizarPermisos_ShouldDeduplicateAndKeepCatalogOrder()
        {
            var result = RolCatalogo.NormalizarPermisos(new[]
            {
                "Roles.Consultar", "Clientes.Consultar", "Clientes.Consultar", "Nope"
            });

            Assert.Equal(new[] { "Clientes.Consultar", "Roles.Consultar" }, result);
        }

        [Theory]
        [InlineData("administrador", true)]
        [InlineData(" Empleado ", true)]
        [InlineData("Supervisor", false)]
        public void EsRolProtegido_ShouldIgnoreCaseAndWhitespace(string nombre, bool expected)
        {
            Assert.Equal(expected, RolCatalogo.EsRolProtegido(nombre));
        }

        [Fact]
        public void RolesBase_ShouldContainAdministratorWithAllPermissions()
        {
            var admin = RolCatalogo.RolesBase.Single(r => r.Nombre == "Administrador");
            var empleado = RolCatalogo.RolesBase.Single(r => r.Nombre == "Empleado");

            Assert.Equal(36, admin.Permisos.Count);
            Assert.Equal(11, empleado.Permisos.Count);
            Assert.DoesNotContain(empleado.Permisos, p => p.StartsWith("Roles."));
        }
    }
}

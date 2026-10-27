using Xunit;

namespace Firebase.Tests.Services
{
    /// <summary>
    /// Unit tests for the role permission catalog.
    /// Covers the per-module permission matrix, normalization, protected roles, and base role definitions.
    /// </summary>
    public class RolCatalogoTests
    {
        [Fact]
        public void TodosLosPermisos_ShouldHave28UniqueKeys()
        {
            Assert.Equal(28, RolCatalogo.TodosLosPermisos.Count);
            Assert.Equal(28, RolCatalogo.TodosLosPermisos.Distinct().Count());
        }

        [Theory]
        [InlineData("Clientes", "Consultar,Crear,Modificar,Eliminar")]
        [InlineData("Vehiculos", "Consultar,Crear,Modificar,Eliminar")]
        [InlineData("Servicios", "Consultar,Crear,Modificar,Eliminar")]
        [InlineData("Paquetes", "Consultar,Crear,Modificar,Eliminar")]
        [InlineData("Lavados", "Consultar,Crear,Modificar")]
        [InlineData("Personal", "Consultar,Modificar")]
        [InlineData("Configuracion", "Consultar,Modificar")]
        [InlineData("Auditoria", "Consultar")]
        [InlineData("Roles", "Consultar,Crear,Modificar,Eliminar")]
        public void AccionesPara_ShouldReturnTheActionsThatExistForEachModule(string modulo, string expected)
        {
            var esperadas = expected.Split(',');
            Assert.Equal(esperadas, RolCatalogo.AccionesPara(modulo));
        }

        [Fact]
        public void AccionesPara_ShouldReturnEmptyForUnknownModule()
        {
            Assert.Empty(RolCatalogo.AccionesPara("Turnos"));
        }

        [Theory]
        [InlineData("Auditoria.Crear")]
        [InlineData("Auditoria.Modificar")]
        [InlineData("Auditoria.Eliminar")]
        [InlineData("Configuracion.Crear")]
        [InlineData("Configuracion.Eliminar")]
        [InlineData("Lavados.Eliminar")]
        [InlineData("Personal.Crear")]
        [InlineData("Personal.Eliminar")]
        public void EsPermisoValido_ShouldRejectActionsThatDoNotExistForTheModule(string clave)
        {
            Assert.False(RolCatalogo.EsPermisoValido(clave));
        }

        [Fact]
        public void EsPermisoValido_ShouldRejectUnknownAction()
        {
            Assert.True(RolCatalogo.EsPermisoValido("Clientes.Consultar"));
            Assert.False(RolCatalogo.EsPermisoValido("Clientes.Borrar"));
            Assert.False(RolCatalogo.EsPermisoValido("Turnos.Consultar"));
        }

        [Fact]
        public void NormalizarPermisos_ShouldDeduplicateKeepCatalogOrderAndDropInvalid()
        {
            var result = RolCatalogo.NormalizarPermisos(new[]
            {
                "Roles.Consultar", "Clientes.Consultar", "Clientes.Consultar", "Nope",
                "Auditoria.Crear", "Configuracion.Eliminar", "Lavados.Eliminar", "Personal.Crear"
            });

            Assert.Equal(new[] { "Clientes.Consultar", "Roles.Consultar" }, result);
        }

        [Fact]
        public void NormalizarPermisos_ShouldPreserveEveryValidPermission()
        {
            var result = RolCatalogo.NormalizarPermisos(RolCatalogo.TodosLosPermisos);
            Assert.Equal(RolCatalogo.TodosLosPermisos, result);
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

            Assert.Equal(28, admin.Permisos.Count);
            Assert.Equal(11, empleado.Permisos.Count);
            Assert.DoesNotContain(empleado.Permisos, p => p.StartsWith("Roles."));
            Assert.All(RolCatalogo.PermisosCriticosAdministrador, p => Assert.Contains(p, admin.Permisos));
        }
    }
}

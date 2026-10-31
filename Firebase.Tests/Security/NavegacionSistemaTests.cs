using Firebase.Security;
using Firebase.Controllers;
using Xunit;

namespace Firebase.Tests.Security
{
    /// <summary>
    /// Unit tests for the permission-aware navigation catalog and landing resolution.
    /// </summary>
    public class NavegacionSistemaTests
    {
        [Fact]
        public void PrimerModuloAccesible_ShouldRespectSidebarOrder()
        {
            var permisos = new[]
            {
                "Auditoria.Consultar", "Configuracion.Consultar", "Configuracion.Modificar",
                "Clientes.Consultar"
            };

            var modulo = NavegacionSistema.PrimerModuloAccesible(permisos);

            Assert.NotNull(modulo);
            Assert.Equal("Cliente", modulo!.Controlador);
        }

        [Fact]
        public void RutaLanding_ShouldReturnTheFirstAccessibleRouteForTheAcceptanceScenario()
        {
            var permisos = new[]
            {
                "Auditoria.Consultar", "Configuracion.Consultar", "Configuracion.Modificar"
            };

            Assert.Equal("/Auditoria", NavegacionSistema.RutaLanding(permisos));
        }

        [Fact]
        public void ModulosVisibles_ShouldOnlyReturnAuditoriaAndConfiguracionForTheAcceptanceScenario()
        {
            var permisos = new[]
            {
                "Auditoria.Consultar", "Configuracion.Consultar", "Configuracion.Modificar"
            };

            var visibles = NavegacionSistema.ModulosVisibles(permisos).Select(m => m.Controlador).ToArray();

            Assert.Equal(new[] { "Auditoria", "Configuracion" }, visibles);
            Assert.All(NavegacionSistema.ModulosVisibles(permisos), m => Assert.True(permisos.Contains(m.Permiso)));
        }

        [Fact]
        public void ModulosVisibles_ShouldShowClientsWithReadModifyDeleteButWithoutCreate()
        {
            var permisos = new[] { "Clientes.Consultar", "Clientes.Modificar", "Clientes.Eliminar" };

            Assert.Contains(NavegacionSistema.ModulosVisibles(permisos), m => m.Controlador == "Cliente");
        }

        [Fact]
        public void RutaLanding_ShouldReturnAccessDeniedWhenThereIsNoModulePermission()
        {
            Assert.Equal("/Home/AccessDenied", NavegacionSistema.RutaLanding(Array.Empty<string>()));
            Assert.Equal("/Cliente", NavegacionSistema.RutaLanding(new[] { "Clientes.Crear" }));
        }

        [Fact]
        public void ModulosVisibles_ShouldShowClientsWithCreatePermissionOnly()
        {
            Assert.Equal(
                new[] { "Cliente" },
                NavegacionSistema.ModulosVisibles(new[] { "Clientes.Crear" }).Select(m => m.Controlador));
        }

        [Theory]
        [InlineData("Lavados.Crear", "Lavados")]
        [InlineData("Servicios.Crear", "Servicio")]
        [InlineData("Paquetes.Crear", "PaqueteServicio")]
        [InlineData("Roles.Crear", "Rol")]
        public void ModulosVisibles_ShouldShowModuleWithCreatePermissionOnly(string permission, string controller)
        {
            Assert.Contains(NavegacionSistema.ModulosVisibles(new[] { permission }),
                module => module.Controlador == controller);
        }

        [Fact]
        public void VehicleModule_ShouldNotBeVisibleWithCreatePermissionOnly()
        {
            Assert.DoesNotContain(NavegacionSistema.ModulosVisibles(new[] { "Vehiculos.Crear" }),
                module => module.Controlador == "Vehiculo");
        }

        [Fact]
        public void ClienteIndex_ShouldAllowConsultOrCreatePermission()
        {
            var permiso = typeof(ClienteController).GetMethod(nameof(ClienteController.Index))!
                .GetCustomAttributes(typeof(PermisoAttribute), inherit: true)
                .Cast<PermisoAttribute>()
                .Single();

            Assert.Equal(new[] { "Clientes.Consultar", "Clientes.Crear" }, permiso.Permisos);
        }

        [Fact]
        public void TodosLosModulos_ShouldUseUniqueControllersAndPermissions()
        {
            Assert.Equal(
                NavegacionSistema.Modulos.Count,
                NavegacionSistema.Modulos.Select(m => m.Controlador).Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(
                NavegacionSistema.Modulos.Count,
                NavegacionSistema.Modulos.Select(m => m.Permiso).Distinct(StringComparer.Ordinal).Count());
        }
    }
}

using Firebase.Models;
using Xunit;

namespace Firebase.Tests.Services
{
    /// <summary>
    /// Unit tests for the role business rules.
    /// Covers name/description/permission validation, protection rules, and duplicate/search helpers.
    /// </summary>
    public class RolReglasTests
    {
        [Fact]
        public void ValidarNombre_ShouldRejectEmptyAndTooLong()
        {
            Assert.Equal(RolReglas.MensajeNombreObligatorio, RolReglas.ValidarNombre("   "));
            Assert.Equal(RolReglas.MensajeNombreLargo, RolReglas.ValidarNombre(new string('a', 51)));
            Assert.Null(RolReglas.ValidarNombre("Supervisor"));
        }

        [Fact]
        public void ValidarDescripcion_ShouldRejectTooLongButAllowEmpty()
        {
            Assert.Null(RolReglas.ValidarDescripcion(null));
            Assert.Null(RolReglas.ValidarDescripcion("   "));
            Assert.Null(RolReglas.ValidarDescripcion(new string('a', 250)));
            Assert.Equal(RolReglas.MensajeDescripcionLarga,
                RolReglas.ValidarDescripcion(new string('a', 251)));
        }

        [Fact]
        public void ValidarPermisos_ShouldRejectEmptyAndUnknown()
        {
            Assert.Equal(RolReglas.MensajePermisoRequerido, RolReglas.ValidarPermisos(Array.Empty<string>()));
            Assert.Equal(RolReglas.MensajePermisoInvalido, RolReglas.ValidarPermisos(new[] { "Nope" }));
            Assert.Null(RolReglas.ValidarPermisos(new[] { "Clientes.Consultar" }));
        }

        [Fact]
        public void ValidarEdicion_ShouldProtectAdministratorCriticalPermissions()
        {
            var admin = new Rol
            {
                Nombre = "Administrador",
                Permisos = RolCatalogo.TodosLosPermisos.ToList()
            };
            var sinCritico = admin.Permisos.Where(p => p != "Roles.Consultar").ToList();

            Assert.Equal(RolReglas.MensajePermisosCriticos,
                RolReglas.ValidarEdicion(admin, "Administrador", sinCritico));
            Assert.Equal(RolReglas.MensajeRolProtegidoNoEditable,
                RolReglas.ValidarEdicion(admin, "Admin", admin.Permisos));
            Assert.Null(RolReglas.ValidarEdicion(admin, "Administrador", admin.Permisos));
        }

        [Fact]
        public void ValidarEliminacion_ShouldBlockProtectedAndAssignedRoles()
        {
            var admin = new Rol { Nombre = "Administrador", Permisos = RolCatalogo.TodosLosPermisos.ToList() };
            var custom = new Rol { Nombre = "Supervisor", Permisos = new List<string> { "Clientes.Consultar" } };

            Assert.Equal(RolReglas.MensajeRolProtegidoNoEliminable,
                RolReglas.ValidarEliminacion(admin, 0));
            Assert.Equal(
                "No es posible desactivar el rol: Existen 3 empleados que tienen este rol asignado. Reasígnelos a otro rol antes de continuar",
                RolReglas.ValidarEliminacion(custom, 3));
            Assert.Null(RolReglas.ValidarEliminacion(custom, 0));
        }

        [Fact]
        public void DuplicateAndSearchHelpers_ShouldIgnoreCaseAccentsAndWhitespace()
        {
            Assert.True(RolReglas.SonNombresIguales("  Ventas ", "ventas"));
            Assert.True(RolReglas.ContieneTexto("Administración", "administracion"));
            Assert.True(RolReglas.ContieneTexto("Configuración", "CONFIGURACION"));
            Assert.False(RolReglas.ContieneTexto("Clientes", "lavados"));
        }
    }
}

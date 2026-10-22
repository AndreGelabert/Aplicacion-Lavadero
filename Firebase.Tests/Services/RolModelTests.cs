using Firebase.Models;
using Xunit;

namespace Firebase.Tests.Services
{
    /// <summary>
    /// Unit tests for the role model defaults.
    /// </summary>
    public class RolModelTests
    {
        [Fact]
        public void NewRol_ShouldDefaultToActiveWithEmptyPermissions()
        {
            var rol = new Rol();

            Assert.Equal("Activo", rol.Estado);
            Assert.Empty(rol.Permisos);
            Assert.True((DateTime.UtcNow - rol.FechaCreacion).TotalMinutes < 1);
        }
    }
}

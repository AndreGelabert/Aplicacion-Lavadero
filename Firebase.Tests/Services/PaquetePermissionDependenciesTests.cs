using Xunit;

namespace Firebase.Tests.Services;

public class PaquetePermissionDependenciesTests
{
    [Theory]
    [InlineData("Paquetes.Crear")]
    [InlineData("Paquetes.Modificar")]
    public void PackageChangesRequireServiceConsultation(string permission)
    {
        var permissions = new[] { permission, "Paquetes.Consultar" };
        Assert.NotNull(RolReglas.ValidarPermisos(permissions));
        Assert.Null(RolReglas.ValidarPermisos(permissions.Append("Servicios.Consultar")));
    }
}

using Firebase.Security;
using Xunit;

namespace Firebase.Tests.Security;

public class LocalEmulatorSeedTests
{
    [Fact]
    public void InitialPasswordMustBeSuppliedWithoutLoggingIt()
    {
        Assert.Throws<InvalidOperationException>(() => LocalEmulatorSeed.RequireInitialPassword(null));
        Assert.Throws<InvalidOperationException>(() => LocalEmulatorSeed.RequireInitialPassword("short"));
        Assert.Equal("example-local-password", LocalEmulatorSeed.RequireInitialPassword("example-local-password"));
    }

    [Fact]
    public void SyntheticRecordsUseActualDocumentAndVehicleTypeNames()
    {
        var documents = LocalEmulatorSeed.Documents();
        Assert.Equal("DNI", documents[("tiposDocumento", "local-seed-dni")]["Nombre"]);
        Assert.Equal("nnnnnnnn", documents[("tiposDocumento", "local-seed-dni")]["Formato"]);
        Assert.Equal("DNI", documents[("clientes", "local-seed-cliente")]["TipoDocumento"]);
        Assert.Equal("Automóvil", documents[("vehiculos", "local-seed-vehiculo")]["TipoVehiculo"]);
        Assert.Equal("Automóvil", documents[("servicios", "local-seed-servicio")]["TipoVehiculo"]);
    }

    [Fact]
    public void SeedsAUserWithCreateOnlyPermissionsForAbmFlowChecks()
    {
        var documents = LocalEmulatorSeed.Documents();
        var user = documents[("empleados", LocalEmulatorSeed.CreateOnlyUserId)];
        var role = documents[("roles", LocalEmulatorSeed.CreateOnlyRoleId)];
        var permissions = Assert.IsAssignableFrom<IEnumerable<string>>(role["Permisos"]);

        Assert.Equal(LocalEmulatorSeed.CreateOnlyEmail, user["Email"]);
        Assert.Equal(LocalEmulatorSeed.CreateOnlyRoleName, user["Rol"]);
        Assert.Equal(new[]
        {
            "Clientes.Crear",
            "Vehiculos.Crear",
            "Servicios.Crear",
            "Servicios.Consultar",
            "Paquetes.Crear",
            "Lavados.Crear",
            "Roles.Crear"
        }, permissions);
    }
}

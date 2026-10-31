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
}

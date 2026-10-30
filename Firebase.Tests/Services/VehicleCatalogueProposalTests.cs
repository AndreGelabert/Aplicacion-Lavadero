using Firebase.Services;
using Xunit;

namespace Firebase.Tests.Services;

public class VehicleCatalogueProposalTests
{
    [Fact]
    public void ConfirmedManualValuesProducePendingProposal()
    {
        var data = new Dictionary<string, string>
        {
            ["VehiculoMarcaManual"] = "true", ["VehiculoModeloManual"] = "true",
            ["VehiculoMarca"] = "Marca nueva", ["VehiculoModelo"] = "Modelo nuevo",
            ["VehiculoTipo"] = "Camión"
        };

        var proposal = VehicleCatalogueProposal.FromSession(data);
        Assert.NotNull(proposal);
        Assert.Equal("Pendiente", proposal.Estado);
        Assert.True(proposal.MarcaManual);
        Assert.True(proposal.ModeloManual);
        Assert.Equal("Marca nueva", proposal.Marca);
        Assert.Equal("Modelo nuevo", proposal.Modelo);
        Assert.Equal("Camión", proposal.TipoVehiculo);
    }

    [Fact]
    public void CatalogueSelectionsDoNotProduceProposal()
    {
        Assert.Null(VehicleCatalogueProposal.FromSession(new Dictionary<string, string>()));
    }

    [Fact]
    public void ClientModalCreatesProposalOnlyForExplicitManualValues()
    {
        var proposal = VehicleCatalogueProposal.FromClientInput(
            "Camión", "Toyota", "1", "Modelo nuevo", brandManual: false, modelManual: true);

        Assert.NotNull(proposal);
        Assert.Equal("Camión", proposal.TipoVehiculo);
        Assert.Equal("Toyota", proposal.Marca);
        Assert.Equal("1", proposal.MarcaId);
        Assert.Equal("Modelo nuevo", proposal.Modelo);
        Assert.False(proposal.MarcaManual);
        Assert.True(proposal.ModeloManual);
        Assert.Equal("Pendiente", proposal.Estado);
    }

    [Fact]
    public void ClientModalCatalogueSelectionsDoNotCreateProposal()
    {
        Assert.Null(VehicleCatalogueProposal.FromClientInput(
            "Automóvil", "Toyota", "1", "Corolla", brandManual: false, modelManual: false));
    }

    [Fact]
    public void ClientModalRejectsEmptyOrOversizedManualNames()
    {
        Assert.Throws<ArgumentException>(() => VehicleCatalogueProposal.FromClientInput(
            "Automóvil", "Toyota", "1", " ", brandManual: false, modelManual: true));
        Assert.Throws<ArgumentException>(() => VehicleCatalogueProposal.FromClientInput(
            "Automóvil", new string('A', 101), "", "Modelo", brandManual: true, modelManual: false));
    }
}

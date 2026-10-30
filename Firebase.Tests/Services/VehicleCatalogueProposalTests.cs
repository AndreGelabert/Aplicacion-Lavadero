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
}

using Google.Cloud.Firestore;

namespace Firebase.Services;

/// <summary>A customer-supplied catalogue proposal; never an approved catalogue entry.</summary>
[FirestoreData]
public sealed class VehicleCatalogueProposal
{
    [FirestoreProperty] public string VehiculoId { get; set; } = "";
    [FirestoreProperty] public string TipoVehiculo { get; set; } = "";
    [FirestoreProperty] public string Marca { get; set; } = "";
    [FirestoreProperty] public string MarcaId { get; set; } = "";
    [FirestoreProperty] public string Modelo { get; set; } = "";
    [FirestoreProperty] public bool MarcaManual { get; set; }
    [FirestoreProperty] public bool ModeloManual { get; set; }
    [FirestoreProperty] public string Estado { get; set; } = "Pendiente";
    [FirestoreProperty] public DateTime CreadaEn { get; set; } = DateTime.UtcNow;

    public static VehicleCatalogueProposal? FromSession(IReadOnlyDictionary<string, string> data)
    {
        var marcaManual = data.GetValueOrDefault("VehiculoMarcaManual") == "true";
        var modeloManual = data.GetValueOrDefault("VehiculoModeloManual") == "true";
        if (!marcaManual && !modeloManual) return null;
        return new VehicleCatalogueProposal
        {
            TipoVehiculo = data.GetValueOrDefault("VehiculoTipo", ""),
            Marca = data.GetValueOrDefault("VehiculoMarca", ""),
            MarcaId = data.GetValueOrDefault("VehiculoMarcaId", ""),
            Modelo = data.GetValueOrDefault("VehiculoModelo", ""),
            MarcaManual = marcaManual,
            ModeloManual = modeloManual
        };
    }

    public static VehicleCatalogueProposal? FromClientInput(
        string tipoVehiculo, string marca, string marcaId, string modelo,
        bool brandManual, bool modelManual)
    {
        if (!brandManual && !modelManual) return null;

        if (string.IsNullOrWhiteSpace(tipoVehiculo) || tipoVehiculo.Trim().Length > 100 ||
            string.IsNullOrWhiteSpace(marca) || marca.Trim().Length > 100 ||
            string.IsNullOrWhiteSpace(modelo) || modelo.Trim().Length > 100)
            throw new ArgumentException("La marca, el modelo y el tipo de vehículo deben tener entre 1 y 100 caracteres.");

        return new VehicleCatalogueProposal
        {
            TipoVehiculo = tipoVehiculo.Trim(),
            Marca = marca.Trim(),
            MarcaId = marcaId?.Trim() ?? "",
            Modelo = modelo.Trim(),
            MarcaManual = brandManual,
            ModeloManual = modelManual
        };
    }
}

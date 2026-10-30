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
}

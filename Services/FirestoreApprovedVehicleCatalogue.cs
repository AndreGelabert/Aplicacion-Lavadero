using Firebase.Models.Dtos;
using Google.Cloud.Firestore;

namespace Firebase.Services;

public sealed class FirestoreApprovedVehicleCatalogue : IApprovedVehicleCatalogue
{
    private readonly FirestoreDb _firestore;
    public FirestoreApprovedVehicleCatalogue(FirestoreDb firestore) => _firestore = firestore;

    public async Task<List<MarcaSimpleDto>> GetBrandsAsync(string? vehicleType)
    {
        var snapshot = await _firestore.Collection("approvedVehicleBrands").GetSnapshotAsync();
        return snapshot.Documents
            .Where(document => vehicleType == null || document.GetValue<string>("TipoVehiculo").Equals(vehicleType, StringComparison.OrdinalIgnoreCase))
            .Select(document => new MarcaSimpleDto(document.GetValue<string>("MarcaId"), document.GetValue<string>("Nombre")))
            .DistinctBy(brand => brand.Id).ToList();
    }

    public async Task<List<ModeloSimpleDto>> GetModelsAsync(string brandId)
    {
        var snapshot = await _firestore.Collection("approvedVehicleModels")
            .WhereEqualTo("MarcaId", brandId).GetSnapshotAsync();
        return snapshot.Documents.Select(document => new ModeloSimpleDto(document.GetValue<string>("Nombre"), brandId)).ToList();
    }
}

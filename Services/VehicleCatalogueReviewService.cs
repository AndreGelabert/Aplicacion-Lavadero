using Google.Cloud.Firestore;
using System.Security.Cryptography;
using System.Text;

namespace Firebase.Services;

public sealed class VehicleCatalogueReviewService
{
    private readonly FirestoreDb _firestore;
    private readonly ICarQueryService _catalogue;

    public VehicleCatalogueReviewService(FirestoreDb firestore, ICarQueryService catalogue)
    {
        _firestore = firestore;
        _catalogue = catalogue;
    }

    public async Task<List<VehicleCatalogueProposal>> GetPendingAsync()
    {
        var snapshot = await _firestore.Collection("vehicleCatalogueProposals")
            .WhereEqualTo("Estado", "Pendiente").GetSnapshotAsync();
        return snapshot.Documents.Select(document => document.ConvertTo<VehicleCatalogueProposal>())
            .OrderBy(proposal => proposal.CreadaEn).ToList();
    }

    public async Task ResolveAsync(string id, string action, string? brand, string? model, string actorId, string actorEmail)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Contains('/') || action is not ("approve" or "link" or "discard"))
            throw new ArgumentException("Solicitud de revisión inválida.");

        var proposalRef = _firestore.Collection("vehicleCatalogueProposals").Document(id);
        var initial = await proposalRef.GetSnapshotAsync();
        if (!initial.Exists) throw new InvalidOperationException("La propuesta ya no existe.");
        var proposal = initial.ConvertTo<VehicleCatalogueProposal>();
        if (proposal.Estado != "Pendiente") throw new InvalidOperationException("La propuesta ya fue revisada.");

        var finalBrand = (brand ?? "").Trim();
        var finalModel = (model ?? "").Trim();
        string brandId = "";
        bool createBrand = false, createModel = false;
        if (action != "discard")
        {
            if (finalBrand.Length is < 1 or > 100 || finalModel.Length is < 1 or > 100)
                throw new ArgumentException("Indica una marca y un modelo de hasta 100 caracteres.");
            var brands = await _catalogue.GetMarcasAsync();
            if (brands.Count == 0) throw new InvalidOperationException("No se puede comprobar el catálogo de marcas ahora.");
            var matches = brands.Where(item => VehicleNameMatcher.IsExact(finalBrand, item.Nombre)).ToList();
            if (matches.Count > 1) throw new InvalidOperationException("La marca es ambigua; selecciona un nombre inequívoco.");
            var selected = matches.SingleOrDefault();
            if (selected == null && action == "link") throw new InvalidOperationException("La marca no existe en el catálogo.");
            createBrand = selected == null;
            brandId = selected?.Id ?? "local-" + Key(finalBrand);
            finalBrand = selected?.Nombre ?? finalBrand;
            var models = createBrand ? new List<Firebase.Models.Dtos.ModeloSimpleDto>() : await _catalogue.GetModelosAsync(brandId);
            if (!createBrand && models.Count == 0)
                throw new InvalidOperationException("No se puede comprobar el catálogo de modelos de esta marca ahora.");
            var modelMatches = models.Where(item => VehicleNameMatcher.IsExact(finalModel, item.Nombre)).ToList();
            if (modelMatches.Count > 1) throw new InvalidOperationException("El modelo es ambiguo; selecciona un nombre inequívoco.");
            var selectedModel = modelMatches.SingleOrDefault();
            if (selectedModel == null && action == "link") throw new InvalidOperationException("El modelo no existe en el catálogo de la marca.");
            createModel = selectedModel == null;
            finalModel = selectedModel?.Nombre ?? finalModel;
        }

        await _firestore.RunTransactionAsync(async transaction =>
        {
            var current = await transaction.GetSnapshotAsync(proposalRef);
            if (!current.Exists || current.GetValue<string>("Estado") != "Pendiente" || current.UpdateTime != initial.UpdateTime)
                throw new InvalidOperationException("La propuesta cambió; vuelve a cargarla.");
            var vehicleRef = _firestore.Collection("vehiculos").Document(proposal.VehiculoId);
            var vehicle = await transaction.GetSnapshotAsync(vehicleRef);
            if (!vehicle.Exists || vehicle.GetValue<string>("Marca") != proposal.Marca || vehicle.GetValue<string>("Modelo") != proposal.Modelo)
                throw new InvalidOperationException("El vehículo cambió; revisa sus datos antes de continuar.");

            if (action != "discard")
            {
                transaction.Set(_firestore.Collection("approvedVehicleBrands").Document(Key(proposal.TipoVehiculo + "|" + finalBrand)),
                    new Dictionary<string, object> { ["MarcaId"] = brandId, ["Nombre"] = finalBrand, ["TipoVehiculo"] = proposal.TipoVehiculo });
                if (createModel)
                    transaction.Set(_firestore.Collection("approvedVehicleModels").Document(Key(brandId + "|" + finalModel)),
                        new Dictionary<string, object> { ["MarcaId"] = brandId, ["Nombre"] = finalModel });
                transaction.Update(vehicleRef, new Dictionary<string, object> { ["Marca"] = finalBrand, ["Modelo"] = finalModel });
            }
            transaction.Update(proposalRef, new Dictionary<string, object> { ["Estado"] = action == "discard" ? "Descartada" : "Revisada", ["RevisadaPor"] = actorId, ["RevisadaEn"] = Timestamp.GetCurrentTimestamp() });
            transaction.Create(_firestore.Collection("registros_auditoria").Document(), new Dictionary<string, object>
            {
                ["UserId"] = actorId, ["UserEmail"] = actorEmail, ["Action"] = "Revision de catalogo: " + action,
                ["TargetId"] = proposal.VehiculoId, ["TargetType"] = "Vehiculo", ["Timestamp"] = Timestamp.GetCurrentTimestamp()
            });
        });
    }

    private static string Key(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.Trim().ToUpperInvariant()))).ToLowerInvariant();
}

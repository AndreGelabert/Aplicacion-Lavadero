using Firebase.Models;
using Firebase.Models.Dtos;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Moq;
using Xunit;

namespace Firebase.Tests.Integration;

/// <summary>Isolated Firestore-emulator tests; never connects to a production project.</summary>
public class VehicleCatalogueFirestoreTests
{
    private static FirestoreDb CreateDatabase() => new FirestoreDbBuilder
    {
        ProjectId = "demo-lava-" + Guid.NewGuid().ToString("N")[..16],
        EmulatorDetection = EmulatorDetection.EmulatorOnly
    }.Build();

    [LocalFirestoreFact]
    public async Task OlderSnapshotCannotOverwriteNewerSnapshot()
    {
        var store = new FirestoreVehicleCatalogueSnapshotStore(CreateDatabase());
        var first = await store.ReserveAsync("makes:all");
        var second = await store.ReserveAsync("makes:all");
        await store.WriteAsync("makes:all", "new", second);
        await store.WriteAsync("makes:all", "old", first);
        Assert.Equal("new", await store.ReadAsync("makes:all"));
    }

    [LocalFirestoreFact]
    public async Task HybridCatalogueUsesPersistedBrandsAndModelsAfterUpstreamFailureOrEmptyResponse()
    {
        var db = CreateDatabase();
        var store = new FirestoreVehicleCatalogueSnapshotStore(db);
        var approvedBrand = db.Collection("approvedVehicleBrands").Document("approved-brand");
        await approvedBrand.SetAsync(new Dictionary<string, object>
        {
            ["MarcaId"] = "local-brand", ["Nombre"] = "Marca Aprobada", ["TipoVehiculo"] = "Automóvil"
        });
        var approvedModel = db.Collection("approvedVehicleModels").Document("approved-model");
        await approvedModel.SetAsync(new Dictionary<string, object>
        {
            ["MarcaId"] = "1", ["Nombre"] = "Modelo Aprobado"
        });

        var primingUpstream = new Mock<ICarQueryService>();
        primingUpstream.Setup(service => service.GetMarcasAsync())
            .ReturnsAsync(new List<MarcaSimpleDto> { new("1", "Toyota") });
        primingUpstream.Setup(service => service.GetModelosAsync("1", null))
            .ReturnsAsync(new List<ModeloSimpleDto> { new("Corolla", "1") });
        var primingCatalogue = Hybrid(db, store, primingUpstream.Object);
        Assert.Contains(await primingCatalogue.GetMarcasAsync(), brand => brand.Nombre == "Toyota");
        Assert.Contains(await primingCatalogue.GetModelosAsync("1"), model => model.Nombre == "Corolla");

        foreach (var upstreamMode in new[] { "failure", "empty" })
        {
            var unavailableUpstream = new Mock<ICarQueryService>();
            if (upstreamMode == "failure")
            {
                unavailableUpstream.Setup(service => service.GetMarcasAsync()).ThrowsAsync(new HttpRequestException("offline"));
                unavailableUpstream.Setup(service => service.GetModelosAsync("1", null)).ThrowsAsync(new HttpRequestException("offline"));
            }
            else
            {
                unavailableUpstream.Setup(service => service.GetMarcasAsync()).ReturnsAsync(new List<MarcaSimpleDto>());
                unavailableUpstream.Setup(service => service.GetModelosAsync("1", null)).ReturnsAsync(new List<ModeloSimpleDto>());
            }

            // A new service/store instance proves the fallback is durable across process restarts.
            var restartedCatalogue = Hybrid(db, new FirestoreVehicleCatalogueSnapshotStore(db), unavailableUpstream.Object);
            var brands = await restartedCatalogue.GetMarcasAsync();
            Assert.Contains(brands, brand => brand.Id == "1" && brand.Nombre == "Toyota");
            Assert.Contains(brands, brand => brand.Id == "local-brand" && brand.Nombre == "Marca Aprobada");
            Assert.Equal(2, brands.Count);

            var models = await restartedCatalogue.GetModelosAsync("1");
            Assert.Contains(models, model => model.Nombre == "Corolla" && model.MarcaId == "1");
            Assert.Contains(models, model => model.Nombre == "Modelo Aprobado" && model.MarcaId == "1");
            Assert.Equal(2, models.Count);
        }
    }

    [LocalFirestoreFact]
    public async Task ManualProposalStaysPrivateUntilApprovedAndRecordsAudit()
    {
        var db = CreateDatabase();
        var vehicle = NewVehicle();
        var proposal = NewProposal();
        await new VehiculoService(db).CrearVehiculoConPropuesta(vehicle, proposal);

        Assert.True((await db.Collection("vehiculos").Document(vehicle.Id).GetSnapshotAsync()).Exists);
        var pending = await db.Collection("vehicleCatalogueProposals").Document(vehicle.Id).GetSnapshotAsync();
        Assert.Equal("Pendiente", pending.GetValue<string>("Estado"));
        Assert.Empty((await db.Collection("approvedVehicleModels").GetSnapshotAsync()).Documents);

        var catalogue = KnownCatalogue();
        await new VehicleCatalogueReviewService(db, catalogue.Object)
            .ResolveAsync(vehicle.Id, "approve", "Toyota", "Modelo Nuevo", "tester", "tester@example.invalid");

        var updated = await db.Collection("vehiculos").Document(vehicle.Id).GetSnapshotAsync();
        Assert.Equal("Modelo Nuevo", updated.GetValue<string>("Modelo"));
        Assert.Equal("Revisada", (await db.Collection("vehicleCatalogueProposals").Document(vehicle.Id).GetSnapshotAsync()).GetValue<string>("Estado"));
        var models = await new FirestoreApprovedVehicleCatalogue(db).GetModelsAsync("1");
        Assert.Equal("Modelo Nuevo", Assert.Single(models).Nombre);
        var audits = await db.Collection("registros_auditoria").WhereEqualTo("TargetId", vehicle.Id).GetSnapshotAsync();
        Assert.Single(audits.Documents);
    }

    [LocalFirestoreFact]
    public async Task ChangedVehicleRejectsReviewWithoutPublishingOrChangingProposal()
    {
        var db = CreateDatabase();
        var vehicle = NewVehicle();
        await new VehiculoService(db).CrearVehiculoConPropuesta(vehicle, NewProposal());
        await db.Collection("vehiculos").Document(vehicle.Id).UpdateAsync("Modelo", "Corregido");

        await Assert.ThrowsAsync<InvalidOperationException>(() => new VehicleCatalogueReviewService(db, KnownCatalogue().Object)
            .ResolveAsync(vehicle.Id, "approve", "Toyota", "Modelo Nuevo", "tester", "tester@example.invalid"));

        Assert.Equal("Corregido", (await db.Collection("vehiculos").Document(vehicle.Id).GetSnapshotAsync()).GetValue<string>("Modelo"));
        Assert.Equal("Pendiente", (await db.Collection("vehicleCatalogueProposals").Document(vehicle.Id).GetSnapshotAsync()).GetValue<string>("Estado"));
        Assert.Empty((await db.Collection("approvedVehicleModels").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task LinkingExistingModelCorrectsVehicleWithoutPublishingDuplicate()
    {
        var db = CreateDatabase();
        var vehicle = NewVehicle();
        await new VehiculoService(db).CrearVehiculoConPropuesta(vehicle, NewProposal());
        await new VehicleCatalogueReviewService(db, KnownCatalogue().Object)
            .ResolveAsync(vehicle.Id, "link", "Toyota", "Corolla", "tester", "tester@example.invalid");

        Assert.Equal("Corolla", (await db.Collection("vehiculos").Document(vehicle.Id).GetSnapshotAsync()).GetValue<string>("Modelo"));
        Assert.Equal("Revisada", (await db.Collection("vehicleCatalogueProposals").Document(vehicle.Id).GetSnapshotAsync()).GetValue<string>("Estado"));
        Assert.Empty((await db.Collection("approvedVehicleModels").GetSnapshotAsync()).Documents);
        Assert.Single((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task DiscardLeavesVehicleAndCatalogueUnchanged()
    {
        var db = CreateDatabase();
        var vehicle = NewVehicle();
        await new VehiculoService(db).CrearVehiculoConPropuesta(vehicle, NewProposal());
        await new VehicleCatalogueReviewService(db, KnownCatalogue().Object)
            .ResolveAsync(vehicle.Id, "discard", null, null, "tester", "tester@example.invalid");

        Assert.Equal("Modelo Nuevo", (await db.Collection("vehiculos").Document(vehicle.Id).GetSnapshotAsync()).GetValue<string>("Modelo"));
        Assert.Equal("Descartada", (await db.Collection("vehicleCatalogueProposals").Document(vehicle.Id).GetSnapshotAsync()).GetValue<string>("Estado"));
        Assert.Empty((await db.Collection("approvedVehicleModels").GetSnapshotAsync()).Documents);
        Assert.Single((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task ApprovedBrandIsAvailableForEachReviewedVehicleType()
    {
        var db = CreateDatabase();
        var upstream = new Mock<ICarQueryService>();
        upstream.Setup(c => c.GetMarcasAsync()).ReturnsAsync(new List<MarcaSimpleDto> { new("1", "Toyota") });
        upstream.Setup(c => c.GetMarcasPorTipoAsync(It.IsAny<string>())).ReturnsAsync(new List<MarcaSimpleDto>());
        upstream.Setup(c => c.GetModelosAsync(It.IsAny<string>(), null)).ReturnsAsync(new List<ModeloSimpleDto>());
        var catalogue = new HybridVehicleCatalogue(upstream.Object, new FirestoreVehicleCatalogueSnapshotStore(db),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<HybridVehicleCatalogue>.Instance, new FirestoreApprovedVehicleCatalogue(db));

        foreach (var type in new[] { "Automóvil", "Camión" })
        {
            var vehicle = NewVehicle();
            vehicle.TipoVehiculo = type;
            vehicle.Marca = "Marca Nueva";
            vehicle.Modelo = "Modelo Uno";
            var proposal = new VehicleCatalogueProposal
            {
                TipoVehiculo = type, Marca = vehicle.Marca, Modelo = vehicle.Modelo, MarcaManual = true, ModeloManual = true
            };
            await new VehiculoService(db).CrearVehiculoConPropuesta(vehicle, proposal);
            await new VehicleCatalogueReviewService(db, catalogue)
                .ResolveAsync(vehicle.Id, "approve", vehicle.Marca, vehicle.Modelo, "tester", "tester@example.invalid");
        }

        Assert.Contains(await catalogue.GetMarcasPorTipoAsync("Camión"), brand => brand.Nombre == "Marca Nueva");
        Assert.Contains(await catalogue.GetMarcasPorTipoAsync("Automóvil"), brand => brand.Nombre == "Marca Nueva");
    }

    private static Vehiculo NewVehicle() => new()
    {
        Id = "", Patente = "AB123CD", TipoVehiculo = "Automóvil", Marca = "Toyota",
        Modelo = "Modelo Nuevo", Color = "Blanco", ClienteId = "test-client"
    };

    private static VehicleCatalogueProposal NewProposal() => new()
    {
        TipoVehiculo = "Automóvil", Marca = "Toyota", MarcaId = "1", Modelo = "Modelo Nuevo", ModeloManual = true
    };

    private static Mock<ICarQueryService> KnownCatalogue()
    {
        var catalogue = new Mock<ICarQueryService>();
        catalogue.Setup(c => c.GetMarcasAsync()).ReturnsAsync(new List<MarcaSimpleDto> { new("1", "Toyota") });
        catalogue.Setup(c => c.GetModelosAsync("1", null)).ReturnsAsync(new List<ModeloSimpleDto> { new("Corolla", "1") });
        return catalogue;
    }

    private static HybridVehicleCatalogue Hybrid(FirestoreDb db, IVehicleCatalogueSnapshotStore snapshots, ICarQueryService upstream) =>
        new(upstream, snapshots, Microsoft.Extensions.Logging.Abstractions.NullLogger<HybridVehicleCatalogue>.Instance,
            new FirestoreApprovedVehicleCatalogue(db));
}

public sealed class LocalFirestoreFactAttribute : FactAttribute
{
    public LocalFirestoreFactAttribute()
    {
        var host = Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST");
        if (!Uri.TryCreate("http://" + host, UriKind.Absolute, out var uri) || uri.Host != "127.0.0.1" || uri.Port <= 0)
            Skip = "A Firestore emulator on 127.0.0.1 is required; production connections are forbidden.";
    }
}

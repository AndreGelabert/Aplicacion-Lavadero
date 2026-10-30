using Firebase.Models.Dtos;
using Firebase.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Firebase.Tests.Services;

public class HybridVehicleCatalogueTests
{
    [Fact]
    public async Task BrandLookupRetainsLastSuccessfulSnapshotWhenApiReturnsEmpty()
    {
        var upstream = new Mock<ICarQueryService>();
        upstream.SetupSequence(x => x.GetMarcasPorTipoAsync("Camión"))
            .ReturnsAsync(new List<MarcaSimpleDto> { new("12", "Scania") })
            .ReturnsAsync(new List<MarcaSimpleDto>());
        var store = new MemorySnapshotStore();
        var catalogue = new HybridVehicleCatalogue(upstream.Object, store, NullLogger<HybridVehicleCatalogue>.Instance);

        Assert.Equal("Scania", Assert.Single(await catalogue.GetMarcasPorTipoAsync("Camión")).Nombre);
        Assert.Equal("Scania", Assert.Single(await catalogue.GetMarcasPorTipoAsync("Camión")).Nombre);
    }

    [Fact]
    public async Task ModelSnapshotsAreIsolatedByBrand()
    {
        var upstream = new Mock<ICarQueryService>();
        upstream.Setup(x => x.GetModelosAsync("1", null)).ReturnsAsync(new List<ModeloSimpleDto> { new("Corolla", "1") });
        upstream.Setup(x => x.GetModelosAsync("2", null)).ReturnsAsync(new List<ModeloSimpleDto>());
        var catalogue = new HybridVehicleCatalogue(upstream.Object, new MemorySnapshotStore(), NullLogger<HybridVehicleCatalogue>.Instance);

        Assert.Single(await catalogue.GetModelosAsync("1"));
        Assert.Empty(await catalogue.GetModelosAsync("2"));
    }

    [Fact]
    public async Task StoreFailureDoesNotBlockSuccessfulApiResponse()
    {
        var upstream = new Mock<ICarQueryService>();
        upstream.Setup(x => x.GetMarcasAsync()).ReturnsAsync(new List<MarcaSimpleDto> { new("1", "Toyota") });
        var store = new Mock<IVehicleCatalogueSnapshotStore>();
        store.Setup(x => x.ReserveAsync(It.IsAny<string>())).ReturnsAsync(1);
        store.Setup(x => x.WriteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>())).ThrowsAsync(new InvalidOperationException());
        var catalogue = new HybridVehicleCatalogue(upstream.Object, store.Object, NullLogger<HybridVehicleCatalogue>.Instance);

        Assert.Equal("Toyota", Assert.Single(await catalogue.GetMarcasAsync()).Nombre);
    }

    [Fact]
    public async Task ModelLookupUsesStoredSnapshotAfterUpstreamFailure()
    {
        var upstream = new Mock<ICarQueryService>();
        upstream.SetupSequence(x => x.GetModelosAsync("1", null))
            .ReturnsAsync(new List<ModeloSimpleDto> { new("Corolla", "1") })
            .ThrowsAsync(new HttpRequestException());
        var catalogue = new HybridVehicleCatalogue(upstream.Object, new MemorySnapshotStore(), NullLogger<HybridVehicleCatalogue>.Instance);

        await catalogue.GetModelosAsync("1");
        Assert.Equal("Corolla", Assert.Single(await catalogue.GetModelosAsync("1")).Nombre);
    }

    [Fact]
    public async Task ExistingColorAndYearQueriesAreDelegated()
    {
        var upstream = new Mock<ICarQueryService>();
        upstream.Setup(x => x.GetColoresComunes()).ReturnsAsync(new List<string> { "Rojo" });
        upstream.Setup(x => x.GetYearsAsync("1")).ReturnsAsync((1980, 2026));
        var catalogue = new HybridVehicleCatalogue(upstream.Object, new MemorySnapshotStore(), NullLogger<HybridVehicleCatalogue>.Instance);

        Assert.Equal("Rojo", Assert.Single(await catalogue.GetColoresComunes()));
        Assert.Equal((1980, 2026), await catalogue.GetYearsAsync("1"));
    }

    [Fact]
    public async Task ApprovedLocalModelsAreMergedWithUpstream()
    {
        var upstream = new Mock<ICarQueryService>();
        upstream.Setup(x => x.GetModelosAsync("1", null)).ReturnsAsync(new List<ModeloSimpleDto> { new("Corolla", "1") });
        var approved = new Mock<IApprovedVehicleCatalogue>();
        approved.Setup(x => x.GetModelsAsync("1")).ReturnsAsync(new List<ModeloSimpleDto> { new("Nuevo", "1") });
        var catalogue = new HybridVehicleCatalogue(upstream.Object, new MemorySnapshotStore(), NullLogger<HybridVehicleCatalogue>.Instance, approved.Object);

        var result = await catalogue.GetModelosAsync("1");
        Assert.Equal(new[] { "Corolla", "Nuevo" }, result.Select(model => model.Nombre));
    }

    [Fact]
    public async Task DelayedOlderResponseCannotOverwriteNewerSnapshot()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var upstream = new Mock<ICarQueryService>();
        var calls = 0;
        upstream.Setup(x => x.GetMarcasAsync()).Returns(() => Interlocked.Increment(ref calls) switch
        {
            1 => Task.FromResult(new List<MarcaSimpleDto> { new("1", "Old") }),
            2 => Task.FromResult(new List<MarcaSimpleDto> { new("2", "New") }),
            _ => Task.FromResult(new List<MarcaSimpleDto>())
        });
        var store = new MemorySnapshotStore { BeforeWrite = async revision =>
        {
            if (revision == 1) { entered.SetResult(); await gate.Task; }
        }};
        var catalogue = new HybridVehicleCatalogue(upstream.Object, store, NullLogger<HybridVehicleCatalogue>.Instance);
        var old = catalogue.GetMarcasAsync();
        await entered.Task;
        await catalogue.GetMarcasAsync();
        gate.SetResult();
        await old;

        Assert.Equal("New", Assert.Single(await catalogue.GetMarcasAsync()).Nombre);
    }

    private sealed class MemorySnapshotStore : IVehicleCatalogueSnapshotStore
    {
        private readonly Dictionary<string, string> _values = new();
        private readonly Dictionary<string, long> _reserved = new();
        private readonly Dictionary<string, long> _saved = new();
        public Func<long, Task>? BeforeWrite { get; init; }
        public Task<string?> ReadAsync(string key) => Task.FromResult(_values.GetValueOrDefault(key));
        public Task<long> ReserveAsync(string key)
        {
            lock (_values) { var revision = _reserved.GetValueOrDefault(key) + 1; _reserved[key] = revision; return Task.FromResult(revision); }
        }
        public async Task WriteAsync(string key, string value, long revision)
        {
            if (BeforeWrite != null) await BeforeWrite(revision);
            lock (_values)
            {
                if (revision <= _saved.GetValueOrDefault(key)) return;
                _values[key] = value;
                _saved[key] = revision;
            }
        }
    }
}

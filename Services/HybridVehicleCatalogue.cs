using System.Text.Json;
using Firebase.Models.Dtos;

namespace Firebase.Services;

public interface IVehicleCatalogueSnapshotStore
{
    Task<string?> ReadAsync(string key);
    Task<long> ReserveAsync(string key);
    Task WriteAsync(string key, string value, long revision);
}

public interface IApprovedVehicleCatalogue
{
    Task<List<MarcaSimpleDto>> GetBrandsAsync(string? vehicleType);
    Task<List<ModeloSimpleDto>> GetModelsAsync(string brandId);
}

/// <summary>Uses the last nonempty persisted response when the upstream is unavailable.</summary>
public sealed class HybridVehicleCatalogue : ICarQueryService
{
    private readonly ICarQueryService _upstream;
    private readonly IVehicleCatalogueSnapshotStore _snapshots;
    private readonly ILogger<HybridVehicleCatalogue> _logger;
    private readonly IApprovedVehicleCatalogue? _approved;

    public HybridVehicleCatalogue(ICarQueryService upstream, IVehicleCatalogueSnapshotStore snapshots, ILogger<HybridVehicleCatalogue> logger, IApprovedVehicleCatalogue? approved = null)
    {
        _upstream = upstream;
        _snapshots = snapshots;
        _logger = logger;
        _approved = approved;
    }

    public async Task<List<MarcaSimpleDto>> GetMarcasAsync() =>
        await MergeBrands(await GetAsync("makes:all", _upstream.GetMarcasAsync), null);

    public async Task<List<MarcaSimpleDto>> GetMarcasPorTipoAsync(string tipoVehiculo) =>
        await MergeBrands(await GetAsync("makes:type:" + tipoVehiculo.ToUpperInvariant(), () => _upstream.GetMarcasPorTipoAsync(tipoVehiculo)), tipoVehiculo);

    public async Task<List<ModeloSimpleDto>> GetModelosAsync(string marcaId, int? year = null)
    {
        var result = (await GetAsync($"models:{marcaId}:{year}", () => _upstream.GetModelosAsync(marcaId, year))).ToList();
        if (_approved == null) return result;
        try
        {
            foreach (var item in await _approved.GetModelsAsync(marcaId))
                if (!result.Any(existing => VehicleNameMatcher.IsExact(existing.Nombre, item.Nombre))) result.Add(item);
        }
        catch (Exception exception) { _logger.LogWarning(exception, "Approved model lookup failed"); }
        return result.OrderBy(model => model.Nombre, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task<List<MarcaSimpleDto>> MergeBrands(List<MarcaSimpleDto> result, string? type)
    {
        if (_approved == null) return result;
        result = result.ToList();
        try
        {
            foreach (var item in await _approved.GetBrandsAsync(type))
                if (!result.Any(existing => VehicleNameMatcher.IsExact(existing.Nombre, item.Nombre))) result.Add(item);
        }
        catch (Exception exception) { _logger.LogWarning(exception, "Approved brand lookup failed"); }
        return result.OrderBy(brand => brand.Nombre, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public Task<(int minYear, int maxYear)> GetYearsAsync(string marcaId) => _upstream.GetYearsAsync(marcaId);

    public Task<List<string>> GetColoresComunes() => _upstream.GetColoresComunes();

    private async Task<List<T>> GetAsync<T>(string key, Func<Task<List<T>>> fetch)
    {
        List<T>? result = null;
        long? revision = null;
        try { revision = await _snapshots.ReserveAsync(key); }
        catch (Exception exception) { _logger.LogWarning(exception, "Vehicle catalogue snapshot reservation failed"); }
        try
        {
            result = await fetch();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Vehicle catalogue upstream lookup failed");
        }

        if (result is { Count: > 0 })
        {
            try { if (revision.HasValue) await _snapshots.WriteAsync(key, JsonSerializer.Serialize(result), revision.Value); }
            catch (Exception exception) { _logger.LogWarning(exception, "Vehicle catalogue snapshot write failed"); }
            return result;
        }

        try
        {
            var snapshot = await _snapshots.ReadAsync(key);
            return snapshot == null ? new List<T>() : JsonSerializer.Deserialize<List<T>>(snapshot) ?? new List<T>();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Vehicle catalogue snapshot read failed");
            return new List<T>();
        }
    }
}

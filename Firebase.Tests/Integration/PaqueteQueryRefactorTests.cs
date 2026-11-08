using Firebase.Models;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Firebase.Tests.Integration;

public class PaqueteQueryRefactorTests
{
    [LocalFirestoreFact]
    public async Task FiltersSearchAndPaginationUseDynamicValuesAndDefaultState()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-pquery-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var services = new ServicioService(db);
        var packages = new PaqueteServicioService(db, services, cache);
        foreach (var (id, type, vehicle, price, time) in new[]
        {
            ("a", "Exterior", "Auto", 100, 10), ("b", "Interior", "Auto", 200, 20),
            ("c", "Exterior", "Moto", 200, 20), ("d", "Interior", "Moto", 300, 30),
            ("e", "Cera", "Moto", 100, 10)
        })
            await db.Collection("servicios").Document(id).SetAsync(new Dictionary<string, object>
            {
                ["Nombre"] = id, ["Tipo"] = type, ["TipoVehiculo"] = vehicle, ["Precio"] = price,
                ["TiempoEstimado"] = time, ["Descripcion"] = "", ["Estado"] = "Activo"
            });
        var alpha = new PaqueteServicio { Id = "input", Nombre = "Alfa", Estado = "Activo", TipoVehiculo = "Auto", PorcentajeDescuento = 10, ServiciosIds = new() { "a", "b" } };
        var beta = new PaqueteServicio { Id = "input", Nombre = "Beta", Estado = "Activo", TipoVehiculo = "Moto", PorcentajeDescuento = 20, ServiciosIds = new() { "c", "d", "e" } };
        var gamma = new PaqueteServicio { Id = "input", Nombre = "Gamma", Estado = "Inactivo", TipoVehiculo = "Auto", PorcentajeDescuento = 10, ServiciosIds = new() { "a", "b" } };
        foreach (var package in new[] { alpha, beta, gamma }) await packages.CrearPaquete(package);

        var prices = await packages.CalcularPreciosAsync(new[] { alpha, beta });
        var times = await packages.CalcularTiemposAsync(new[] { alpha, beta });
        Assert.Equal(270m, prices[alpha.Id]); Assert.Equal(480m, prices[beta.Id]);
        Assert.Equal(30, times[alpha.Id]); Assert.Equal(60, times[beta.Id]);
        var states = new List<string>();
        Assert.Equal(new[] { alpha.Id, beta.Id }, (await packages.ObtenerPaquetes(states)).Select(p => p.Id));
        Assert.Equal(new[] { "Activo" }, states);
        Assert.Equal(new[] { gamma.Id }, (await packages.ObtenerPaquetes(new() { "Inactivo" })).Select(p => p.Id));
        Assert.Equal(new[] { beta.Id }, (await packages.ObtenerPaquetes(tiposVehiculo: new() { "Moto" })).Select(p => p.Id));

        foreach (var (minPrice, maxPrice, minTime, maxTime, minDiscount, maxDiscount, minCount, maxCount, expected) in new[]
        {
            (270m, 270m, 30, 30, 10m, 10m, 2, 2, alpha.Id),
            (480m, 480m, 60, 60, 20m, 20m, 3, 3, beta.Id)
        })
        {
            var rows = await packages.ObtenerPaquetes(precioMin: minPrice, precioMax: maxPrice, tiempoMin: minTime, tiempoMax: maxTime,
                descuentoMin: minDiscount, descuentoMax: maxDiscount, serviciosMin: minCount, serviciosMax: maxCount);
            Assert.Equal(expected, Assert.Single(rows).Id);
            Assert.Equal(1, await packages.ObtenerTotalPaginas(null!, null!, 1, minPrice, maxPrice, minTime, maxTime, minDiscount, maxDiscount, minCount, maxCount));
        }
        Assert.Empty(await packages.ObtenerPaquetes(precioMin: 271, precioMax: 479));
        Assert.Equal(0, await packages.ObtenerTotalPaginas(null!, null!, 1, precioMin: 271, precioMax: 479));
        Assert.Equal(new[] { beta.Id }, (await packages.ObtenerPaquetes(pageNumber: 1, pageSize: 1, sortBy: "Precio", sortOrder: "desc")).Select(p => p.Id));
        Assert.Equal(new[] { alpha.Id }, (await packages.ObtenerPaquetes(pageNumber: 2, pageSize: 1, sortBy: "Precio", sortOrder: "desc")).Select(p => p.Id));
        Assert.Equal(2, await packages.ObtenerTotalPaginas(null!, null!, 1));
        await Assert.ThrowsAsync<ArgumentException>(() => packages.ObtenerPaquetes(pageNumber: 0));

        foreach (var (term, expected) in new[]
        {
            ("30 min", alpha.Id), ("20%", beta.Id), ("480", beta.Id), ("alFA", alpha.Id),
            ("bad min", ""), ("bad%", "")
        })
        {
            var rows = await packages.BuscarPaquetes(term);
            Assert.Equal(expected == "" ? Array.Empty<string>() : new[] { expected }, rows.Select(p => p.Id));
            Assert.Equal(expected == "" ? 0 : 1, await packages.ObtenerTotalPaquetesBusqueda(term, null!, null!));
        }
        Assert.Equal(alpha.Id, Assert.Single(await packages.BuscarPaquetes("", pageNumber: 0, pageSize: 0)).Id);
        Assert.Equal<(decimal? min, decimal? max)>((270m, 480m), await packages.ObtenerRangoPrecio(null!, null!, null!));
        Assert.Equal<(decimal? min, decimal? max)>((10m, 20m), await packages.ObtenerRangoDescuento(null!, null!, null!));
    }
}

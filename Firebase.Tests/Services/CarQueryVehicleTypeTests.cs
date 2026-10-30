using System.Net;
using Firebase.Services;
using Firebase.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Firebase.Tests.Services;

public class CarQueryVehicleTypeTests
{
    [Theory]
    [InlineData("Camión", "GetMakesForVehicleType/Truck")]
    [InlineData("Motocicleta", "GetMakesForVehicleType/Motorcycle")]
    [InlineData("Ómnibus", "getallmakes")]
    public async Task UsesKnownCategoryOrGeneralCatalogue(string type, string expectedPath)
    {
        var handler = new RecordingHandler();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CarQueryService(new HttpClient(handler), cache, NullLogger<CarQueryService>.Instance);

        var brands = await service.GetMarcasPorTipoAsync(type);

        Assert.Contains(expectedPath, handler.Path, StringComparison.OrdinalIgnoreCase);
        Assert.Single(brands);
    }

    [Theory]
    [InlineData("Camión", "GetMakesForVehicleType/Truck")]
    [InlineData("Motocicleta", "GetMakesForVehicleType/Motorcycle")]
    [InlineData("Ómnibus", "getallmakes")]
    public async Task WebActionReturnsBrandsForVehicleType(string type, string expectedPath)
    {
        var handler = new RecordingHandler();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CarQueryService(new HttpClient(handler), cache, NullLogger<CarQueryService>.Instance);
        var controller = new VehiculoController(null!, null!, null!, null!, service, NullLogger<VehiculoController>.Instance);

        var result = Assert.IsType<JsonResult>(await controller.GetMarcasPorTipo(type));

        Assert.Contains(expectedPath, handler.Path, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("[{\"Id\":\"1\",\"Nombre\":\"Example\"}]", System.Text.Json.JsonSerializer.Serialize(result.Value));
        Assert.Null(result.StatusCode);
    }

    [Fact]
    public async Task WebActionRejectsEmptyType()
    {
        var controller = new VehiculoController(null!, null!, null!, null!, null!, NullLogger<VehiculoController>.Instance);
        var result = Assert.IsType<BadRequestObjectResult>(await controller.GetMarcasPorTipo(" "));
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("El parámetro tipoVehiculo es requerido", result.Value!.GetType().GetProperty("error")!.GetValue(result.Value));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string Path { get; private set; } = "";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Path = request.RequestUri!.AbsolutePath;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"Results\":[{\"MakeId\":1,\"MakeName\":\"Example\"}]}")
            });
        }
    }
}

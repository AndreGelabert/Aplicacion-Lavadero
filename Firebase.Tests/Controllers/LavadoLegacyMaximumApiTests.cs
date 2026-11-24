using System.Text.Json;
using Firebase.Models;
using FirebaseLoginCustom.Controllers;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace Firebase.Tests.Controllers;

public class LavadoLegacyMaximumApiTests
{
    [Fact]
    public async Task ConfigurationOmitsLegacyMaximumAndPreservesTimingValues()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set("system_configuration", new Configuracion { TiempoNotificacionMinutos = 21, TiempoToleranciaMinutos = 11, IntervaloPreguntas = 7, EmpleadosMaximosPorLavado = 9 });
        var db = new FirestoreDbBuilder { ProjectId = "demo-test", Endpoint = "localhost:8189", ChannelCredentials = Grpc.Core.ChannelCredentials.Insecure }.Build();
        var controller = Controller(new ConfiguracionService(db, cache));
        var json = JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(await controller.ObtenerConfiguracion()).Value);
        Assert.False(json.TryGetProperty("empleadosMaximosPorLavado", out _));
        Assert.Equal(21, json.GetProperty("tiempoNotificacionMinutos").GetInt32());
        Assert.Equal(11, json.GetProperty("tiempoToleranciaMinutos").GetInt32());
        Assert.Equal(7, json.GetProperty("intervaloPreguntas").GetInt32());
    }

    [Fact]
    public async Task EmployeeFallbackOmitsLegacyMaximumAndPreservesOtherFields()
    {
        var controller = Controller(null!);
        var json = JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(await controller.ObtenerEmpleadosDisponibles()).Value);
        Assert.False(json.TryGetProperty("empleadosMaximosPorLavado", out _));
        Assert.Equal(0, json.GetProperty("totalActivos").GetInt32());
        Assert.Equal(0, json.GetProperty("totalDisponibles").GetInt32());
        Assert.Empty(json.GetProperty("empleados").EnumerateArray());
    }

    private static LavadosController Controller(ConfiguracionService config) => new(
        NullLogger<LavadosController>.Instance, null!, null!, null!, null!, null!, null!, null!,
        config, null!, null!, null!, null!);
}

using System.Reflection;
using Firebase.Controllers;
using Firebase.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Firebase.Tests.Controllers;

public sealed class CotizacionProveedorControllerTests
{
    private sealed class RecordingLogger : ILogger<CotizacionProveedorController>
    {
        public List<string> Messages { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }

    [Fact]
    public void ControladorEsAllowAnonymousYExponeSoloDetalleYResponder()
    {
        Assert.NotNull(typeof(CotizacionProveedorController).GetCustomAttribute<AllowAnonymousAttribute>());
        var methods = typeof(CotizacionProveedorController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.Equal(2, methods.Length);
        Assert.Contains(methods, m => m.Name == nameof(CotizacionProveedorController.Detalle));
        Assert.Contains(methods, m => m.Name == nameof(CotizacionProveedorController.Responder));
    }

    [Fact]
    public async Task Detalle_ValidoEInvalido_Retorna200O400Generico()
    {
        var controller = new CotizacionProveedorController(
            (solId, token, _) =>
            {
                if (token == "valid")
                    return Task.FromResult(new DetalleCotizacionProveedor { NombreLavadero = "Lavadero X" });
                throw new CotizacionProveedorInvalidaException();
            },
            (_, _, _, _) => Task.CompletedTask);

        Assert.IsType<BadRequestResult>(await controller.Detalle(null));
        Assert.IsType<BadRequestResult>(await controller.Detalle(new CotizacionProveedorTokenRequest { SolicitudId = "s1", Token = " " }));
        Assert.IsType<BadRequestResult>(await controller.Detalle(new CotizacionProveedorTokenRequest { SolicitudId = "s1", Token = "bad" }));

        var ok = Assert.IsType<OkObjectResult>(await controller.Detalle(new CotizacionProveedorTokenRequest { SolicitudId = "s1", Token = "valid" }));
        var detail = Assert.IsType<DetalleCotizacionProveedor>(ok.Value);
        Assert.Equal("Lavadero X", detail.NombreLavadero);
    }

    [Fact]
    public async Task Responder_DatosInvalidosYCapacidadInvalida_Retorna400Adecuado()
    {
        var controller = new CotizacionProveedorController(
            (_, _, _) => Task.FromResult(new DetalleCotizacionProveedor()),
            (_, _, req, _) =>
            {
                if (req.PlazoEntrega == "bad-data")
                    throw new CotizacionProveedorDatosInvalidosException("Ingrese precios y plazo de entrega válidos para todos los ítems.");
                if (req.Token == "bad-cap")
                    throw new CotizacionProveedorInvalidaException();
                return Task.CompletedTask;
            });

        Assert.IsType<BadRequestResult>(await controller.Responder(null));
        Assert.IsType<BadRequestResult>(await controller.Responder(new RespuestaCotizacionProveedorRequest { SolicitudId = "s1", Token = "t", Items = new() }));

        var badData = Assert.IsType<BadRequestObjectResult>(await controller.Responder(new RespuestaCotizacionProveedorRequest
        {
            SolicitudId = "s1",
            Token = "t",
            PlazoEntrega = "bad-data",
            Items = new() { new RespuestaCotizacionProveedorItem { InsumoId = "i1", Precio = 10 } }
        }));
        Assert.Contains("Ingrese precios", badData.Value?.ToString());

        Assert.IsType<BadRequestResult>(await controller.Responder(new RespuestaCotizacionProveedorRequest
        {
            SolicitudId = "s1",
            Token = "bad-cap",
            PlazoEntrega = "2",
            Items = new() { new RespuestaCotizacionProveedorItem { InsumoId = "i1", Precio = 10 } }
        }));

        Assert.IsType<OkResult>(await controller.Responder(new RespuestaCotizacionProveedorRequest
        {
            SolicitudId = "s1",
            Token = "ok",
            PlazoEntrega = "2",
            Items = new() { new RespuestaCotizacionProveedorItem { InsumoId = "i1", Precio = 10 } }
        }));
    }

    [Fact]
    public async Task ErrorInesperado_Retorna503YSinFiltrarSecretosEnLog()
    {
        var logger = new RecordingLogger();
        var controller = new CotizacionProveedorController(
            (_, _, _) => throw new Exception("secret-db-detail"),
            (_, _, _, _) => throw new Exception("secret-token-leak"),
            logger);

        var r1 = Assert.IsType<StatusCodeResult>(await controller.Detalle(new CotizacionProveedorTokenRequest { SolicitudId = "s1", Token = "secret-token" }));
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, r1.StatusCode);

        var r2 = Assert.IsType<StatusCodeResult>(await controller.Responder(new RespuestaCotizacionProveedorRequest
        {
            SolicitudId = "s1",
            Token = "secret-token",
            PlazoEntrega = "1",
            Items = new() { new RespuestaCotizacionProveedorItem { InsumoId = "i1", Precio = 1 } }
        }));
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, r2.StatusCode);

        Assert.DoesNotContain(logger.Messages, m => m.Contains("secret-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CancellationToken_SePropagaYOperationCanceledNoSeEnmascaraComo503()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var controller = new CotizacionProveedorController(
            (_, _, ct) => { ct.ThrowIfCancellationRequested(); return Task.FromResult(new DetalleCotizacionProveedor()); },
            (_, _, _, ct) => { ct.ThrowIfCancellationRequested(); return Task.CompletedTask; })
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestAborted = cts.Token }
            }
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            controller.Detalle(new CotizacionProveedorTokenRequest { SolicitudId = "s1", Token = "t" }));
    }
}

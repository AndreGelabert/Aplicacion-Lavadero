using Firebase.Controllers;
using Firebase.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Firebase.Tests.Services;

public class AuditWriterContractTests
{
    [Fact]
    public async Task ReadOnlyConsumerAcceptsStrictWriterWithoutCallingIt()
    {
        var writer = new Mock<IAuditWriter>(MockBehavior.Strict);
        var controller = new TipoVehiculoController(null!, writer.Object);
        var result = Assert.IsType<JsonResult>(await controller.ObtenerFormato(" "));
        var payload = System.Text.Json.JsonSerializer.SerializeToElement(result.Value);
        Assert.False(payload.GetProperty("success").GetBoolean());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, payload.GetProperty("formato").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, payload.GetProperty("regex").ValueKind);
        writer.VerifyNoOtherCalls();
    }

    [Fact]
    public void WriterContractExposesOnlyExistingEmissionOperations()
    {
        var contract = typeof(AuditService).Assembly.GetType("Firebase.Services.IAuditWriter");
        Assert.NotNull(contract);
        Assert.True(contract.IsAssignableFrom(typeof(AuditService)));
        Assert.Equal(new[] { "LogEvent", "LogEvent", "LogSessionEvent" }, contract.GetMethods().Select(m => m.Name).OrderBy(n => n));
        Assert.Equal(new[] { 5, 5, 8 }, contract.GetMethods().Select(m => m.GetParameters().Length).OrderBy(n => n));
        foreach (var method in contract.GetMethods())
        {
            var original = typeof(AuditService).GetMethod(method.Name, method.GetParameters().Select(p => p.ParameterType).ToArray());
            Assert.NotNull(original);
            Assert.Equal(typeof(Task), original.ReturnType);
        }
    }

    [Fact]
    public void EmissionConsumersDependOnWriterContractInsteadOfQueryService()
    {
        var assembly = typeof(AuditService).Assembly;
        foreach (var name in new[] { "ClienteController", "ConfiguracionController", "LavadosController", "PaqueteServicioController",
            "PersonalController", "RolController", "ServicioController", "TipoDocumentoController", "TipoVehiculoController",
            "VehiculoController", "AuthenticationService", "SessionActivityMiddleware" })
        {
            var type = Assert.Single(assembly.GetTypes(), t => t.Name == name);
            var parameters = type.GetConstructors().SelectMany(c => c.GetParameters()).ToList();
            // Middleware receives scoped dependencies through Invoke rather than its constructor.
            parameters.AddRange(type.GetMethods().Where(m => m.Name is "Invoke" or "InvokeAsync").SelectMany(m => m.GetParameters()));
            Assert.DoesNotContain(parameters, p => p.ParameterType == typeof(AuditService));
            Assert.Contains(parameters, p => p.ParameterType.FullName == "Firebase.Services.IAuditWriter");
        }
    }
}

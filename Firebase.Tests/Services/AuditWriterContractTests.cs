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

    private sealed class LegacyWriter : IAuditWriter
    {
        public Task LogEvent(string userId, string userEmail, string action, string targetId, string targetType) => Task.CompletedTask;
        public Task LogEvent(string userId, string userEmail, string action, string targetId, string targetType,
            Dictionary<string, string> antes, Dictionary<string, string> despues, string nombreHistorico = null!) => Task.CompletedTask;
        public Task LogSessionEvent(string userId, string userEmail, string action, string targetId, string targetType) => Task.CompletedTask;
    }

    [Fact]
    public void WriterContractExposesOnlyExistingEmissionOperations()
    {
        var contract = typeof(AuditService).Assembly.GetType("Firebase.Services.IAuditWriter");
        Assert.NotNull(contract);
        Assert.True(contract.IsAssignableFrom(typeof(AuditService)));
        var methods = contract.GetMethods();
        Assert.Equal(new[] { "EnlistarEnTransaccion", "LogEvent", "LogEvent", "LogSessionEvent" }, methods.Select(m => m.Name).OrderBy(n => n));
        var legacy = methods.Where(m => m.Name is "LogEvent" or "LogSessionEvent").ToArray();
        Assert.Equal(new[] { 5, 8 }, legacy.Where(m => m.Name == "LogEvent").Select(m => m.GetParameters().Length).OrderBy(n => n));
        Assert.Equal(5, Assert.Single(legacy, m => m.Name == "LogSessionEvent").GetParameters().Length);
        foreach (var method in legacy)
        {
            var original = typeof(AuditService).GetMethod(method.Name, method.GetParameters().Select(p => p.ParameterType).ToArray());
            Assert.NotNull(original);
            Assert.Equal(typeof(Task), method.ReturnType);
            Assert.Equal(typeof(Task), original.ReturnType);
        }
        var transactional = Assert.Single(methods, m => m.Name == "EnlistarEnTransaccion");
        Assert.Equal(new[] { typeof(Google.Cloud.Firestore.Transaction), typeof(global::AuditLog) }, transactional.GetParameters().Select(p => p.ParameterType));
        Assert.Equal(typeof(void), transactional.ReturnType);
        var implementation = typeof(AuditService).GetMethod(transactional.Name, transactional.GetParameters().Select(p => p.ParameterType).ToArray());
        Assert.NotNull(implementation);
        Assert.Equal(typeof(void), implementation.ReturnType);
        IAuditWriter legacyWriter = new LegacyWriter();
        var error = Assert.Throws<NotSupportedException>(() => legacyWriter.EnlistarEnTransaccion(null!, null!));
        Assert.Equal("El escritor de auditoría no admite transacciones.", error.Message);
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

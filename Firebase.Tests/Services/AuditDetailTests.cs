using Firebase.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Firebase.Tests.Services;

public class AuditDetailTests
{
    [Fact]
    public void DetailRequiresAuthenticationAndAuditConsultPermissionAndGet()
    {
        Assert.NotEmpty(typeof(AuditoriaController).GetCustomAttributes(typeof(AuthorizeAttribute), true));
        var method = typeof(AuditoriaController).GetMethod(nameof(AuditoriaController.Detalle))!;
        var permission = Assert.Single(method.GetCustomAttributes(typeof(PermisoAttribute), true).Cast<PermisoAttribute>());
        Assert.Equal(new[] { "Auditoria.Consultar" }, permission.Permisos);
        Assert.NotEmpty(method.GetCustomAttributes(typeof(HttpGetAttribute), true));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("bad/id")]
    [InlineData(".")]
    [InlineData("..")]
    public async Task InvalidIdsReturn404WithoutBackend(string id)
    {
        var controller = new AuditoriaController(null!, null!, null!, null!, null!, null!, null!, null!, null!);
        Assert.IsType<NotFoundResult>(await controller.Detalle(id));
    }

    [Fact]
    public async Task BackendFailureReturnsGenericError()
    {
        var controller = new AuditoriaController(null!, null!, null!, null!, null!, null!, null!, null!, null!);
        var result = Assert.IsType<ObjectResult>(await controller.Detalle("valid"));
        Assert.Equal(500, result.StatusCode);
        Assert.Equal("No se pudo cargar el detalle de auditoría.", result.Value);
    }
}

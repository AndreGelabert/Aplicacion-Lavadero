using System.Security.Claims;
using Firebase.Controllers;
using Firebase.Models;
using Firebase.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Firebase.Tests.Services;

public class InsumosOperacionPermisosTests
{
    [Theory]
    [InlineData("Insumos.Crear", false)]
    [InlineData("Insumos.Modificar", true)]
    public async Task GuardadosRechazanPermisoDeOtraOperacionAntesDeMutar(string permiso, bool crear)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(PermisoClaim.Tipo, permiso) }, "test"));
        var controlador = new InsumosController(null!, null!, null!, null!, null!, null!, null!) {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } }
        };
        var insumo = new Insumo { Id = "x", Nombre = "original", Tipo = "tipo", UnidadMedida = "l" };
        var proveedor = new Proveedor { Id = "y", RazonSocial = "original" };
        Assert.IsType<ForbidResult>(await controlador.Guardar(insumo, crear));
        Assert.IsType<ForbidResult>(await controlador.GuardarProveedor(proveedor, crear));
        Assert.Equal("original", insumo.Nombre);
        Assert.Equal("original", proveedor.RazonSocial);
    }
}

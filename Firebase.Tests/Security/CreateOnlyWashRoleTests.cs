using System.Security.Claims;
using Firebase.Security;
using Firebase.Controllers;
using FirebaseLoginCustom.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Firebase.Tests.Security;

public class CreateOnlyWashRoleTests
{
    private static ClaimsPrincipal UserWith(string permission) => new(
        new ClaimsIdentity(new[] { new Claim(PermisoClaim.Tipo, permission) }, "test"));

    [Theory]
    [InlineData(typeof(LavadosController), "Lavados")]
    [InlineData(typeof(RolController), "Roles")]
    public void IndexAllowsConsultOrCreateButListingRemainsConsultOnly(Type controller, string module)
    {
        var index = controller.GetMethod("Index")!.GetCustomAttributes(typeof(PermisoAttribute), true).Cast<PermisoAttribute>().Single();
        Assert.Equal(new[] { module + ".Consultar", module + ".Crear" }, index.Permisos);
        foreach (var name in new[] { "SearchPartial", "TablePartial" })
        {
            var permission = controller.GetMethod(name)!.GetCustomAttributes(typeof(PermisoAttribute), true).Cast<PermisoAttribute>().Single();
            Assert.Equal(new[] { module + ".Consultar" }, permission.Permisos);
        }
    }

    [Fact]
    public async Task CreateOnlyRoleIndexDoesNotReadRoleStorage()
    {
        var controller = new RolController(null!, null!) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = UserWith("Roles.Crear") } } };
        var result = Assert.IsType<ViewResult>(await controller.Index(null));
        Assert.Empty(Assert.IsType<List<Firebase.Models.Rol>>(result.Model));
    }

    [Fact]
    public async Task CreateOnlyCannotFetchExistingRoleOrWashForm()
    {
        var roles = new RolController(null!, null!) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = UserWith("Roles.Crear") } } };
        var washes = new LavadosController(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = UserWith("Lavados.Crear") } } };
        Assert.IsType<ForbidResult>(await roles.FormPartial("existing"));
        Assert.IsType<ForbidResult>(await washes.FormPartial("existing"));
    }

    [Fact]
    public async Task CreateOnlyVehicleLookupRejectsBlankInputWithoutStorageAccess()
    {
        var controller = new LavadosController(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = UserWith("Lavados.Crear") } } };
        var result = Assert.IsType<JsonResult>(await controller.BuscarVehiculosPorPatente(" "));
        Assert.Empty(Assert.IsType<List<object>>(result.Value));
    }

    [Fact]
    public async Task CustomerLookupRequiresPlateForCreateOnlyUser()
    {
        var controller = new LavadosController(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = UserWith("Lavados.Crear") } } };
        var result = Assert.IsType<JsonResult>(await controller.ObtenerClientesVehiculo("existing"));
        Assert.Empty(Assert.IsType<List<object>>(result.Value));
    }

    [Theory]
    [InlineData("Lavados.Crear")]
    [InlineData("Lavados.Consultar")]
    public void WashCreationLookupsAcceptCreateOrConsult(string permission)
    {
        foreach (var name in new[] { "BuscarVehiculosPorPatente", "ObtenerClientesVehiculo", "ObtenerServiciosPorTipoVehiculo", "ObtenerPaquetesPorTipoVehiculo", "ObtenerEmpleadosPorTipoVehiculo", "ObtenerEmpleadosDisponibles", "ObtenerConfiguracion" })
        {
            var attribute = typeof(LavadosController).GetMethod(name)!.GetCustomAttributes(typeof(PermisoAttribute), true).Cast<PermisoAttribute>().Single();
            Assert.Contains(permission, attribute.Permisos);
        }
        var customerList = typeof(LavadosController).GetMethod("ObtenerClientes")!.GetCustomAttributes(typeof(PermisoAttribute), true).Cast<PermisoAttribute>().Single();
        Assert.Equal(new[] { "Lavados.Consultar" }, customerList.Permisos);
    }

    [Theory]
    [InlineData("Roles.Crear")]
    [InlineData("Roles.Consultar")]
    public async Task BlankRoleFormRemainsAvailable(string permission)
    {
        var controller = new RolController(null!, null!) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = UserWith(permission) } } };
        Assert.IsType<PartialViewResult>(await controller.FormPartial());
    }
}

using Firebase.Security;
using Firebase.Controllers;
using Xunit;

namespace Firebase.Tests.Security;

public class CreateOnlyCatalogueAbmTests
{
    [Fact]
    public void StandaloneVehicleOwnerLookupIsRemovedWithStandaloneCreation()
    {
        Assert.Null(typeof(VehiculoController).GetMethod("BuscarPropietarioPorDocumento"));
    }

    [Fact]
    public void EmbeddedVehicleFormRequiresClientPermissionAndVehicleCreatePermission()
    {
        var method = typeof(VehiculoController).GetMethod("FormPartialCliente");
        Assert.NotNull(method);
        var permission = (PermisoAttribute)method!.GetCustomAttributes(typeof(PermisoAttribute), true).Single();
        Assert.Equal(new[] { "Clientes.Crear", "Clientes.Modificar" }, permission.Permisos);
        Assert.True(VehiculoController.PuedeGestionarVehiculoDesdeCliente(UserWith("Clientes.Crear", "Vehiculos.Crear"), "Clientes.Crear"));
        Assert.True(VehiculoController.PuedeGestionarVehiculoDesdeCliente(UserWith("Clientes.Modificar", "Vehiculos.Crear"), "Clientes.Modificar"));
        Assert.False(VehiculoController.PuedeGestionarVehiculoDesdeCliente(UserWith("Clientes.Crear"), "Clientes.Crear"));
        Assert.False(VehiculoController.PuedeGestionarVehiculoDesdeCliente(UserWith("Vehiculos.Crear"), "Clientes.Crear"));
        Assert.Null(typeof(VehiculoController).GetMethod("CrearVehiculoAjax"));
    }

    [Theory]
    [InlineData("CrearClienteAjax", "Clientes.Crear")]
    [InlineData("ActualizarClienteAjax", "Clientes.Modificar")]
    public void CustomerVehicleWriteActionsRequireVehicleCreatePermission(string actionName, string clientPermission)
    {
        var method = typeof(ClienteController).GetMethod(actionName)!;
        var clientAuthorization = method.GetCustomAttributes(typeof(PermisoAttribute), true)
            .Cast<PermisoAttribute>().Single();

        Assert.Equal(new[] { clientPermission }, clientAuthorization.Permisos);
        Assert.True(ClienteController.PuedeGestionarVehiculosDesdeCliente(
            UserWith(clientPermission, "Vehiculos.Crear"), clientPermission));
        Assert.False(ClienteController.PuedeGestionarVehiculosDesdeCliente(
            UserWith(clientPermission), clientPermission));
    }

    [Theory]
    [InlineData(typeof(ServicioController), "Servicios.Consultar", "Servicios.Crear")]
    [InlineData(typeof(PaqueteServicioController), "Paquetes.Consultar", "Paquetes.Crear")]
    public void IndexAllowsConsultOrCreate(Type controller, string consult, string create)
    {
        var permission = controller.GetMethod("Index")!.GetCustomAttributes(typeof(PermisoAttribute), true)
            .Cast<PermisoAttribute>().Single();
        Assert.Equal(new[] { consult, create }, permission.Permisos);
    }

    [Fact]
    public void VehicleIndexRequiresAViewPermissionAndStandaloneCreationIsRemoved()
    {
        var indexPermission = typeof(VehiculoController).GetMethod("Index")!
            .GetCustomAttributes(typeof(PermisoAttribute), true)
            .Cast<PermisoAttribute>().Single();
        Assert.Equal(new[] { "Vehiculos.Consultar", "Vehiculos.Modificar", "Vehiculos.Eliminar" }, indexPermission.Permisos);
        Assert.Null(typeof(VehiculoController).GetMethod("CrearVehiculoAjax"));
    }

    [Fact]
    public void CustomerVehicleEndpointRequiresBothCustomerAndVehicleConsultation()
    {
        var permission = typeof(ClienteController).GetMethod("GetVehiculosCliente")!
            .GetCustomAttributes(typeof(PermisoAttribute), true)
            .Cast<PermisoAttribute>().Single();
        Assert.Equal(new[] { "Clientes.Consultar" }, permission.Permisos);
        Assert.False(ClienteController.PuedeConsultarVehiculosCliente(UserWith("Clientes.Consultar")));
        Assert.False(ClienteController.PuedeConsultarVehiculosCliente(UserWith("Vehiculos.Consultar")));
        Assert.True(ClienteController.PuedeConsultarVehiculosCliente(UserWith("Clientes.Consultar", "Vehiculos.Consultar")));
    }

    private static System.Security.Claims.ClaimsPrincipal UserWith(params string[] permissions) =>
        new(new System.Security.Claims.ClaimsIdentity(
            permissions.Select(permission => new System.Security.Claims.Claim("permiso", permission)), "test"));
}

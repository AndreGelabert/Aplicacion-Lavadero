using Xunit;

public class RolDependenciasTests
{
    [Theory]
    [InlineData("Servicios.Modificar")]
    [InlineData("Servicios.Eliminar")]
    [InlineData("Clientes.Crear")]
    [InlineData("Clientes.Eliminar")]
    [InlineData("Vehiculos.Eliminar")]
    public void ValidarPermisos_RechazaDependenciasFaltantes(string permiso)
    {
        Assert.NotNull(RolReglas.ValidarPermisos(new[] { permiso }));
    }

    [Fact]
    public void ValidarPermisos_AceptaDependenciasCompletas()
    {
        var permisos = new[] { "Clientes.Consultar", "Clientes.Crear", "Clientes.Modificar", "Clientes.Eliminar", "Vehiculos.Consultar", "Vehiculos.Crear", "Vehiculos.Eliminar" };
        Assert.Null(RolReglas.ValidarPermisos(permisos));
    }

    [Fact]
    public void ValidarPermisos_ExigeCrearVehiculoParaCrearCliente()
    {
        Assert.Equal("Para crear clientes debe seleccionar Crear vehículos.",
            RolReglas.ValidarPermisos(new[] { "Clientes.Consultar", "Clientes.Crear" }));
    }

    [Fact]
    public void ValidarPermisos_ExigeEliminarVehiculoParaEliminarCliente()
    {
        Assert.Equal("Para eliminar clientes debe seleccionar Eliminar vehículos.",
            RolReglas.ValidarPermisos(new[] { "Clientes.Consultar", "Clientes.Modificar", "Clientes.Eliminar" }));
    }

    [Fact]
    public void ValidarPermisos_ExigeModificarClienteParaEliminarVehiculo()
    {
        Assert.Equal("Para eliminar vehículos debe seleccionar Modificar clientes.",
            RolReglas.ValidarPermisos(new[] { "Clientes.Consultar", "Vehiculos.Consultar", "Vehiculos.Eliminar" }));
    }

    [Fact]
    public void ValidarPermisos_ConservaConsultaSinCreacion()
    {
        Assert.Null(RolReglas.ValidarPermisos(new[] { "Servicios.Consultar", "Servicios.Modificar", "Servicios.Eliminar" }));
    }
}

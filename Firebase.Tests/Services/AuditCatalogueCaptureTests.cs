using Firebase.Models;
using Xunit;

namespace Firebase.Tests.Services;

public class AuditCatalogueCaptureTests
{
    [Fact]
    public void PackageCapturesServicesPriceAndTime()
    {
        var p = new PaqueteServicio { Id = "p", Nombre = "Completo", Estado = "Activo", TipoVehiculo = "Auto", Precio = 120, TiempoEstimado = 30, PorcentajeDescuento = 10, ServiciosIds = new() { "s1", "s2" } };
        var snapshot = AuditCampos.CapturarPaquete(p);
        Assert.Equal("[\"s1\",\"s2\"]", snapshot["ServiciosIds"]);
        Assert.Equal("120", snapshot["Precio"]);
        Assert.Equal("30", snapshot["TiempoEstimado"]);
        Assert.Equal(7, snapshot.Count);
    }

    [Fact]
    public void ConfigurationCapturesOrdinaryFieldsOnly()
    {
        var snapshot = AuditCampos.CapturarConfiguracion(new Configuracion { NombreLavadero = "Lavadero", Ubicacion = "Centro", ConsiderarEmpleadosActivos = false, EmpleadosMaximosPorLavado = 4, TiempoNotificacionMinutos = 10, TiempoToleranciaMinutos = 20, IntervaloPreguntas = 3, HorariosOperacion = new() { ["Lunes"] = "CERRADO" } });
        Assert.Equal("Lavadero", snapshot["NombreLavadero"]);
        Assert.Equal("Centro", snapshot["Ubicacion"]);
        Assert.Equal("{\"Lunes\":\"CERRADO\"}", snapshot["HorariosOperacion"]);
        Assert.Equal("False", snapshot["ConsiderarEmpleadosActivos"]);
        Assert.Equal("4", snapshot["EmpleadosMaximosPorLavado"]);
        Assert.Equal("10", snapshot["TiempoNotificacionMinutos"]);
        Assert.Equal("20", snapshot["TiempoToleranciaMinutos"]);
        Assert.Equal("3", snapshot["IntervaloPreguntas"]);
        Assert.Equal(15, snapshot.Count);
    }
}

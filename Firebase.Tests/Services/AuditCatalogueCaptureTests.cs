using System;
using System.Linq;
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
        var config = new Configuracion
        {
            Id = "system_config",
            NombreLavadero = "Lavadero",
            Ubicacion = "Centro",
            ConsiderarEmpleadosActivos = false,
            EmpleadosMaximosPorLavado = 4,
            TiempoNotificacionMinutos = 10,
            TiempoToleranciaMinutos = 20,
            IntervaloPreguntas = 3,
            HorariosOperacion = new() { ["Lunes"] = "CERRADO" },
            PorcentajeAlertaStock = 25m,
            VigenciaCompraRecienteDias = 7,
            FechaActualizacion = DateTime.UtcNow,
            ActualizadoPor = "admin@lavadero.local"
        };

        var snapshot = AuditCampos.CapturarConfiguracion(config);

        Assert.Equal("Lavadero", snapshot["NombreLavadero"]);
        Assert.Equal("Centro", snapshot["Ubicacion"]);
        Assert.Equal("{\"Lunes\":\"CERRADO\"}", snapshot["HorariosOperacion"]);
        Assert.Equal("False", snapshot["ConsiderarEmpleadosActivos"]);
        Assert.Equal("4", snapshot["EmpleadosMaximosPorLavado"]);
        Assert.Equal("10", snapshot["TiempoNotificacionMinutos"]);
        Assert.Equal("20", snapshot["TiempoToleranciaMinutos"]);
        Assert.Equal("3", snapshot["IntervaloPreguntas"]);
        Assert.Equal("25", snapshot["PorcentajeAlertaStock"]);
        Assert.Equal("7", snapshot["VigenciaCompraRecienteDias"]);

        var expectedFields = new[]
        {
            "CancelacionAnticipadaDescuento",
            "CancelacionAnticipadaHorasMinimas",
            "CancelacionAnticipadaValidezDias",
            "PaquetesDescuentoStep",
            "CapacidadMaximaConcurrente",
            "SesionDuracionHoras",
            "SesionInactividadMinutos",
            "NombreLavadero",
            "Ubicacion",
            "HorariosOperacion",
            "ConsiderarEmpleadosActivos",
            "EmpleadosMaximosPorLavado",
            "TiempoNotificacionMinutos",
            "TiempoToleranciaMinutos",
            "IntervaloPreguntas",
            "PorcentajeAlertaStock",
            "VigenciaCompraRecienteDias"
        };

        Assert.Equal(expectedFields.OrderBy(x => x, StringComparer.Ordinal), snapshot.Keys.OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal(AuditCampos.Configuracion.OrderBy(x => x, StringComparer.Ordinal), snapshot.Keys.OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal(17, snapshot.Count);

        // Exclusión estricta de metadatos internos y datos sensibles
        Assert.DoesNotContain("Id", snapshot.Keys);
        Assert.DoesNotContain("FechaActualizacion", snapshot.Keys);
        Assert.DoesNotContain("ActualizadoPor", snapshot.Keys);
        Assert.DoesNotContain(snapshot.Keys, k => k.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
                                                  k.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
                                                  k.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
                                                  k.Contains("Credential", StringComparison.OrdinalIgnoreCase) ||
                                                  k.Contains("Key", StringComparison.OrdinalIgnoreCase));
    }
}

using Firebase.Models;
using Xunit;

namespace Firebase.Tests.Services;

public class AuditServiceCaptureTests
{
    [Fact]
    public void ServiceAllowlistMatchesCapturedFieldsIncludingStages()
    {
        var service = new Servicio { Id = "id", Nombre = "Lavado", Tipo = "Exterior", TipoVehiculo = "Auto", Descripcion = "Detalle", Estado = "Activo", TiempoEstimado = 10, Etapas = new() { new Etapa { Id = "stage", Nombre = "Enjuague" } } };
        var captured = AuditCampos.CapturarServicio(service);
        Assert.Equal(AuditCampos.Servicio.OrderBy(x => x), captured.Keys.OrderBy(x => x));
        Assert.Equal("[{\"Id\":\"stage\",\"Nombre\":\"Enjuague\"}]", captured["Etapas"]);
    }
}

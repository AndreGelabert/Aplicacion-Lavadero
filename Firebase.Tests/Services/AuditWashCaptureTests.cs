using Firebase.Models;
using Xunit;

namespace Firebase.Tests.Services;

public class AuditWashCaptureTests
{
    [Fact]
    public void WashSnapshotCapturesNestedValuesAndIsIndependentOfMutation()
    {
        var method = typeof(AuditCampos).GetMethod("CapturarLavado");
        Assert.NotNull(method);
        var wash = new Lavado { Id = "w", Estado = "EnProceso", ClienteId = "c", VehiculoId = "v", VehiculoPatente = "ABC", Precio = 100, Servicios = new() { new ServicioEnLavado { ServicioId = "s", Estado = "Pendiente", Etapas = new() { new EtapaEnLavado { EtapaId = "e", Estado = "Pendiente" } } } }, Pago = new PagoLavado() };
        var before = Assert.IsType<Dictionary<string, string>>(method!.Invoke(null, new object[] { wash }));
        wash.Servicios[0].Etapas[0].Estado = "Realizado";
        wash.Pago.MontoPagado = 50;
        var after = Assert.IsType<Dictionary<string, string>>(method.Invoke(null, new object[] { wash }));
        Assert.Contains("Pendiente", before["Servicios"]);
        Assert.Contains("Realizado", after["Servicios"]);
        Assert.Contains("\"MontoPagado\":0", before["Pago"]);
        Assert.Contains("\"MontoPagado\":50", after["Pago"]);
        Assert.Equal("ABC", before["VehiculoPatente"]);
        Assert.DoesNotContain("ClaveAsociacion", string.Join(" ", after.Values));
    }
}

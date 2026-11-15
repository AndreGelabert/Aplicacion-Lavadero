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

    [Fact]
    public void ProveedorAllowlistMatchesCapturedFieldsAndExcludesSensitiveKeys()
    {
        var proveedor = new Proveedor
        {
            Id = "prov-1",
            IdentificacionTributaria = "20-12345678-9",
            RazonSocial = "Química Sur",
            Contacto = "Carlos",
            EmailOrdenes = "compras@example.invalid",
            Estado = "Activo"
        };
        var captured = AuditCampos.CapturarProveedor(proveedor);
        Assert.Equal(AuditCampos.Proveedor.OrderBy(x => x), captured.Keys.OrderBy(x => x));
        Assert.DoesNotContain("Id", captured.Keys);
        Assert.DoesNotContain(captured.Keys, k => k.Contains("Password", StringComparison.OrdinalIgnoreCase) || k.Contains("Token", StringComparison.OrdinalIgnoreCase) || k.Contains("Secret", StringComparison.OrdinalIgnoreCase) || k.Contains("Clave", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("20-12345678-9", captured["IdentificacionTributaria"]);
        Assert.Equal("Química Sur", captured["RazonSocial"]);
        Assert.Equal("Carlos", captured["Contacto"]);
        Assert.Equal("compras@example.invalid", captured["EmailOrdenes"]);
        Assert.Equal("Activo", captured["Estado"]);
        Assert.Empty(AuditCampos.CapturarProveedor(null));
    }

    [Fact]
    public void InsumoAllowlistMatchesCapturedFieldsAndExcludesSensitiveKeys()
    {
        var insumo = new Insumo
        {
            Id = "ins-1",
            Nombre = "Cera Líquida",
            Tipo = "Quimico",
            UnidadMedida = "l",
            StockActual = 8m,
            StockMinimo = 2m,
            CantidadReabastecimiento = 10m,
            ReabastecimientoAutomatico = true,
            ProveedoresIds = new() { "p2", "p1" },
            Estado = "Activo"
        };
        var captured = AuditCampos.CapturarInsumo(insumo);
        Assert.Equal(AuditCampos.Insumo.OrderBy(x => x), captured.Keys.OrderBy(x => x));
        Assert.DoesNotContain("Id", captured.Keys);
        Assert.DoesNotContain(captured.Keys, k => k.Contains("Password", StringComparison.OrdinalIgnoreCase) || k.Contains("Token", StringComparison.OrdinalIgnoreCase) || k.Contains("Secret", StringComparison.OrdinalIgnoreCase) || k.Contains("Clave", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Cera Líquida", captured["Nombre"]);
        Assert.Equal("Quimico", captured["Tipo"]);
        Assert.Equal("l", captured["UnidadMedida"]);
        Assert.Equal("8", captured["StockActual"]);
        Assert.Equal("2", captured["StockMinimo"]);
        Assert.Equal("10", captured["CantidadReabastecimiento"]);
        Assert.Equal("True", captured["ReabastecimientoAutomatico"]);
        Assert.Equal("[\"p1\",\"p2\"]", captured["ProveedoresIds"]);
        Assert.Equal("Activo", captured["Estado"]);
        Assert.Empty(AuditCampos.CapturarInsumo(null));
    }

    [Fact]
    public void OrdenCompraAllowlistMatchesCapturedFieldsAndExcludesSensitiveKeys()
    {
        var orden = new OrdenCompra
        {
            Id = "ord-1",
            ProveedorId = "p1",
            FechaEmision = new DateTime(2025, 3, 4, 5, 6, 7, DateTimeKind.Utc),
            Estado = "PendienteAprobacion",
            GrupoCotizacionId = "g1",
            Items = new()
            {
                new OrdenCompraItem
                {
                    InsumoId = "i1",
                    NombreInsumo = "Shampoo",
                    Cantidad = 3m,
                    CantidadRecibida = 0m,
                    PrecioUnitario = 8.5m,
                    Disponible = true
                }
            },
            Notas = "Requiere confirmacion",
            MotivoCancelacion = ""
        };
        var captured = AuditCampos.CapturarOrdenCompra(orden);
        Assert.Equal(AuditCampos.OrdenCompra.OrderBy(x => x), captured.Keys.OrderBy(x => x));
        Assert.DoesNotContain("Id", captured.Keys);
        Assert.DoesNotContain(captured.Keys, k => k.Contains("Password", StringComparison.OrdinalIgnoreCase) || k.Contains("Token", StringComparison.OrdinalIgnoreCase) || k.Contains("Secret", StringComparison.OrdinalIgnoreCase) || k.Contains("Clave", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("p1", captured["ProveedorId"]);
        Assert.Equal("2025-03-04T05:06:07.0000000Z", captured["FechaEmision"]);
        Assert.Equal("PendienteAprobacion", captured["Estado"]);
        Assert.Equal("g1", captured["GrupoCotizacionId"]);
        Assert.Contains("Shampoo", captured["Items"]);
        Assert.Equal("Requiere confirmacion", captured["Notas"]);
        Assert.Equal("", captured["MotivoCancelacion"]);
        Assert.Empty(AuditCampos.CapturarOrdenCompra(null));
    }
}

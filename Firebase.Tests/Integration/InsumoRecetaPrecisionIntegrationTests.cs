using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;

namespace Firebase.Tests.Integration;

public sealed class InsumoRecetaPrecisionIntegrationTests
{
    private static FirestoreDb Db() => new FirestoreDbBuilder
    {
        ProjectId = "demo-precision-" + Guid.NewGuid().ToString("N")[..16],
        EmulatorDetection = EmulatorDetection.EmulatorOnly
    }.Build();

    [LocalFirestoreFact]
    public async Task DosisNoRepresentableSeRechazaSinEfectos()
    {
        await VerificarRechazo(1.0000000000000001m);
    }

    [LocalFirestoreFact]
    public async Task SaldoNoRepresentableSeRechazaSinEfectos()
    {
        // Ambos operandos son recuperables individualmente; 2 - 0.0000000000000001 no lo es.
        await VerificarRechazo(0.0000000000000001m);
    }

    private static async Task VerificarRechazo(decimal dosis)
    {
        var db = Db();
        var insumos = new InsumoService(db);
        var stock = new StockService(db);
        var insumo = new Insumo { Nombre = "Jabon", Tipo = "Quimico", UnidadMedida = "ml" };
        Assert.True((await insumos.Guardar(insumo, true)).EsExitoso);
        Assert.True((await stock.Ajustar(insumo.Id, 2m, true, "Ingreso", "actor")).EsExitoso);
        var movimientosAntes = (await stock.ObtenerMovimientos(insumo.Id)).Select(m => m.Id).OrderBy(x => x).ToArray();
        var auditoriasAntes = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents.Select(d => d.Id).OrderBy(x => x).ToArray();
        var reservasAntes = (await stock.ObtenerReservasInventario(new[] { insumo.Id }))[insumo.Id];

        var error = await Record.ExceptionAsync(() => stock.ConsumirReceta("precision", new[] { new RecetaInsumo { InsumoId = insumo.Id, DosisTeorica = dosis } }));

        Assert.Equal(2m, (await insumos.Obtener(insumo.Id))!.StockActual);
        Assert.Equal(movimientosAntes, (await stock.ObtenerMovimientos(insumo.Id)).Select(m => m.Id).OrderBy(x => x).ToArray());
        Assert.Equal(auditoriasAntes, (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents.Select(d => d.Id).OrderBy(x => x).ToArray());
        Assert.Equal(reservasAntes, (await stock.ObtenerReservasInventario(new[] { insumo.Id }))[insumo.Id]);
        var precision = Assert.IsType<InvalidOperationException>(error);
        Assert.Equal("La cantidad no puede persistirse sin pérdida de precisión.", precision.Message);
    }

    [LocalFirestoreFact]
    public async Task ConsumoLegacyValidoPreservaSaldoKardexEIdempotencia()
    {
        var db = Db();
        var insumos = new InsumoService(db);
        var stock = new StockService(db);
        var insumo = new Insumo { Nombre = "Jabon", Tipo = "Quimico", UnidadMedida = "ml" };
        Assert.True((await insumos.Guardar(insumo, true)).EsExitoso);
        Assert.True((await stock.Ajustar(insumo.Id, 10m, true, "Ingreso", "actor")).EsExitoso);
        var receta = new[] { new RecetaInsumo { InsumoId = insumo.Id, DosisTeorica = 2.5m } };
        await stock.ConsumirReceta("legacy", receta);
        await stock.ConsumirReceta("legacy", receta);
        Assert.Equal(7.5m, (await insumos.Obtener(insumo.Id))!.StockActual);
        var movimiento = Assert.Single((await stock.ObtenerMovimientos(insumo.Id)).Where(m => m.Tipo == nameof(TipoMovimientoStock.ConsumoEtapa)));
        Assert.Equal(-2.5m, movimiento.Cantidad);
        Assert.Equal(7.5m, movimiento.SaldoResultante);
    }
}

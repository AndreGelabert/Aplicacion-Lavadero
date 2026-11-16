using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Firebase.Tests.Integration;

public sealed class ReabastecimientoIntegrationTests
{
    private static async Task CrearOrdenCotizada(ComprasService compras, OrdenCompra orden)
    {
        var solicitud = new SolicitudCotizacion { ProveedorId = orden.ProveedorId, Items = orden.Items.Select(x => new OrdenCompraItem { InsumoId = x.InsumoId, Cantidad = x.Cantidad }).ToList() };
        var anteriores = (await compras.ObtenerSolicitudes()).Select(s => s.Id).ToHashSet();
        Assert.True((await compras.CrearSolicitudes(new() { solicitud.ProveedorId }, solicitud.Items, null)).EsExitoso);
        solicitud = Assert.Single((await compras.ObtenerSolicitudes()).Where(s => !anteriores.Contains(s.Id)));
        var respuesta = new CotizacionRespuesta { SolicitudId = solicitud.Id, PlazoEntrega = "2", Items = orden.Items.Select(x => new OrdenCompraItem { InsumoId = x.InsumoId, PrecioUnitario = x.PrecioUnitario }).ToList() };
        Assert.True((await compras.RegistrarRespuesta(respuesta)).EsExitoso);
        Assert.True((await compras.Adjudicar(solicitud.Id, respuesta.Id, new Sender())).EsExitoso);
        orden.Id = Assert.Single((await compras.ObtenerOrdenes()).Where(x => x.GrupoCotizacionId == solicitud.GrupoId)).Id;
    }

    private sealed class Sender : IEmailSender
    {
        public System.Collections.Concurrent.ConcurrentBag<EmailMessage> Messages { get; } = new();
        public Task<ResultadoEnvioEmail> EnviarAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.FromResult(new ResultadoEnvioEmail(true, "Aceptado"));
        }
    }

    [LocalFirestoreFact]
    public async Task AntecedentesSeleccionanCompraMasRecienteYDesempateEstable()
    {
        foreach (var empate in new[] { false, true })
        {
            var db = new FirestoreDbBuilder { ProjectId = "demo-reab-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
            var proveedores = new ProveedorService(db);
            var a = new Proveedor { RazonSocial = "A", IdentificacionTributaria = "1" };
            var b = new Proveedor { RazonSocial = "B", IdentificacionTributaria = "2" };
            Assert.True((await proveedores.Guardar(a, true)).EsExitoso);
            Assert.True((await proveedores.Guardar(b, true)).EsExitoso);
            var insumos = new InsumoService(db);
            var insumo = new Insumo { Nombre = "Jabon", StockMinimo = 1, CantidadReabastecimiento = 5, ReabastecimientoAutomatico = true, ProveedoresIds = new() { a.Id, b.Id } };
            Assert.True((await insumos.Guardar(insumo, true)).EsExitoso);
            var fecha = DateTime.UtcNow.AddDays(-1);
            foreach (var (id, proveedor, precio, cuando) in new[] { ("a", a.Id, 11m, fecha), ("b", b.Id, 22m, empate ? fecha : fecha.AddHours(1)) })
            {
                await db.Collection("ordenesCompra").Document(id).SetAsync(new OrdenCompra { Id = id, ProveedorId = proveedor, Estado = "Recibida", Items = new() { new() { InsumoId = insumo.Id, Cantidad = 2, PrecioUnitario = precio }, new() { InsumoId = "otro", Cantidad = 9, PrecioUnitario = 99 } } });
                await db.Collection("movimientosStock").Document(id).SetAsync(new MovimientoStock { Id = id, InsumoId = insumo.Id, ReferenciaId = id, Tipo = "RecepcionOrden", Cantidad = 2, Fecha = cuando });
            }
            var sender = new Sender();
            var stock = new StockService(db, sender, proveedores, NullLogger<StockService>.Instance);
            var compras = new ComprasService(db);
            await stock.EvaluarReabastecimiento(insumo.Id);
            var orden = Assert.Single(await compras.ObtenerOrdenes(), o => o.Estado == "PendienteAprobacion");
            Assert.Equal(empate ? a.Id : b.Id, orden.ProveedorId);
            var item = Assert.Single(orden.Items);
            Assert.Equal(insumo.Id, item.InsumoId);
            Assert.Equal(5, item.Cantidad);
            Assert.Equal(empate ? 11 : 22, item.PrecioUnitario);
            Assert.Empty(await compras.ObtenerSolicitudes());
            Assert.Empty(sender.Messages);
            Assert.Equal(0, (await insumos.Obtener(insumo.Id))!.StockActual);
            Assert.Equal(2, (await stock.ObtenerMovimientos(insumo.Id)).Count);
        }
    }

    [LocalFirestoreFact]
    public async Task VigenciaPrecioYProveedorInactivoDeterminanFallback()
    {
        foreach (var caso in new[] { "dentro", "fuera", "cero", "inactivo", "sinprecio", "sinrecepcion", "noasignado" })
        {
            var db = new FirestoreDbBuilder { ProjectId = "demo-reab-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
            var proveedores = new ProveedorService(db);
            var activo = new Proveedor { RazonSocial = "Activo", IdentificacionTributaria = "1" };
            var otro = new Proveedor { RazonSocial = "Otro", IdentificacionTributaria = "2" };
            Assert.True((await proveedores.Guardar(activo, true)).EsExitoso);
            Assert.True((await proveedores.Guardar(otro, true)).EsExitoso);
            if (caso == "inactivo") Assert.True((await proveedores.CambiarEstado(otro.Id, "Inactivo")).EsExitoso);
            var insumos = new InsumoService(db);
            var insumo = new Insumo { Nombre = "Jabon", StockMinimo = 1, CantidadReabastecimiento = 5, ReabastecimientoAutomatico = true, ProveedoresIds = caso == "inactivo" ? new() { activo.Id, otro.Id } : new() { activo.Id } };
            Assert.True((await insumos.Guardar(insumo, true)).EsExitoso);
            await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion { VigenciaCompraRecienteDias = caso == "cero" ? 0 : 2 });
            var proveedorId = caso is "inactivo" or "noasignado" ? otro.Id : activo.Id;
            await db.Collection("ordenesCompra").Document("ant").SetAsync(new OrdenCompra { Id = "ant", ProveedorId = proveedorId, Estado = "Recibida", Items = new() { new() { InsumoId = insumo.Id, Cantidad = 2, PrecioUnitario = caso == "sinprecio" ? 0 : 10 } } });
            if (caso != "sinrecepcion")
                await db.Collection("movimientosStock").Document("mov").SetAsync(new MovimientoStock { Id = "mov", InsumoId = insumo.Id, ReferenciaId = "ant", Tipo = "RecepcionOrden", Cantidad = 2, Fecha = DateTime.UtcNow.AddHours(caso == "fuera" ? -49 : -47) });
            var sender = new Sender();
            var stock = new StockService(db, sender, proveedores, NullLogger<StockService>.Instance);
            var compras = new ComprasService(db);
            await stock.EvaluarReabastecimiento(insumo.Id);
            var ordenes = await compras.ObtenerOrdenes();
            var solicitudes = await compras.ObtenerSolicitudes();
            if (caso == "dentro")
            {
                var orden = Assert.Single(ordenes, o => o.Estado == "PendienteAprobacion");
                Assert.Equal(activo.Id, orden.ProveedorId);
                Assert.Equal(10, Assert.Single(orden.Items).PrecioUnitario);
                Assert.Empty(solicitudes);
            }
            else
            {
                Assert.DoesNotContain(ordenes, o => o.Estado == "PendienteAprobacion");
                var solicitud = Assert.Single(solicitudes);
                Assert.Equal(activo.Id, solicitud.ProveedorId);
                Assert.Equal("PendienteAprobacion", solicitud.Estado);
                Assert.Equal(insumo.Id, Assert.Single(solicitud.Items).InsumoId);
            }
            Assert.Empty(sender.Messages);
            Assert.Equal(0, (await insumos.Obtener(insumo.Id))!.StockActual);
            Assert.Equal(caso == "sinrecepcion" ? 0 : 1, (await stock.ObtenerMovimientos(insumo.Id)).Count);
        }
    }

    [LocalFirestoreFact]
    public async Task CompraRecientePreparaUnaOrdenSinEnvioInclusoConEvaluacionesConcurrentes()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-reab-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var proveedores = new ProveedorService(db);
        var proveedor = new Proveedor { RazonSocial = "Proveedor", IdentificacionTributaria = "123", EmailOrdenes = "p@example.invalid" };
        Assert.True((await proveedores.Guardar(proveedor, true)).EsExitoso);
        var insumos = new InsumoService(db);
        var insumo = new Insumo { Nombre = "Jabon", StockMinimo = 1, CantidadReabastecimiento = 5, ReabastecimientoAutomatico = true, ProveedoresIds = new() { proveedor.Id } };
        Assert.True((await insumos.Guardar(insumo, true)).EsExitoso);
        var compras = new ComprasService(db);
        var anterior = new OrdenCompra { ProveedorId = proveedor.Id, Items = new() { new OrdenCompraItem { InsumoId = insumo.Id, Cantidad = 2, PrecioUnitario = 17 } } };
        await CrearOrdenCotizada(compras, anterior);
        Assert.True((await compras.Recibir(anterior.Id)).EsExitoso);
        var sender = new Sender();
        Assert.True((await compras.GuardarDirectiva(new DirectivaReabastecimiento { ModoEmision = "EnvioDirecto" })).EsExitoso);
        var stock = new StockService(db, sender, proveedores, NullLogger<StockService>.Instance);
        Assert.True((await stock.Ajustar(insumo.Id, 1, false, "uso", "usuario")).EsExitoso);
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => stock.EvaluarReabastecimiento(insumo.Id)));
        var orden = Assert.Single((await compras.ObtenerOrdenes()).Where(x => x.Estado == "PendienteAprobacion"));
        Assert.Equal(proveedor.Id, orden.ProveedorId);
        var item = Assert.Single(orden.Items);
        Assert.Equal(insumo.Id, item.InsumoId);
        Assert.Equal(5, item.Cantidad);
        Assert.Equal(17, item.PrecioUnitario);
        var solicitudAnterior = Assert.Single(await compras.ObtenerSolicitudes());
        Assert.Equal("Adjudicada", solicitudAnterior.Estado);
        Assert.Empty(sender.Messages);
        Assert.Equal(1, (await insumos.Obtener(insumo.Id))!.StockActual);
        Assert.Equal(2, (await stock.ObtenerMovimientos(insumo.Id)).Count);
    }

    [LocalFirestoreFact]
    public async Task OrdenesAbiertasImpidenNuevasSolicitudesYOrdenesCerradasNo()
    {
        foreach (var estado in new[] { "PendienteAprobacion", "RevisionManual", "Enviada", "Confirmada", "Despachada", "Recibida", "Cancelada" })
        {
            var db = new FirestoreDbBuilder { ProjectId = "demo-reab-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
            var proveedores = new ProveedorService(db);
            var proveedor = new Proveedor { RazonSocial = "Proveedor", IdentificacionTributaria = "123", EmailOrdenes = "p@example.invalid" };
            Assert.True((await proveedores.Guardar(proveedor, true)).EsExitoso);
            var insumos = new InsumoService(db);
            var insumo = new Insumo { Nombre = "Jabon", StockMinimo = 1, CantidadReabastecimiento = 5, ReabastecimientoAutomatico = true, ProveedoresIds = new() { proveedor.Id } };
            Assert.True((await insumos.Guardar(insumo, true)).EsExitoso);
            var compras = new ComprasService(db);
            var orden = new OrdenCompra { ProveedorId = proveedor.Id, Items = new() { new OrdenCompraItem { InsumoId = insumo.Id, Cantidad = 5 } } };
            await CrearOrdenCotizada(compras, orden);
            if (estado != "Enviada") await db.Collection("ordenesCompra").Document(orden.Id).UpdateAsync("Estado", estado);
            var previas = (await compras.ObtenerSolicitudes()).Select(s => s.Id).ToHashSet();
            Assert.Single(previas);
            var sender = new Sender();
            var stock = new StockService(db, sender, proveedores, NullLogger<StockService>.Instance);
            await stock.EvaluarReabastecimiento(insumo.Id);
            var abiertas = estado is "PendienteAprobacion" or "RevisionManual" or "Enviada" or "Confirmada" or "Despachada";
            var solicitudes = await compras.ObtenerSolicitudes();
            Assert.All(previas, id => Assert.Contains(solicitudes, s => s.Id == id && s.Estado == "Adjudicada"));
            Assert.Equal(abiertas ? 0 : 1, solicitudes.Count(s => !previas.Contains(s.Id)));
            Assert.Equal(previas.Count + (abiertas ? 0 : 1), solicitudes.Count);
            Assert.Empty(sender.Messages);
            Assert.Equal(0, (await insumos.Obtener(insumo.Id))!.StockActual);
            Assert.Empty(await stock.ObtenerMovimientos(insumo.Id));
        }
    }

    [LocalFirestoreFact]
    public async Task SinProveedoresValidosAlertaYNoCreaSolicitudes()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-reab-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var proveedores = new ProveedorService(db);
        var proveedor = new Proveedor { RazonSocial = "Proveedor", IdentificacionTributaria = "123", EmailOrdenes = "p@example.invalid" };
        Assert.True((await proveedores.Guardar(proveedor, true)).EsExitoso);
        Assert.True((await proveedores.CambiarEstado(proveedor.Id, "Inactivo")).EsExitoso);
        var insumos = new InsumoService(db);
        var insumo = new Insumo { Nombre = "Jabon", StockMinimo = 1, CantidadReabastecimiento = 5, ReabastecimientoAutomatico = true, ProveedoresIds = new() { proveedor.Id, "inexistente" } };
        Assert.True((await insumos.Guardar(insumo, true)).EsExitoso);
        var sender = new Sender();
        var compras = new ComprasService(db);
        Assert.True((await compras.GuardarDirectiva(new DirectivaReabastecimiento { ModoEmision = "EnvioDirecto" })).EsExitoso);
        var stock = new StockService(db, sender, proveedores, NullLogger<StockService>.Instance);
        await stock.EvaluarReabastecimiento(insumo.Id);
        Assert.Empty(await compras.ObtenerSolicitudes());
        Assert.Empty(sender.Messages);
        Assert.Single(await stock.ObtenerAlertasPendientes());
        Assert.Equal(0, (await insumos.Obtener(insumo.Id))!.StockActual);
        Assert.Empty(await stock.ObtenerMovimientos(insumo.Id));
    }

    [LocalFirestoreFact]
    public async Task ProveedoresMixtosSoloGeneranSolicitudYEnvioAlActivo()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-reab-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var proveedores = new ProveedorService(db);
        var activo = new Proveedor { RazonSocial = "Activo", IdentificacionTributaria = "1", EmailOrdenes = "activo@example.invalid" };
        var inactivo = new Proveedor { RazonSocial = "Inactivo", IdentificacionTributaria = "2", EmailOrdenes = "inactivo@example.invalid" };
        Assert.True((await proveedores.Guardar(activo, true)).EsExitoso);
        Assert.True((await proveedores.Guardar(inactivo, true)).EsExitoso);
        Assert.True((await proveedores.CambiarEstado(inactivo.Id, "Inactivo")).EsExitoso);
        var insumos = new InsumoService(db);
        var insumo = new Insumo { Nombre = "Jabon", StockMinimo = 1, CantidadReabastecimiento = 5, ReabastecimientoAutomatico = true, ProveedoresIds = new() { activo.Id, inactivo.Id, "inexistente" } };
        Assert.True((await insumos.Guardar(insumo, true)).EsExitoso);
        var compras = new ComprasService(db);
        Assert.True((await compras.GuardarDirectiva(new DirectivaReabastecimiento { ModoEmision = "EnvioDirecto" })).EsExitoso);
        var sender = new Sender();
        var stock = new StockService(db, sender, proveedores, NullLogger<StockService>.Instance);
        await stock.EvaluarReabastecimiento(insumo.Id);
        Assert.Equal(activo.Id, Assert.Single(await compras.ObtenerSolicitudes()).ProveedorId);
        Assert.Equal("activo@example.invalid", Assert.Single(sender.Messages).To);
        Assert.Empty(await stock.ObtenerAlertasPendientes());
        Assert.Equal(0, (await insumos.Obtener(insumo.Id))!.StockActual);
        Assert.Empty(await stock.ObtenerMovimientos(insumo.Id));
    }

    [LocalFirestoreFact]
    public async Task AdjudicacionYEvaluacionConcurrentesNoGeneranOtraSolicitudEnReintento()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-reab-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var proveedores = new ProveedorService(db);
        var proveedor = new Proveedor { RazonSocial = "Proveedor", IdentificacionTributaria = "123", EmailOrdenes = "proveedor@example.invalid" };
        Assert.True((await proveedores.Guardar(proveedor, true)).EsExitoso);
        var insumos = new InsumoService(db);
        var compras = new ComprasService(db);
        var stock = new StockService(db);
        for (int i = 0; i < 10; i++)
        {
            var insumo = new Insumo { Nombre = "Jabon" + i, StockMinimo = 1, CantidadReabastecimiento = 5, ReabastecimientoAutomatico = true, ProveedoresIds = new() { proveedor.Id } };
            Assert.True((await insumos.Guardar(insumo, true)).EsExitoso);
            var solicitud = new SolicitudCotizacion { ProveedorId = proveedor.Id, Items = new() { new OrdenCompraItem { InsumoId = insumo.Id, Cantidad = 5 } } };
            Assert.True((await compras.CrearSolicitudes(new() { proveedor.Id }, solicitud.Items, null)).EsExitoso);
            solicitud = Assert.Single((await compras.ObtenerSolicitudes()).Where(s => s.Items.Any(x => x.InsumoId == insumo.Id)));
            var respuesta = new CotizacionRespuesta { SolicitudId = solicitud.Id, PlazoEntrega = "2", Items = new() { new OrdenCompraItem { InsumoId = insumo.Id, PrecioUnitario = 5 } } };
            Assert.True((await compras.RegistrarRespuesta(respuesta)).EsExitoso);
            var previas = (await compras.ObtenerSolicitudes()).Where(s => s.Items.Any(x => x.InsumoId == insumo.Id)).Select(s => s.Id).ToHashSet();
            Assert.Single(previas);
            var adjudicar = compras.Adjudicar(solicitud.Id, respuesta.Id, new Sender());
            await Task.WhenAll(adjudicar, stock.EvaluarReabastecimiento(insumo.Id));
            Assert.True((await adjudicar).EsExitoso);
            var posteriores = (await compras.ObtenerSolicitudes()).Where(s => s.Items.Any(x => x.InsumoId == insumo.Id)).ToList();
            Assert.Contains(posteriores, s => s.Id == solicitud.Id && s.Estado == "Adjudicada");
            Assert.InRange(posteriores.Count(s => !previas.Contains(s.Id)), 0, 1);
            var ids = posteriores.Select(s => s.Id).OrderBy(id => id).ToArray();
            await stock.EvaluarReabastecimiento(insumo.Id);
            Assert.Equal(ids, (await compras.ObtenerSolicitudes()).Where(s => s.Items.Any(x => x.InsumoId == insumo.Id)).Select(s => s.Id).OrderBy(id => id).ToArray());
            Assert.Single((await compras.ObtenerOrdenes()).Where(o => o.Items.Any(x => x.InsumoId == insumo.Id)));
            Assert.Equal(0, (await insumos.Obtener(insumo.Id))!.StockActual);
            Assert.Empty(await stock.ObtenerMovimientos(insumo.Id));
        }
    }

    [LocalFirestoreFact]
    public async Task DesactivacionConcurrenteYEvaluacionNoGeneranMasSolicitudesEnReintento()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-reab-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var proveedores = new ProveedorService(db);
        var insumos = new InsumoService(db);
        var compras = new ComprasService(db);
        for (int i = 0; i < 10; i++)
        {
            var proveedor = new Proveedor { RazonSocial = "Proveedor" + i, IdentificacionTributaria = "id" + i };
            Assert.True((await proveedores.Guardar(proveedor, true)).EsExitoso);
            var insumo = new Insumo { Nombre = "Jabon" + i, StockMinimo = 1, CantidadReabastecimiento = 5, ReabastecimientoAutomatico = true, ProveedoresIds = new() { proveedor.Id } };
            Assert.True((await insumos.Guardar(insumo, true)).EsExitoso);
            var stock = new StockService(db);
            var desactivar = proveedores.CambiarEstado(proveedor.Id, "Inactivo");
            await Task.WhenAll(desactivar, stock.EvaluarReabastecimiento(insumo.Id));
            Assert.True((await desactivar).EsExitoso);
            Assert.Equal("Inactivo", (await proveedores.Obtener(proveedor.Id))!.Estado);
            var solicitudes = (await compras.ObtenerSolicitudes()).Where(s => s.Items.Any(x => x.InsumoId == insumo.Id)).ToList();
            Assert.InRange(solicitudes.Count, 0, 1);
            await stock.EvaluarReabastecimiento(insumo.Id);
            Assert.Equal(solicitudes.Count, (await compras.ObtenerSolicitudes()).Count(s => s.Items.Any(x => x.InsumoId == insumo.Id)));
            Assert.Equal(0, (await insumos.Obtener(insumo.Id))!.StockActual);
            Assert.Empty(await stock.ObtenerMovimientos(insumo.Id));
        }
    }

    [LocalFirestoreFact]
    public async Task EvaluacionesConcurrentesGeneranUnaSolaCompulsa()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-reab-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var sender = new Sender();
        var proveedores = new ProveedorService(db);
        var proveedor = new Proveedor { RazonSocial = "Proveedor", IdentificacionTributaria = "123", EmailOrdenes = "p@example.invalid" };
        Assert.True((await proveedores.Guardar(proveedor, true)).EsExitoso);
        var insumos = new InsumoService(db);
        var insumo = new Insumo { Nombre = "Jabon", StockMinimo = 1, CantidadReabastecimiento = 5, ReabastecimientoAutomatico = true, ProveedoresIds = new() { proveedor.Id } };
        Assert.True((await insumos.Guardar(insumo, true)).EsExitoso);
        var compras = new ComprasService(db);
        Assert.True((await compras.GuardarDirectiva(new DirectivaReabastecimiento { ModoEmision = "EnvioDirecto" })).EsExitoso);
        var stock = new StockService(db, sender, proveedores, NullLogger<StockService>.Instance);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => stock.EvaluarReabastecimiento(insumo.Id)));
        await stock.EvaluarReabastecimiento(insumo.Id);
        var solicitud = Assert.Single(await compras.ObtenerSolicitudes());
        Assert.Equal(proveedor.Id, solicitud.ProveedorId);
        Assert.Equal(insumo.Id, Assert.Single(solicitud.Items).InsumoId);
        Assert.Single(sender.Messages);
        Assert.Equal(0, (await insumos.Obtener(insumo.Id))!.StockActual);
        Assert.Empty(await stock.ObtenerMovimientos(insumo.Id));
    }

    [LocalFirestoreFact]
    public async Task ReabastecimientoAutomaticoRegistraAuditoriaTransaccionalConIdentidadSistema()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-reab-audit-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var proveedores = new ProveedorService(db);
        var proveedor = new Proveedor { RazonSocial = "Proveedor Audit", IdentificacionTributaria = "30-11111111-9", EmailOrdenes = "proveedor@example.invalid" };
        Assert.True((await proveedores.Guardar(proveedor, true)).EsExitoso);

        var insumos = new InsumoService(db);
        var insumo = new Insumo { Nombre = "Detergente Auto", StockMinimo = 15, CantidadReabastecimiento = 20, ReabastecimientoAutomatico = true, ProveedoresIds = new() { proveedor.Id } };
        Assert.True((await insumos.Guardar(insumo, true)).EsExitoso);

        var compras = new ComprasService(db);
        var anterior = new OrdenCompra { ProveedorId = proveedor.Id, Items = new() { new OrdenCompraItem { InsumoId = insumo.Id, Cantidad = 10, PrecioUnitario = 45m } } };
        await CrearOrdenCotizada(compras, anterior);
        Assert.True((await compras.Recibir(anterior.Id)).EsExitoso);

        var auditsPrevios = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents.Select(d => d.Id).ToHashSet();

        var sender = new Sender();
        var stock = new StockService(db, sender, proveedores, NullLogger<StockService>.Instance);
        await stock.EvaluarReabastecimiento(insumo.Id);

        var ordenes = await compras.ObtenerOrdenes();
        var orden = Assert.Single(ordenes, o => o.Estado == "PendienteAprobacion");
        Assert.Equal(proveedor.Id, orden.ProveedorId);
        Assert.Equal("El precio y la disponibilidad requieren confirmación del proveedor.", orden.Notas);
        var item = Assert.Single(orden.Items);
        Assert.Equal(insumo.Id, item.InsumoId);
        Assert.Equal(20, item.Cantidad);
        Assert.Equal(45m, item.PrecioUnitario);

        var todosAudits = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents;
        Assert.All(auditsPrevios, id => Assert.Contains(todosAudits, d => d.Id == id));
        Assert.Single(todosAudits.Where(d => !auditsPrevios.Contains(d.Id)));
        var audits = (await db.Collection("registros_auditoria").WhereEqualTo("TargetId", orden.Id).GetSnapshotAsync()).Documents;
        var ev = Assert.Single(audits);
        Assert.Equal("Creacion de orden de compra", ev.GetValue<string>("Action"));
        Assert.Equal("OrdenCompra", ev.GetValue<string>("TargetType"));
        Assert.Equal("sistema", ev.GetValue<string>("UserId"));
        Assert.Equal("sistema", ev.GetValue<string>("UserEmail"));
        Assert.Equal($"Orden {orden.Id}", ev.GetValue<string>("NombreHistorico"));

        var antes = ev.GetValue<Dictionary<string, string>>("Antes");
        Assert.NotNull(antes);
        Assert.Empty(antes);

        var despues = ev.GetValue<Dictionary<string, string>>("Despues");
        Assert.NotNull(despues);
        Assert.Equal("PendienteAprobacion", despues["Estado"]);
        Assert.Equal(proveedor.Id, despues["ProveedorId"]);
        Assert.Equal("El precio y la disponibilidad requieren confirmación del proveedor.", despues["Notas"]);
        Assert.Contains(insumo.Id, despues["Items"]);
    }

    [LocalFirestoreFact]
    public async Task ReabastecimientoFalloSimuladoEnAuditoriaRevierteCreacionDeOrdenYPermiteReintento()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-reab-rollback-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var proveedores = new ProveedorService(db);
        var proveedor = new Proveedor { RazonSocial = "Proveedor Rollback", IdentificacionTributaria = "30-22222222-9", EmailOrdenes = "p@example.invalid" };
        Assert.True((await proveedores.Guardar(proveedor, true)).EsExitoso);

        var insumos = new InsumoService(db);
        var insumo = new Insumo { Nombre = "Cera Rollback", StockMinimo = 10, CantidadReabastecimiento = 25, ReabastecimientoAutomatico = true, ProveedoresIds = new() { proveedor.Id } };
        Assert.True((await insumos.Guardar(insumo, true)).EsExitoso);

        var compras = new ComprasService(db);
        var anterior = new OrdenCompra { ProveedorId = proveedor.Id, Items = new() { new OrdenCompraItem { InsumoId = insumo.Id, Cantidad = 10, PrecioUnitario = 80m } } };
        await CrearOrdenCotizada(compras, anterior);
        Assert.True((await compras.Recibir(anterior.Id)).EsExitoso);

        var auditsPrevios = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents.Select(d => d.Id).OrderBy(id => id).ToArray();

        var stock = new StockService(db);
        stock.InterceptorAuditoria = _ => throw new InvalidOperationException("Fallo simulado en registro de auditoria.");

        await Assert.ThrowsAsync<InvalidOperationException>(() => stock.EvaluarReabastecimiento(insumo.Id));

        // Verificamos que la orden automatica no se creo en la base (rollback transaccional)
        var ordenesPostFallo = await compras.ObtenerOrdenes();
        Assert.DoesNotContain(ordenesPostFallo, o => o.Estado == "PendienteAprobacion");
        Assert.Single(ordenesPostFallo); // Solo la anterior recibida

        // Verificamos que no se crearon registros de auditoria
        var auditsPostFallo = await db.Collection("registros_auditoria").GetSnapshotAsync();
        Assert.Equal(auditsPrevios, auditsPostFallo.Documents.Select(d => d.Id).OrderBy(id => id).ToArray());

        // Reintento: eliminamos el fallo simulado y volvemos a evaluar
        stock.InterceptorAuditoria = null;
        await stock.EvaluarReabastecimiento(insumo.Id);

        var ordenesPostReintento = await compras.ObtenerOrdenes();
        var ordenReintentada = Assert.Single(ordenesPostReintento, o => o.Estado == "PendienteAprobacion");
        Assert.Equal(insumo.Id, Assert.Single(ordenReintentada.Items).InsumoId);

        var auditsPostReintento = await db.Collection("registros_auditoria").WhereEqualTo("TargetId", ordenReintentada.Id).GetSnapshotAsync();
        var audit = Assert.Single(auditsPostReintento.Documents);
        Assert.Equal("Creacion de orden de compra", audit.GetValue<string>("Action"));
        Assert.Equal("sistema", audit.GetValue<string>("UserId"));
        Assert.Equal("sistema", audit.GetValue<string>("UserEmail"));
    }

    [LocalFirestoreFact]
    public async Task ReabastecimientoAutomaticoConcurrenteGeneraUnaSolaOrdenYUnSoloRegistroDeAuditoria()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-reab-conc-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var proveedores = new ProveedorService(db);
        var proveedor = new Proveedor { RazonSocial = "Proveedor Concurrente", IdentificacionTributaria = "30-33333333-9", EmailOrdenes = "p@example.invalid" };
        Assert.True((await proveedores.Guardar(proveedor, true)).EsExitoso);

        var insumos = new InsumoService(db);
        var insumo = new Insumo { Nombre = "Shampoo Conc", StockMinimo = 10, CantidadReabastecimiento = 30, ReabastecimientoAutomatico = true, ProveedoresIds = new() { proveedor.Id } };
        Assert.True((await insumos.Guardar(insumo, true)).EsExitoso);

        var compras = new ComprasService(db);
        var anterior = new OrdenCompra { ProveedorId = proveedor.Id, Items = new() { new OrdenCompraItem { InsumoId = insumo.Id, Cantidad = 10, PrecioUnitario = 50m } } };
        await CrearOrdenCotizada(compras, anterior);
        Assert.True((await compras.Recibir(anterior.Id)).EsExitoso);

        var auditsPrevios = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents.Select(d => d.Id).ToHashSet();

        var stock = new StockService(db);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => stock.EvaluarReabastecimiento(insumo.Id)));
        await stock.EvaluarReabastecimiento(insumo.Id);

        var ordenes = await compras.ObtenerOrdenes();
        var ordenesPendientes = ordenes.Where(o => o.Estado == "PendienteAprobacion").ToList();
        var orden = Assert.Single(ordenesPendientes);
        Assert.Equal(proveedor.Id, orden.ProveedorId);

        var todosAudits = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents;
        Assert.All(auditsPrevios, id => Assert.Contains(todosAudits, d => d.Id == id));
        Assert.Single(todosAudits.Where(d => !auditsPrevios.Contains(d.Id)));
        var audits = await db.Collection("registros_auditoria").WhereEqualTo("TargetId", orden.Id).GetSnapshotAsync();
        var audit = Assert.Single(audits.Documents);
        Assert.Equal("Creacion de orden de compra", audit.GetValue<string>("Action"));
        Assert.Equal("sistema", audit.GetValue<string>("UserId"));
        Assert.Equal("sistema", audit.GetValue<string>("UserEmail"));
    }

    [LocalFirestoreFact]
    public async Task ReabastecimientoSinAntecedentesPreservaComportamientoDeCotizacionYSinAuditoriaDeOrden()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-reab-coti-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var proveedores = new ProveedorService(db);
        var proveedor = new Proveedor { RazonSocial = "Proveedor Coti", IdentificacionTributaria = "30-44444444-9", EmailOrdenes = "p@example.invalid" };
        Assert.True((await proveedores.Guardar(proveedor, true)).EsExitoso);

        var insumos = new InsumoService(db);
        var insumo = new Insumo { Nombre = "Aroma Coti", StockMinimo = 5, CantidadReabastecimiento = 10, ReabastecimientoAutomatico = true, ProveedoresIds = new() { proveedor.Id } };
        Assert.True((await insumos.Guardar(insumo, true)).EsExitoso);

        var compras = new ComprasService(db);
        Assert.True((await compras.GuardarDirectiva(new DirectivaReabastecimiento { ModoEmision = "RequiereAprobacion" })).EsExitoso);

        var stock = new StockService(db);
        await stock.EvaluarReabastecimiento(insumo.Id);

        // Se deben generar solicitudes de cotización, no órdenes de compra
        var solicitudes = await compras.ObtenerSolicitudes();
        var solicitud = Assert.Single(solicitudes);
        Assert.Equal(proveedor.Id, solicitud.ProveedorId);
        Assert.Equal("PendienteAprobacion", solicitud.Estado);

        var ordenes = await compras.ObtenerOrdenes();
        Assert.Empty(ordenes);

        var auditsOrden = await db.Collection("registros_auditoria").WhereEqualTo("TargetType", "OrdenCompra").GetSnapshotAsync();
        Assert.Empty(auditsOrden.Documents);
    }

    private sealed class ConfigurableSender : IEmailSender
    {
        public System.Collections.Concurrent.ConcurrentBag<EmailMessage> Messages { get; } = new();
        public bool DebeFallar { get; set; }
        public bool LanzarExcepcion { get; set; }

        public Task<ResultadoEnvioEmail> EnviarAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            if (LanzarExcepcion) throw new InvalidOperationException("Falla simulada de red en transporte");
            if (DebeFallar) return Task.FromResult(new ResultadoEnvioEmail(false, "Rechazo del transporte"));
            Messages.Add(message);
            return Task.FromResult(new ResultadoEnvioEmail(true, "Aceptado"));
        }
    }

    [LocalFirestoreFact]
    public async Task VigenciaPorDefecto14DiasSinDocumentoConfiguracionLimitesExactos()
    {
        foreach (var (dentro, delta) in new[] { (true, TimeSpan.FromDays(14) - TimeSpan.FromMinutes(5)), (false, TimeSpan.FromDays(14) + TimeSpan.FromMinutes(5)) })
        {
            var db = new FirestoreDbBuilder { ProjectId = "demo-vig14-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
            // Aseguramos que NO exista el documento de configuracion en Firestore
            var configSnap = await db.Collection("configuracion").Document("system_config").GetSnapshotAsync();
            Assert.False(configSnap.Exists);

            var proveedores = new ProveedorService(db);
            var proveedor = new Proveedor { RazonSocial = "Proveedor 14d", IdentificacionTributaria = "30-55555555-9", EmailOrdenes = "p@example.invalid" };
            Assert.True((await proveedores.Guardar(proveedor, true)).EsExitoso);

            var insumos = new InsumoService(db);
            var insumo = new Insumo { Nombre = "Cera 14d", StockMinimo = 5, CantidadReabastecimiento = 10, ReabastecimientoAutomatico = true, ProveedoresIds = new() { proveedor.Id } };
            Assert.True((await insumos.Guardar(insumo, true)).EsExitoso);

            var fechaRecepcion = DateTime.UtcNow - delta;
            await db.Collection("ordenesCompra").Document("ord-ant").SetAsync(new OrdenCompra
            {
                Id = "ord-ant",
                ProveedorId = proveedor.Id,
                Estado = "Recibida",
                Items = new() { new() { InsumoId = insumo.Id, Cantidad = 10, PrecioUnitario = 25m } }
            });
            await db.Collection("movimientosStock").Document("mov-ant").SetAsync(new MovimientoStock
            {
                Id = "mov-ant",
                InsumoId = insumo.Id,
                ReferenciaId = "ord-ant",
                Tipo = "RecepcionOrden",
                Cantidad = 10,
                Fecha = fechaRecepcion
            });

            var sender = new ConfigurableSender();
            var stock = new StockService(db, sender, proveedores, NullLogger<StockService>.Instance);
            var compras = new ComprasService(db);

            await stock.EvaluarReabastecimiento(insumo.Id);

            var ordenes = await compras.ObtenerOrdenes();
            var solicitudes = await compras.ObtenerSolicitudes();

            if (dentro)
            {
                // Dentro del límite de 14 días (13 días 23h 55m): se omite cotización y se prepara orden para aprobación
                var orden = Assert.Single(ordenes, o => o.Estado == "PendienteAprobacion");
                Assert.Equal(proveedor.Id, orden.ProveedorId);
                Assert.Equal("El precio y la disponibilidad requieren confirmación del proveedor.", orden.Notas);
                var item = Assert.Single(orden.Items);
                Assert.Equal(insumo.Id, item.InsumoId);
                Assert.Equal(10, item.Cantidad);
                Assert.Equal(25m, item.PrecioUnitario);
                Assert.Empty(solicitudes);
                Assert.Empty(sender.Messages); // No se envía antes de aprobación
            }
            else
            {
                // Fuera del límite de 14 días (14 días 5m): cotiza primero
                Assert.DoesNotContain(ordenes, o => o.Estado == "PendienteAprobacion");
                var solicitud = Assert.Single(solicitudes);
                Assert.Equal(proveedor.Id, solicitud.ProveedorId);
                Assert.Equal(insumo.Id, Assert.Single(solicitud.Items).InsumoId);
                Assert.Empty(sender.Messages);
            }
        }
    }

    [LocalFirestoreFact]
    public async Task VigenciaConfigurableLimitesExactosEnLimitesYValoresExtremos()
    {
        // Probamos vigencias configurables: 7 días, 30 días, 0 días y -1 (desactivado)
        var casos = new (int vigenciaDias, TimeSpan delta, bool esperaOrden)[]
        {
            (7, TimeSpan.FromDays(7) - TimeSpan.FromMinutes(5), true),
            (7, TimeSpan.FromDays(7) + TimeSpan.FromMinutes(5), false),
            (30, TimeSpan.FromDays(30) - TimeSpan.FromMinutes(5), true),
            (30, TimeSpan.FromDays(30) + TimeSpan.FromMinutes(5), false),
            (0, TimeSpan.FromMinutes(10), false),
            (-1, TimeSpan.FromDays(1), false)
        };

        foreach (var (vigenciaDias, delta, esperaOrden) in casos)
        {
            var db = new FirestoreDbBuilder { ProjectId = "demo-vigcfg-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
            await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion { VigenciaCompraRecienteDias = vigenciaDias });

            var proveedores = new ProveedorService(db);
            var proveedor = new Proveedor { RazonSocial = "Proveedor Cfg", IdentificacionTributaria = "30-66666666-9", EmailOrdenes = "p@example.invalid" };
            Assert.True((await proveedores.Guardar(proveedor, true)).EsExitoso);

            var insumos = new InsumoService(db);
            var insumo = new Insumo { Nombre = "Shampoo Cfg", StockMinimo = 5, CantidadReabastecimiento = 8, ReabastecimientoAutomatico = true, ProveedoresIds = new() { proveedor.Id } };
            Assert.True((await insumos.Guardar(insumo, true)).EsExitoso);

            var fechaRecepcion = DateTime.UtcNow - delta;
            await db.Collection("ordenesCompra").Document("ord-ant").SetAsync(new OrdenCompra
            {
                Id = "ord-ant",
                ProveedorId = proveedor.Id,
                Estado = "Recibida",
                Items = new() { new() { InsumoId = insumo.Id, Cantidad = 8, PrecioUnitario = 40m } }
            });
            await db.Collection("movimientosStock").Document("mov-ant").SetAsync(new MovimientoStock
            {
                Id = "mov-ant",
                InsumoId = insumo.Id,
                ReferenciaId = "ord-ant",
                Tipo = "RecepcionOrden",
                Cantidad = 8,
                Fecha = fechaRecepcion
            });

            var sender = new ConfigurableSender();
            var stock = new StockService(db, sender, proveedores, NullLogger<StockService>.Instance);
            var compras = new ComprasService(db);

            await stock.EvaluarReabastecimiento(insumo.Id);

            var ordenes = await compras.ObtenerOrdenes();
            var solicitudes = await compras.ObtenerSolicitudes();

            if (esperaOrden)
            {
                var orden = Assert.Single(ordenes, o => o.Estado == "PendienteAprobacion");
                Assert.Equal(proveedor.Id, orden.ProveedorId);
                Assert.Equal(40m, Assert.Single(orden.Items).PrecioUnitario);
                Assert.Empty(solicitudes);
            }
            else
            {
                Assert.DoesNotContain(ordenes, o => o.Estado == "PendienteAprobacion");
                var solicitud = Assert.Single(solicitudes);
                Assert.Equal(proveedor.Id, solicitud.ProveedorId);
            }
        }
    }

    [LocalFirestoreFact]
    public async Task EleccionAntecedenteMasRecienteConMultiplesProveedoresEInactivos()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-ant-mult-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var proveedores = new ProveedorService(db);
        var p1Activo = new Proveedor { RazonSocial = "P1 Activo", IdentificacionTributaria = "30-1111-1", EmailOrdenes = "p1@example.invalid" };
        var p2Inactivo = new Proveedor { RazonSocial = "P2 Inactivo", IdentificacionTributaria = "30-2222-2", EmailOrdenes = "p2@example.invalid" };
        var p3Activo = new Proveedor { RazonSocial = "P3 Activo", IdentificacionTributaria = "30-3333-3", EmailOrdenes = "p3@example.invalid" };
        Assert.True((await proveedores.Guardar(p1Activo, true)).EsExitoso);
        Assert.True((await proveedores.Guardar(p2Inactivo, true)).EsExitoso);
        Assert.True((await proveedores.Guardar(p3Activo, true)).EsExitoso);
        // Desactivamos P2
        Assert.True((await proveedores.CambiarEstado(p2Inactivo.Id, "Inactivo")).EsExitoso);

        var insumos = new InsumoService(db);
        var insumo = new Insumo { Nombre = "Microfibra", StockMinimo = 2, CantidadReabastecimiento = 15, ReabastecimientoAutomatico = true, ProveedoresIds = new() { p1Activo.Id, p2Inactivo.Id, p3Activo.Id } };
        Assert.True((await insumos.Guardar(insumo, true)).EsExitoso);

        var ahora = DateTime.UtcNow;
        // P2 (Inactivo) tiene la compra más reciente: 1 día atrás, precio 10m
        await db.Collection("ordenesCompra").Document("ord-p2").SetAsync(new OrdenCompra
        {
            Id = "ord-p2", ProveedorId = p2Inactivo.Id, Estado = "Recibida",
            Items = new() { new() { InsumoId = insumo.Id, Cantidad = 10, PrecioUnitario = 10m } }
        });
        await db.Collection("movimientosStock").Document("mov-p2").SetAsync(new MovimientoStock
        {
            Id = "mov-p2", InsumoId = insumo.Id, ReferenciaId = "ord-p2", Tipo = "RecepcionOrden", Cantidad = 10, Fecha = ahora.AddDays(-1)
        });

        // P1 (Activo) tiene compra de 3 días atrás, precio 15m
        await db.Collection("ordenesCompra").Document("ord-p1").SetAsync(new OrdenCompra
        {
            Id = "ord-p1", ProveedorId = p1Activo.Id, Estado = "Recibida",
            Items = new() { new() { InsumoId = insumo.Id, Cantidad = 10, PrecioUnitario = 15m } }
        });
        await db.Collection("movimientosStock").Document("mov-p1").SetAsync(new MovimientoStock
        {
            Id = "mov-p1", InsumoId = insumo.Id, ReferenciaId = "ord-p1", Tipo = "RecepcionOrden", Cantidad = 10, Fecha = ahora.AddDays(-3)
        });

        // P3 (Activo) tiene compra de 5 días atrás, precio 20m
        await db.Collection("ordenesCompra").Document("ord-p3").SetAsync(new OrdenCompra
        {
            Id = "ord-p3", ProveedorId = p3Activo.Id, Estado = "Recibida",
            Items = new() { new() { InsumoId = insumo.Id, Cantidad = 10, PrecioUnitario = 20m } }
        });
        await db.Collection("movimientosStock").Document("mov-p3").SetAsync(new MovimientoStock
        {
            Id = "mov-p3", InsumoId = insumo.Id, ReferenciaId = "ord-p3", Tipo = "RecepcionOrden", Cantidad = 10, Fecha = ahora.AddDays(-5)
        });

        var sender = new ConfigurableSender();
        var stock = new StockService(db, sender, proveedores, NullLogger<StockService>.Instance);
        var compras = new ComprasService(db);

        await stock.EvaluarReabastecimiento(insumo.Id);

        var ordenes = await compras.ObtenerOrdenes();
        var orden = Assert.Single(ordenes, o => o.Estado == "PendienteAprobacion");

        // El antecedente de P2 (inactivo) DEBE ser descartado.
        // Entre P1 (3 días) y P3 (5 días), P1 es el más reciente de los activos.
        Assert.Equal(p1Activo.Id, orden.ProveedorId);
        var item = Assert.Single(orden.Items);
        Assert.Equal(15m, item.PrecioUnitario); // Precio de P1 como referencia
        Assert.Equal(15, item.Cantidad);
        Assert.Empty(await compras.ObtenerSolicitudes());
    }

    [LocalFirestoreFact]
    public async Task FlujoS22AusenciaDeDespachoEdicionDePrecioCantidadYResultadoIncierto()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-s22-flow-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var proveedores = new ProveedorService(db);
        var proveedor = new Proveedor { RazonSocial = "Proveedor Flow", IdentificacionTributaria = "30-77777777-9", EmailOrdenes = "flow@example.invalid" };
        Assert.True((await proveedores.Guardar(proveedor, true)).EsExitoso);

        var insumos = new InsumoService(db);
        var insumo = new Insumo { Nombre = "Desengrasante", StockMinimo = 15, CantidadReabastecimiento = 20, ReabastecimientoAutomatico = true, ProveedoresIds = new() { proveedor.Id } };
        Assert.True((await insumos.Guardar(insumo, true)).EsExitoso);

        var compras = new ComprasService(db);
        // Directiva con EnvioDirecto para probar que aún con EnvioDirecto, la orden NO se despacha automáticamente
        Assert.True((await compras.GuardarDirectiva(new DirectivaReabastecimiento { ModoEmision = "EnvioDirecto" })).EsExitoso);

        // Compra previa elegible
        var anterior = new OrdenCompra { ProveedorId = proveedor.Id, Items = new() { new OrdenCompraItem { InsumoId = insumo.Id, Cantidad = 10, PrecioUnitario = 50m } } };
        await CrearOrdenCotizada(compras, anterior);
        Assert.True((await compras.Recibir(anterior.Id)).EsExitoso);

        var sender = new ConfigurableSender();
        var stock = new StockService(db, sender, proveedores, NullLogger<StockService>.Instance);

        // 1. Reabastecimiento automático genera la orden en PendienteAprobacion SIN envío previo
        await stock.EvaluarReabastecimiento(insumo.Id);

        var ordenes = await compras.ObtenerOrdenes();
        var orden = Assert.Single(ordenes, o => o.Estado == "PendienteAprobacion");
        Assert.Equal(proveedor.Id, orden.ProveedorId);
        Assert.Equal("El precio y la disponibilidad requieren confirmación del proveedor.", orden.Notas);
        Assert.Empty(sender.Messages); // Ausencia de despacho previo comprobada

        // 2. Revisión y modificación de cantidad y precio por un humano
        var editRes = await compras.EditarOrdenPendienteConAuditoria(orden.Id, insumo.Id, 25m, 48m, "admin1", "admin@lavadero.com");
        Assert.True(editRes.Resultado.EsExitoso);
        Assert.Equal("Orden actualizada. El precio y la disponibilidad requieren confirmación del proveedor.", editRes.Resultado.MensajeExito);

        var ordenEditada = (await db.Collection("ordenesCompra").Document(orden.Id).GetSnapshotAsync()).ConvertTo<OrdenCompra>();
        var itemEditado = Assert.Single(ordenEditada.Items);
        Assert.Equal(25m, itemEditado.Cantidad);
        Assert.Equal(48m, itemEditado.PrecioUnitario);

        // 3. Aprobación con resultado de envío incierto (falla en transporte)
        sender.DebeFallar = true;
        var aprobarRes = await compras.AprobarOrdenConAuditoria(orden.Id, sender, "admin1", "admin@lavadero.com");
        Assert.False(aprobarRes.Resultado.EsExitoso);
        Assert.True(aprobarRes.TransicionARevisionManual);
        Assert.Equal("Resultado incierto. Se requiere revisión manual.", aprobarRes.Resultado.MensajeError);

        // La orden permanece en RevisionManual
        var ordenPostFallo = (await db.Collection("ordenesCompra").Document(orden.Id).GetSnapshotAsync()).ConvertTo<OrdenCompra>();
        Assert.Equal("RevisionManual", ordenPostFallo.Estado);

        // 4. Bloqueo de reenvío automático y de cambios de estado arbitrarios
        // Reintentar aprobación directamente debe ser rechazado
        var reintentoAprobar = await compras.AprobarOrdenConAuditoria(orden.Id, sender, "admin1", "admin@lavadero.com");
        Assert.False(reintentoAprobar.Resultado.EsExitoso);
        Assert.Contains("La orden no está pendiente de aprobación; revise manualmente el resultado del envío.", reintentoAprobar.Resultado.MensajeError);

        // Intentar cambiar estado a Enviada directamente debe ser rechazado
        var cambioEstado = await compras.CambiarEstadoOrdenConAuditoria(orden.Id, "Enviada", null, "admin1", "admin@lavadero.com");
        Assert.False(cambioEstado.Resultado.EsExitoso);
        Assert.Contains("Se requiere revisión manual del resultado del envío.", cambioEstado.Resultado.MensajeError);

        // El sender no registró ningún envío exitoso
        Assert.Empty(sender.Messages);

        // 5. Aprobación exitosa en caso normal (sin fallo): demuestra mensaje sin afirmar entrega ni recepción
        var ordenNormal = new OrdenCompra { ProveedorId = proveedor.Id, Estado = "PendienteAprobacion", Items = new() { new() { InsumoId = insumo.Id, Cantidad = 5, PrecioUnitario = 50m } } };
        await db.Collection("ordenesCompra").Document("ord-norm").SetAsync(ordenNormal);
        sender.DebeFallar = false;
        var aprobarNorm = await compras.AprobarOrdenConAuditoria("ord-norm", sender, "admin1", "admin@lavadero.com");
        Assert.True(aprobarNorm.Resultado.EsExitoso);
        Assert.Equal("El transporte aceptó la solicitud; esto no confirma entrega ni lectura.", aprobarNorm.Resultado.MensajeExito);
        var ordenEnviada = (await db.Collection("ordenesCompra").Document("ord-norm").GetSnapshotAsync()).ConvertTo<OrdenCompra>();
        Assert.Equal("Enviada", ordenEnviada.Estado);
        Assert.Single(sender.Messages);
    }
}

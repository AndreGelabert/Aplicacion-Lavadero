using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace Firebase.Tests.Integration;

public sealed class InsumosStockIntegrationTests
{
    private static FirestoreDb Db() => new FirestoreDbBuilder
    {
        ProjectId = "demo-insumos-" + Guid.NewGuid().ToString("N")[..16],
        EmulatorDetection = EmulatorDetection.EmulatorOnly
    }.Build();

    private static Task<TipoInsumo> PrepararHerramienta(FirestoreDb db) =>
        new TipoInsumoService(db).AsegurarTipoHerramienta("actor", "actor@example.invalid");

    [LocalFirestoreFact]
    public Task ReintentoCancelacionServicioRecuperaMermasTrasInterrupcion() => VerificarRecuperacionMerma(false);

    [LocalFirestoreFact]
    public Task ReintentoCancelacionLavadoRecuperaMermasTrasInterrupcion() => VerificarRecuperacionMerma(true);

    [LocalFirestoreFact]
    public Task ReintentoServicioTrasPrimeraMerma() => VerificarRecuperacionMerma(true, true);

    [LocalFirestoreFact]
    public Task ReintentoLavadoTrasPrimeraMerma() => VerificarRecuperacionMerma(true, false);

    private async Task VerificarRecuperacionMerma(bool lavadoCompleto, bool? interrupcionParcial = null)
    {
        var db = Db();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var servicios = new ServicioService(db);
        var stock = new StockService(db);
        var insumos = new InsumoService(db);
        var lavados = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache), new PersonalService(db), new ConfiguracionService(db, cache), stock);
        var item = new Insumo { Nombre = "Jabon", Tipo = "Quimico" };
        Assert.True((await insumos.Guardar(item, true)).EsExitoso);
        Assert.True((await stock.Ajustar(item.Id, 3, true, "Ingreso", "actor")).EsExitoso);
        await lavados.ActualizarLavado(new Lavado { Id = "l", Estado = "EnProceso", ClienteId = "c", VehiculoId = "v", FechaCreacion = DateTime.UtcNow, Servicios = new() { new ServicioEnLavado { ServicioId = "s", Estado = "EnProceso", Etapas = new() {
            new EtapaEnLavado { EtapaId = "e1", Receta = new() { new RecetaInsumo { InsumoId = item.Id, DosisTeorica = 1 } } },
            new EtapaEnLavado { EtapaId = "e2", Receta = new() { new RecetaInsumo { InsumoId = item.Id, DosisTeorica = 1 } } },
            new EtapaEnLavado { EtapaId = "e3" }
        } } } });
        await lavados.IniciarEtapa("l", "s", "e1");
        await lavados.FinalizarEtapa("l", "s", "e1");
        await lavados.FinalizarEtapa("l", "s", "e2");
        var antes = await stock.ObtenerMovimientos(item.Id);
        Assert.Equal(2, antes.Count(m => m.Tipo == nameof(TipoMovimientoStock.ConsumoEtapa)));
        if (interrupcionParcial.HasValue) lavadoCompleto = !interrupcionParcial.Value;
        // Una referencia de marcador inválida provoca fallo antes del commit transaccional.
        var segundo = antes.Where(m => m.Tipo == nameof(TipoMovimientoStock.ConsumoEtapa)).Last();
        var fallo = db.Collection("movimientosStock").Document(segundo.Id);
        await fallo.UpdateAsync("Id", "/");
        if (interrupcionParcial.HasValue) lavadoCompleto = !interrupcionParcial.Value;
        Func<Task> cancelar = lavadoCompleto ? () => lavados.CancelarLavado("l", "motivo") : () => lavados.CancelarServicio("l", "s", "motivo");
        await Assert.ThrowsAnyAsync<Exception>(cancelar);
        var intermedio = (await lavados.ObtenerLavado("l"))!;
        Assert.Equal("EnProceso", intermedio.Servicios[0].Estado);
        Assert.Equal(1, (await insumos.Obtener(item.Id))!.StockActual);
        Assert.Empty((await stock.ObtenerMovimientos(item.Id)).Where(m => m.Tipo == nameof(TipoMovimientoStock.MermaCancelacion)));
        await fallo.UpdateAsync("Id", segundo.Id);
        await cancelar();
        await cancelar();
        var despues = await stock.ObtenerMovimientos(item.Id);
        var consumos = despues.Where(m => m.Tipo == nameof(TipoMovimientoStock.ConsumoEtapa)).ToList();
        var mermas = despues.Where(m => m.Tipo == nameof(TipoMovimientoStock.MermaCancelacion)).ToList();
        Assert.Equal(2, consumos.Count);
        Assert.Equal(2, mermas.Count);
        Assert.Equal(antes.Select(m => m.Id).OrderBy(x => x), consumos.Concat(despues.Where(m => m.Tipo == nameof(TipoMovimientoStock.IngresoManual))).Select(m => m.Id).OrderBy(x => x));
        Assert.Equal(consumos.Select(m => m.ReferenciaId).OrderBy(x => x), mermas.Select(m => m.ReferenciaId).OrderBy(x => x));
        Assert.All(mermas, m => { Assert.Equal(0, m.Cantidad); Assert.Equal("motivo", m.Justificacion); });
        Assert.Equal(1, (await insumos.Obtener(item.Id))!.StockActual);
    }

    [LocalFirestoreFact]
    public Task CancelacionEtapaLiberaReservasMixtas() => VerificarCancelacionMixta("etapa");

    [LocalFirestoreFact]
    public Task CancelacionServicioConEtapasLiberaReservasMixtas() => VerificarCancelacionMixta("servicio");

    [LocalFirestoreFact]
    public Task CancelacionLavadoLiberaReservasMixtas() => VerificarCancelacionMixta("lavado");

    private async Task VerificarCancelacionMixta(string tipo)
    {
        {
            var db = Db();
            await PrepararHerramienta(db);
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var servicios = new ServicioService(db);
            var stock = new StockService(db);
            var lavados = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache), new PersonalService(db), new ConfiguracionService(db, cache), stock);
            var insumos = new InsumoService(db);
            var quimico = new Insumo { Nombre = "Jabon", Tipo = "Quimico" };
            var herramienta = new Insumo { Nombre = "Hidro", Tipo = "Herramienta" };
            foreach (var item in new[] { quimico, herramienta })
            {
                Assert.True((await insumos.Guardar(item, true)).EsExitoso);
                Assert.True((await stock.Ajustar(item.Id, 2, true, "Ingreso", "actor")).EsExitoso);
            }
            await lavados.ActualizarLavado(new Lavado { Id = "l", Estado = "EnProceso", ClienteId = "c", VehiculoId = "v", FechaCreacion = DateTime.UtcNow, Servicios = new() { new ServicioEnLavado { ServicioId = "s", Estado = "EnProceso", Etapas = new() {
                new EtapaEnLavado { EtapaId = "e1", Receta = new() { new RecetaInsumo { InsumoId = quimico.Id, DosisTeorica = 1 } } },
                new EtapaEnLavado { EtapaId = "e2", Receta = new[] { quimico, herramienta }.Select(i => new RecetaInsumo { InsumoId = i.Id, DosisTeorica = 1 }).ToList() }
            } } } });
            await lavados.IniciarEtapa("l", "s", "e1");
            await lavados.FinalizarEtapa("l", "s", "e1");
            Assert.False((await stock.Ajustar(quimico.Id, 1, false, "egreso", "actor")).EsExitoso);
            Func<Task> cancelar = tipo switch {
                "etapa" => () => lavados.CancelarEtapa("l", "s", "e2", "motivo"),
                "servicio" => () => lavados.CancelarServicio("l", "s", "motivo"),
                _ => () => lavados.CancelarLavado("l", "motivo")
            };
            await Task.WhenAll(cancelar(), cancelar());
            await cancelar();
            var actual = (await lavados.ObtenerLavado("l"))!;
            Assert.Equal("Realizado", actual.Servicios[0].Etapas[0].Estado);
            Assert.Equal("Cancelado", actual.Servicios[0].Etapas[1].Estado);
            Assert.Equal(1, (await insumos.Obtener(quimico.Id))!.StockActual);
            Assert.Equal(2, (await insumos.Obtener(herramienta.Id))!.StockActual);
            var movimientos = await stock.ObtenerMovimientos(quimico.Id);
            var consumo = Assert.Single(movimientos.Where(m => m.Tipo == nameof(TipoMovimientoStock.ConsumoEtapa)));
            Assert.Equal(-1, consumo.Cantidad);
            Assert.Equal("l_s_e1", consumo.ReferenciaId);
            var mermas = movimientos.Where(m => m.Tipo == nameof(TipoMovimientoStock.MermaCancelacion)).ToList();
            if (tipo == "etapa") Assert.Empty(mermas);
            else
            {
                var merma = Assert.Single(mermas);
                Assert.Equal(0, merma.Cantidad);
                Assert.Equal(consumo.SaldoResultante, merma.SaldoResultante);
                Assert.Equal(consumo.ReferenciaId, merma.ReferenciaId);
                Assert.Equal("motivo", merma.Justificacion);
            }
            Assert.True((await stock.Ajustar(quimico.Id, 1, false, "egreso", "actor")).EsExitoso);
            Assert.True((await stock.ReservarHerramienta("otras", herramienta.Id, 2)).EsExitoso);
            Assert.False((await stock.ReservarHerramienta("extra", herramienta.Id, 1)).EsExitoso);
        }
    }

    [LocalFirestoreFact]
    public async Task SiguienteServicioEsperaStockSinReservarHerramienta()
    {
        var db = Db();
        await PrepararHerramienta(db);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var servicios = new ServicioService(db);
        var stock = new StockService(db);
        var lavados = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache), new PersonalService(db), new ConfiguracionService(db, cache), stock);
        var insumos = new InsumoService(db);
        var herramienta = new Insumo { Nombre = "Hidro", Tipo = "Herramienta" };
        var quimico = new Insumo { Nombre = "Jabon", Tipo = "Quimico" };
        foreach (var item in new[] { herramienta, quimico }) Assert.True((await insumos.Guardar(item, true)).EsExitoso);
        Assert.True((await stock.Ajustar(herramienta.Id, 1, true, "Ingreso", "actor")).EsExitoso);
        await lavados.ActualizarLavado(new Lavado { Id = "l", Estado = "EnProceso", ClienteId = "c", VehiculoId = "v", FechaCreacion = DateTime.UtcNow, Servicios = new() {
            new ServicioEnLavado { ServicioId = "s1", Estado = "EnProceso", Orden = 1 },
            new ServicioEnLavado { ServicioId = "s2", Orden = 2, Receta = new[] { herramienta, quimico }.Select(i => new RecetaInsumo { InsumoId = i.Id, DosisTeorica = 1 }).ToList() }
        } });
        await lavados.FinalizarServicio("l", "s1");
        var actual = (await lavados.ObtenerLavado("l"))!;
        Assert.Equal(new[] { "Realizado", "Pendiente" }, actual.Servicios.Select(s => s.Estado));
        Assert.Null(actual.Servicios[1].TiempoInicio);
        Assert.Equal(0, (await insumos.Obtener(quimico.Id))!.StockActual);
        Assert.True((await stock.ReservarHerramienta("competidor", herramienta.Id, 1)).EsExitoso);
        Assert.Empty(await stock.ObtenerMovimientos(quimico.Id));
    }

    [LocalFirestoreFact]
    public async Task SiguientesServiciosConcurrentesReservanRecetaMixtaEnAmbasRutas()
    {
        var db = Db();
        await PrepararHerramienta(db);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var servicios = new ServicioService(db);
        var stock = new StockService(db);
        var lavados = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache), new PersonalService(db), new ConfiguracionService(db, cache), stock);
        var insumos = new InsumoService(db);
        var herramienta = new Insumo { Nombre = "Hidro", Tipo = "Herramienta" };
        var quimico = new Insumo { Nombre = "Jabon", Tipo = "Quimico" };
        foreach (var item in new[] { herramienta, quimico })
        {
            Assert.True((await insumos.Guardar(item, true)).EsExitoso);
            Assert.True((await stock.Ajustar(item.Id, 1, true, "Ingreso", "actor")).EsExitoso);
        }
        var receta = new[] { herramienta, quimico }.Select(i => new RecetaInsumo { InsumoId = i.Id, DosisTeorica = 1 }).ToList();
        await lavados.ActualizarLavado(new Lavado { Id = "l1", Estado = "EnProceso", ClienteId = "c", VehiculoId = "v1", FechaCreacion = DateTime.UtcNow, Servicios = new() {
            new ServicioEnLavado { ServicioId = "s1", Estado = "EnProceso", Orden = 1 },
            new ServicioEnLavado { ServicioId = "s2", Orden = 2, Receta = receta } } });
        await lavados.ActualizarLavado(new Lavado { Id = "l2", Estado = "EnProceso", ClienteId = "c", VehiculoId = "v2", FechaCreacion = DateTime.UtcNow, Servicios = new() {
            new ServicioEnLavado { ServicioId = "s1", Estado = "EnProceso", Orden = 1, Etapas = new() { new EtapaEnLavado { EtapaId = "e1", Estado = "EnProceso" } } },
            new ServicioEnLavado { ServicioId = "s2", Orden = 2, Etapas = new() { new EtapaEnLavado { EtapaId = "e2", Receta = receta } } } } });
        await Task.WhenAll(lavados.FinalizarServicio("l1", "s1"), lavados.FinalizarEtapa("l2", "s1", "e1"));
        var primero = (await lavados.ObtenerLavado("l1"))!;
        var segundo = (await lavados.ObtenerLavado("l2"))!;
        Assert.Equal("Realizado", primero.Servicios[0].Estado);
        Assert.Equal("Realizado", segundo.Servicios[0].Estado);
        Assert.Single(new[] { primero, segundo }.Where(l => l.Servicios[1].Estado == "EnProceso"));
        Assert.Single(new[] { primero, segundo }.Where(l => l.Servicios[1].Estado == "Pendiente"));
        Assert.Equal(segundo.Servicios[1].Estado, segundo.Servicios[1].Etapas[0].Estado);
        Assert.False((await stock.ReservarHerramienta("competidor", herramienta.Id, 1)).EsExitoso);
        Assert.False((await stock.Ajustar(quimico.Id, 1, false, "egreso", "actor")).EsExitoso);
        Assert.Equal(1, (await insumos.Obtener(quimico.Id))!.StockActual);
        Assert.Single(await stock.ObtenerMovimientos(quimico.Id));
    }

    [LocalFirestoreFact]
    public async Task AjusteRechazaEgresoMayorAlStockSinCrearMovimiento()
    {
        var db = Db();
        await db.Collection("insumos").Document("i1").SetAsync(new Insumo
        {
            Id = "i1", Nombre = "Químico", Tipo = "Quimico", UnidadMedida = "ml", StockActual = 2
        });
        var stock = new StockService(db, new FakeEmailSender(), new ProveedorService(db),
            NullLogger<StockService>.Instance);

        var resultado = await stock.Ajustar("i1", 3, false, "Prueba", "actor");

        Assert.False(resultado.EsExitoso);
        Assert.Equal("El egreso supera el stock disponible.", resultado.MensajeError);
        Assert.Equal(2, (await db.Collection("insumos").Document("i1").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
        Assert.Empty((await db.Collection("movimientosStock").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task AjusteManualValidoActualizaStockYRegistraKardex()
    {
        var db = Db();
        await db.Collection("insumos").Document("i1").SetAsync(new Insumo
        {
            Id = "i1", Nombre = "Químico", Tipo = "Quimico", UnidadMedida = "ml", StockActual = 1m
        });
        var stock = new StockService(db, new FakeEmailSender(), new ProveedorService(db),
            NullLogger<StockService>.Instance);

        var ingreso = await stock.Ajustar("i1", 2.5m, true, "Compra", "actor");
        var egreso = await stock.Ajustar("i1", 1.25m, false, "Merma", "actor");
        var inexistente = await stock.Ajustar("missing", 1m, true, "Prueba", "actor");

        Assert.True(ingreso.EsExitoso);
        Assert.True(egreso.EsExitoso);
        Assert.False(inexistente.EsExitoso);
        Assert.Equal("El insumo no existe.", inexistente.MensajeError);
        Assert.Equal(2.25m, (await db.Collection("insumos").Document("i1").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
        var movimientos = (await db.Collection("movimientosStock").GetSnapshotAsync()).Documents
            .Select(d => d.ConvertTo<MovimientoStock>()).ToList();
        Assert.Equal(2, movimientos.Count);
        Assert.Contains(movimientos, m => m.Tipo == nameof(TipoMovimientoStock.IngresoManual) && m.Cantidad == 2.5m && m.SaldoResultante == 3.5m);
        Assert.Contains(movimientos, m => m.Tipo == nameof(TipoMovimientoStock.EgresoManual) && m.Cantidad == -1.25m && m.SaldoResultante == 2.25m);
    }

    [LocalFirestoreFact]
    public async Task ConsumoDeRecetaEsIdempotenteYNoDuplicaKardex()
    {
        var db = Db();
        await db.Collection("insumos").Document("i1").SetAsync(new Insumo
        {
            Id = "i1", Nombre = "Químico", Tipo = "Quimico", UnidadMedida = "ml", StockActual = 10
        });
        var stock = new StockService(db, new FakeEmailSender(), new ProveedorService(db),
            NullLogger<StockService>.Instance);
        var receta = new[] { new RecetaInsumo { InsumoId = "i1", DosisTeorica = 3 } };

        await stock.ConsumirReceta("lavado-servicio-etapa", receta);
        await stock.ConsumirReceta("lavado-servicio-etapa", receta);

        Assert.Equal(7, (await db.Collection("insumos").Document("i1").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
        Assert.Single((await db.Collection("movimientosStock").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task ConsumoDeRecetaConDesvioRegistraCantidadRealYJustificacion()
    {
        var db = Db();
        await db.Collection("insumos").Document("i1").SetAsync(new Insumo
        {
            Id = "i1", Nombre = "Químico", Tipo = "Quimico", UnidadMedida = "ml", StockActual = 10m
        });
        var stock = new StockService(db, new FakeEmailSender(), new ProveedorService(db),
            NullLogger<StockService>.Instance);
        var receta = new[] { new RecetaInsumo { InsumoId = "i1", DosisTeorica = 3m } };

        await stock.ConsumirReceta("lavado-servicio-etapa-desvio", receta,
            new Dictionary<string, decimal> { ["i1"] = 2.5m }, "Derrame parcial");

        Assert.Equal(7.5m, (await db.Collection("insumos").Document("i1").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
        var mov = Assert.Single((await db.Collection("movimientosStock").GetSnapshotAsync()).Documents)
            .ConvertTo<MovimientoStock>();
        Assert.Equal(nameof(TipoMovimientoStock.ConsumoEtapa), mov.Tipo);
        Assert.Equal(-2.5m, mov.Cantidad);
        Assert.Equal(7.5m, mov.SaldoResultante);
        Assert.Equal("Derrame parcial", mov.Justificacion);
    }

    [LocalFirestoreFact]
    public async Task ConsumoRechazaHerramientaSinModificarStockNiKardex()
    {
        var db = Db();
        await db.Collection("insumos").Document("h1").SetAsync(new Insumo
        {
            Id = "h1", Nombre = "Hidrolavadora", Tipo = "Herramienta", UnidadMedida = "unidad", StockActual = 1
        });
        var stock = new StockService(db);
        var receta = new[] { new RecetaInsumo { InsumoId = "h1", DosisTeorica = 1 } };

        await Assert.ThrowsAsync<InvalidOperationException>(() => stock.ConsumirReceta("ref", receta));
        Assert.Equal(1, (await db.Collection("insumos").Document("h1").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
        Assert.Empty((await db.Collection("movimientosStock").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task ConsumoFisicoExistentePermaneceCompatibleEnAmbasOperaciones()
    {
        var db = Db();
        var insumos = new InsumoService(db);
        var fisico = new Insumo { Nombre = "Paño", Tipo = "Fisico", UnidadMedida = "unidad" };
        Assert.True((await insumos.Guardar(fisico, true)).EsExitoso);
        var stock = new StockService(db);
        Assert.True((await stock.Ajustar(fisico.Id, 5, true, "Ingreso", "actor")).EsExitoso);
        await stock.ConsumirReceta("receta-fisica", new[] { new RecetaInsumo { InsumoId = fisico.Id, DosisTeorica = 2 } });
        await stock.Consumir("individual-fisico", fisico.Id, 1);
        Assert.Equal(2, (await insumos.Obtener(fisico.Id))!.StockActual);
        var movimientos = await stock.ObtenerMovimientos(fisico.Id);
        Assert.Equal(2, movimientos.Count(m => m.Tipo == nameof(TipoMovimientoStock.ConsumoEtapa)));
    }

    [LocalFirestoreFact]
    public async Task ConsumirIndividualActualizaStockYNoDuplicaKardex()
    {
        var db = Db();
        await db.Collection("insumos").Document("i1").SetAsync(new Insumo
        {
            Id = "i1", Nombre = "Químico", Tipo = "Quimico", UnidadMedida = "ml", StockActual = 10m
        });
        var stock = new StockService(db, new FakeEmailSender(), new ProveedorService(db),
            NullLogger<StockService>.Instance);

        await stock.Consumir("ref-1", "i1", 2.5m, "Etapa 1");
        await stock.Consumir("ref-1", "i1", 2.5m, "Etapa 1");

        Assert.Equal(7.5m, (await db.Collection("insumos").Document("i1").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
        var mov = Assert.Single((await db.Collection("movimientosStock").GetSnapshotAsync()).Documents)
            .ConvertTo<MovimientoStock>();
        Assert.Equal(nameof(TipoMovimientoStock.ConsumoEtapa), mov.Tipo);
        Assert.Equal(-2.5m, mov.Cantidad);
        Assert.Equal(7.5m, mov.SaldoResultante);
    }

    [LocalFirestoreFact]
    public async Task RecepcionMultipleEsAtomicaYReintentosNoDuplican()
    {
        var db = Db();
        var insumos = new InsumoService(db);
        var stock = new StockService(db);
        var compras = new ComprasService(db);
        var a = new Insumo { Nombre = "A", Tipo = "Quimico" };
        var b = new Insumo { Nombre = "B", Tipo = "Quimico" };
        Assert.True((await insumos.Guardar(a, true)).EsExitoso);
        Assert.True((await insumos.Guardar(b, true)).EsExitoso);
        var proveedor = new Proveedor { RazonSocial = "Proveedor", IdentificacionTributaria = "123", Contacto = "1234567890", EmailOrdenes = "proveedor@example.invalid" };
        Assert.True((await new ProveedorService(db).Guardar(proveedor, true)).EsExitoso);
        var items = new List<OrdenCompraItem> {
            new() { InsumoId = a.Id, NombreInsumo = a.Nombre, Cantidad = 2 },
            new() { InsumoId = b.Id, NombreInsumo = b.Nombre, Cantidad = 3 } };
        Assert.True((await compras.CrearSolicitudes(new() { proveedor.Id }, items, null)).EsExitoso);
        var solicitud = Assert.Single(await compras.ObtenerSolicitudes());
        var respuesta = new CotizacionRespuesta { SolicitudId = solicitud.Id, PlazoEntrega = "1", Items = items.Select(i => new OrdenCompraItem { InsumoId = i.InsumoId, NombreInsumo = i.NombreInsumo, Cantidad = i.Cantidad, PrecioUnitario = 1 }).ToList() };
        Assert.True((await compras.RegistrarRespuesta(respuesta)).EsExitoso);
        var sender = new FakeEmailSender();
        Assert.True((await compras.Adjudicar(solicitud.Id, respuesta.Id, sender)).EsExitoso);
        Assert.Single(sender.Messages);
        var orden = Assert.Single(await compras.ObtenerOrdenes());
        var auditoriasPrevias = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents.Select(d => d.Id).OrderBy(id => id).ToArray();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => compras.Recibir(orden.Id, new Dictionary<string, decimal> { [a.Id] = 2, [b.Id] = 4 }));
        Assert.Equal("La recepción de 'B' supera la cantidad pendiente.", error.Message);
        Assert.Equal(0, (await insumos.Obtener(a.Id))!.StockActual);
        Assert.Equal(0, (await insumos.Obtener(b.Id))!.StockActual);
        Assert.Empty(await stock.ObtenerMovimientos());
        Assert.Equal("Enviada", (await compras.ObtenerOrdenes()).Single().Estado);
        Assert.Equal(auditoriasPrevias, (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents.Select(d => d.Id).OrderBy(id => id).ToArray());
        Assert.True((await compras.Recibir(orden.Id)).EsExitoso);
        var auditoriasRecibida = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents.Select(d => d.Id).OrderBy(id => id).ToArray();
        Assert.Equal(auditoriasPrevias.Length + 1, auditoriasRecibida.Length);
        Assert.True((await compras.Recibir(orden.Id)).EsExitoso);
        Assert.Equal(auditoriasRecibida, (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents.Select(d => d.Id).OrderBy(id => id).ToArray());
        Assert.Equal(2, (await insumos.Obtener(a.Id))!.StockActual);
        Assert.Equal(3, (await insumos.Obtener(b.Id))!.StockActual);
        var recibida = (await compras.ObtenerOrdenes()).Single();
        Assert.Equal("Recibida", recibida.Estado);
        Assert.Equal(new decimal[] { 2, 3 }, recibida.Items.Select(i => i.CantidadRecibida));
        var movimientos = await stock.ObtenerMovimientos();
        Assert.Equal(2, movimientos.Count);
        Assert.Contains(movimientos, m => m.InsumoId == a.Id && m.Cantidad == 2 && m.SaldoResultante == 2 && m.Tipo == nameof(TipoMovimientoStock.RecepcionOrden));
        Assert.Contains(movimientos, m => m.InsumoId == b.Id && m.Cantidad == 3 && m.SaldoResultante == 3 && m.Tipo == nameof(TipoMovimientoStock.RecepcionOrden));
    }

    [LocalFirestoreFact]
    public async Task RecepcionDeOrdenIncrementaStockRegistraKardexYEsIdempotente()
    {
        var db = Db();
        await db.Collection("insumos").Document("i1").SetAsync(new Insumo
        {
            Id = "i1", Nombre = "Químico", Tipo = "Quimico", UnidadMedida = "ml",
            StockActual = 1m, StockMinimo = 5m, CantidadReabastecimiento = 10m,
            ReabastecimientoAutomatico = true, ProveedoresIds = new() { "p1" }
        });
        await db.Collection("ordenesCompra").Document("o1").SetAsync(new OrdenCompra
        {
            Id = "o1",
            ProveedorId = "p1",
            Estado = "Despachada",
            Items = new()
            {
                new OrdenCompraItem { InsumoId = "i1", NombreInsumo = "Químico", Cantidad = 2.5m }
            }
        });
        var compras = new ComprasService(db);

        var primero = await compras.Recibir("o1");
        var segundo = await compras.Recibir("o1");

        Assert.True(primero.EsExitoso);
        Assert.True(segundo.EsExitoso);
        Assert.Equal(3.5m, (await db.Collection("insumos").Document("i1").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
        var orden = (await db.Collection("ordenesCompra").Document("o1").GetSnapshotAsync()).ConvertTo<OrdenCompra>();
        Assert.Equal("Recibida", orden.Estado);
        Assert.Equal(2.5m, Assert.Single(orden.Items).CantidadRecibida);
        var mov = Assert.Single((await db.Collection("movimientosStock").GetSnapshotAsync()).Documents)
            .ConvertTo<MovimientoStock>();
        Assert.Equal(nameof(TipoMovimientoStock.RecepcionOrden), mov.Tipo);
        Assert.Equal(2.5m, mov.Cantidad);
        Assert.Equal(3.5m, mov.SaldoResultante);
        Assert.Empty((await db.Collection("solicitudesCotizacion").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task ReabastecimientoNoDuplicaSolicitudRespondida()
    {
        var db = Db();
        var sender = new FakeEmailSender();
        await db.Collection("proveedores").Document("p").SetAsync(new Proveedor { Id = "p", EmailOrdenes = "p@example.invalid" });
        await db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "Jabon", StockActual = 1, StockMinimo = 2, CantidadReabastecimiento = 5, ReabastecimientoAutomatico = true, ProveedoresIds = new() { "p" } });
        await db.Collection("solicitudesCotizacion").Document("s").SetAsync(new SolicitudCotizacion { Id = "s", Estado = "Respondida", ProveedorId = "p", Items = new() { new OrdenCompraItem { InsumoId = "i", Cantidad = 5 } } });
        var stock = new StockService(db, sender, new ProveedorService(db), NullLogger<StockService>.Instance);
        await stock.EvaluarReabastecimiento("i");
        var solicitudes = (await db.Collection("solicitudesCotizacion").GetSnapshotAsync()).Documents;
        Assert.Equal("Respondida", Assert.Single(solicitudes).ConvertTo<SolicitudCotizacion>().Estado);
        Assert.Empty(sender.Messages);
        Assert.Equal(1, (await db.Collection("insumos").Document("i").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
    }

    [LocalFirestoreFact]
    public async Task ReabastecimientoDirectoCreaSolicitudYUsaTransporteInyectado()
    {
        var db = Db();
        var sender = new FakeEmailSender();
        await db.Collection("proveedores").Document("p1").SetAsync(new Proveedor
        {
            Id = "p1", RazonSocial = "Proveedor", EmailOrdenes = "proveedor@example.invalid"
        });
        await db.Collection("insumos").Document("i1").SetAsync(new Insumo
        {
            Id = "i1", Nombre = "Químico", Tipo = "Quimico", UnidadMedida = "ml",
            StockActual = 1, StockMinimo = 2, CantidadReabastecimiento = 5,
            ReabastecimientoAutomatico = true, ProveedoresIds = new() { "p1" }
        });
        await db.Collection("directivasReabastecimiento").Document("principal").SetAsync(
            new DirectivaReabastecimiento { ModoEmision = "EnvioDirecto", PlantillaEmail = "Pedido" });
        var stock = new StockService(db, sender, new ProveedorService(db),
            NullLogger<StockService>.Instance);

        await stock.EvaluarReabastecimiento("i1");

        Assert.Single((await db.Collection("solicitudesCotizacion").GetSnapshotAsync()).Documents);
        Assert.Single(sender.Messages);
        Assert.Equal("Pedido", sender.Messages[0].Subject);
    }

    [LocalFirestoreFact]
    public async Task ReservasConcurrentesNoExcedenStockYLiberacionEsIdempotente()
    {
        var db = Db();
        await PrepararHerramienta(db);
        var insumos = new InsumoService(db);
        var herramienta = new Insumo { Nombre = "Hidro", Tipo = "Herramienta" };
        Assert.True((await insumos.Guardar(herramienta, true)).EsExitoso);
        var stock = new StockService(db);
        Assert.True((await stock.Ajustar(herramienta.Id, 1, true, "Ingreso", "actor")).EsExitoso);

        var resultados = await Task.WhenAll(stock.ReservarHerramienta("ref-a", herramienta.Id, 1),
            stock.ReservarHerramienta("ref-b", herramienta.Id, 1));
        Assert.Single(resultados.Where(x => x.EsExitoso));
        var ganadora = resultados[0].EsExitoso ? "ref-a" : "ref-b";
        var perdedora = resultados[0].EsExitoso ? "ref-b" : "ref-a";
        Assert.True((await stock.ReservarHerramienta(ganadora, herramienta.Id, 1)).EsExitoso);
        Assert.False((await stock.Ajustar(herramienta.Id, 1, false, "Egreso", "actor")).EsExitoso);
        Assert.Equal(1, (await insumos.Obtener(herramienta.Id))!.StockActual);
        Assert.Single(await stock.ObtenerMovimientos(herramienta.Id));
        Assert.True((await stock.LiberarHerramienta(ganadora, herramienta.Id)).EsExitoso);
        Assert.True((await stock.LiberarHerramienta(ganadora, herramienta.Id)).EsExitoso);
        var reintentoLiberada = await stock.ReservarHerramienta(ganadora, herramienta.Id, 1);
        Assert.False(reintentoLiberada.EsExitoso);
        Assert.Equal("La reserva ya fue liberada.", reintentoLiberada.MensajeError);
        Assert.True((await stock.ReservarHerramienta(perdedora, herramienta.Id, 1)).EsExitoso);
        Assert.False((await stock.ReservarHerramienta("ref-c", herramienta.Id, 1)).EsExitoso);
        Assert.Equal(1, (await insumos.Obtener(herramienta.Id))!.StockActual);
        Assert.Single(await stock.ObtenerMovimientos(herramienta.Id));
    }

    [LocalFirestoreFact]
    public async Task EditarPreservaStockEstadoYCamposAdicionales()
    {
        var db = Db();
        await PrepararHerramienta(db);
        var insumos = new InsumoService(db);
        var item = new Insumo { Nombre = "Hidro", Tipo = "Herramienta" };
        Assert.True((await insumos.Guardar(item, true)).EsExitoso);
        var stock = new StockService(db);
        Assert.True((await stock.Ajustar(item.Id, 2, true, "Ingreso", "actor")).EsExitoso);
        await db.Collection("insumos").Document(item.Id).UpdateAsync("ReservaAdicional", "conservar");
        Assert.True((await insumos.CambiarEstado(item.Id, "Inactivo")).EsExitoso);
        item.Nombre = "Hidro editada";
        item.StockMinimo = 1;
        Assert.True((await insumos.Guardar(item, false)).EsExitoso);
        var actual = await insumos.Obtener(item.Id);
        Assert.Equal("Hidro editada", actual!.Nombre);
        Assert.Equal(1, actual.StockMinimo);
        Assert.Equal(2, actual.StockActual);
        Assert.Equal("Inactivo", actual.Estado);
        Assert.Equal("conservar", (await db.Collection("insumos").Document(item.Id).GetSnapshotAsync()).GetValue<string>("ReservaAdicional"));
        Assert.Single(await stock.ObtenerMovimientos(item.Id));
    }

    [LocalFirestoreFact]
    public async Task ReservaYEgresoConcurrentesNoComprometenLaMismaHerramienta()
    {
        var db = Db();
        await PrepararHerramienta(db);
        var insumos = new InsumoService(db);
        var item = new Insumo { Nombre = "Hidro", Tipo = "Herramienta" };
        Assert.True((await insumos.Guardar(item, true)).EsExitoso);
        var stock = new StockService(db);
        Assert.True((await stock.Ajustar(item.Id, 1, true, "Ingreso", "actor")).EsExitoso);
        var resultados = await Task.WhenAll(stock.ReservarHerramienta("ref", item.Id, 1),
            stock.Ajustar(item.Id, 1, false, "Egreso", "actor"));
        Assert.Single(resultados.Where(r => r.EsExitoso));
        Assert.Equal(resultados[0].EsExitoso ? 1 : 0, (await insumos.Obtener(item.Id))!.StockActual);
        Assert.Equal(resultados[1].EsExitoso ? 2 : 1, (await stock.ObtenerMovimientos(item.Id)).Count);
        Assert.False((await stock.ReservarHerramienta("otra", item.Id, 1)).EsExitoso);
    }

    [LocalFirestoreFact]
    public async Task FinalizarServicioSinEtapasConsumeUnaVezYFinalizaLavado()
    {
        var db = Db();
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var servicios = new ServicioService(db);
        var lavados = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache),
            new PersonalService(db), new ConfiguracionService(db, cache), new StockService(db));
        var insumos = new InsumoService(db);
        var insumo = new Insumo { Nombre = "Jabon", Tipo = "Quimico", UnidadMedida = "ml" };
        Assert.True((await insumos.Guardar(insumo, true)).EsExitoso);
        var stock = new StockService(db);
        Assert.True((await stock.Ajustar(insumo.Id, 3, true, "Ingreso", "actor")).EsExitoso);
        var lavado = new Lavado { Id = "lavado", Estado = "EnProceso", ClienteId = "cliente", VehiculoId = "vehiculo", FechaCreacion = DateTime.UtcNow,
            Servicios = new() { new ServicioEnLavado { ServicioId = "servicio", Estado = "EnProceso", Receta = new() { new RecetaInsumo { InsumoId = insumo.Id, DosisTeorica = 3 } } } } };
        await lavados.ActualizarLavado(lavado);
        await lavados.FinalizarServicio(lavado.Id, "servicio");
        await lavados.FinalizarServicio(lavado.Id, "servicio");
        Assert.Equal(0, (await insumos.Obtener(insumo.Id))!.StockActual);
        var movimientos = await stock.ObtenerMovimientos(insumo.Id);
        var consumo = Assert.Single(movimientos.Where(m => m.Tipo == nameof(TipoMovimientoStock.ConsumoEtapa)));
        Assert.Equal(-3, consumo.Cantidad);
        Assert.Equal(0, consumo.SaldoResultante);
        var final = (await lavados.ObtenerLavado(lavado.Id))!;
        Assert.Equal("Realizado", Assert.Single(final.Servicios).Estado);
        Assert.Equal("Realizado", final.Estado);
    }

    [LocalFirestoreFact]
    public async Task FinalizarSinEtapasInsuficienteNoCambiaLavadoNiStock()
    {
        var db = Db();
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var servicios = new ServicioService(db);
        var lavados = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache), new PersonalService(db), new ConfiguracionService(db, cache), new StockService(db));
        var insumos = new InsumoService(db);
        var item = new Insumo { Nombre = "Jabon", Tipo = "Quimico", UnidadMedida = "ml" };
        Assert.True((await insumos.Guardar(item, true)).EsExitoso);
        var stock = new StockService(db);
        Assert.True((await stock.Ajustar(item.Id, 1, true, "Ingreso", "actor")).EsExitoso);
        await lavados.ActualizarLavado(new Lavado { Id = "l", Estado = "EnProceso", ClienteId = "c", VehiculoId = "v", FechaCreacion = DateTime.UtcNow, Servicios = new() { new ServicioEnLavado { ServicioId = "s", Estado = "EnProceso", Receta = new() { new RecetaInsumo { InsumoId = item.Id, DosisTeorica = 2 } } } } });
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => lavados.FinalizarServicio("l", "s"));
        Assert.Equal("Stock insuficiente para el insumo 'Jabon'.", error.Message);
        Assert.Equal(1, (await insumos.Obtener(item.Id))!.StockActual);
        Assert.Single(await stock.ObtenerMovimientos(item.Id));
        var actual = (await lavados.ObtenerLavado("l"))!;
        Assert.Equal("EnProceso", actual.Estado);
        Assert.Equal("EnProceso", Assert.Single(actual.Servicios).Estado);
        Assert.Null(actual.TiempoFinalizacion);
        Assert.Null(actual.Servicios[0].TiempoFinalizacion);
    }

    [LocalFirestoreFact]
    public async Task FinalizacionesConcurrentesYReintentoNoInicianMasDeUnServicio()
    {
        var db = Db();
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var servicios = new ServicioService(db);
        var lavados = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache), new PersonalService(db), new ConfiguracionService(db, cache), new StockService(db));
        var insumos = new InsumoService(db);
        var item = new Insumo { Nombre = "Jabon", Tipo = "Quimico", UnidadMedida = "ml" };
        Assert.True((await insumos.Guardar(item, true)).EsExitoso);
        var stock = new StockService(db);
        Assert.True((await stock.Ajustar(item.Id, 2, true, "Ingreso", "actor")).EsExitoso);
        await lavados.ActualizarLavado(new Lavado { Id = "l", Estado = "EnProceso", ClienteId = "c", VehiculoId = "v", FechaCreacion = DateTime.UtcNow, Servicios = new() {
            new ServicioEnLavado { ServicioId = "s", Estado = "EnProceso", Orden = 1, Receta = new() { new RecetaInsumo { InsumoId = item.Id, DosisTeorica = 1 } } },
            new ServicioEnLavado { ServicioId = "s2", Estado = "Pendiente", Orden = 2 },
            new ServicioEnLavado { ServicioId = "s3", Estado = "Pendiente", Orden = 3 } } });
        await Task.WhenAll(lavados.FinalizarServicio("l", "s"), lavados.FinalizarServicio("l", "s"));
        await lavados.FinalizarServicio("l", "s");
        var actual = (await lavados.ObtenerLavado("l"))!;
        Assert.Equal(new[] { "Realizado", "EnProceso", "Pendiente" }, actual.Servicios.Select(s => s.Estado));
        Assert.Equal("EnProceso", actual.Estado);
        Assert.Equal(1, (await insumos.Obtener(item.Id))!.StockActual);
        Assert.Single((await stock.ObtenerMovimientos(item.Id)).Where(m => m.Tipo == nameof(TipoMovimientoStock.ConsumoEtapa)));
    }

    [LocalFirestoreFact]
    public async Task FinalizarServicioConEtapasPreservaValidacionYFinalizacion()
    {
        var db = Db();
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var servicios = new ServicioService(db);
        var lavados = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache), new PersonalService(db), new ConfiguracionService(db, cache), new StockService(db));
        var lavado = new Lavado { Id = "l", Estado = "EnProceso", ClienteId = "c", VehiculoId = "v", FechaCreacion = DateTime.UtcNow, Servicios = new() { new ServicioEnLavado { ServicioId = "s", Estado = "EnProceso", Etapas = new() { new EtapaEnLavado { EtapaId = "e", Estado = "Pendiente" } } } } };
        await lavados.ActualizarLavado(lavado);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => lavados.FinalizarServicio("l", "s"));
        Assert.Equal("No se puede finalizar el servicio porque hay etapas pendientes o en proceso.", error.Message);
        Assert.Equal("EnProceso", (await lavados.ObtenerLavado("l"))!.Servicios[0].Estado);
        lavado.Servicios[0].Etapas[0].Estado = "Realizado";
        await lavados.ActualizarLavado(lavado);
        await lavados.FinalizarServicio("l", "s");
        var actual = (await lavados.ObtenerLavado("l"))!;
        Assert.Equal("Realizado", actual.Servicios[0].Estado);
        Assert.Equal("Realizado", actual.Estado);
        Assert.Empty(await new StockService(db).ObtenerMovimientos());
    }

    [LocalFirestoreFact]
    public async Task ServicioSinEtapasReservaHerramientaHastaFinalizarSinConsumirla()
    {
        var db = Db();
        await PrepararHerramienta(db);
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var servicios = new ServicioService(db);
        var stock = new StockService(db);
        var lavados = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache), new PersonalService(db), new ConfiguracionService(db, cache), stock);
        var insumos = new InsumoService(db);
        var herramienta = new Insumo { Nombre = "Hidro", Tipo = "Herramienta" };
        Assert.True((await insumos.Guardar(herramienta, true)).EsExitoso);
        Assert.True((await stock.Ajustar(herramienta.Id, 1, true, "Ingreso", "actor")).EsExitoso);
        await lavados.ActualizarLavado(new Lavado { Id = "l", Estado = "EnProceso", ClienteId = "c", VehiculoId = "v", FechaCreacion = DateTime.UtcNow, Servicios = new() { new ServicioEnLavado { ServicioId = "s", Estado = "Pendiente", Receta = new() { new RecetaInsumo { InsumoId = herramienta.Id, DosisTeorica = 1 } } } } });

        await lavados.IniciarServicio("l", "s");
        Assert.False((await stock.ReservarHerramienta("otra", herramienta.Id, 1)).EsExitoso);
        await lavados.FinalizarServicio("l", "s");
        await lavados.FinalizarServicio("l", "s");
        Assert.True((await stock.ReservarHerramienta("otra", herramienta.Id, 1)).EsExitoso);
        Assert.Equal(1, (await insumos.Obtener(herramienta.Id))!.StockActual);
        Assert.Single(await stock.ObtenerMovimientos(herramienta.Id));
        Assert.Equal("Realizado", (await lavados.ObtenerLavado("l"))!.Servicios[0].Estado);
    }

    [LocalFirestoreFact]
    public async Task InicioConcurrenteDeServiciosConHerramientasNoComparteReservas()
    {
        var db = Db();
        await PrepararHerramienta(db);
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var servicios = new ServicioService(db);
        var lavados = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache), new PersonalService(db), new ConfiguracionService(db, cache), new StockService(db));
        var insumos = new InsumoService(db);
        var stock = new StockService(db);
        var herramientas = new[] { new Insumo { Nombre = "Hidro", Tipo = "Herramienta" }, new Insumo { Nombre = "Aspiradora", Tipo = "Herramienta" } };
        foreach (var herramienta in herramientas)
        {
            Assert.True((await insumos.Guardar(herramienta, true)).EsExitoso);
            Assert.True((await stock.Ajustar(herramienta.Id, 1, true, "Ingreso", "actor")).EsExitoso);
        }
        foreach (var id in new[] { "l1", "l2" })
            await lavados.ActualizarLavado(new Lavado { Id = id, Estado = "EnProceso", ClienteId = "c", VehiculoId = "v", FechaCreacion = DateTime.UtcNow, Servicios = new() { new ServicioEnLavado { ServicioId = "s", Estado = "Pendiente", Receta = herramientas.Select(h => new RecetaInsumo { InsumoId = h.Id, DosisTeorica = 1 }).ToList() } } });

        async Task<bool> Iniciar(string id)
        {
            try { await lavados.IniciarServicio(id, "s"); return true; }
            catch (InvalidOperationException) { return false; }
        }
        var resultados = await Task.WhenAll(Iniciar("l1"), Iniciar("l2"));
        Assert.Single(resultados.Where(x => x));
        Assert.Equal(1, new[] { (await lavados.ObtenerLavado("l1"))!.Servicios[0].Estado, (await lavados.ObtenerLavado("l2"))!.Servicios[0].Estado }.Count(x => x == "EnProceso"));
        foreach (var herramienta in herramientas)
        {
            Assert.Equal(1, (await insumos.Obtener(herramienta.Id))!.StockActual);
            Assert.False((await stock.Ajustar(herramienta.Id, 1, false, "Egreso", "actor")).EsExitoso);
            Assert.Single(await stock.ObtenerMovimientos(herramienta.Id));
        }
    }

    [LocalFirestoreFact]
    public async Task CancelacionSinEtapasLiberaTodasLasHerramientasUnaVezSinConsumir()
    {
        var db = Db();
        await PrepararHerramienta(db);
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var servicios = new ServicioService(db);
        var stock = new StockService(db);
        var lavados = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache), new PersonalService(db), new ConfiguracionService(db, cache), stock);
        var insumos = new InsumoService(db);
        var items = new[] { new Insumo { Nombre = "Hidro", Tipo = "Herramienta" }, new Insumo { Nombre = "Aspiradora", Tipo = "Herramienta" }, new Insumo { Nombre = "Jabon", Tipo = "Quimico" } };
        foreach (var item in items)
        {
            Assert.True((await insumos.Guardar(item, true)).EsExitoso);
            Assert.True((await stock.Ajustar(item.Id, 1, true, "Ingreso", "actor")).EsExitoso);
        }
        await lavados.ActualizarLavado(new Lavado { Id = "l", Estado = "EnProceso", ClienteId = "c", VehiculoId = "v", FechaCreacion = DateTime.UtcNow, Servicios = new() { new ServicioEnLavado { ServicioId = "s", Estado = "Pendiente", Receta = items.Select(i => new RecetaInsumo { InsumoId = i.Id, DosisTeorica = 1 }).ToList() } } });
        await lavados.IniciarServicio("l", "s");
        await Task.WhenAll(lavados.CancelarServicio("l", "s", "motivo"), lavados.CancelarServicio("l", "s", "motivo"));
        await lavados.CancelarServicio("l", "s", "motivo");
        var actual = (await lavados.ObtenerLavado("l"))!;
        Assert.Equal("Cancelado", actual.Estado);
        Assert.Equal("Cancelado", Assert.Single(actual.Servicios).Estado);
        foreach (var item in items)
        {
            Assert.Equal(1, (await insumos.Obtener(item.Id))!.StockActual);
            Assert.Single(await stock.ObtenerMovimientos(item.Id));
        }
        foreach (var herramienta in items.Take(2))
        {
            Assert.True((await stock.ReservarHerramienta("nueva-" + herramienta.Id, herramienta.Id, 1)).EsExitoso);
            Assert.False((await stock.ReservarHerramienta("extra-" + herramienta.Id, herramienta.Id, 1)).EsExitoso);
        }
    }

    [LocalFirestoreFact]
    public async Task EtapasConcurrentesReservanHerramientaYLaLiberanAlFinalizar()
    {
        var db = Db();
        await PrepararHerramienta(db);
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var servicios = new ServicioService(db);
        var stock = new StockService(db);
        var lavados = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache), new PersonalService(db), new ConfiguracionService(db, cache), stock);
        var insumos = new InsumoService(db);
        var herramienta = new Insumo { Nombre = "Hidro", Tipo = "Herramienta" };
        Assert.True((await insumos.Guardar(herramienta, true)).EsExitoso);
        Assert.True((await stock.Ajustar(herramienta.Id, 1, true, "Ingreso", "actor")).EsExitoso);
        foreach (var id in new[] { "l1", "l2" })
            await lavados.ActualizarLavado(new Lavado { Id = id, Estado = "EnProceso", ClienteId = "c", VehiculoId = "v", FechaCreacion = DateTime.UtcNow, Servicios = new() { new ServicioEnLavado { ServicioId = "s", Estado = "EnProceso", Etapas = new() { new EtapaEnLavado { EtapaId = "e", Receta = new() { new RecetaInsumo { InsumoId = herramienta.Id, DosisTeorica = 1 } } } } } } });
        async Task<bool> Iniciar(string id)
        {
            try { await lavados.IniciarEtapa(id, "s", "e"); return true; }
            catch (InvalidOperationException) { return false; }
        }
        var resultados = await Task.WhenAll(Iniciar("l1"), Iniciar("l2"));
        Assert.Single(resultados.Where(x => x));
        var ganador = resultados[0] ? "l1" : "l2";
        var perdedor = resultados[0] ? "l2" : "l1";
        Assert.Equal("Pendiente", (await lavados.ObtenerLavado(perdedor))!.Servicios[0].Etapas[0].Estado);
        await lavados.FinalizarEtapa(ganador, "s", "e");
        await lavados.IniciarEtapa(perdedor, "s", "e");
        Assert.Equal("EnProceso", (await lavados.ObtenerLavado(perdedor))!.Servicios[0].Etapas[0].Estado);
        Assert.Equal(1, (await insumos.Obtener(herramienta.Id))!.StockActual);
        Assert.Single(await stock.ObtenerMovimientos(herramienta.Id));
    }

    [LocalFirestoreFact]
    public async Task EtapaMixtaConcurrenteConsumeUnaVezYLiberaHerramienta()
    {
        var db = Db();
        await PrepararHerramienta(db);
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var servicios = new ServicioService(db);
        var stock = new StockService(db);
        var lavados = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache), new PersonalService(db), new ConfiguracionService(db, cache), stock);
        var insumos = new InsumoService(db);
        var quimico = new Insumo { Nombre = "Jabon", Tipo = "Quimico" };
        var herramienta = new Insumo { Nombre = "Hidro", Tipo = "Herramienta" };
        foreach (var item in new[] { quimico, herramienta })
        {
            Assert.True((await insumos.Guardar(item, true)).EsExitoso);
            Assert.True((await stock.Ajustar(item.Id, 1, true, "Ingreso", "actor")).EsExitoso);
        }
        await lavados.ActualizarLavado(new Lavado { Id = "l", Estado = "EnProceso", ClienteId = "c", VehiculoId = "v", FechaCreacion = DateTime.UtcNow, Servicios = new() { new ServicioEnLavado { ServicioId = "s", Estado = "EnProceso", Etapas = new() { new EtapaEnLavado { EtapaId = "e", Estado = "Pendiente", Receta = new[] { quimico, herramienta }.Select(i => new RecetaInsumo { InsumoId = i.Id, DosisTeorica = 1 }).ToList() } } } } });
        await lavados.IniciarEtapa("l", "s", "e");
        Assert.False((await stock.ReservarHerramienta("otra", herramienta.Id, 1)).EsExitoso);
        await Task.WhenAll(lavados.FinalizarEtapa("l", "s", "e"), lavados.FinalizarEtapa("l", "s", "e"));
        await lavados.FinalizarEtapa("l", "s", "e");
        Assert.Equal(0, (await insumos.Obtener(quimico.Id))!.StockActual);
        Assert.Equal(1, (await insumos.Obtener(herramienta.Id))!.StockActual);
        var consumo = Assert.Single((await stock.ObtenerMovimientos(quimico.Id)).Where(m => m.Tipo == nameof(TipoMovimientoStock.ConsumoEtapa)));
        Assert.Equal(-1, consumo.Cantidad);
        Assert.Equal(0, consumo.SaldoResultante);
        Assert.Single(await stock.ObtenerMovimientos(herramienta.Id));
        var actual = (await lavados.ObtenerLavado("l"))!;
        Assert.Equal("Realizado", actual.Estado);
        Assert.Equal("Realizado", actual.Servicios[0].Etapas[0].Estado);
        Assert.True((await stock.ReservarHerramienta("otra", herramienta.Id, 1)).EsExitoso);
        Assert.False((await stock.ReservarHerramienta("extra", herramienta.Id, 1)).EsExitoso);
    }

    [LocalFirestoreFact]
    public async Task EtapaMixtaInsuficienteNoModificaEstadoStockKardexNiReserva()
    {
        var db = Db();
        await PrepararHerramienta(db);
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var servicios = new ServicioService(db);
        var stock = new StockService(db);
        var lavados = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache), new PersonalService(db), new ConfiguracionService(db, cache), stock);
        var insumos = new InsumoService(db);
        var quimico = new Insumo { Nombre = "Jabon", Tipo = "Quimico" };
        var herramienta = new Insumo { Nombre = "Hidro", Tipo = "Herramienta" };
        foreach (var item in new[] { quimico, herramienta })
        {
            Assert.True((await insumos.Guardar(item, true)).EsExitoso);
            Assert.True((await stock.Ajustar(item.Id, 1, true, "Ingreso", "actor")).EsExitoso);
        }
        await lavados.ActualizarLavado(new Lavado { Id = "l", Estado = "EnProceso", ClienteId = "c", VehiculoId = "v", FechaCreacion = DateTime.UtcNow, Servicios = new() { new ServicioEnLavado { ServicioId = "s", Estado = "EnProceso", Etapas = new() { new EtapaEnLavado { EtapaId = "e", Estado = "Pendiente", Receta = new[] { quimico, herramienta }.Select(i => new RecetaInsumo { InsumoId = i.Id, DosisTeorica = 1 }).ToList() } } } } });
        await lavados.IniciarEtapa("l", "s", "e");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => lavados.FinalizarEtapa("l", "s", "e", System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, decimal> { [quimico.Id] = 2 }), "desvio"));
        Assert.Equal("Stock insuficiente para el insumo 'Jabon'.", error.Message);
        var actual = (await lavados.ObtenerLavado("l"))!;
        Assert.Equal("EnProceso", actual.Estado);
        Assert.Equal("EnProceso", actual.Servicios[0].Estado);
        Assert.Equal("EnProceso", actual.Servicios[0].Etapas[0].Estado);
        Assert.Null(actual.Servicios[0].Etapas[0].TiempoFinalizacion);
        foreach (var item in new[] { quimico, herramienta })
        {
            Assert.Equal(1, (await insumos.Obtener(item.Id))!.StockActual);
            Assert.Single(await stock.ObtenerMovimientos(item.Id));
        }
        Assert.False((await stock.ReservarHerramienta("otra", herramienta.Id, 1)).EsExitoso);
        Assert.False((await stock.Ajustar(herramienta.Id, 1, false, "egreso", "actor")).EsExitoso);
    }

    [LocalFirestoreFact]
    public async Task SiguienteEtapaReutilizaHerramientaOEsperaSinReservasParciales()
    {
        var db = Db();
        await PrepararHerramienta(db);
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var servicios = new ServicioService(db);
        var stock = new StockService(db);
        var lavados = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache), new PersonalService(db), new ConfiguracionService(db, cache), stock);
        var insumos = new InsumoService(db);
        var hidro = new Insumo { Nombre = "Hidro", Tipo = "Herramienta" };
        var otra = new Insumo { Nombre = "Otra", Tipo = "Herramienta" };
        foreach (var item in new[] { hidro, otra })
        {
            Assert.True((await insumos.Guardar(item, true)).EsExitoso);
            Assert.True((await stock.Ajustar(item.Id, 1, true, "Ingreso", "actor")).EsExitoso);
        }
        var recetaHidro = new RecetaInsumo { InsumoId = hidro.Id, DosisTeorica = 1 };
        await lavados.ActualizarLavado(new Lavado { Id = "l", Estado = "EnProceso", ClienteId = "c", VehiculoId = "v", FechaCreacion = DateTime.UtcNow, Servicios = new() { new ServicioEnLavado { ServicioId = "s", Estado = "EnProceso", Etapas = new() {
            new EtapaEnLavado { EtapaId = "e1", Receta = new() { recetaHidro } },
            new EtapaEnLavado { EtapaId = "e2", Receta = new() { recetaHidro } },
            new EtapaEnLavado { EtapaId = "e3", Receta = new() { new RecetaInsumo { InsumoId = hidro.Id, DosisTeorica = 1 }, new RecetaInsumo { InsumoId = otra.Id, DosisTeorica = 1 } } }
        } } } });
        await lavados.IniciarEtapa("l", "s", "e1");
        await lavados.FinalizarEtapa("l", "s", "e1");
        var actual = (await lavados.ObtenerLavado("l"))!;
        Assert.Equal(new[] { "Realizado", "EnProceso", "Pendiente" }, actual.Servicios[0].Etapas.Select(e => e.Estado));
        Assert.False((await stock.ReservarHerramienta("competidor", hidro.Id, 1)).EsExitoso);
        Assert.True((await stock.ReservarHerramienta("ocupada", otra.Id, 1)).EsExitoso);
        await lavados.FinalizarEtapa("l", "s", "e2");
        actual = (await lavados.ObtenerLavado("l"))!;
        Assert.Equal(new[] { "Realizado", "Realizado", "Pendiente" }, actual.Servicios[0].Etapas.Select(e => e.Estado));
        Assert.True((await stock.ReservarHerramienta("competidor", hidro.Id, 1)).EsExitoso);
        Assert.False((await stock.ReservarHerramienta("extra", otra.Id, 1)).EsExitoso);
        Assert.Equal(1, (await insumos.Obtener(hidro.Id))!.StockActual);
        Assert.Single(await stock.ObtenerMovimientos(hidro.Id));
    }

    [LocalFirestoreFact]
    public async Task TransicionesConcurrentesCompitenPorHerramientaSinReservasParciales()
    {
        var db = Db();
        await PrepararHerramienta(db);
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var servicios = new ServicioService(db);
        var stock = new StockService(db);
        var lavados = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache), new PersonalService(db), new ConfiguracionService(db, cache), stock);
        var insumos = new InsumoService(db);
        var hidro = new Insumo { Nombre = "Hidro", Tipo = "Herramienta" };
        Assert.True((await insumos.Guardar(hidro, true)).EsExitoso);
        Assert.True((await stock.Ajustar(hidro.Id, 1, true, "Ingreso", "actor")).EsExitoso);
        foreach (var id in new[] { "l1", "l2" })
        {
            await lavados.ActualizarLavado(new Lavado { Id = id, Estado = "EnProceso", ClienteId = "c", VehiculoId = "v", FechaCreacion = DateTime.UtcNow, Servicios = new() { new ServicioEnLavado { ServicioId = "s", Estado = "EnProceso", Etapas = new() {
                new EtapaEnLavado { EtapaId = "e1", Estado = "EnProceso" },
                new EtapaEnLavado { EtapaId = "e2", Receta = new() { new RecetaInsumo { InsumoId = hidro.Id, DosisTeorica = 1 } } }
            } } } });
        }
        await Task.WhenAll(lavados.FinalizarEtapa("l1", "s", "e1"), lavados.FinalizarEtapa("l2", "s", "e1"));
        var estados = new List<string>();
        foreach (var id in new[] { "l1", "l2" })
        {
            var lavado = (await lavados.ObtenerLavado(id))!;
            Assert.Equal("EnProceso", lavado.Estado);
            Assert.Equal("EnProceso", lavado.Servicios[0].Estado);
            Assert.Equal("Realizado", lavado.Servicios[0].Etapas[0].Estado);
            estados.Add(lavado.Servicios[0].Etapas[1].Estado);
        }
        Assert.Equal(1, estados.Count(e => e == "EnProceso"));
        Assert.Equal(1, estados.Count(e => e == "Pendiente"));
        Assert.Equal(1, (await insumos.Obtener(hidro.Id))!.StockActual);
        Assert.Single(await stock.ObtenerMovimientos(hidro.Id));
        Assert.False((await stock.ReservarHerramienta("competidor", hidro.Id, 1)).EsExitoso);
        var ganador = estados[0] == "EnProceso" ? "l1" : "l2";
        var perdedor = ganador == "l1" ? "l2" : "l1";
        await lavados.FinalizarEtapa(ganador, "s", "e2");
        await lavados.IniciarEtapa(perdedor, "s", "e2");
        Assert.Equal("EnProceso", (await lavados.ObtenerLavado(perdedor))!.Servicios[0].Etapas[1].Estado);
        Assert.False((await stock.ReservarHerramienta("extra", hidro.Id, 1)).EsExitoso);
        Assert.Equal(1, (await insumos.Obtener(hidro.Id))!.StockActual);
        Assert.Single(await stock.ObtenerMovimientos(hidro.Id));
    }

    [LocalFirestoreFact]
    public async Task ConsumoDeEtapaDejaSiguientePendienteCuandoStockResultanteNoAlcanza()
    {
        var db = Db();
        await PrepararHerramienta(db);
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var servicios = new ServicioService(db);
        var stock = new StockService(db);
        var lavados = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache), new PersonalService(db), new ConfiguracionService(db, cache), stock);
        var insumos = new InsumoService(db);
        var quimico = new Insumo { Nombre = "Jabon", Tipo = "Quimico" };
        var hidro = new Insumo { Nombre = "Hidro", Tipo = "Herramienta" };
        foreach (var item in new[] { quimico, hidro })
        {
            Assert.True((await insumos.Guardar(item, true)).EsExitoso);
            Assert.True((await stock.Ajustar(item.Id, 1, true, "Ingreso", "actor")).EsExitoso);
        }
        await lavados.ActualizarLavado(new Lavado { Id = "l", Estado = "EnProceso", ClienteId = "c", VehiculoId = "v", FechaCreacion = DateTime.UtcNow, Servicios = new() { new ServicioEnLavado { ServicioId = "s", Estado = "EnProceso", Etapas = new() {
            new EtapaEnLavado { EtapaId = "e1", Estado = "EnProceso", Receta = new() { new RecetaInsumo { InsumoId = quimico.Id, DosisTeorica = 1 } } },
            new EtapaEnLavado { EtapaId = "e2", Receta = new() { new RecetaInsumo { InsumoId = hidro.Id, DosisTeorica = 1 }, new RecetaInsumo { InsumoId = quimico.Id, DosisTeorica = 1 } } }
        } } } });
        await lavados.FinalizarEtapa("l", "s", "e1");
        var lavado = (await lavados.ObtenerLavado("l"))!;
        Assert.Equal("EnProceso", lavado.Estado);
        Assert.Equal("EnProceso", lavado.Servicios[0].Estado);
        Assert.Equal(new[] { "Realizado", "Pendiente" }, lavado.Servicios[0].Etapas.Select(e => e.Estado));
        Assert.Null(lavado.Servicios[0].Etapas[1].TiempoInicio);
        Assert.Equal(0, (await insumos.Obtener(quimico.Id))!.StockActual);
        Assert.Equal(1, (await insumos.Obtener(hidro.Id))!.StockActual);
        var movimientos = await stock.ObtenerMovimientos(quimico.Id);
        Assert.Equal(2, movimientos.Count);
        var consumo = Assert.Single(movimientos.Where(m => m.Tipo == nameof(TipoMovimientoStock.ConsumoEtapa)));
        Assert.Equal(-1, consumo.Cantidad);
        Assert.Equal(0, consumo.SaldoResultante);
        Assert.Single(await stock.ObtenerMovimientos(hidro.Id));
        Assert.True((await stock.ReservarHerramienta("competidor", hidro.Id, 1)).EsExitoso);
        Assert.False((await stock.ReservarHerramienta("extra", hidro.Id, 1)).EsExitoso);
    }

    [LocalFirestoreFact]
    public async Task CreacionesConcurrentesNoCompartenConsumibleYConsumoExactoOcurreAlFinalizar()
    {
        var db = Db();
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        cache.Set("system_configuration", new Configuracion { HorariosOperacion = new(), CapacidadMaximaConcurrente = 10 });
        var servicios = new ServicioService(db);
        var stock = new StockService(db);
        var lavados = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache), new PersonalService(db), new ConfiguracionService(db, cache), stock);
        var insumos = new InsumoService(db);
        var item = new Insumo { Nombre = "Jabon", Tipo = "Quimico" };
        Assert.True((await insumos.Guardar(item, true)).EsExitoso);
        Assert.True((await stock.Ajustar(item.Id, 1, true, "Ingreso", "actor")).EsExitoso);
        Lavado Nuevo(string vehiculo) => new() { Id = "", Estado = "Pendiente", ClienteId = "c", VehiculoId = vehiculo, Servicios = new() { new ServicioEnLavado { ServicioId = "s", Receta = new() { new RecetaInsumo { InsumoId = item.Id, DosisTeorica = 1 } } } } };
        async Task<Lavado?> Crear(string vehiculo)
        {
            try { return await lavados.CrearLavadoConInicio(Nuevo(vehiculo)); }
            catch (InvalidOperationException ex)
            {
                Assert.Equal("No hay stock suficiente de 'Jabon' para iniciar la operación.", ex.Message);
                return null;
            }
        }
        var resultados = await Task.WhenAll(Crear("v1"), Crear("v2"));
        var ganador = Assert.Single(resultados.Where(x => x != null))!;
        Assert.Single(await lavados.ObtenerLavados());
        Assert.Equal(1, (await insumos.Obtener(item.Id))!.StockActual);
        Assert.Single(await stock.ObtenerMovimientos(item.Id));
        await lavados.FinalizarServicio(ganador.Id, "s");
        await lavados.FinalizarServicio(ganador.Id, "s");
        Assert.Equal(0, (await insumos.Obtener(item.Id))!.StockActual);
        var consumo = Assert.Single((await stock.ObtenerMovimientos(item.Id)).Where(x => x.Tipo == nameof(TipoMovimientoStock.ConsumoEtapa)));
        Assert.Equal(-1, consumo.Cantidad);
        Assert.Equal(0, consumo.SaldoResultante);
        Assert.Equal("Realizado", (await lavados.ObtenerLavado(ganador.Id))!.Estado);
    }

    [LocalFirestoreFact]
    public async Task EgresoNoComprometeConsumibleReservadoYCancelacionLoLibera()
    {
        var db = Db();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set("system_configuration", new Configuracion { HorariosOperacion = new(), CapacidadMaximaConcurrente = 10 });
        var servicios = new ServicioService(db);
        var stock = new StockService(db);
        var lavados = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache),
            new PersonalService(db), new ConfiguracionService(db, cache), stock);
        var insumos = new InsumoService(db);
        var item = new Insumo { Nombre = "Jabon", Tipo = "Quimico" };
        Assert.True((await insumos.Guardar(item, true)).EsExitoso);
        Assert.True((await stock.Ajustar(item.Id, 1, true, "Ingreso", "actor")).EsExitoso);
        var lavado = await lavados.CrearLavadoConInicio(new Lavado { Id = "", Estado = "Pendiente", ClienteId = "c", VehiculoId = "v",
            Servicios = new() { new ServicioEnLavado { ServicioId = "s", Receta = new() { new RecetaInsumo { InsumoId = item.Id, DosisTeorica = 1 } } } } });

        var rechazado = await stock.Ajustar(item.Id, 1, false, "Egreso", "actor");
        Assert.False(rechazado.EsExitoso);
        Assert.Equal("El egreso supera el stock disponible.", rechazado.MensajeError);
        Assert.Equal  (1, (await insumos.Obtener(item.Id))!.StockActual);
        Assert.Single(await stock.ObtenerMovimientos(item.Id));
        await lavados.CancelarServicio(lavado.Id, "s", "motivo");
        Assert.True((await stock.Ajustar(item.Id, 1, false, "Egreso", "actor")).EsExitoso);
        Assert.Equal(0, (await insumos.Obtener(item.Id))!.StockActual);
        Assert.Equal(2, (await stock.ObtenerMovimientos(item.Id)).Count);
    }

    [LocalFirestoreFact]
    public async Task ConsumosDirectosNoUsanReservaDeOtroLavado()
    {
        var db = Db();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set("system_configuration", new Configuracion { HorariosOperacion = new(), CapacidadMaximaConcurrente = 10 });
        var servicios = new ServicioService(db);
        var stock = new StockService(db);
        var lavados = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache), new PersonalService(db), new ConfiguracionService(db, cache), stock);
        var insumos = new InsumoService(db);
        var item = new Insumo { Nombre = "Jabon", Tipo = "Quimico" };
        Assert.True((await insumos.Guardar(item, true)).EsExitoso);
        Assert.True((await stock.Ajustar(item.Id, 2, true, "Ingreso", "actor")).EsExitoso);
        var receta = new[] { new RecetaInsumo { InsumoId = item.Id, DosisTeorica = 1 } };
        await lavados.CrearLavadoConInicio(new Lavado { Id = "", Estado = "Pendiente", ClienteId = "c", VehiculoId = "v", Servicios = new() { new ServicioEnLavado { ServicioId = "s", Receta = receta.ToList() } } });
        foreach (var operacion in new Func<Task>[] { () => stock.Consumir("directo", item.Id, 2), () => stock.ConsumirReceta("receta", new[] { new RecetaInsumo { InsumoId = item.Id, DosisTeorica = 2 } }) })
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(operacion);
            Assert.Equal("Stock insuficiente para el insumo 'Jabon'.", error.Message);
            Assert.Equal(2, (await insumos.Obtener(item.Id))!.StockActual);
            Assert.Single(await stock.ObtenerMovimientos(item.Id));
            Assert.False((await stock.Ajustar(item.Id, 2, false, "egreso", "actor")).EsExitoso);
        }
        await stock.Consumir("directo", item.Id, 1);
        await stock.Consumir("directo", item.Id, 1);
        Assert.Equal(1, (await insumos.Obtener(item.Id))!.StockActual);
        Assert.Equal(2, (await stock.ObtenerMovimientos(item.Id)).Count);
    }

    [LocalFirestoreFact]
    public async Task ConsumirRecetaNoUsaReservaDeOtroLavado()
    {
        var db = Db();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set("system_configuration", new Configuracion { HorariosOperacion = new(), CapacidadMaximaConcurrente = 10 });
        var servicios = new ServicioService(db);
        var stock = new StockService(db);
        var lavados = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache), new PersonalService(db), new ConfiguracionService(db, cache), stock);
        var insumos = new InsumoService(db);
        var item = new Insumo { Nombre = "Jabon", Tipo = "Quimico" };
        Assert.True((await insumos.Guardar(item, true)).EsExitoso);
        Assert.True((await stock.Ajustar(item.Id, 2, true, "Ingreso", "actor")).EsExitoso);
        var receta = new[] { new RecetaInsumo { InsumoId = item.Id, DosisTeorica = 1 } };
        await lavados.CrearLavadoConInicio(new Lavado { Id = "", Estado = "Pendiente", ClienteId = "c", VehiculoId = "v", Servicios = new() { new ServicioEnLavado { ServicioId = "s", Receta = receta.ToList() } } });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => stock.ConsumirReceta("receta", new[] { new RecetaInsumo { InsumoId = item.Id, DosisTeorica = 2 } }));
        Assert.Equal("Stock insuficiente para el insumo 'Jabon'.", error.Message);
        Assert.Equal(2, (await insumos.Obtener(item.Id))!.StockActual);
        Assert.Single(await stock.ObtenerMovimientos(item.Id));
        Assert.False((await stock.Ajustar(item.Id, 2, false, "egreso", "actor")).EsExitoso);
        await stock.ConsumirReceta("receta", receta);
        await stock.ConsumirReceta("receta", receta);
        Assert.Equal(1, (await insumos.Obtener(item.Id))!.StockActual);
        Assert.Equal(2, (await stock.ObtenerMovimientos(item.Id)).Count);
        Assert.False((await stock.Ajustar(item.Id, 1, false, "egreso", "actor")).EsExitoso);
    }

    [LocalFirestoreFact]
    public async Task DesvioMayorNoConsumeReservaDeOtroLavado()
    {
        var db = Db();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set("system_configuration", new Configuracion { HorariosOperacion = new(), CapacidadMaximaConcurrente = 10 });
        var servicios = new ServicioService(db);
        var stock = new StockService(db);
        var lavados = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache), new PersonalService(db), new ConfiguracionService(db, cache), stock);
        var insumos = new InsumoService(db);
        var item = new Insumo { Nombre = "Jabon", Tipo = "Quimico" };
        Assert.True((await insumos.Guardar(item, true)).EsExitoso);
        Assert.True((await stock.Ajustar(item.Id, 2, true, "Ingreso", "actor")).EsExitoso);
        Lavado Nuevo(string v) => new() { Id = "", Estado = "Pendiente", ClienteId = "c", VehiculoId = v, Servicios = new() { new ServicioEnLavado { ServicioId = "s", Etapas = new() { new EtapaEnLavado { EtapaId = "e", Receta = new() { new RecetaInsumo { InsumoId = item.Id, DosisTeorica = 1 } } } } } } };
        var a = await lavados.CrearLavadoConInicio(Nuevo("a"));
        var b = await lavados.CrearLavadoConInicio(Nuevo("b"));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => lavados.FinalizarEtapa(a.Id, "s", "e", System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, decimal> { [item.Id] = 2 }), "desvio"));
        Assert.Equal("Stock insuficiente para el insumo 'Jabon'.", error.Message);
        Assert.Equal(2, (await insumos.Obtener(item.Id))!.StockActual);
        Assert.Single(await stock.ObtenerMovimientos(item.Id));
        Assert.Equal("EnProceso", (await lavados.ObtenerLavado(a.Id))!.Servicios[0].Etapas[0].Estado);
        Assert.False((await stock.Ajustar(item.Id, 1, false, "egreso", "actor")).EsExitoso);
        await lavados.FinalizarEtapa(b.Id, "s", "e");
        await lavados.FinalizarEtapa(a.Id, "s", "e");
        Assert.Equal(0, (await insumos.Obtener(item.Id))!.StockActual);
        Assert.Equal(3, (await stock.ObtenerMovimientos(item.Id)).Count);
    }

    [LocalFirestoreFact]
    public async Task ServiciosConsecutivosReutilizanInsumoYHerramientaConReintentos()
    {
        var db = Db();
        await PrepararHerramienta(db);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set("system_configuration", new Configuracion { HorariosOperacion = new(), CapacidadMaximaConcurrente = 10 });
        var servicios = new ServicioService(db);
        var stock = new StockService(db);
        var insumos = new InsumoService(db);
        var lavados = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache), new PersonalService(db), new ConfiguracionService(db, cache), stock);
        var jabon = new Insumo { Nombre = "Jabon", Tipo = "Quimico" };
        var hidro = new Insumo { Nombre = "Hidro", Tipo = "Herramienta" };
        foreach (var item in new[] { jabon, hidro })
        {
            Assert.True((await insumos.Guardar(item, true)).EsExitoso);
            Assert.True((await stock.Ajustar(item.Id, item == jabon ? 2 : 1, true, "Ingreso", "actor")).EsExitoso);
        }
        List<RecetaInsumo> Receta() => new[] { jabon, hidro }.Select(i => new RecetaInsumo { InsumoId = i.Id, DosisTeorica = 1 }).ToList();
        var lavado = await lavados.CrearLavadoConInicio(new Lavado { Id = "", Estado = "Pendiente", ClienteId = "c", VehiculoId = "v", Servicios = new() {
            new ServicioEnLavado { ServicioId = "s1", Orden = 1, Receta = Receta() },
            new ServicioEnLavado { ServicioId = "s2", Orden = 2, Receta = Receta() } } });
        await lavados.FinalizarServicio(lavado.Id, "s1");
        await lavados.FinalizarServicio(lavado.Id, "s1");
        var actual = (await lavados.ObtenerLavado(lavado.Id))!;
        Assert.Equal(new[] { "Realizado", "EnProceso" }, actual.Servicios.Select(s => s.Estado));
        Assert.Equal(1, (await insumos.Obtener(jabon.Id))!.StockActual);
        Assert.False((await stock.Ajustar(jabon.Id, 1, false, "egreso", "actor")).EsExitoso);
        Assert.False((await stock.ReservarHerramienta("competidor", hidro.Id, 1)).EsExitoso);
        await lavados.FinalizarServicio(lavado.Id, "s2");
        await lavados.FinalizarServicio(lavado.Id, "s2");
        actual = (await lavados.ObtenerLavado(lavado.Id))!;
        Assert.Equal("Realizado", actual.Estado);
        Assert.All(actual.Servicios, s => Assert.Equal("Realizado", s.Estado));
        Assert.Equal(0, (await insumos.Obtener(jabon.Id))!.StockActual);
        Assert.Equal(1, (await insumos.Obtener(hidro.Id))!.StockActual);
        Assert.Equal(2, (await stock.ObtenerMovimientos(jabon.Id)).Count(m => m.Tipo == nameof(TipoMovimientoStock.ConsumoEtapa)));
        Assert.Single(await stock.ObtenerMovimientos(hidro.Id));
        Assert.True((await stock.ReservarHerramienta("competidor", hidro.Id, 1)).EsExitoso);
    }

    [LocalFirestoreFact]
    public async Task SiguienteServicioEsperaHerramientaSinReservaParcialYReintenta()
    {
        var db = Db();
        await PrepararHerramienta(db);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set("system_configuration", new Configuracion { HorariosOperacion = new(), CapacidadMaximaConcurrente = 10 });
        var servicios = new ServicioService(db);
        var stock = new StockService(db);
        var insumos = new InsumoService(db);
        var lavados = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache), new PersonalService(db), new ConfiguracionService(db, cache), stock);
        var jabon = new Insumo { Nombre = "Jabon", Tipo = "Quimico" };
        var hidro = new Insumo { Nombre = "Hidro", Tipo = "Herramienta" };
        foreach (var item in new[] { jabon, hidro })
        {
            Assert.True((await insumos.Guardar(item, true)).EsExitoso);
            Assert.True((await stock.Ajustar(item.Id, 1, true, "Ingreso", "actor")).EsExitoso);
        }
        Assert.True((await stock.ReservarHerramienta("ocupada", hidro.Id, 1)).EsExitoso);
        var lavado = await lavados.CrearLavadoConInicio(new Lavado { Id = "", Estado = "Pendiente", ClienteId = "c", VehiculoId = "v", Servicios = new() {
            new ServicioEnLavado { ServicioId = "s1", Orden = 1 },
            new ServicioEnLavado { ServicioId = "s2", Orden = 2, Receta = new[] { jabon, hidro }.Select(i => new RecetaInsumo { InsumoId = i.Id, DosisTeorica = 1 }).ToList() } } });
        await lavados.FinalizarServicio(lavado.Id, "s1");
        await lavados.FinalizarServicio(lavado.Id, "s1");
        var actual = (await lavados.ObtenerLavado(lavado.Id))!;
        Assert.Equal("EnProceso", actual.Estado);
        Assert.Equal(new[] { "Realizado", "Pendiente" }, actual.Servicios.Select(s => s.Estado));
        Assert.Null(actual.Servicios[1].TiempoInicio);
        Assert.True((await stock.Ajustar(jabon.Id, 1, false, "egreso", "actor")).EsExitoso);
        Assert.True((await stock.Ajustar(jabon.Id, 1, true, "ingreso", "actor")).EsExitoso);
        var error = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => lavados.IniciarServicio(lavado.Id, "s2"));
        Assert.Equal("No hay herramientas suficientes disponibles.", error.Message);
        Assert.Equal("Pendiente", (await lavados.ObtenerLavado(lavado.Id))!.Servicios[1].Estado);
        Assert.True((await stock.LiberarHerramienta("ocupada", hidro.Id)).EsExitoso);
        await lavados.IniciarServicio(lavado.Id, "s2");
        Assert.False((await stock.Ajustar(jabon.Id, 1, false, "egreso", "actor")).EsExitoso);
        Assert.False((await stock.ReservarHerramienta("otra", hidro.Id, 1)).EsExitoso);
        await lavados.FinalizarServicio(lavado.Id, "s2");
        await lavados.FinalizarServicio(lavado.Id, "s2");
        Assert.Equal("Realizado", (await lavados.ObtenerLavado(lavado.Id))!.Estado);
        Assert.Equal(0, (await insumos.Obtener(jabon.Id))!.StockActual);
        Assert.Equal(1, (await insumos.Obtener(hidro.Id))!.StockActual);
        Assert.Single((await stock.ObtenerMovimientos(jabon.Id)).Where(m => m.Tipo == nameof(TipoMovimientoStock.ConsumoEtapa)));
        Assert.Single(await stock.ObtenerMovimientos(hidro.Id));
        Assert.True((await stock.ReservarHerramienta("otra", hidro.Id, 1)).EsExitoso);
    }

    [LocalFirestoreFact]
    public async Task AjusteManualIngresoRegistraAuditoriaTransaccionalConActorYBeforeAfter()
    {
        var db = Db();
        await db.Collection("insumos").Document("i1").SetAsync(new Insumo
        {
            Id = "i1", Nombre = "Químico", Tipo = "Quimico", UnidadMedida = "ml", StockActual = 10m
        });
        var stock = new StockService(db, new FakeEmailSender(), new ProveedorService(db), NullLogger<StockService>.Instance);

        var resultado = await stock.Ajustar("i1", 5m, true, "Ingreso de prueba", "user-actor-1", "actor1@example.invalid");

        Assert.True(resultado.EsExitoso);
        var insumo = (await db.Collection("insumos").Document("i1").GetSnapshotAsync()).ConvertTo<Insumo>();
        Assert.Equal(15m, insumo.StockActual);

        var movimientos = (await db.Collection("movimientosStock").GetSnapshotAsync()).Documents.Select(d => d.ConvertTo<MovimientoStock>()).ToList();
        var mov = Assert.Single(movimientos);
        Assert.Equal(nameof(TipoMovimientoStock.IngresoManual), mov.Tipo);
        Assert.Equal(5m, mov.Cantidad);
        Assert.Equal(15m, mov.SaldoResultante);
        Assert.Equal("user-actor-1", mov.UsuarioId);

        var auditorias = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents.Select(d => d.ConvertTo<AuditLog>()).ToList();
        var auditLog = Assert.Single(auditorias);
        Assert.Equal("user-actor-1", auditLog.UserId);
        Assert.Equal("actor1@example.invalid", auditLog.UserEmail);
        Assert.Equal("Ingreso manual de stock", auditLog.Action);
        Assert.Equal("i1", auditLog.TargetId);
        Assert.Equal("Insumo", auditLog.TargetType);
        Assert.Equal("Químico", auditLog.NombreHistorico);
        Assert.NotNull(auditLog.Antes);
        Assert.NotNull(auditLog.Despues);
        Assert.Equal("10", auditLog.Antes["StockActual"]);
        Assert.Equal("15", auditLog.Despues["StockActual"]);
    }

    [LocalFirestoreFact]
    public async Task AjusteManualEgresoRegistraAuditoriaTransaccionalConActorYBeforeAfter()
    {
        var db = Db();
        await db.Collection("insumos").Document("i1").SetAsync(new Insumo
        {
            Id = "i1", Nombre = "Químico", Tipo = "Quimico", UnidadMedida = "ml", StockActual = 10m
        });
        var stock = new StockService(db, new FakeEmailSender(), new ProveedorService(db), NullLogger<StockService>.Instance);

        var resultado = await stock.Ajustar("i1", 4m, false, "Egreso de prueba", "user-actor-2", "actor2@example.invalid");

        Assert.True(resultado.EsExitoso);
        var insumo = (await db.Collection("insumos").Document("i1").GetSnapshotAsync()).ConvertTo<Insumo>();
        Assert.Equal(6m, insumo.StockActual);

        var movimientos = (await db.Collection("movimientosStock").GetSnapshotAsync()).Documents.Select(d => d.ConvertTo<MovimientoStock>()).ToList();
        var mov = Assert.Single(movimientos);
        Assert.Equal(nameof(TipoMovimientoStock.EgresoManual), mov.Tipo);
        Assert.Equal(-4m, mov.Cantidad);
        Assert.Equal(6m, mov.SaldoResultante);
        Assert.Equal("user-actor-2", mov.UsuarioId);

        var auditorias = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents.Select(d => d.ConvertTo<AuditLog>()).ToList();
        var auditLog = Assert.Single(auditorias);
        Assert.Equal("user-actor-2", auditLog.UserId);
        Assert.Equal("actor2@example.invalid", auditLog.UserEmail);
        Assert.Equal("Egreso manual de stock", auditLog.Action);
        Assert.Equal("i1", auditLog.TargetId);
        Assert.Equal("Insumo", auditLog.TargetType);
        Assert.Equal("Químico", auditLog.NombreHistorico);
        Assert.NotNull(auditLog.Antes);
        Assert.NotNull(auditLog.Despues);
        Assert.Equal("10", auditLog.Antes["StockActual"]);
        Assert.Equal("6", auditLog.Despues["StockActual"]);
    }

    [LocalFirestoreFact]
    public async Task FalloSimuladoDeAuditoriaEnAjusteManualHaceRollbackTotalDeStockYKardex()
    {
        var db = Db();
        await db.Collection("insumos").Document("i1").SetAsync(new Insumo
        {
            Id = "i1", Nombre = "Químico", Tipo = "Quimico", UnidadMedida = "ml", StockActual = 10m
        });
        var stock = new StockService(db, new FakeEmailSender(), new ProveedorService(db), NullLogger<StockService>.Instance);
        stock.InterceptorAuditoria = log =>
        {
            if (log.Action == "Ingreso manual de stock")
                throw new InvalidOperationException("Fallo forzado al auditar ingreso transaccional");
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            stock.Ajustar("i1", 5m, true, "Ingreso", "actor", "actor@example.invalid"));

        var insumo = (await db.Collection("insumos").Document("i1").GetSnapshotAsync()).ConvertTo<Insumo>();
        Assert.Equal(10m, insumo.StockActual);
        Assert.Empty((await db.Collection("movimientosStock").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);

        stock.InterceptorAuditoria = log =>
        {
            if (log.Action == "Egreso manual de stock")
                throw new InvalidOperationException("Fallo forzado al auditar egreso transaccional");
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            stock.Ajustar("i1", 3m, false, "Egreso", "actor", "actor@example.invalid"));

        insumo = (await db.Collection("insumos").Document("i1").GetSnapshotAsync()).ConvertTo<Insumo>();
        Assert.Equal(10m, insumo.StockActual);
        Assert.Empty((await db.Collection("movimientosStock").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task RechazosEnAjusteManualNoGeneranAuditoriaNiMovimientoNiModificanStock()
    {
        var db = Db();
        await db.Collection("insumos").Document("i1").SetAsync(new Insumo
        {
            Id = "i1", Nombre = "Químico", Tipo = "Quimico", UnidadMedida = "ml", StockActual = 5m
        });
        var stock = new StockService(db, new FakeEmailSender(), new ProveedorService(db), NullLogger<StockService>.Instance);

        var r1 = await stock.Ajustar("i1", 0m, true, "Cero", "actor", "actor@example.invalid");
        Assert.False(r1.EsExitoso);

        var r2 = await stock.Ajustar("i1", -2m, true, "Negativo", "actor", "actor@example.invalid");
        Assert.False(r2.EsExitoso);

        var r3 = await stock.Ajustar("i1", 2m, true, "   ", "actor", "actor@example.invalid");
        Assert.False(r3.EsExitoso);

        var r4 = await stock.Ajustar("missing", 2m, true, "Inexistente", "actor", "actor@example.invalid");
        Assert.False(r4.EsExitoso);

        var r5 = await stock.Ajustar("i1", 10m, false, "Supera stock", "actor", "actor@example.invalid");
        Assert.False(r5.EsExitoso);

        var insumo = (await db.Collection("insumos").Document("i1").GetSnapshotAsync()).ConvertTo<Insumo>();
        Assert.Equal(5m, insumo.StockActual);
        Assert.Empty((await db.Collection("movimientosStock").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task AjustesManualesConcurrentesMantienenCoherenciaDeStockKardexYAuditoria()
    {
        var db = Db();
        await db.Collection("insumos").Document("i_conc").SetAsync(new Insumo
        {
            Id = "i_conc", Nombre = "Insumo Concurrente", Tipo = "Quimico", UnidadMedida = "ml", StockActual = 100m
        });
        var stock = new StockService(db, new FakeEmailSender(), new ProveedorService(db), NullLogger<StockService>.Instance);

        var tareas = new List<Task<ResultadoOperacion>>();
        for (var i = 0; i < 5; i++)
        {
            var idx = i;
            tareas.Add(stock.Ajustar("i_conc", 10m, true, $"Ingreso {idx}", $"user-in-{idx}", $"in{idx}@example.invalid"));
            tareas.Add(stock.Ajustar("i_conc", 5m, false, $"Egreso {idx}", $"user-eg-{idx}", $"eg{idx}@example.invalid"));
        }
        await Task.WhenAll(tareas);

        Assert.All(tareas, t => Assert.True(t.Result.EsExitoso));

        var insumo = (await db.Collection("insumos").Document("i_conc").GetSnapshotAsync()).ConvertTo<Insumo>();
        // 100 + (5 * 10) - (5 * 5) = 125
        Assert.Equal(125m, insumo.StockActual);

        var movimientos = (await db.Collection("movimientosStock").WhereEqualTo("InsumoId", "i_conc").GetSnapshotAsync()).Documents
            .Select(d => d.ConvertTo<MovimientoStock>()).ToList();
        Assert.Equal(10, movimientos.Count);

        var auditorias = (await db.Collection("registros_auditoria").WhereEqualTo("TargetId", "i_conc").GetSnapshotAsync()).Documents
            .Select(d => d.ConvertTo<AuditLog>()).ToList();
        Assert.Equal(10, auditorias.Count);
        Assert.Equal(5, auditorias.Count(a => a.Action == "Ingreso manual de stock"));
        Assert.Equal(5, auditorias.Count(a => a.Action == "Egreso manual de stock"));
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<EmailMessage> Messages { get; } = new();

        public Task<ResultadoEnvioEmail> EnviarAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.FromResult(new ResultadoEnvioEmail(true, "ok", "fake-id"));
        }
    }
}

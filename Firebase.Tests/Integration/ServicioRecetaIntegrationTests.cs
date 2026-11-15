using Firebase.Models;
using Firebase.Services;
using Microsoft.Extensions.Caching.Memory;
using Google.Api.Gax;
using Google.Cloud.Firestore;

namespace Firebase.Tests.Integration;

public sealed class ServicioRecetaIntegrationTests
{
    private static FirestoreDb Db() => new FirestoreDbBuilder
    {
        ProjectId = "demo-receta-" + Guid.NewGuid().ToString("N")[..16],
        EmulatorDetection = EmulatorDetection.EmulatorOnly
    }.Build();

    private static Servicio Nuevo() => new()
    {
        Id = "", Nombre = "Lavado", Tipo = "Lavado", TipoVehiculo = "Auto",
        Descripcion = "Descripcion", Estado = "Activo", Precio = 123.5m, TiempoEstimado = 30
    };

    private static RecetaInsumo Item(decimal dosis) => new()
    {
        InsumoId = "i1", NombreInsumo = "Jabon", UnidadMedida = "ml", DosisTeorica = dosis
    };

    [LocalFirestoreFact]
    public async Task CrearYActualizarConservaRecetasServicioYEtapas()
    {
        var db = Db();
        await db.Collection("insumos").Document("i1").SetAsync(new Insumo { Id = "i1", Nombre = "Jabon", Tipo = "Quimico", UnidadMedida = "ml" });
        var service = new ServicioService(db);
        var servicio = Nuevo();
        servicio.Receta.Add(Item(1.25m));
        await service.CrearServicio(servicio);
        var leido = await service.ObtenerServicio(servicio.Id);
        Verificar(Assert.Single(leido.Receta), 1.25m);
        leido.Receta[0].DosisTeorica = 3.75m;
        await service.ActualizarServicio(leido);
        var actualizado = await service.ObtenerServicio(servicio.Id);
        Verificar(Assert.Single(actualizado.Receta), 3.75m);
        Assert.Empty(actualizado.Etapas);
        Assert.Equal(123.5m, actualizado.Precio);
        var listado = await service.ObtenerServicios();
        Verificar(Assert.Single(Assert.Single(listado).Receta), 3.75m);
    }

    [LocalFirestoreFact]
    public async Task RegistroHistoricoSinRecetasSeLeeYActualiza()
    {
        var db = Db();
        await db.Collection("servicios").Document("historico").SetAsync(new Dictionary<string, object>
        {
            ["Nombre"] = "Lavado", ["Tipo"] = "Lavado", ["TipoVehiculo"] = "Auto",
            ["Descripcion"] = "Descripcion", ["Estado"] = "Activo", ["Precio"] = 10.0,
            ["TiempoEstimado"] = 20, ["Etapas"] = new[] { new Dictionary<string, object> { ["Id"] = "e1", ["Nombre"] = "Limpieza" } }
        });
        var service = new ServicioService(db);
        var leido = await service.ObtenerServicio("historico");
        Assert.Empty(leido.Receta);
        Assert.Empty(Assert.Single(leido.Etapas).Receta);
        await service.ActualizarServicio(leido);
        var actualizado = await service.ObtenerServicio("historico");
        Assert.Empty(actualizado.Receta);
        Assert.Empty(Assert.Single(actualizado.Etapas).Receta);
        Assert.Equal(10m, actualizado.Precio);
    }

    [LocalFirestoreFact]
    public async Task RecetasDualConUnidadesCompatiblesSePersistenYRechazanIncompatibles()
    {
        var db = Db();
        await db.Collection("tiposInsumo").Document("tipo1").SetAsync(new TipoInsumo { Id = "tipo1", Nombre = "Jabon", Categoria = "Volumen", UnidadResumen = "l" });
        await db.Collection("insumos").Document("i1").SetAsync(new Insumo { Id = "i1", Nombre = "Jabon liquido", TipoId = "tipo1", Tipo = "Jabon", UnidadMedida = "l" });
        var service = new ServicioService(db);
        var servicio = Nuevo();
        servicio.Etapas.Add(new Etapa { Id = "e1", Nombre = "Limpieza", Receta = new() { new RecetaInsumo { InsumoId = "i1", UnidadMedida = "ml", DosisTeorica = 250 }, new RecetaInsumo { Modalidad = "Tipo", TipoId = "tipo1", UnidadMedida = "l", DosisTeorica = 1 } } });
        await service.CrearServicio(servicio);
        var saved = await service.ObtenerServicio(servicio.Id);
        var items = Assert.Single(saved.Etapas).Receta;
        Assert.Equal("Producto", items[0].Modalidad);
        Assert.Equal("Jabon liquido", items[0].NombreInsumo);
        Assert.Equal("ml", items[0].UnidadMedida);
        var tipo = items[1];
        Assert.Equal("Tipo", tipo.Modalidad);
        Assert.Equal("tipo1", tipo.TipoId);
        Assert.Equal("Jabon", tipo.NombreTipo);
        Assert.Equal("Volumen", tipo.Categoria);
        Assert.Equal("l", tipo.UnidadMedida);
        saved.Etapas[0].Receta[0].UnidadMedida = "kg";
        await Assert.ThrowsAsync<ArgumentException>(() => service.ActualizarServicio(saved));
        Assert.Equal("ml", (await service.ObtenerServicio(servicio.Id)).Etapas[0].Receta[0].UnidadMedida);
    }

    [LocalFirestoreFact]
    public async Task EditarSinCambiarRecetaPreservaSnapshotsLegacyYTipoInactivo()
    {
        var db = Db();
        var doc = db.Collection("servicios").Document("historico");
        await doc.SetAsync(new Dictionary<string, object> {
            ["Nombre"] = "Lavado", ["Tipo"] = "Lavado", ["TipoVehiculo"] = "Auto", ["Descripcion"] = "Original", ["Estado"] = "Activo", ["Precio"] = 10.0, ["TiempoEstimado"] = 20,
            ["Receta"] = new[] { new Dictionary<string, object> { ["InsumoId"] = "viejo", ["NombreInsumo"] = "Nombre historico", ["UnidadMedida"] = "ml", ["DosisTeorica"] = 25.0 } },
            ["Etapas"] = new[] { new Dictionary<string, object> { ["Id"] = "e1", ["Nombre"] = "Limpieza", ["Receta"] = new[] { new Dictionary<string, object> { ["Modalidad"] = "Tipo", ["TipoId"] = "tipo1", ["NombreTipo"] = "Nombre anterior", ["Categoria"] = "Volumen", ["InsumoId"] = "", ["NombreInsumo"] = "", ["UnidadMedida"] = "l", ["DosisTeorica"] = 1.0 } } } }
        });
        await db.Collection("tiposInsumo").Document("tipo1").SetAsync(new TipoInsumo { Id = "tipo1", Nombre = "Renombrado", Categoria = "Volumen", UnidadResumen = "l", Estado = "Inactivo" });
        var service = new ServicioService(db);
        var original = await service.ObtenerServicio("historico");
        original.Descripcion = "Actualizado";
        original.Receta[0].NombreInsumo = "Manipulado";
        original.Etapas[0].Receta[0].NombreTipo = "Manipulado";
        await service.ActualizarServicio(original);
        var guardado = await service.ObtenerServicio("historico");
        Assert.Equal("Actualizado", guardado.Descripcion);
        Assert.Equal("Producto", guardado.Receta[0].Modalidad);
        Assert.Equal("Nombre historico", guardado.Receta[0].NombreInsumo);
        Assert.Equal("Nombre anterior", guardado.Etapas[0].Receta[0].NombreTipo);
        Assert.Equal("Volumen", guardado.Etapas[0].Receta[0].Categoria);
        guardado.Etapas[0].Receta[0].DosisTeorica = 2;
        await Assert.ThrowsAsync<ArgumentException>(() => service.ActualizarServicio(guardado));
        var rechazado = await service.ObtenerServicio("historico");
        Assert.Equal(1m, rechazado.Etapas[0].Receta[0].DosisTeorica);
        Assert.Equal("Nombre anterior", rechazado.Etapas[0].Receta[0].NombreTipo);
        Assert.Equal("Actualizado", rechazado.Descripcion);
        rechazado.Receta[0].UnidadMedida = "l";
        await Assert.ThrowsAsync<ArgumentException>(() => service.ActualizarServicio(rechazado));
        rechazado.Receta[0].UnidadMedida = "ml";
        rechazado.Receta[0].InsumoId = "nuevo-inexistente";
        await Assert.ThrowsAsync<ArgumentException>(() => service.ActualizarServicio(rechazado));
        var final = await service.ObtenerServicio("historico");
        Assert.Equal("viejo", final.Receta[0].InsumoId);
        Assert.Equal("ml", final.Receta[0].UnidadMedida);
        Assert.Equal("Nombre historico", final.Receta[0].NombreInsumo);
    }

    [LocalFirestoreFact]
    public async Task RecetasInvalidasNoCreanServicio()
    {
        var db = Db();
        await db.Collection("tiposInsumo").Document("tipo1").SetAsync(new TipoInsumo { Id = "tipo1", Nombre = "Jabon", Categoria = "Volumen", UnidadResumen = "l" });
        await db.Collection("insumos").Document("i1").SetAsync(new Insumo { Id = "i1", Nombre = "Jabon", Tipo = "Jabon", UnidadMedida = "l" });
        var service = new ServicioService(db);
        var casos = new[] {
            new RecetaInsumo { InsumoId = "i1", UnidadMedida = "kg", DosisTeorica = 1 },
            new RecetaInsumo { InsumoId = "i1", UnidadMedida = "ml", DosisTeorica = 0 },
            new RecetaInsumo { InsumoId = "ausente", UnidadMedida = "ml", DosisTeorica = 1 },
            new RecetaInsumo { Modalidad = "Tipo", TipoId = "tipo1", UnidadMedida = "kg", DosisTeorica = 1 },
            new RecetaInsumo { Modalidad = "Tipo", TipoId = "tipo1", InsumoId = "i1", UnidadMedida = "ml", DosisTeorica = 1 }
        };
        foreach (var item in casos) {
            var servicio = Nuevo(); servicio.Receta.Add(item);
            await Assert.ThrowsAsync<ArgumentException>(() => service.CrearServicio(servicio));
            Assert.Empty((await db.Collection("servicios").GetSnapshotAsync()).Documents);
        }
    }

    [LocalFirestoreFact]
    public async Task ServicioConEtapasYRecetaPropiaSeRechazaSinModificarSnapshot()
    {
        var db = Db();
        await db.Collection("insumos").Document("i1").SetAsync(new Insumo { Id = "i1", Nombre = "Jabon", Tipo = "Quimico", UnidadMedida = "ml" });
        var service = new ServicioService(db);
        var servicio = Nuevo();
        servicio.Receta.Add(Item(2));
        await service.CrearServicio(servicio);
        servicio.Etapas.Add(new Etapa { Id = "e", Nombre = "Limpieza" });
        await Assert.ThrowsAsync<ArgumentException>(() => service.ActualizarServicio(servicio));
        var guardado = await service.ObtenerServicio(servicio.Id);
        Assert.Empty(guardado.Etapas);
        Verificar(Assert.Single(guardado.Receta), 2);
    }

    [LocalFirestoreFact]
    public async Task EliminarEtapaConRecetaExigePrimeroVaciarla()
    {
        var db = Db();
        await db.Collection("insumos").Document("i1").SetAsync(new Insumo { Id = "i1", Nombre = "Jabon", Tipo = "Quimico", UnidadMedida = "ml" });
        var service = new ServicioService(db);
        var servicio = Nuevo();
        servicio.Etapas.Add(new Etapa { Id = "e1", Nombre = "Limpieza", Receta = new() { Item(2) } });
        await service.CrearServicio(servicio);
        servicio.Etapas.Clear();
        await Assert.ThrowsAsync<ArgumentException>(() => service.ActualizarServicio(servicio));
        var guardado = await service.ObtenerServicio(servicio.Id);
        Verificar(Assert.Single(Assert.Single(guardado.Etapas).Receta), 2);
        guardado.Etapas[0].Receta.Clear();
        await service.ActualizarServicio(guardado);
        guardado.Etapas.Clear();
        await service.ActualizarServicio(guardado);
        Assert.Empty((await service.ObtenerServicio(servicio.Id)).Etapas);
    }

    [LocalFirestoreFact]
    public async Task MutacionPosteriorCatalogoPreservaSnapshotsServicioYLavado()
    {
        var db = Db();
        var actor = new ActorAuditoria("snapshot-test", "snapshot@example.invalid");
        var tipos = new TipoInsumoService(db);
        var tipo = new TipoInsumo { Nombre = "Jabon original", Categoria = "Volumen", UnidadResumen = "l" };
        Assert.True((await tipos.Guardar(tipo, true, actor)).EsExitoso);
        var insumos = new InsumoService(db);
        var insumo = new Insumo { Nombre = "Producto original", TipoId = tipo.Id, Tipo = tipo.Nombre, UnidadMedida = "l" };
        Assert.True((await insumos.GuardarConAuditoria(insumo, true, actor)).Resultado.EsExitoso);
        var service = new ServicioService(db);
        var servicio = Nuevo();
        servicio.Etapas.Add(new Etapa { Id = "etapa", Nombre = "Limpieza", Receta = new() {
            new RecetaInsumo { InsumoId = insumo.Id, UnidadMedida = "ml", DosisTeorica = 250m },
            new RecetaInsumo { Modalidad = "Tipo", TipoId = tipo.Id, UnidadMedida = "l", DosisTeorica = 1m }
        } });
        await service.CrearServicio(servicio);
        var original = await service.ObtenerServicio(servicio.Id);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var lavados = new LavadoService(db, service, new PaqueteServicioService(db, service, cache), new PersonalService(db), new ConfiguracionService(db, cache));
        var lavado = new Lavado { Id = "snapshot-lavado", Estado = "Finalizado", ClienteId = "c", VehiculoId = "v", FechaCreacion = DateTime.UtcNow, Servicios = new() { lavados.CrearServicioEnLavado(original, 0, null, null) } };
        await lavados.ActualizarLavado(lavado);
        var antesLavado = (await lavados.ObtenerLavado(lavado.Id))!;
        static (string, string, string, string, string, string, string, decimal)[] Campos(IEnumerable<RecetaInsumo> receta) =>
            receta.Select(i => (i.Modalidad, i.TipoId, i.NombreTipo, i.Categoria, i.InsumoId, i.NombreInsumo, i.UnidadMedida, i.DosisTeorica)).ToArray();
        var esperado = Campos(Assert.Single(original.Etapas).Receta);
        Assert.Equal(2, esperado.Length);
        Assert.Equal(("Producto", "", "", "Volumen", insumo.Id, "Producto original", "ml", 250m), esperado[0]);
        Assert.Equal(("Tipo", tipo.Id, "Jabon original", "Volumen", "", "", "l", 1m), esperado[1]);
        Assert.Empty(original.Receta);
        var etapaLavado = Assert.Single(Assert.Single(antesLavado.Servicios).Etapas);
        Assert.Equal("etapa", etapaLavado.EtapaId);
        Assert.Equal(esperado, Campos(etapaLavado.Receta));
        insumo = (await insumos.Obtener(insumo.Id))!;
        insumo.Nombre = "Producto nuevo";
        var cambioInsumo = (await insumos.GuardarConAuditoria(insumo, false, actor)).Resultado;
        Assert.True(cambioInsumo.EsExitoso, cambioInsumo.MensajeError);
        tipo = (await tipos.ObtenerPorId(tipo.Id))!;
        tipo.Nombre = "Jabon nuevo";
        var cambioTipo = await tipos.Guardar(tipo, false, actor);
        Assert.True(cambioTipo.EsExitoso, cambioTipo.MensajeError);
        Assert.Equal("Producto nuevo", (await insumos.Obtener(insumo.Id))!.Nombre);
        Assert.Equal("Jabon nuevo", (await tipos.ObtenerPorId(tipo.Id))!.Nombre);
        var despuesServicio = await service.ObtenerServicio(servicio.Id);
        Assert.Empty(despuesServicio.Receta);
        var etapaServicio = Assert.Single(despuesServicio.Etapas);
        Assert.Equal("etapa", etapaServicio.Id);
        Assert.Equal(esperado, Campos(etapaServicio.Receta));
        var despuesLavado = (await lavados.ObtenerLavado(lavado.Id))!;
        var servicioLavado = Assert.Single(despuesLavado.Servicios);
        Assert.Equal(servicio.Id, servicioLavado.ServicioId);
        Assert.Empty(servicioLavado.Receta);
        var etapaFinal = Assert.Single(servicioLavado.Etapas);
        Assert.Equal("etapa", etapaFinal.EtapaId);
        Assert.Equal(esperado, Campos(etapaFinal.Receta));
    }

    [LocalFirestoreFact]
    public async Task EditarDosisServicioPreservaConsumoYSnapshotLavadoFinalizado()
    {
        var db = Db();
        var actor = new ActorAuditoria("snapshot-test", "snapshot@example.invalid");
        var tipos = new TipoInsumoService(db);
        var tipo = new TipoInsumo { Nombre = "Jabon", Categoria = "Volumen", UnidadResumen = "l" };
        Assert.True((await tipos.Guardar(tipo, true, actor)).EsExitoso);
        var insumos = new InsumoService(db);
        var insumo = new Insumo { Nombre = "Producto", TipoId = tipo.Id, Tipo = tipo.Nombre, UnidadMedida = "l" };
        Assert.True((await insumos.GuardarConAuditoria(insumo, true, actor)).Resultado.EsExitoso);
        var stock = new StockService(db);
        Assert.True((await stock.Ajustar(insumo.Id, 1m, true, "Ingreso", "snapshot-test")).EsExitoso);
        var servicios = new ServicioService(db);
        var servicio = Nuevo();
        servicio.Etapas.Add(new Etapa { Id = "etapa", Nombre = "Limpieza", Receta = new() {
            new RecetaInsumo { InsumoId = insumo.Id, UnidadMedida = "ml", DosisTeorica = 250m }
        } });
        await servicios.CrearServicio(servicio);
        var original = await servicios.ObtenerServicio(servicio.Id);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var lavados = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache), new PersonalService(db), new ConfiguracionService(db, cache), stock);
        var instancia = lavados.CrearServicioEnLavado(original, 0, null, null);
        instancia.Estado = "EnProceso";
        var lavado = new Lavado { Id = "lavado-historico", Estado = "EnProceso", ClienteId = "c", VehiculoId = "v", FechaCreacion = DateTime.UtcNow, Servicios = new() { instancia } };
        await lavados.ActualizarLavado(lavado);
        await lavados.IniciarEtapa(lavado.Id, servicio.Id, "etapa");
        await lavados.FinalizarEtapa(lavado.Id, servicio.Id, "etapa");
        var historico = (await lavados.ObtenerLavado(lavado.Id))!;
        Assert.Equal("Realizado", historico.Estado);
        var etapa = Assert.Single(Assert.Single(historico.Servicios).Etapas);
        Assert.Equal("Realizado", etapa.Estado);
        static (string, string, string, string, string, string, string, decimal) Campos(RecetaInsumo r) =>
            (r.Modalidad, r.TipoId, r.NombreTipo, r.Categoria, r.InsumoId, r.NombreInsumo, r.UnidadMedida, r.DosisTeorica);
        var recetaHistorica = Campos(Assert.Single(etapa.Receta));
        Assert.Equal(("Producto", "", "", "Volumen", insumo.Id, "Producto", "ml", 250m), recetaHistorica);
        var movimientos = await stock.ObtenerMovimientos(insumo.Id);
        Assert.Equal(2, movimientos.Count);
        var consumo = Assert.Single(movimientos.Where(m => m.Tipo == "ConsumoEtapa"));
        Assert.Equal(-0.25m, consumo.Cantidad);
        Assert.Equal(0.75m, consumo.SaldoResultante);
        Assert.Equal(0.75m, (await insumos.Obtener(insumo.Id))!.StockActual);
        static (string, string, DateTime, string, decimal, decimal, string, string, string) Movimiento(MovimientoStock m) =>
            (m.Id, m.InsumoId, m.Fecha, m.Tipo, m.Cantidad, m.SaldoResultante, m.ReferenciaId, m.UsuarioId, m.Justificacion);
        var kardexAntes = movimientos.Select(Movimiento).OrderBy(m => m.Item1).ToArray();
        original.Etapas[0].Receta[0].DosisTeorica = 500m;
        await servicios.ActualizarServicio(original);
        Assert.Equal(500m, Assert.Single(Assert.Single((await servicios.ObtenerServicio(servicio.Id)).Etapas).Receta).DosisTeorica);
        var despues = (await lavados.ObtenerLavado(lavado.Id))!;
        Assert.Equal("Realizado", despues.Estado);
        var etapaDespues = Assert.Single(Assert.Single(despues.Servicios).Etapas);
        Assert.Equal("Realizado", etapaDespues.Estado);
        Assert.Equal(recetaHistorica, Campos(Assert.Single(etapaDespues.Receta)));
        Assert.Equal(0.75m, (await insumos.Obtener(insumo.Id))!.StockActual);
        var kardexDespues = await stock.ObtenerMovimientos(insumo.Id);
        Assert.Equal(2, kardexDespues.Count);
        Assert.Equal(kardexAntes, kardexDespues.Select(Movimiento).OrderBy(m => m.Item1).ToArray());
    }

    private static void Verificar(RecetaInsumo item, decimal dosis)
    {
        Assert.Equal("i1", item.InsumoId);
        Assert.Equal("Jabon", item.NombreInsumo);
        Assert.Equal("ml", item.UnidadMedida);
        Assert.Equal(dosis, item.DosisTeorica);
    }
}

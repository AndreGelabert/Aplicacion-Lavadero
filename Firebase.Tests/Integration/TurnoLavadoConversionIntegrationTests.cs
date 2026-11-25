using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Firebase.Tests.Integration;

public class TurnoLavadoConversionIntegrationTests
{
    private static FirestoreDb Db() => new FirestoreDbBuilder { ProjectId = "demo-conversion-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
    private static LavadoService Service(FirestoreDb db, MemoryCache cache)
    {
        var services = new ServicioService(db);
        return new LavadoService(db, services, new PaqueteServicioService(db, services, cache), new PersonalService(db), new ConfiguracionService(db, cache));
    }
    private static Turno Appointment(string tool)
    {
        var now = DateTime.UtcNow;
        return new Turno { Id = "turno", ClienteId = "cliente", Estado = "Pendiente", Origen = "web", ClaveIdempotencia = "clave", InicioUtc = now, FinUtc = now.AddMinutes(10), PrecioAcordado = 72, Descuento = 10,
            DesglosePrecio = new() { Version = 1, Subtotal = 80, Total = 72 },
            Vehiculos = [new() { VehiculoId = "vehiculo", CantidadEmpleadosRequeridos = 1, Servicios = [new() { ServicioId = "s", Precio = 80, TiempoEstimado = 10 }] }],
            RecetaOperativa = new() { Version = 1, Servicios = [new() { ServicioId = "s", Nombre = "Congelado", Tipo = "Lavado", Receta = [
                new() { Modalidad = "Tipo", TipoId = "tipo", UnidadMedida = "ml", DosisTeorica = 250 },
                new() { InsumoId = tool, UnidadMedida = "unidad", DosisTeorica = 1 } ] }] } };
    }
    private static Task ConfigurarCapacidad(FirestoreDb db, int capacidad = 10) =>
        db.Collection("configuracion").Document("system_config").SetAsync(new { CapacidadMaximaConcurrente = capacidad, ToleranciaLlegadaTurnosMinutos = 15 });

    private static async Task<string[]> Snapshot(FirestoreDb db)
    {
        var result = new List<string>();
        foreach (var name in new[] { "turnos", "lavados", "coordinacion", "insumos", "reservasHerramientas", "reservasHerramientasTotales", "movimientosStock", "configuracion" })
        {
            var docs = await db.Collection(name).GetSnapshotAsync();
            result.AddRange(docs.Documents.Select(x => $"{name}/{x.Id}:{x.UpdateTime}:{System.Text.Json.JsonSerializer.Serialize(x.ToDictionary())}"));
        }
        return result.OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }
    private static async Task<string> SetupItems(FirestoreDb db, decimal stockAmount)
    {
        await db.Collection("insumos").Document("jabon").SetAsync(new Insumo { Id = "jabon", Nombre = "Jabon", TipoId = "tipo", Tipo = "Jabon", UnidadMedida = "l", StockActual = stockAmount });
        await new TipoInsumoService(db).AsegurarTipoHerramienta("actor", "actor@example.invalid");
        var tool = new Insumo { Nombre = "Herramienta", Tipo = "Herramienta", UnidadMedida = "unidad" };
        Assert.True((await new InsumoService(db).Guardar(tool, true)).EsExitoso);
        Assert.True((await new StockService(db).Ajustar(tool.Id, 1, true, "Ingreso", "actor")).EsExitoso);
        return tool.Id;
    }

    [LocalFirestoreFact]
    public async Task RecetaCongeladaReservaSeleccionYLiberaHerramientaSinConsumirla()
    {
        var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = Service(db, cache); var stock = new StockService(db);
        await ConfigurarCapacidad(db);
        var tool = await SetupItems(db, 1);
        var turno = Appointment(tool); var turnRef = db.Collection("turnos").Document("turno");
        await turnRef.SetAsync(turno);
        await db.Collection("empleados").Document("empleado").SetAsync(new { Estado = "Activo" });
        await db.Collection("servicios").Document("s").SetAsync(new { Nombre = "Catalogo cambiado" });
        var before = await Snapshot(db);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConvertirTurnoEnLavado("turno", null));
        Assert.Equal("Debe seleccionar un producto para cada ítem por tipo.", error.Message);
        Assert.Equal(before, await Snapshot(db));
        var selected = new Dictionary<int, string> { [0] = "jabon" };
        var id = await service.ConvertirTurnoEnLavado("turno", selected);
        var wash = (await service.ObtenerLavado(id))!;
        Assert.Equal("Congelado", wash.Servicios[0].ServicioNombre);
        Assert.Equal("jabon", wash.Servicios[0].Receta[0].InsumoId);
        Assert.Equal(250m, wash.Servicios[0].Receta[0].DosisTeorica);
        Assert.Equal("", (await turnRef.GetSnapshotAsync()).ConvertTo<Turno>().RecetaOperativa!.Servicios[0].Receta[0].InsumoId);
        Assert.Equal(0.25m, (await stock.ObtenerReservasInventario(new[] { "jabon" }))["jabon"]);
        Assert.Equal(1m, (await db.Collection("insumos").Document("jabon").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
        Assert.False((await stock.ReservarHerramienta("competidor", tool, 1)).EsExitoso);
        Assert.Empty(await stock.ObtenerMovimientos("jabon"));
        var converted = await Snapshot(db);
        Assert.Equal(id, await service.ConvertirTurnoEnLavado("turno", selected));
        Assert.Equal(converted, await Snapshot(db));
        await service.FinalizarServicio(id, "s");
        Assert.Equal(0.75m, (await db.Collection("insumos").Document("jabon").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
        Assert.Equal(1m, (await db.Collection("insumos").Document(tool).GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
        Assert.Equal(0m, (await stock.ObtenerReservasInventario(new[] { "jabon" }))["jabon"]);
        Assert.True((await stock.ReservarHerramienta("competidor", tool, 1)).EsExitoso);
        Assert.Single(await stock.ObtenerMovimientos("jabon"));
        Assert.DoesNotContain(await stock.ObtenerMovimientos(tool), x => x.Tipo == "ConsumoEtapa");
    }

    [LocalFirestoreFact]
    public async Task RecetaHerramientaConservaBanderaEnInstantaneaYLavadoConOSinEtapas()
    {
        foreach (var conEtapa in new[] { false, true })
        {
            var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = Service(db, cache); var stock = new StockService(db);
            await ConfigurarCapacidad(db);
            var tool = await SetupItems(db, 1);
            var receta = new RecetaInsumo { InsumoId = tool, UnidadMedida = "unidad", DosisTeorica = 1, EsHerramienta = true };
            var catalogo = new Servicio { Id = "s", Nombre = "Congelado", Tipo = "Lavado", TipoVehiculo = "Auto", Descripcion = "Servicio", Estado = "Activo" };
            if (conEtapa) catalogo.Etapas.Add(new Etapa { Id = "e", Nombre = "Etapa", Receta = [receta] });
            else catalogo.Receta.Add(receta);
            var turno = Appointment(tool);
            turno.RecetaOperativa = TurnoSnapshotBuilder.CapturarRecetaOperativa([catalogo]);
            await db.Collection("turnos").Document("turno").SetAsync(turno);
            var guardado = (await db.Collection("turnos").Document("turno").GetSnapshotAsync()).ConvertTo<Turno>();
            Assert.True((conEtapa ? guardado.RecetaOperativa!.Servicios[0].Etapas[0].Receta : guardado.RecetaOperativa!.Servicios[0].Receta)[0].EsHerramienta);
            await db.Collection("empleados").Document("empleado").SetAsync(new { Estado = "Activo" });
            var id = await service.ConvertirTurnoEnLavado("turno", null);
            var lavado = (await service.ObtenerLavado(id))!;
            Assert.True((conEtapa ? lavado.Servicios[0].Etapas[0].Receta : lavado.Servicios[0].Receta)[0].EsHerramienta);
            Assert.False((await stock.ReservarHerramienta("competidor", tool, 1)).EsExitoso);
            if (conEtapa) await service.FinalizarEtapa(id, "s", "e");
            else await service.FinalizarServicio(id, "s");
            Assert.Equal(1m, (await db.Collection("insumos").Document(tool).GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
            Assert.True((await stock.ReservarHerramienta("competidor", tool, 1)).EsExitoso);
            Assert.DoesNotContain(await stock.ObtenerMovimientos(tool), x => x.Tipo == "ConsumoEtapa");
        }
    }

    [LocalFirestoreFact]
    public async Task ConversionConcurrenteDelMismoTurnoReservaUnaSolaVez()
    {
        var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = Service(db, cache); var stock = new StockService(db);
        await ConfigurarCapacidad(db);
        var tool = await SetupItems(db, 1);
        await db.Collection("turnos").Document("turno").SetAsync(Appointment(tool));
        await db.Collection("empleados").Document("empleado").SetAsync(new { Estado = "Activo" });
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<string> Convertir()
        {
            await start.Task;
            return await service.ConvertirTurnoEnLavado("turno", new Dictionary<int, string> { [0] = "jabon" });
        }
        var first = Convertir(); var second = Convertir();
        start.SetResult();
        var ids = await Task.WhenAll(first, second);
        Assert.Equal(ids[0], ids[1]);
        var washes = await db.Collection("lavados").GetSnapshotAsync();
        var wash = Assert.Single(washes.Documents);
        Assert.Equal(ids[0], wash.Id);
        Assert.Equal("turno", wash.GetValue<string>("TurnoId"));
        Assert.Equal("EnProceso", wash.GetValue<string>("Estado"));
        var turno = await db.Collection("turnos").Document("turno").GetSnapshotAsync();
        Assert.Equal("EnProceso", turno.GetValue<string>("Estado"));
        Assert.Equal(wash.Id, turno.GetValue<string>("LavadoId"));
        var reservas = await stock.ObtenerReservasInventario(new[] { "jabon", tool });
        Assert.Equal(0.25m, reservas["jabon"]);
        Assert.Equal(1m, reservas[tool]);
        Assert.Equal(1m, (await db.Collection("insumos").Document("jabon").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
        Assert.Equal(1m, (await db.Collection("insumos").Document(tool).GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
        Assert.Empty(await stock.ObtenerMovimientos("jabon"));
    }

    [LocalFirestoreFact]
    public async Task ConversionYInicioOrdinarioCompitenPorUltimaPlazaSinReservasParciales()
    {
        var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = Service(db, cache); var stock = new StockService(db);
        await ConfigurarCapacidad(db, 1);
        cache.Set("system_configuration", new Configuracion { HorariosOperacion = new(), CapacidadMaximaConcurrente = 1 });
        var toolTurno = await SetupItems(db, 1);
        await db.Collection("insumos").Document("jabon-ordinario").SetAsync(new Insumo { Id = "jabon-ordinario", Nombre = "Jabon ordinario", TipoId = "tipo", Tipo = "Jabon", UnidadMedida = "l", StockActual = 1 });
        var toolOrdinario = new Insumo { Nombre = "Herramienta ordinaria", Tipo = "Herramienta", UnidadMedida = "unidad" };
        Assert.True((await new InsumoService(db).Guardar(toolOrdinario, true)).EsExitoso);
        Assert.True((await stock.Ajustar(toolOrdinario.Id, 1, true, "Ingreso", "actor")).EsExitoso);
        var turnoRef = db.Collection("turnos").Document("turno");
        await turnoRef.SetAsync(Appointment(toolTurno));
        await db.Collection("empleados").Document("empleado-turno").SetAsync(new { Estado = "Activo" });
        await db.Collection("empleados").Document("empleado-ordinario").SetAsync(new { Estado = "Activo" });
        var ordinario = new Lavado { Id = "", Estado = "Pendiente", ClienteId = "otro-cliente", VehiculoId = "otro-vehiculo", CantidadEmpleadosRequeridos = 1,
            EmpleadosAsignadosIds = ["empleado-ordinario"], Servicios = [new ServicioEnLavado { ServicioId = "otro-servicio", Receta = [
                new() { InsumoId = "jabon-ordinario", UnidadMedida = "l", DosisTeorica = 0.25m },
                new() { InsumoId = toolOrdinario.Id, UnidadMedida = "unidad", DosisTeorica = 1, EsHerramienta = true } ] }] };
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<Exception?> Convertir() { await start.Task; return await Record.ExceptionAsync(() => service.ConvertirTurnoEnLavado("turno", new Dictionary<int, string> { [0] = "jabon" })); }
        async Task<Exception?> Iniciar() { await start.Task; return await Record.ExceptionAsync(() => service.CrearLavadoConInicio(ordinario)); }
        var conversion = Convertir(); var inicio = Iniciar(); start.SetResult();
        var outcomes = await Task.WhenAll(conversion, inicio);
        Assert.Single(outcomes, x => x == null);
        var failure = Assert.IsType<InvalidOperationException>(Assert.Single(outcomes, x => x != null));
        Assert.Contains("capacidad máxima", failure.Message);
        var wash = Assert.Single((await db.Collection("lavados").GetSnapshotAsync()).Documents);
        Assert.Equal("EnProceso", wash.GetValue<string>("Estado"));
        var savedTurno = await turnoRef.GetSnapshotAsync();
        var converted = outcomes[0] == null;
        if (converted)
        {
            Assert.Equal("turno", wash.GetValue<string>("TurnoId"));
            Assert.Equal("EnProceso", savedTurno.GetValue<string>("Estado"));
            Assert.Equal(wash.Id, savedTurno.GetValue<string>("LavadoId"));
        }
        else
        {
            Assert.Equal(ordinario.Id, wash.Id);
            Assert.False(wash.ContainsField("TurnoId"));
            Assert.Equal("Pendiente", savedTurno.GetValue<string>("Estado"));
            Assert.False(savedTurno.ContainsField("LavadoId"));
        }
        // Reservation IDs encode the wash/service reference and resource; inspecting the
        // complete collections also catches orphan documents for an unknown losing ID.
        var winnerService = converted ? "s" : "otro-servicio";
        var winnerItems = converted ? new[] { "jabon", toolTurno } : new[] { "jabon-ordinario", toolOrdinario.Id };
        var expectedIds = winnerItems.Select(id => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{wash.Id}_{winnerService}_servicio_{id}")))).OrderBy(id => id).ToArray();
        var reservationDocs = await db.Collection("reservasHerramientas").GetSnapshotAsync();
        Assert.Equal(expectedIds, reservationDocs.Documents.Select(d => d.Id).OrderBy(id => id).ToArray());
        Assert.Equal(winnerItems.OrderBy(id => id), reservationDocs.Documents.Select(d => d.GetValue<string>("InsumoId")).OrderBy(id => id));
        Assert.All(reservationDocs.Documents, d => Assert.False(d.GetValue<bool>("Liberada")));
        var counterDocs = await db.Collection("reservasHerramientasTotales").GetSnapshotAsync();
        Assert.Equal(winnerItems.OrderBy(id => id), counterDocs.Documents.Select(d => d.Id).OrderBy(id => id));
        var reservations = await stock.ObtenerReservasInventario(new[] { "jabon", toolTurno, "jabon-ordinario", toolOrdinario.Id });
        Assert.Equal(converted ? 0.25m : 0m, reservations["jabon"]);
        Assert.Equal(converted ? 1m : 0m, reservations[toolTurno]);
        Assert.Equal(converted ? 0m : 0.25m, reservations["jabon-ordinario"]);
        Assert.Equal(converted ? 0m : 1m, reservations[toolOrdinario.Id]);
        foreach (var id in new[] { "jabon", toolTurno, "jabon-ordinario", toolOrdinario.Id })
            Assert.Equal(1m, (await db.Collection("insumos").Document(id).GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
        Assert.Empty(await stock.ObtenerMovimientos("jabon"));
        Assert.Empty(await stock.ObtenerMovimientos("jabon-ordinario"));
    }

    [LocalFirestoreFact]
    public async Task SinStockOSinEmpleadoNoEscribe()
    {
        foreach (var noEmployee in new[] { false, true })
        {
            var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions()); var service = Service(db, cache);
            await ConfigurarCapacidad(db);
            var tool = await SetupItems(db, noEmployee ? 1 : 0.1m);
            await db.Collection("turnos").Document("turno").SetAsync(Appointment(tool));
            if (!noEmployee) await db.Collection("empleados").Document("empleado").SetAsync(new { Estado = "Activo" });
            var before = await Snapshot(db);
            var error = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.ConvertirTurnoEnLavado("turno", new Dictionary<int, string> { [0] = "jabon" }));
            Assert.Equal(noEmployee ? "No hay suficientes empleados disponibles. Se requieren 1 pero solo hay 0 disponibles." : "No hay stock suficiente de 'Jabon' para iniciar la operación.", error.Message);
            Assert.Equal(before, await Snapshot(db));
        }
    }

    [LocalFirestoreFact]
    public async Task CapacidadAusenteInvalidaOLlenaRechazaSinEscribir()
    {
        foreach (var caso in new[] { "ausente", "campo-ausente", "texto", "cero", "negativo", "llena" })
        {
            var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = Service(db, cache);
            var turno = Appointment("sin-uso");
            turno.RecetaOperativa!.Servicios[0].Receta.Clear();
            await db.Collection("turnos").Document("turno").SetAsync(turno);
            await db.Collection("empleados").Document("empleado").SetAsync(new { Estado = "Activo" });
            var config = db.Collection("configuracion").Document("system_config");
            if (caso == "campo-ausente") await config.SetAsync(new { Otro = 1, ToleranciaLlegadaTurnosMinutos = 15 });
            if (caso == "texto") await config.SetAsync(new { CapacidadMaximaConcurrente = "invalida", ToleranciaLlegadaTurnosMinutos = 15 });
            if (caso == "cero") await ConfigurarCapacidad(db, 0);
            if (caso == "negativo") await ConfigurarCapacidad(db, -1);
            if (caso == "llena")
            {
                await ConfigurarCapacidad(db, 1);
                await db.Collection("lavados").Document("ocupado").SetAsync(new { Estado = "EnProceso" });
            }
            var before = await Snapshot(db);
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConvertirTurnoEnLavado("turno", null));
            Assert.Equal(caso == "ausente" ? "Configuración no encontrada." : caso == "llena" ? "Se ha alcanzado la capacidad máxima de lavados concurrentes." : "Configuración inválida.", error.Message);
            Assert.Equal(before, await Snapshot(db));
        }
    }

    [LocalFirestoreFact]
    public async Task InicioAnteriorNoAgregaVinculoNulo()
    {
        var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set("system_configuration", new Configuracion { HorariosOperacion = new(), CapacidadMaximaConcurrente = 10 });
        var service = Service(db, cache);
        var wash = new Lavado { Id = "", Estado = "EnProceso", ClienteId = "c", VehiculoId = "v", Servicios = [new ServicioEnLavado { ServicioId = "s" }] };
        await service.CrearLavadoConInicio(wash);
        var snapshot = await db.Collection("lavados").Document(wash.Id).GetSnapshotAsync();
        Assert.False(snapshot.ContainsField("TurnoId"));
        Assert.Equal("EnProceso", (await service.ObtenerLavado(wash.Id))!.Servicios[0].Estado);
    }

    [LocalFirestoreFact]
    public async Task ConversionPersisteNombresParaConsulta()
    {
        var db = Db(); using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = Service(db, cache);
        await ConfigurarCapacidad(db);
        var turno = Appointment("sin-uso");
        turno.RecetaOperativa!.Servicios[0].Receta.Clear();
        await db.Collection("turnos").Document("turno").SetAsync(turno);
        await db.Collection("clientes").Document("cliente").SetAsync(new { Nombre = "Ana", Apellido = "Perez" });
        await db.Collection("vehiculos").Document("vehiculo").SetAsync(new { Patente = "ABC123", TipoVehiculo = "Auto" });
        await db.Collection("empleados").Document("empleado").SetAsync(new { Estado = "Activo", NombreCompleto = "Luis Lopez" });
        var id = await service.ConvertirTurnoEnLavado("turno", null);
        var lavado = (await service.ObtenerLavado(id))!;
        Assert.Equal("Ana Perez", lavado.ClienteNombre);
        Assert.Equal("ABC123", lavado.VehiculoPatente);
        Assert.Equal(new[] { "Luis Lopez" }, lavado.EmpleadosAsignadosNombres);
        Assert.Equal("Congelado", lavado.Servicios[0].ServicioNombre);
    }

    [LocalFirestoreFact]
    public async Task ConvierteInstantaneaYReplayNoEscribe()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-conversion-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var services = new ServicioService(db);
        var washes = new LavadoService(db, services, new PaqueteServicioService(db, services, cache), new PersonalService(db), new ConfiguracionService(db, cache));
        var now = DateTime.UtcNow;
        var turno = new Turno { Id = "turno", ClienteId = "cliente", Estado = "Pendiente", Origen = "web", ClaveIdempotencia = "clave", InicioUtc = now, FinUtc = now.AddMinutes(10), PrecioAcordado = 72, Descuento = 10,
            DesglosePrecio = new() { Version = 1, Subtotal = 80, Total = 72 },
            Vehiculos = [new() { VehiculoId = "vehiculo", CantidadEmpleadosRequeridos = 1, Servicios = [new() { ServicioId = "s", Precio = 80, TiempoEstimado = 10 }] }],
            RecetaOperativa = new() { Version = 1, Servicios = [new() { ServicioId = "s", Nombre = "Congelado", Tipo = "Lavado" }] } };
        await ConfigurarCapacidad(db);
        var appointment = db.Collection("turnos").Document("turno");
        await appointment.SetAsync(turno);
        await db.Collection("empleados").Document("empleado").SetAsync(new { Estado = "Activo" });
        var id = await washes.ConvertirTurnoEnLavado("turno", null);
        var wash = db.Collection("lavados").Document(id);
        var created = await wash.GetSnapshotAsync();
        var converted = await appointment.GetSnapshotAsync();
        Assert.Equal("turno", created.GetValue<string>("TurnoId"));
        Assert.Equal(id, converted.GetValue<string>("LavadoId"));
        Assert.Equal("EnProceso", converted.GetValue<string>("Estado"));
        Assert.Equal(72d, created.GetValue<double>("Precio"));
        var mapped = (await washes.ObtenerLavado(id))!;
        Assert.Equal("Congelado", mapped.Servicios[0].ServicioNombre);
        Assert.Equal("EnProceso", mapped.Servicios[0].Estado);
        Assert.Equal(new[] { "empleado" }, mapped.EmpleadosAsignadosIds);
        Assert.Equal(id, await washes.ConvertirTurnoEnLavado("turno", null));
        Assert.Equal(created.UpdateTime, (await wash.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(converted.UpdateTime, (await appointment.GetSnapshotAsync()).UpdateTime);
    }
}

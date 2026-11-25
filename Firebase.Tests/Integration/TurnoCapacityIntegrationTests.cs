using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;

namespace Firebase.Tests.Integration;

public class TurnoCapacityIntegrationTests
{
    [Fact]
    public void OpeningHoursUseSharedArgentinaZoneAndPreserveLocalValidation()
    {
        var source = File.ReadAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(SourcePath())!, "../../Services/TurnoService.cs")));
        Assert.Contains("TimeZoneHelper.ArgentinaZone", source);
        Assert.DoesNotContain("FindSystemTimeZoneById", source);
        var config = new Configuracion { HorariosOperacion = new() { ["Martes"] = "09:00-18:00" } };
        var openings = TurnoService.ObtenerAperturas(config, new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc), out var anchor);
        Assert.Equal(new DateTime(2030, 1, 1, 3, 0, 0, DateTimeKind.Utc), anchor);
        Assert.Equal(new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc), Assert.Single(openings).Start);
        config.HorariosOperacion["Martes"] = "bad";
        Assert.Equal("Horario inválido.", Assert.Throws<InvalidOperationException>(() => TurnoService.ObtenerAperturas(config, new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc), out _)).Message);
    }

    private static string SourcePath([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;
    private static readonly DateTime Day = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static Turno Booking(string id, int start = 600, int minutes = 30, int staff = 1) => new()
    {
        Id = id, ClaveIdempotencia = id, ClienteId = "client", Estado = "Pendiente", Origen = "internal",
        InicioUtc = Day.AddMinutes(start), FinUtc = Day.AddMinutes(start + minutes), PrecioAcordado = 12m,
        DesglosePrecio = new() { Version = 1, Subtotal = 12m, Total = 12m },
        RecetaOperativa = new() { Version = 1, Servicios = [new() { ServicioId = "service", Nombre = "Lavado", Tipo = "Lavado" }] },
        InsumosForecast = [],
        Vehiculos = [new() { VehiculoId = "vehicle", CantidadEmpleadosRequeridos = staff,
            Servicios = [new() { ServicioId = "service", TiempoEstimado = minutes, Precio = 12m }] }]
    };
    private static async Task<(FirestoreDb db, TurnoService service)> Setup(int staff = 1)
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-turno-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        for (int i = 0; i < staff; i++) await db.Collection("empleados").Document(i.ToString()).SetAsync(new { Estado = "Activo" });
        return (db, new TurnoService(db));
    }
    private static Task<bool> Create(TurnoService s, Turno t, int slots = 1) =>
        s.Crear(t, slots, [new TurnoOpening(Day, Day.AddDays(1))], Day, Day);

    private static Task ConfigureCancellation(FirestoreDb db) =>
        db.Collection("configuracion").Document("system_config").UpdateAsync(new Dictionary<string, object>
        {
            ["CancelacionAnticipadaHorasMinimas"] = 24,
            ["CancelacionAnticipadaDescuento"] = 0,
            ["CancelacionAnticipadaValidezDias"] = 30
        });

    [Fact]
    public async Task RetryAbortedSucceedsAfterTransientAbort()
    {
        var attempts = 0;
        var result = await TurnoService.RetryAborted<int>(() =>
        {
            attempts++;
            if (attempts < 3) throw new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.Aborted, "conflict"));
            return Task.FromResult(42);
        });
        Assert.Equal(42, result);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task RetryAbortedStopsAfterFiveAttempts()
    {
        var attempts = 0;
        var error = await Assert.ThrowsAsync<Grpc.Core.RpcException>(() => TurnoService.RetryAborted<int>(() =>
        {
            attempts++;
            throw new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.Aborted, "conflict"));
        }));
        Assert.Equal(Grpc.Core.StatusCode.Aborted, error.StatusCode);
        Assert.Equal(5, attempts);
    }

    [Fact]
    public async Task RetryAbortedDoesNotRetryOtherRpcErrors()
    {
        var attempts = 0;
        var error = await Assert.ThrowsAsync<Grpc.Core.RpcException>(() => TurnoService.RetryAborted<int>(() =>
        {
            attempts++;
            throw new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.Unavailable, "transport"));
        }));
        Assert.Equal(Grpc.Core.StatusCode.Unavailable, error.StatusCode);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task RetryAbortedCancellationBeforeAttemptDoesNotInvokeOperation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var attempts = 0;
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TurnoService.RetryAborted(() =>
        {
            attempts++;
            return Task.FromResult(42);
        }, cts.Token));
        Assert.Equal(cts.Token, error.CancellationToken);
        Assert.Equal(0, attempts);
    }

    [Fact]
    public async Task RetryAbortedCancellationDuringBackoffStopsFurtherAttempts()
    {
        using var cts = new CancellationTokenSource();
        var attempts = 0;
        var retry = TurnoService.RetryAborted<int>(() =>
        {
            attempts++;
            throw new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.Aborted, "conflict"));
        }, cts.Token);
        cts.Cancel();
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => retry);
        Assert.Equal(cts.Token, error.CancellationToken);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task RetryAbortedCancellationOverloadPreservesRpcBehavior()
    {
        var attempts = 0;
        var result = await TurnoService.RetryAborted<int>(() =>
        {
            if (++attempts < 3) throw new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.Aborted, "conflict"));
            return Task.FromResult(42);
        }, CancellationToken.None);
        Assert.Equal(42, result);
        Assert.Equal(3, attempts);
        attempts = 0;
        var aborted = await Assert.ThrowsAsync<Grpc.Core.RpcException>(() => TurnoService.RetryAborted<int>(() =>
        {
            attempts++;
            throw new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.Aborted, "conflict"));
        }, CancellationToken.None));
        Assert.Equal(Grpc.Core.StatusCode.Aborted, aborted.StatusCode);
        Assert.Equal(5, attempts);
        attempts = 0;
        var unavailable = await Assert.ThrowsAsync<Grpc.Core.RpcException>(() => TurnoService.RetryAborted<int>(() =>
        {
            attempts++;
            throw new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.Unavailable, "transport"));
        }, CancellationToken.None));
        Assert.Equal(Grpc.Core.StatusCode.Unavailable, unavailable.StatusCode);
        Assert.Equal(1, attempts);
    }

    [LocalFirestoreFact]
    public async Task SnapshotReadsCatalogAndOwnershipWithoutWriting()
    {
        var (db, s) = await Setup();
        await db.Collection("clientes").Document("c").SetAsync(new { Id = "c" });
        await db.Collection("vehiculos").Document("v").SetAsync(new Vehiculo { Id = "v", Patente = "ABC123", TipoVehiculo = "Auto", Marca = "a", Modelo = "b", Color = "c", ClienteId = "c" });
        await db.Collection("tiposVehiculos").Document("t").SetAsync(new TipoVehiculo { Id = "t", Nombre = "Auto", FormatoPatente = "lllnnn", CantidadEmpleadosRequeridos = 2 });
        await db.Collection("servicios").Document("s").SetAsync(new Servicio { Id = "s", Nombre = "Lavado", Tipo = "Lavado", TipoVehiculo = "Auto", Descripcion = "x", Estado = "Activo", Precio = 100, TiempoEstimado = 30 });
        await db.Collection("paquetes_servicios").Document("p").SetAsync(new PaqueteServicio { Id = "p", Nombre = "Pack", Estado = "Activo", TipoVehiculo = "Auto", ServiciosIds = ["s"], PorcentajeDescuento = 10 });
        var input = new TurnoService.SnapshotRequest("id", "c", "v", "Web", "key", Day.AddHours(10), true, [new("s", "p")]);
        var result = await s.ConstruirSnapshot(input);
        Assert.Equal(90m, result.PrecioAcordado);
        Assert.Equal(Day.AddHours(10).AddMinutes(30), result.FinUtc);
        Assert.Equal(2, result.Vehiculos.Single().CantidadEmpleadosRequeridos);
        Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        Assert.False((await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).Exists);
        Assert.False((await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync()).Exists);
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.ConstruirSnapshot(input with { ClienteId = "missing" }));
        await db.Collection("vehiculos").Document("v").UpdateAsync("ClienteId", "other");
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.ConstruirSnapshot(input));
        await db.Collection("vehiculos").Document("v").UpdateAsync("ClienteId", "c");
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.ConstruirSnapshot(input with { Servicios = [new("missing", null)] }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.ConstruirSnapshot(input with { Servicios = [new("s", "missing")] }));
        await db.Collection("tiposVehiculos").Document("duplicate").SetAsync(new TipoVehiculo { Id = "duplicate", Nombre = "Auto", FormatoPatente = "lllnnn" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.ConstruirSnapshot(input));
        Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        Assert.False((await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).Exists);
        Assert.False((await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync()).Exists);
    }

    [LocalFirestoreFact]
    public async Task SnapshotRequiresReferencedClientAndAcceptsSharedOwner()
    {
        var (db, s) = await Setup();
        await db.Collection("vehiculos").Document("v").SetAsync(new Vehiculo { Id = "v", Patente = "ABC123", TipoVehiculo = "Auto", Marca = "a", Modelo = "b", Color = "c", ClienteId = "primary", ClientesIds = ["shared", "missing"] });
        await db.Collection("clientes").Document("shared").SetAsync(new { Id = "shared" });
        await db.Collection("tiposVehiculos").Document("t").SetAsync(new TipoVehiculo { Id = "t", Nombre = "Auto", FormatoPatente = "lllnnn" });
        await db.Collection("servicios").Document("s").SetAsync(new Servicio { Id = "s", Nombre = "Lavado", Tipo = "Lavado", TipoVehiculo = "Auto", Descripcion = "x", Estado = "Activo", Precio = 100, TiempoEstimado = 30 });
        var request = new TurnoService.SnapshotRequest("id", "shared", "v", "Web", "key", Day.AddHours(10), false, [new("s", null)]);
        Assert.Equal("shared", (await s.ConstruirSnapshot(request)).ClienteId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.ConstruirSnapshot(request with { ClienteId = "missing" }));
        Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        Assert.False((await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).Exists);
        Assert.False((await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync()).Exists);
    }

    [LocalFirestoreFact]
    public async Task SnapshotRejectsInvalidIdsAndInstantsWithoutWrites()
    {
        var (db, s) = await Setup();
        await db.Collection("clientes").Document("c").SetAsync(new { Id = "c" });
        await db.Collection("vehiculos").Document("v").SetAsync(new Vehiculo { Id = "v", Patente = "ABC123", TipoVehiculo = "Auto", Marca = "a", Modelo = "b", Color = "c", ClienteId = "c" });
        await db.Collection("tiposVehiculos").Document("t").SetAsync(new TipoVehiculo { Id = "t", Nombre = "Auto", FormatoPatente = "lllnnn" });
        await db.Collection("servicios").Document("s").SetAsync(new Servicio { Id = "s", Nombre = "Lavado", Tipo = "Lavado", TipoVehiculo = "Auto", Descripcion = "x", Estado = "Activo", Precio = 100, TiempoEstimado = 30 });
        var request = new TurnoService.SnapshotRequest("id", "c", "v", "Web", "key", Day.AddHours(10), false, [new("s", null)]);
        foreach (var invalid in new[] { "", "  ", "a/b", ".", "..", "__reserved__", new string('x', 1501) })
        {
            foreach (var bad in new[] { request with { Id = invalid }, request with { ClienteId = invalid }, request with { VehiculoId = invalid }, request with { Servicios = [new(invalid, null)] }, request with { Servicios = [new("s", invalid)] } })
                await Assert.ThrowsAsync<ArgumentException>(() => s.ConstruirSnapshot(bad));
        }
        foreach (var instant in new[] { default, DateTime.SpecifyKind(Day, DateTimeKind.Unspecified), DateTime.SpecifyKind(Day, DateTimeKind.Local) })
            await Assert.ThrowsAsync<ArgumentException>(() => s.ConstruirSnapshot(request with { InicioUtc = instant }));
        Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        Assert.False((await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).Exists);
        Assert.False((await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync()).Exists);
    }

    [LocalFirestoreFact]
    public async Task AuthoritativeBookingUsesLocalHoursAndStableIdempotency()
    {
        var (db, s) = await Setup();
        await db.Collection("clientes").Document("c").SetAsync(new { Id = "c" });
        await db.Collection("vehiculos").Document("v").SetAsync(new Vehiculo { Id = "v", Patente = "ABC123", TipoVehiculo = "Auto", Marca = "a", Modelo = "b", Color = "c", ClienteId = "c" });
        await db.Collection("tiposVehiculos").Document("t").SetAsync(new TipoVehiculo { Id = "t", Nombre = "Auto", FormatoPatente = "lllnnn" });
        await db.Collection("servicios").Document("s").SetAsync(new Servicio { Id = "s", Nombre = "Lavado", Tipo = "Lavado", TipoVehiculo = "Auto", Descripcion = "x", Estado = "Activo", Precio = 100, TiempoEstimado = 30 });
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion { CapacidadMaximaConcurrente = 1, HorariosOperacion = new() { ["Martes"] = "09:00-18:00" } });
        var request = new TurnoService.SnapshotRequest("id", "c", "v", "Web", "key", Day.AddHours(12), false, [new("s", null)]);
        Assert.True(await s.Reservar(request));
        await db.Collection("servicios").Document("s").UpdateAsync("Precio", 999d);
        Assert.True(await s.Reservar(request));
        var stored = (await db.Collection("turnos").Document("id").GetSnapshotAsync()).ConvertTo<Turno>();
        Assert.Equal(100m, stored.PrecioAcordado);
        Assert.Equal(1, stored.DesglosePrecio!.Version);
        Assert.Equal(100m, stored.DesglosePrecio.Subtotal);
        Assert.Equal(100m, stored.DesglosePrecio.Total);
        Assert.False(await s.Reservar(request with { Id = "other", ClaveIdempotencia = "other" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Reservar(request with { Id = "other" }));
        Assert.Single((await db.Collection("turnos").GetSnapshotAsync()).Documents);
    }

    private static async Task<(FirestoreDb db, TurnoService service, TurnoService.SnapshotRequest request)> AuthoritativeSetup(
        int activeStaff = 1, int requiredStaff = 1, int duration = 30, int slots = 1)
    {
        var (db, service) = await Setup(activeStaff);
        await db.Collection("clientes").Document("c").SetAsync(new { Id = "c" });
        await db.Collection("vehiculos").Document("v").SetAsync(new Vehiculo { Id = "v", Patente = "ABC123", TipoVehiculo = "Auto", Marca = "a", Modelo = "b", Color = "c", ClienteId = "c" });
        await db.Collection("tiposVehiculos").Document("t").SetAsync(new TipoVehiculo { Id = "t", Nombre = "Auto", FormatoPatente = "lllnnn", CantidadEmpleadosRequeridos = requiredStaff });
        await db.Collection("servicios").Document("s").SetAsync(new Servicio { Id = "s", Nombre = "Lavado", Tipo = "Lavado", TipoVehiculo = "Auto", Descripcion = "x", Estado = "Activo", Precio = 100, TiempoEstimado = duration });
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion { CapacidadMaximaConcurrente = slots, HorariosOperacion = new() { ["Martes"] = "09:00-18:00", ["Lunes"] = "20:00-23:30" } });
        return (db, service, new("id", "c", "v", "Web", "key", Day.AddHours(12), false, [new("s", null)]));
    }

    [LocalFirestoreFact]
    public async Task PrecisePriceSurvivesBookingRoundtripAndReplay()
    {
        var (db, service, request) = await AuthoritativeSetup();
        var catalog = db.Collection("servicios").Document("s");
        await catalog.UpdateAsync("Precio", 1d);
        request = request with { Descuento = 0.0000000000000001m };
        Assert.True(await service.Reservar(request));
        var reference = db.Collection("turnos").Document(request.Id);
        var before = await reference.GetSnapshotAsync();
        var turno = before.ConvertTo<Turno>();
        Assert.Equal(1m, turno.PrecioAcordado);
        Assert.Equal((0.999999999999999999m, 1m), TurnoLavadoSnapshotMapper.ObtenerPreciosLavado(turno));
        var mapped = TurnoLavadoSnapshotMapper.Mapear(turno);
        Assert.Equal(0.999999999999999999m, mapped.Precio);
        await catalog.UpdateAsync("Precio", 99d);
        Assert.True(await service.Reservar(request));
        Assert.Equal(before.UpdateTime, (await reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal((0.999999999999999999m, 1m), TurnoLavadoSnapshotMapper.ObtenerPreciosLavado((await reference.GetSnapshotAsync()).ConvertTo<Turno>()));
    }

    [LocalFirestoreFact]
    public async Task PartialPackageBoundariesAndZeroPersistThroughReplay()
    {
        foreach (var (packageDiscount, overallDiscount, expectedSubtotal, expectedTotal) in new[]
        {
            (0m, 0m, 120m, 120m), (100m, 0m, 20m, 20m),
            (0m, 100m, 120m, 0m), (100m, 100m, 20m, 0m)
        })
        {
            var (db, service, request) = await AuthoritativeSetup();
            var packaged = db.Collection("servicios").Document("s");
            var individual = db.Collection("servicios").Document("s2");
            await individual.SetAsync(new Servicio { Id = "s2", Nombre = "Otro", Tipo = "Lavado", TipoVehiculo = "Auto", Descripcion = "x", Estado = "Activo", Precio = 20, TiempoEstimado = 15 });
            var package = db.Collection("paquetes_servicios").Document("p");
            await package.SetAsync(new PaqueteServicio { Id = "p", Nombre = "Pack", Estado = "Activo", TipoVehiculo = "Auto", ServiciosIds = ["s", "s2"], PorcentajeDescuento = packageDiscount });
            request = request with { Servicios = [new("s", "p"), new("s2", null)], Descuento = overallDiscount };
            Assert.True(await service.Reservar(request));
            var appointment = db.Collection("turnos").Document(request.Id);
            var before = await appointment.GetSnapshotAsync();
            var saved = before.ConvertTo<Turno>();
            Assert.Equal(1, saved.DesglosePrecio!.Version);
            Assert.Equal(expectedSubtotal, saved.DesglosePrecio.Subtotal);
            Assert.Equal(expectedTotal, saved.DesglosePrecio.Total);
            var mapped = TurnoLavadoSnapshotMapper.Mapear(saved);
            Assert.Equal((expectedTotal, expectedSubtotal), (mapped.Precio, mapped.PrecioOriginal));
            Assert.Equal(new decimal[] { 100, 20 }, mapped.Servicios.Select(x => x.Precio));
            var markers = await db.Collection("turno_solicitudes").GetSnapshotAsync();
            var marker = Assert.Single(markers.Documents);
            var capacity = await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
            var membership = await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync();
            await packaged.UpdateAsync("Precio", 999d);
            await individual.UpdateAsync("Precio", 888d);
            await package.UpdateAsync("PorcentajeDescuento", (double)(100m - packageDiscount));
            Assert.True(await service.Reservar(request));
            var after = await appointment.GetSnapshotAsync();
            Assert.Equal(before.UpdateTime, after.UpdateTime);
            Assert.Equal(marker.UpdateTime, (await marker.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(capacity.UpdateTime, (await capacity.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(membership.UpdateTime, (await membership.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Single((await db.Collection("turnos").GetSnapshotAsync()).Documents);
            Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
            mapped = TurnoLavadoSnapshotMapper.Mapear(after.ConvertTo<Turno>());
            Assert.Equal((expectedTotal, expectedSubtotal), (mapped.Precio, mapped.PrecioOriginal));
            Assert.Equal(new decimal[] { 100, 20 }, mapped.Servicios.Select(x => x.Precio));
        }
    }

    [LocalFirestoreFact]
    public async Task PersistedPriceRequiresBothFieldsAndRejectsTampering()
    {
        var (db, service, request) = await AuthoritativeSetup();
        Assert.True(await service.Reservar(request));
        var reference = db.Collection("turnos").Document(request.Id);
        foreach (var breakdown in new[] {
            new Dictionary<string, object?> { ["Version"] = 1, ["Subtotal"] = "100" },
            new Dictionary<string, object?> { ["Version"] = 1, ["Subtotal"] = "100", ["Total"] = null },
            new Dictionary<string, object?> { ["Version"] = 1, ["Subtotal"] = "100", ["Total"] = "99" },
            new Dictionary<string, object?> { ["Version"] = 1, ["Subtotal"] = "malformado", ["Total"] = "100" } })
        {
            await reference.UpdateAsync("DesglosePrecio", breakdown);
            var persisted = await reference.GetSnapshotAsync();
            Assert.ThrowsAny<Exception>(() => TurnoLavadoSnapshotMapper.Mapear(persisted.ConvertTo<Turno>()));
        }
    }

    [LocalFirestoreFact]
    public async Task OperationalRecipeRoundtripIsFrozenAndLegacyIsUnknown()
    {
        var (db, service, request) = await AuthoritativeSetup();
        var catalog = db.Collection("servicios").Document("s");
        var recipe = new RecetaInsumo { Modalidad = "Tipo", TipoId = "tipo", NombreTipo = "Jabon", Categoria = "Volumen", InsumoId = "", NombreInsumo = "", UnidadMedida = "ml", DosisTeorica = 1.25m };
        await catalog.UpdateAsync(new Dictionary<string, object> { ["Receta"] = new[] { recipe }, ["Etapas"] = new[] { new Etapa { Id = "e1", Nombre = "Primera", Receta = [recipe] }, new Etapa { Id = "e2", Nombre = "Segunda", Receta = [] } } });
        Assert.True(await service.Reservar(request));
        var reference = db.Collection("turnos").Document(request.Id);
        var before = await reference.GetSnapshotAsync();
        var snapshot = before.ConvertTo<Turno>().RecetaOperativa!;
        Assert.Equal(1, snapshot.Version);
        var captured = Assert.Single(snapshot.Servicios);
        Assert.Equal("s", captured.ServicioId);
        Assert.Equal("tipo", Assert.Single(captured.Receta).TipoId);
        Assert.Equal(new[] { "e1", "e2" }, captured.Etapas.Select(e => e.Id));
        Assert.Equal("Primera", captured.Etapas[0].Nombre);
        var item = Assert.Single(captured.Etapas[0].Receta);
        Assert.Equal("Tipo", item.Modalidad); Assert.Equal("tipo", item.TipoId); Assert.Equal("Jabon", item.NombreTipo);
        Assert.Equal("Volumen", item.Categoria); Assert.Equal("", item.InsumoId); Assert.Equal("", item.NombreInsumo);
        Assert.Equal("ml", item.UnidadMedida); Assert.Equal(1.25m, item.DosisTeorica);
        Assert.Empty(captured.Etapas[1].Receta);
        await catalog.UpdateAsync("Etapas", Array.Empty<Etapa>());
        Assert.True(await service.Reservar(request));
        Assert.Equal(before.UpdateTime, (await reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(2, (await reference.GetSnapshotAsync()).ConvertTo<Turno>().RecetaOperativa!.Servicios.Single().Etapas.Count);
        var legacy = db.Collection("turnos").Document("legacy");
        await legacy.SetAsync(new { Estado = "Cancelado" });
        Assert.Null((await legacy.GetSnapshotAsync()).ConvertTo<Turno>().RecetaOperativa);
    }

    [LocalFirestoreFact]
    public async Task OperationalRecipeMultipleServicesRoundtripMatchesWashInputsAfterCatalogChanges()
    {
        var (db, service, request) = await AuthoritativeSetup();
        var first = db.Collection("servicios").Document("s");
        var second = db.Collection("servicios").Document("s2");
        var serviceRecipe = new RecetaInsumo { Modalidad = "Producto", TipoId = "tipo-a", NombreTipo = "Tipo A", Categoria = "Volumen", InsumoId = "producto-a", NombreInsumo = "Producto A", UnidadMedida = "ml", DosisTeorica = 1.25m };
        var stageRecipe = new RecetaInsumo { Modalidad = "Tipo", TipoId = "tipo-b", NombreTipo = "Tipo B", Categoria = "Peso", InsumoId = "producto-b", NombreInsumo = "Producto B", UnidadMedida = "g", DosisTeorica = 2.5m };
        await first.UpdateAsync("Receta", new[] { serviceRecipe });
        await second.SetAsync(new Servicio { Id = "s2", Nombre = "Encerado", Tipo = "Detalle", TipoVehiculo = "Auto", Descripcion = "x", Estado = "Activo", Precio = 45m, TiempoEstimado = 15,
            Receta = [serviceRecipe], Etapas = [new Etapa { Id = "etapa-b", Nombre = "Segunda", Receta = [stageRecipe, serviceRecipe] }, new Etapa { Id = "etapa-a", Nombre = "Primera", Receta = [serviceRecipe] }] });
        request = request with { Servicios = [new("s2", null), new("s", null)] };
        Assert.True(await service.Reservar(request));
        var reference = db.Collection("turnos").Document(request.Id);
        var before = await reference.GetSnapshotAsync();

        void AssertRecipe(RecetaInsumo expected, RecetaInsumo actual)
        {
            Assert.Equal(expected.Modalidad, actual.Modalidad); Assert.Equal(expected.TipoId, actual.TipoId);
            Assert.Equal(expected.NombreTipo, actual.NombreTipo); Assert.Equal(expected.Categoria, actual.Categoria);
            Assert.Equal(expected.InsumoId, actual.InsumoId); Assert.Equal(expected.NombreInsumo, actual.NombreInsumo);
            Assert.Equal(expected.UnidadMedida, actual.UnidadMedida); Assert.Equal(expected.DosisTeorica, actual.DosisTeorica);
        }
        void AssertCaptured(Turno turno)
        {
            var snapshot = turno.RecetaOperativa!;
            Assert.Equal(1, snapshot.Version);
            Assert.Equal(new[] { "s2", "s" }, snapshot.Servicios.Select(x => x.ServicioId));
            Assert.Equal(new[] { "s2", "s" }, turno.Vehiculos.Single().Servicios.Select(x => x.ServicioId));
            Assert.Equal(145m, turno.PrecioAcordado);
            Assert.Equal(new decimal[] { 45m, 100m }, turno.Vehiculos[0].Servicios.Select(x => x.Precio));
            Assert.Equal(new[] { 15, 30 }, turno.Vehiculos[0].Servicios.Select(x => x.TiempoEstimado));
            var staged = snapshot.Servicios[0];
            Assert.Equal("Encerado", staged.Nombre); Assert.Equal("Detalle", staged.Tipo);
            AssertRecipe(serviceRecipe, Assert.Single(staged.Receta));
            Assert.Equal(new[] { "etapa-b", "etapa-a" }, staged.Etapas.Select(x => x.Id));
            Assert.Equal(new[] { "Segunda", "Primera" }, staged.Etapas.Select(x => x.Nombre));
            Assert.Equal(2, staged.Etapas[0].Receta.Count);
            AssertRecipe(stageRecipe, staged.Etapas[0].Receta[0]);
            AssertRecipe(serviceRecipe, staged.Etapas[0].Receta[1]);
            AssertRecipe(serviceRecipe, Assert.Single(staged.Etapas[1].Receta));
            var plain = snapshot.Servicios[1];
            Assert.Equal("Lavado", plain.Nombre); Assert.Equal("Lavado", plain.Tipo);
            Assert.Empty(plain.Etapas); AssertRecipe(serviceRecipe, Assert.Single(plain.Receta));
        }
        AssertCaptured(before.ConvertTo<Turno>());
        await first.UpdateAsync(new Dictionary<string, object> { ["Nombre"] = "Modificado", ["Receta"] = Array.Empty<RecetaInsumo>(), ["Precio"] = 999d });
        await second.UpdateAsync(new Dictionary<string, object> { ["Nombre"] = "Cambiado", ["Tipo"] = "Otro", ["Receta"] = Array.Empty<RecetaInsumo>(), ["Etapas"] = Array.Empty<Etapa>(), ["Precio"] = 999d });
        Assert.True(await service.Reservar(request));
        var after = await reference.GetSnapshotAsync();
        Assert.Equal(before.UpdateTime, after.UpdateTime);
        AssertCaptured(after.ConvertTo<Turno>());
    }

    [LocalFirestoreFact]
    public async Task OperationalRecipeEmptyAndRejectedBookingPreservesWrites()
    {
        var (db, service, request) = await AuthoritativeSetup();
        Assert.True(await service.Reservar(request));
        var saved = (await db.Collection("turnos").Document(request.Id).GetSnapshotAsync()).ConvertTo<Turno>();
        var recipe = Assert.Single(saved.RecetaOperativa!.Servicios);
        Assert.Empty(recipe.Receta); Assert.Empty(recipe.Etapas);
        Assert.Empty(saved.InsumosForecast!);
        var capacity = await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
        var membership = await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync();
        Assert.False(await service.Reservar(request with { Id = "other", ClaveIdempotencia = "other" }));
        Assert.Single((await db.Collection("turnos").GetSnapshotAsync()).Documents);
        Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        Assert.Equal(capacity.UpdateTime, (await capacity.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(membership.UpdateTime, (await membership.Reference.GetSnapshotAsync()).UpdateTime);
    }

    [LocalFirestoreFact]
    public async Task AuthoritativeForecastIsPersistedOnceWithoutStockDependency()
    {
        var (db, s, request) = await AuthoritativeSetup();
        var service = db.Collection("servicios").Document("s");
        var item = new RecetaInsumo { Modalidad = "Tipo", TipoId = "t", NombreTipo = "Jabon", Categoria = "Volumen", InsumoId = "i", NombreInsumo = "Producto", UnidadMedida = "ml", DosisTeorica = 1.25m };
        await service.UpdateAsync("Etapas", new[] { new Etapa { Id = "e", Receta = [item] } });
        var stock = db.Collection("insumos").Document("i");
        await stock.SetAsync(new { StockActual = 0 });
        var stockBefore = await stock.GetSnapshotAsync();
        Assert.True(await s.Reservar(request));
        var reference = db.Collection("turnos").Document(request.Id);
        var before = await reference.GetSnapshotAsync();
        var demand = Assert.Single(before.GetValue<List<Dictionary<string, object>>>("InsumosForecast"));
        Assert.Equal("s", demand["ServicioId"]); Assert.Equal("e", demand["EtapaId"]);
        Assert.Equal("Tipo", demand["Modalidad"]); Assert.Equal("t", demand["TipoId"]);
        Assert.Equal("Jabon", demand["NombreTipo"]); Assert.Equal("Volumen", demand["Categoria"]);
        Assert.Equal("i", demand["InsumoId"]); Assert.Equal("Producto", demand["NombreInsumo"]);
        Assert.Equal("ml", demand["UnidadMedida"]); Assert.Equal(1.25d, demand["DosisTeorica"]);
        await service.UpdateAsync("Etapas", Array.Empty<Etapa>());
        Assert.True(await s.Reservar(request));
        Assert.Equal(before.UpdateTime, (await reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(stockBefore.UpdateTime, (await stock.GetSnapshotAsync()).UpdateTime);
        Assert.False(await s.Reservar(request with { Id = "other", ClaveIdempotencia = "other" }));
        Assert.Single((await db.Collection("turnos").GetSnapshotAsync()).Documents);
        Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        Assert.Equal(1L, (await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).GetValue<long>("Version"));
        Assert.Equal(1L, (await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync()).GetValue<long>("Version"));
        Assert.Equal(stockBefore.UpdateTime, (await stock.GetSnapshotAsync()).UpdateTime);
    }

    [LocalFirestoreFact]
    public async Task AuthoritativeEmptyForecastDiffersFromLegacyAbsent()
    {
        var (db, s, request) = await AuthoritativeSetup();
        Assert.True(await s.Reservar(request));
        var current = await db.Collection("turnos").Document(request.Id).GetSnapshotAsync();
        Assert.Empty(current.GetValue<List<Dictionary<string, object>>>("InsumosForecast"));
        var legacy = db.Collection("turnos").Document("legacy");
        await legacy.SetAsync(new { Estado = "Cancelado" });
        Assert.False((await legacy.GetSnapshotAsync()).ContainsField("InsumosForecast"));
        Assert.Null((await legacy.GetSnapshotAsync()).ConvertTo<Turno>().InsumosForecast);
        Assert.Empty(current.ConvertTo<Turno>().InsumosForecast!);
    }

    [LocalFirestoreFact]
    public async Task ForecastFirestoreRoundtripDistinguishesMissingNullEmptyAndPreservesDemand()
    {
        var (db, _, request) = await AuthoritativeSetup();
        var service = new TurnoService(db);
        var catalog = db.Collection("servicios").Document("s");
        await catalog.UpdateAsync("Receta", new[] { new RecetaInsumo { Modalidad = "Tipo", TipoId = "tipo", NombreTipo = "Jabon", Categoria = "Volumen", InsumoId = "", NombreInsumo = "", UnidadMedida = "ml", DosisTeorica = 0.123456789012345m } });
        Assert.True(await service.Reservar(request));
        var result = (await db.Collection("turnos").Document(request.Id).GetSnapshotAsync()).ConvertTo<Turno>();
        var demand = Assert.Single(result.InsumosForecast!);
        Assert.Equal("s", demand.ServicioId); Assert.Null(demand.EtapaId);
        Assert.Equal("Tipo", demand.Modalidad); Assert.Equal("tipo", demand.TipoId);
        Assert.Equal("Jabon", demand.NombreTipo); Assert.Equal("Volumen", demand.Categoria);
        Assert.Equal("", demand.InsumoId); Assert.Equal("", demand.NombreInsumo); Assert.Equal("ml", demand.UnidadMedida);
        Assert.Equal(0.123456789012345m, demand.DosisTeorica);
        var reference = db.Collection("turnos").Document("legacy");
        await reference.SetAsync(new Dictionary<string, object> { ["Estado"] = "Cancelado" });
        Assert.Null((await reference.GetSnapshotAsync()).ConvertTo<Turno>().InsumosForecast);
        await reference.SetAsync(new Dictionary<string, object?> { ["InsumosForecast"] = null });
        Assert.Null((await reference.GetSnapshotAsync()).ConvertTo<Turno>().InsumosForecast);
        await reference.SetAsync(new Dictionary<string, object> { ["InsumosForecast"] = Array.Empty<object>() });
        Assert.Empty((await reference.GetSnapshotAsync()).ConvertTo<Turno>().InsumosForecast!);
    }

    [LocalFirestoreFact]
    public async Task AuthoritativeInvalidDoseRollsBackAndNullRecipesAreEmpty()
    {
        var (db, service, request) = await AuthoritativeSetup();
        var catalog = db.Collection("servicios").Document("s");
        await catalog.UpdateAsync("Receta", new[] { new RecetaInsumo { InsumoId = "i", DosisTeorica = 0 } });
        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.Reservar(request));
        Assert.Equal("La dosis debe ser mayor a cero.", error.Message);
        Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        Assert.False((await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).Exists);
        Assert.False((await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync()).Exists);
        await catalog.UpdateAsync(new Dictionary<string, object> { ["Receta"] = null!, ["Etapas"] = new[] { new Dictionary<string, object> { ["Id"] = "e", ["Receta"] = null! } } });
        Assert.True(await service.Reservar(request));
        Assert.Empty((await db.Collection("turnos").Document(request.Id).GetSnapshotAsync()).ConvertTo<Turno>().InsumosForecast!);
    }

    [LocalFirestoreFact]
    public async Task ForecastDoesNotAuthorizeMalformedRecipe()
    {
        var (db, service, request) = await AuthoritativeSetup();
        await db.Collection("servicios").Document("s").UpdateAsync("Receta", new[]
        {
            new RecetaInsumo { Modalidad = "Producto", InsumoId = "", UnidadMedida = "unsupported", DosisTeorica = 1m }
        });
        Assert.True(await service.Reservar(request));
        var demand = Assert.Single((await db.Collection("turnos").Document(request.Id).GetSnapshotAsync()).ConvertTo<Turno>().InsumosForecast!);
        Assert.Equal("Producto", demand.Modalidad); Assert.Equal("", demand.InsumoId);
        Assert.Equal("unsupported", demand.UnidadMedida); Assert.Equal(1m, demand.DosisTeorica);
    }

    [LocalFirestoreFact]
    public async Task AuthoritativeCurrentlyAcceptsInactiveClientAndVehicle()
    {
        var (db, s, request) = await AuthoritativeSetup();
        await db.Collection("clientes").Document("c").UpdateAsync("Estado", "Inactivo");
        await db.Collection("vehiculos").Document("v").UpdateAsync("Estado", "Inactivo");
        Assert.True(await s.Reservar(request));
        Assert.True((await db.Collection("turnos").Document(request.Id).GetSnapshotAsync()).Exists);
    }

    [LocalFirestoreFact]
    public async Task AuthoritativeRejectsOrphanMarkerWithoutWrites()
    {
        var (db, s, request) = await AuthoritativeSetup();
        var marker = db.Collection("turno_solicitudes").Document(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(request.ClaveIdempotencia))));
        await marker.SetAsync(new Dictionary<string, object> { ["Esquema"] = "ids-v1", ["Solicitud"] = System.Text.Json.JsonSerializer.Serialize(request) });
        var capacity = db.Collection("coordinacion").Document("turnos");
        var membership = db.Collection("coordinacion").Document("empleados");
        await capacity.SetAsync(new { Version = 7 });
        await membership.SetAsync(new { Version = 9 });
        var markerBefore = await marker.GetSnapshotAsync();
        var capacityBefore = await capacity.GetSnapshotAsync();
        var membershipBefore = await membership.GetSnapshotAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Reservar(request));
        Assert.Equal(markerBefore.UpdateTime, (await marker.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(capacityBefore.UpdateTime, (await capacity.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(membershipBefore.UpdateTime, (await membership.GetSnapshotAsync()).UpdateTime);
        Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
        Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task AuthoritativeRejectsLegacyAndUnknownMarkersWithoutWrites()
    {
        var (db, s, request) = await AuthoritativeSetup();
        var marker = db.Collection("turno_solicitudes").Document(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(request.ClaveIdempotencia))));
        foreach (var data in new[] { new Dictionary<string, object> { ["Solicitud"] = "legacy" }, new Dictionary<string, object> { ["Solicitud"] = "x", ["Esquema"] = "unknown" } })
        {
            await marker.SetAsync(data);
            var before = await marker.GetSnapshotAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => s.Reservar(request));
            Assert.Equal(before.UpdateTime, (await marker.GetSnapshotAsync()).UpdateTime);
            Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
            Assert.False((await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).Exists);
            Assert.False((await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync()).Exists);
        }
    }

    [LocalFirestoreFact]
    public async Task AuthoritativeInvalidSourcesAndConfigurationDoNotWrite()
    {
        var (db, s, request) = await AuthoritativeSetup();
        await db.Collection("clientes").Document("other").SetAsync(new { Id = "other" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Reservar(request with { ClienteId = "other" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Reservar(request with { Servicios = [new("missing", null)] }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Reservar(request with { Servicios = [new("s", "missing")] }));
        await db.Collection("configuracion").Document("system_config").UpdateAsync("IntervaloInicioTurnosMinutos", 0);
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Reservar(request));
        Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        Assert.False((await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).Exists);
        Assert.False((await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync()).Exists);
    }

    [LocalFirestoreFact]
    public async Task AuthoritativeGridAndLocalDateBoundaries()
    {
        var (db, s, request) = await AuthoritativeSetup();
        Assert.False(await s.Reservar(request with { InicioUtc = Day.AddHours(11).AddMinutes(45) }));
        Assert.False(await s.Reservar(request with { InicioUtc = Day.AddHours(12).AddMinutes(1) }));
        Assert.False(await s.Reservar(request with { InicioUtc = Day.AddHours(20).AddMinutes(45) }));
        Assert.True(await s.Reservar(request with { InicioUtc = Day.AddHours(1) })); // Monday 22:00 in Buenos Aires
        Assert.Single((await db.Collection("turnos").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task EmulatorLockTimeoutOnConfigMutationLeavesBookingAtomic()
    {
        var (db, s, request) = await AuthoritativeSetup();
        var read = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        s.AfterConfigRead = async () =>
        {
            if (Interlocked.Increment(ref attempts) == 1)
            {
                read.TrySetResult();
                await release.Task.WaitAsync(TimeSpan.FromSeconds(20));
            }
        };
        var booking = s.Reservar(request);
        Task? mutation = null;
        try
        {
            await read.Task.WaitAsync(TimeSpan.FromSeconds(10));
            mutation = db.Collection("configuracion").Document("system_config").UpdateAsync("IntervaloInicioTurnosMinutos", 0);
            var error = await Assert.ThrowsAsync<Grpc.Core.RpcException>(async () => await mutation.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(Grpc.Core.StatusCode.Aborted, error.StatusCode);
            Assert.Contains("Transaction lock timeout", error.Status.Detail);
            release.TrySetResult();
            Assert.True(await booking.WaitAsync(TimeSpan.FromSeconds(30)));
            Assert.Equal(1, attempts);
            Assert.Equal(15L, (await db.Collection("configuracion").Document("system_config").GetSnapshotAsync()).GetValue<long>("IntervaloInicioTurnosMinutos"));
            var appointment = await db.Collection("turnos").Document(request.Id).GetSnapshotAsync();
            Assert.True(appointment.Exists);
            Assert.Equal(100m, appointment.ConvertTo<Turno>().PrecioAcordado);
            Assert.Single((await db.Collection("turnos").GetSnapshotAsync()).Documents);
            var markerId = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(request.ClaveIdempotencia)));
            var marker = await db.Collection("turno_solicitudes").Document(markerId).GetSnapshotAsync();
            Assert.True(marker.Exists);
            Assert.Equal("ids-v2", marker.GetValue<string>("Esquema"));
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(request), marker.GetValue<string>("Solicitud"));
            Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
            Assert.Equal(1L, (await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).GetValue<long>("Version"));
            Assert.Equal(1L, (await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync()).GetValue<long>("Version"));
        }
        finally
        {
            release.TrySetResult();
            try { await booking.WaitAsync(TimeSpan.FromSeconds(30)); } catch { /* observe cleanup without masking the assertion */ }
            if (mutation != null) try { await mutation.WaitAsync(TimeSpan.FromSeconds(30)); } catch { }
        }
    }

    [LocalFirestoreFact]
    public async Task ConfigMutationAfterReservationReadSerializesWithoutPartialWrites()
    {
        var (db, s, request) = await AuthoritativeSetup();
        var read = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        s.AfterConfigRead = async () =>
        {
            Interlocked.Increment(ref attempts);
            read.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(15));
        };
        var booking = s.Reservar(request);
        try
        {
            await read.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var mutation = db.Collection("configuracion").Document("system_config").UpdateAsync("IntervaloInicioTurnosMinutos", 0);
            Assert.NotSame(mutation, await Task.WhenAny(mutation, Task.Delay(500)));
            release.TrySetResult();
            Assert.True(await booking.WaitAsync(TimeSpan.FromSeconds(30)));
            await mutation.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(1, attempts);
            Assert.Equal(0L, (await db.Collection("configuracion").Document("system_config").GetSnapshotAsync()).GetValue<long>("IntervaloInicioTurnosMinutos"));
            Assert.Single((await db.Collection("turnos").GetSnapshotAsync()).Documents);
            Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
            Assert.Equal(1L, (await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).GetValue<long>("Version"));
            Assert.Equal(1L, (await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync()).GetValue<long>("Version"));
        }
        finally { release.TrySetResult(); }
    }

    [LocalFirestoreFact]
    public async Task ConfigMutationCommitsBeforeReservationReadsAndRejectsWithoutWrites()
    {
        var (db, s, request) = await AuthoritativeSetup();
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var aboutToRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumeBooking = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var config = db.Collection("configuracion").Document("system_config");
        s.BeforeConfigRead = async () => { aboutToRead.TrySetResult(); await resumeBooking.Task.WaitAsync(TimeSpan.FromSeconds(20)); };
        var mutation = db.RunTransactionAsync(async tx =>
        {
            await tx.GetSnapshotAsync(config);
            locked.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10));
            tx.Update(config, "IntervaloInicioTurnosMinutos", 0);
        });
        try
        {
            await locked.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var booking = s.Reservar(request);
            await aboutToRead.Task.WaitAsync(TimeSpan.FromSeconds(10));
            release.TrySetResult();
            await mutation.WaitAsync(TimeSpan.FromSeconds(30));
            resumeBooking.TrySetResult();
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await booking.WaitAsync(TimeSpan.FromSeconds(30)));
            Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
            Assert.Empty((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
            Assert.False((await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).Exists);
            Assert.False((await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync()).Exists);
        }
        finally { release.TrySetResult(); resumeBooking.TrySetResult(); }
    }

    [LocalFirestoreFact]
    public async Task ConcurrentAuthoritativeBookingsRespectCapacity()
    {
        var (db, s, request) = await AuthoritativeSetup();
        var results = await Task.WhenAll(s.Reservar(request), s.Reservar(request with { Id = "other", ClaveIdempotencia = "other" }));
        Assert.Single(results.Where(result => result));
        Assert.Single((await db.Collection("turnos").GetSnapshotAsync()).Documents);
        Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        Assert.Equal(1L, (await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).GetValue<long>("Version"));
        Assert.Equal(1L, (await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync()).GetValue<long>("Version"));
    }

    [LocalFirestoreFact]
    public async Task CrearReplayRequiresPersistedIdentityButAllowsLaterChanges()
    {
        foreach (var field in new[] { "Id", "ClaveIdempotencia", "missing Id", "missing ClaveIdempotencia" })
        {
            var (db, service) = await Setup();
            var turno = Booking("replay");
            var appointment = db.Collection("turnos").Document(turno.Id);
            await appointment.SetAsync(turno);
            var markerId = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(turno.ClaveIdempotencia)));
            var marker = db.Collection("turno_solicitudes").Document(markerId);
            await marker.SetAsync(new Dictionary<string, object> { ["Solicitud"] = System.Text.Json.JsonSerializer.Serialize(turno) });
            var capacity = db.Collection("coordinacion").Document("turnos");
            var membership = db.Collection("coordinacion").Document("empleados");
            await capacity.SetAsync(new { Version = 7 });
            await membership.SetAsync(new { Version = 9 });
            await appointment.UpdateAsync(field.StartsWith("missing ") ? field[8..] : field,
                field.StartsWith("missing ") ? FieldValue.Delete : (object)"other");
            var before = await appointment.GetSnapshotAsync();
            var markerBefore = await marker.GetSnapshotAsync();
            var capacityBefore = await capacity.GetSnapshotAsync();
            var membershipBefore = await membership.GetSnapshotAsync();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Create(service, turno));
            Assert.Equal("Identidad de turno incompatible.", error.Message);
            Assert.Equal(before.UpdateTime, (await appointment.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(markerBefore.UpdateTime, (await marker.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(capacityBefore.UpdateTime, (await capacity.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(membershipBefore.UpdateTime, (await membership.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(7L, (await capacity.GetSnapshotAsync()).GetValue<long>("Version"));
            Assert.Equal(9L, (await membership.GetSnapshotAsync()).GetValue<long>("Version"));
        }
        {
            var (db, service) = await Setup();
            var turno = Booking("valid-replay");
            var appointment = db.Collection("turnos").Document(turno.Id);
            await appointment.SetAsync(turno);
            var markerId = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(turno.ClaveIdempotencia)));
            var marker = db.Collection("turno_solicitudes").Document(markerId);
            await marker.SetAsync(new Dictionary<string, object> { ["Solicitud"] = System.Text.Json.JsonSerializer.Serialize(turno) });
            await appointment.UpdateAsync("Estado", "Cancelado");
            var before = await appointment.GetSnapshotAsync();
            var markerBefore = await marker.GetSnapshotAsync();
            Assert.True(await Create(service, turno));
            Assert.Equal(before.UpdateTime, (await appointment.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(markerBefore.UpdateTime, (await marker.GetSnapshotAsync()).UpdateTime);
            Assert.False((await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).Exists);
            Assert.False((await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync()).Exists);
        }
    }

    [LocalFirestoreFact]
    public async Task CrearReplayRejectsMalformedCurrentAppointmentWithoutWrites()
    {
        foreach (var (field, value) in new (string, object)[] {
            ("ClienteId", ""), ("Vehiculos", Array.Empty<object>()),
            ("FinUtc", Day.AddMinutes(600)), ("Estado", "") })
        {
            var (db, service) = await Setup();
            var turno = Booking("replay-malformed");
            var appointment = db.Collection("turnos").Document(turno.Id);
            await appointment.SetAsync(turno);
            var markerId = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(turno.ClaveIdempotencia)));
            var marker = db.Collection("turno_solicitudes").Document(markerId);
            await marker.SetAsync(new Dictionary<string, object> { ["Solicitud"] = System.Text.Json.JsonSerializer.Serialize(turno) });
            var capacity = db.Collection("coordinacion").Document("turnos");
            var membership = db.Collection("coordinacion").Document("empleados");
            await capacity.SetAsync(new { Version = 7 });
            await membership.SetAsync(new { Version = 9 });
            await appointment.UpdateAsync(field, value);
            var before = await appointment.GetSnapshotAsync();
            var markerBefore = await marker.GetSnapshotAsync();
            var capacityBefore = await capacity.GetSnapshotAsync();
            var membershipBefore = await membership.GetSnapshotAsync();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Create(service, turno));
            Assert.Equal("Turno persistido inválido.", error.Message);
            Assert.Equal(before.UpdateTime, (await appointment.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(markerBefore.UpdateTime, (await marker.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(capacityBefore.UpdateTime, (await capacity.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(membershipBefore.UpdateTime, (await membership.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(7L, (await capacity.GetSnapshotAsync()).GetValue<long>("Version"));
            Assert.Equal(9L, (await membership.GetSnapshotAsync()).GetValue<long>("Version"));
        }
    }

    [LocalFirestoreFact]
    public async Task CrearReplayValidatesLifecycleAndMalformedTypeWithoutWrites()
    {
        var (db, service) = await Setup();
        var turno = Booking("states");
        var appointment = db.Collection("turnos").Document(turno.Id);
        await appointment.SetAsync(turno);
        var markerId = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(turno.ClaveIdempotencia)));
        var marker = db.Collection("turno_solicitudes").Document(markerId);
        await marker.SetAsync(new Dictionary<string, object> { ["Solicitud"] = System.Text.Json.JsonSerializer.Serialize(turno) });
        var capacity = db.Collection("coordinacion").Document("turnos");
        var membership = db.Collection("coordinacion").Document("empleados");
        await capacity.SetAsync(new { Version = 7 });
        await membership.SetAsync(new { Version = 9 });
        foreach (var state in new object[] { "Pendiente", "Cancelado", "Ausente", "EnProceso", "Completado", "Desconocido", "", 42L })
        {
            await appointment.UpdateAsync("Estado", state);
            var before = await appointment.GetSnapshotAsync();
            var markerBefore = await marker.GetSnapshotAsync();
            var capacityBefore = await capacity.GetSnapshotAsync();
            var membershipBefore = await membership.GetSnapshotAsync();
            if (state is string text && text is "Pendiente" or "Cancelado" or "Ausente" or "EnProceso" or "Completado")
                Assert.True(await Create(service, turno));
            else
            {
                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Create(service, turno));
                Assert.Equal("Turno persistido inválido.", error.Message);
            }
            Assert.Equal(before.UpdateTime, (await appointment.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(markerBefore.UpdateTime, (await marker.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(capacityBefore.UpdateTime, (await capacity.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(membershipBefore.UpdateTime, (await membership.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(7L, (await capacity.GetSnapshotAsync()).GetValue<long>("Version"));
            Assert.Equal(9L, (await membership.GetSnapshotAsync()).GetValue<long>("Version"));
        }
    }

    [LocalFirestoreFact]
    public async Task CrearRejectsForgedSnapshotWithoutWriting()
    {
        var (db, service) = await Setup();
        var forged = Booking("forged");
        forged.PrecioAcordado = 1m;
        forged.DesglosePrecio!.Subtotal = 1m;
        forged.DesglosePrecio.Total = 1m;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Create(service, forged));
        Assert.Equal("La creación directa de turnos no está permitida.", error.Message);
        Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        Assert.False((await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).Exists);
        Assert.False((await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync()).Exists);
    }

    [LocalFirestoreFact]
    public async Task CrearRejectsMissingAndEmptyForecastWithoutWrites()
    {
        var (db, s) = await Setup();
        var missing = Booking("missing");
        missing.InsumosForecast = null;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Create(s, missing));
        Assert.Equal("La creación directa de turnos no está permitida.", error.Message);
        Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        Assert.False((await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).Exists);
        Assert.False((await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync()).Exists);
        error = await Assert.ThrowsAsync<InvalidOperationException>(() => Create(s, Booking("empty")));
        Assert.Equal("La creación directa de turnos no está permitida.", error.Message);
        Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        Assert.False((await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).Exists);
        Assert.False((await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync()).Exists);
    }

    [LocalFirestoreFact]
    public async Task ConcurrentCreatesCannotExceedCapacity()
    {
        var (db, s, request) = await AuthoritativeSetup();
        var results = await Task.WhenAll(s.Reservar(request), s.Reservar(request with { Id = "b", ClaveIdempotencia = "b" }));
        Assert.Single(results.Where(x => x));
        Assert.Single((await db.Collection("turnos").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task DuplicateRequestIsIdempotentAndConflictingReuseRejected()
    {
        var (db, s, request) = await AuthoritativeSetup();
        Assert.True(await s.Reservar(request));
        Assert.True(await s.Reservar(request));
        var changed = request with { InicioUtc = request.InicioUtc.AddMinutes(15) };
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Reservar(changed));
        Assert.Single((await db.Collection("turnos").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task ReservarReplayRequiresExistingAppointmentAndNeverWrites()
    {
        var (db, service, booking) = await AuthoritativeSetup();
        Assert.True(await service.Reservar(booking));
        var appointment = db.Collection("turnos").Document(booking.Id);
        var marker = Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        var capacity = await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
        var membership = await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync();
        await appointment.UpdateAsync("Estado", "Cancelado");
        var edited = await appointment.GetSnapshotAsync();

        async Task AssertUnchanged()
        {
            Assert.Equal(marker.UpdateTime, (await marker.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(capacity.UpdateTime, (await capacity.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(membership.UpdateTime, (await membership.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        }
        Assert.True(await service.Reservar(booking));
        Assert.Equal(edited.UpdateTime, (await appointment.GetSnapshotAsync()).UpdateTime);
        await AssertUnchanged();
        var conflict = booking with { InicioUtc = booking.InicioUtc.AddMinutes(15) };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.Reservar(conflict));
        Assert.Equal("Clave de idempotencia reutilizada con otro turno.", error.Message);
        Assert.Equal(edited.UpdateTime, (await appointment.GetSnapshotAsync()).UpdateTime);
        await AssertUnchanged();

        await appointment.DeleteAsync();
        error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.Reservar(booking));
        Assert.Equal("El turno de la solicitud no existe.", error.Message);
        Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
        await AssertUnchanged();
    }

    [LocalFirestoreFact]
    public async Task ReservarRejectsMalformedMarkersAndConflictBeforeMissingAppointmentWithoutWrites()
    {
        var (db, service, booking) = await AuthoritativeSetup();
        Assert.True(await service.Reservar(booking));
        var appointment = db.Collection("turnos").Document(booking.Id);
        var marker = Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents).Reference;
        var capacity = await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
        var membership = await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync();
        var originalMarker = await marker.GetSnapshotAsync();
        var originalAppointment = await appointment.GetSnapshotAsync();
        foreach (var data in new[] { new Dictionary<string, object> { ["Other"] = "x" }, new Dictionary<string, object> { ["Solicitud"] = 42L } })
        {
            await marker.SetAsync(data);
            var before = await marker.GetSnapshotAsync();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.Reservar(booking));
            Assert.Equal("Clave de idempotencia reutilizada con otro turno.", error.Message);
            Assert.Equal(before.UpdateTime, (await marker.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(originalAppointment.UpdateTime, (await appointment.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(capacity.UpdateTime, (await capacity.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(membership.UpdateTime, (await membership.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Single((await db.Collection("turnos").GetSnapshotAsync()).Documents);
            Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        }
        await marker.SetAsync(originalMarker.ToDictionary());
        await appointment.DeleteAsync();
        var restored = await marker.GetSnapshotAsync();
        var conflict = booking with { InicioUtc = booking.InicioUtc.AddMinutes(15) };
        var conflictError = await Assert.ThrowsAsync<InvalidOperationException>(() => service.Reservar(conflict));
        Assert.Equal("Clave de idempotencia reutilizada con otro turno.", conflictError.Message);
        Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
        Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        Assert.Equal(restored.UpdateTime, (await marker.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(capacity.UpdateTime, (await capacity.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(membership.UpdateTime, (await membership.Reference.GetSnapshotAsync()).UpdateTime);
    }

    [LocalFirestoreFact]
    public async Task ConcurrentIdenticalRequestsPersistOnlyOnce()
    {
        var (db, s, request) = await AuthoritativeSetup();
        var results = await Task.WhenAll(s.Reservar(request), s.Reservar(request));
        Assert.All(results, Assert.True);
        Assert.Single((await db.Collection("turnos").GetSnapshotAsync()).Documents);
        Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        Assert.Equal  (1L, (await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).GetValue<long>("Version"));
        Assert.Equal(1L, (await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync()).GetValue<long>("Version"));
    }

    [LocalFirestoreFact]
    public async Task RejectionsPreserveExistingDocumentsAndVersions()
    {
        var (db, s, request) = await AuthoritativeSetup();
        Assert.True(await s.Reservar(request));
        var capacity = db.Collection("coordinacion").Document("turnos");
        var membership = db.Collection("coordinacion").Document("empleados");
        var beforeCapacity = await capacity.GetSnapshotAsync();
        var beforeMembership = await membership.GetSnapshotAsync();
        var original = await db.Collection("turnos").Document(request.Id).GetSnapshotAsync();
        var reused = request with { Id = "different" };
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Reservar(reused));
        Assert.False(await s.Reservar(request with { Id = "rejected", ClaveIdempotencia = "rejected" }));
        Assert.Equal(original.UpdateTime, (await original.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(beforeCapacity.UpdateTime, (await capacity.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(beforeMembership.UpdateTime, (await membership.GetSnapshotAsync()).UpdateTime);
        Assert.Single((await db.Collection("turnos").GetSnapshotAsync()).Documents);
        Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task FailureAfterStagedWritesRollsBackEverything()
    {
        var (db, s, request) = await AuthoritativeSetup();
        var version = db.Collection("coordinacion").Document("empleados");
        await version.SetAsync(new Dictionary<string, object> { ["Version"] = long.MaxValue });
        var before = await version.GetSnapshotAsync();
        await Assert.ThrowsAsync<OverflowException>(() => s.Reservar(request));
        Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        Assert.False((await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).Exists);
        Assert.Equal(before.UpdateTime, (await version.GetSnapshotAsync()).UpdateTime);
    }

    [LocalFirestoreFact]
    public async Task ConcurrentEmployeeDeactivationAndBookingHaveAConsistentOutcome()
    {
        var (db, s, request) = await AuthoritativeSetup();
        var staff = new PersonalService(db);
        var booking = s.Reservar(request);
        var change = staff.CambiarEstadoEmpleado("0", "Inactivo");
        await Task.WhenAll(booking, change);
        Assert.Equal("Inactivo", (await db.Collection("empleados").Document("0").GetSnapshotAsync()).GetValue<string>("Estado"));
        Assert.Equal(booking.Result ? 1 : 0, (await db.Collection("turnos").GetSnapshotAsync()).Count);
        Assert.Equal(booking.Result ? 2L : 1L, (await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync()).GetValue<long>("Version"));
    }

    [LocalFirestoreFact]
    public async Task ExactBoundariesAndStaffAreRespected()
    {
        var (db, s, request) = await AuthoritativeSetup(activeStaff: 2, duration: 16, slots: 2);
        var service = db.Collection("servicios").Document("s");
        var vehicleType = db.Collection("tiposVehiculos").Document("t");
        TurnoService.SnapshotRequest At(string id, int minutes) => request with
        {
            Id = id, ClaveIdempotencia = id, InicioUtc = Day.AddHours(12).AddMinutes(minutes)
        };
        Assert.True(await s.Reservar(At("a", 0)));
        await service.UpdateAsync("TiempoEstimado", 15);
        Assert.True(await s.Reservar(At("b", 15)));
        Assert.True(await s.Reservar(At("c", 30)));
        Assert.False(await s.Reservar(At("d", 15)));
        await vehicleType.UpdateAsync("CantidadEmpleadosRequeridos", 2);
        Assert.True(await s.Reservar(At("e", 45)));
        await vehicleType.UpdateAsync("CantidadEmpleadosRequeridos", 1);
        Assert.False(await s.Reservar(At("f", 45)));
    }

    [LocalFirestoreFact]
    public async Task CancellationPersistsVacancyAndNeverResetsItOnReplay()
    {
        var (db, s, input) = await AuthoritativeSetup();
        await ConfigureCancellation(db);
        var request = input with { Id = "original", ClaveIdempotencia = "original" };
        Assert.True(await s.Reservar(request));
        var appointment = db.Collection("turnos").Document("original");
        var persisted = await appointment.GetSnapshotAsync();
        await s.Cancelar("original");
        var cascades = await db.Collection("turno_cascadas").GetSnapshotAsync();
        var cascade = Assert.Single(cascades.Documents);
        Assert.Equal("original", cascade.GetValue<string>("CancelledAppointmentId"));
        Assert.Equal(persisted.GetValue<DateTime>("InicioUtc"), cascade.GetValue<DateTime>("VacancyStartUtc"));
        Assert.Equal(persisted.GetValue<DateTime>("FinUtc"), cascade.GetValue<DateTime>("VacancyEndUtc"));
        Assert.Equal("Pendiente", cascade.GetValue<string>("ContinuationState"));
        Assert.Equal("", cascade.GetValue<string>("PendingId"));
        await cascade.Reference.UpdateAsync("ContinuationState", "Procesando");
        var before = await cascade.Reference.GetSnapshotAsync();
        var cancelled = await appointment.GetSnapshotAsync();
        var capacity = await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
        await s.Cancelar("original");
        Assert.Equal(before.UpdateTime, (await cascade.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(cancelled.UpdateTime, (await appointment.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(capacity.UpdateTime, (await capacity.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task CancellationConflictAndInvalidIntervalLeaveNoWrites()
    {
        var (db, s, input) = await AuthoritativeSetup();
        var request = input with { Id = "original", ClaveIdempotencia = "original" };
        Assert.True(await s.Reservar(request));
        var appointment = db.Collection("turnos").Document("original");
        var id = "cancel-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("original")));
        var cascade = db.Collection("turno_cascadas").Document(id);
        await cascade.SetAsync(new { PendingId = "other" });
        var before = await appointment.GetSnapshotAsync();
        var lockRef = db.Collection("coordinacion").Document("turnos");
        var capacity = await lockRef.GetSnapshotAsync();
        var conflicting = await cascade.GetSnapshotAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Cancelar("original"));
        Assert.Equal(before.UpdateTime, (await appointment.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(capacity.UpdateTime, (await lockRef.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(conflicting.UpdateTime, (await cascade.GetSnapshotAsync()).UpdateTime);
        var invalid = db.Collection("turnos").Document("invalid");
        var invalidBooking = Booking("invalid"); invalidBooking.FinUtc = Day;
        await invalid.SetAsync(invalidBooking);
        before = await invalid.GetSnapshotAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Cancelar("invalid"));
        Assert.Equal(before.UpdateTime, (await invalid.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(capacity.UpdateTime, (await lockRef.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(conflicting.UpdateTime, (await cascade.GetSnapshotAsync()).UpdateTime);
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Cancelar("missing"));
        Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task CancellationReleasesCapacityAndIsIdempotent()
    {
        var (db, s, input) = await AuthoritativeSetup();
        await ConfigureCancellation(db);
        var request = input with { Id = "original", ClaveIdempotencia = "original" };
        Assert.True(await s.Reservar(request));
        var replacement = request with { Id = "replacement", ClaveIdempotencia = "replacement" };
        Assert.False(await s.Reservar(replacement));
        await s.Cancelar("original");
        var original = db.Collection("turnos").Document("original");
        var cancelled = await original.GetSnapshotAsync();
        Assert.Equal("Cancelado", cancelled.GetValue<string>("Estado"));
        var lockRef = db.Collection("coordinacion").Document("turnos");
        var capacity = await lockRef.GetSnapshotAsync();
        Assert.Equal(2L, capacity.GetValue<long>("Version"));
        await s.Cancelar("original");
        Assert.Equal(cancelled.UpdateTime, (await original.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(capacity.UpdateTime, (await lockRef.GetSnapshotAsync()).UpdateTime);
        Assert.True(await s.Reservar(replacement));
        Assert.Equal(2, (await db.Collection("turno_solicitudes").GetSnapshotAsync()).Count);
    }

    [LocalFirestoreFact]
    public async Task CancellationRejectsMissingAndNonPendingWithoutWrites()
    {
        var (db, s, request) = await AuthoritativeSetup();
        Assert.True(await s.Reservar(request with { Id = "started", ClaveIdempotencia = "started" }));
        var doc = db.Collection("turnos").Document("started");
        await doc.UpdateAsync("Estado", "EnProceso");
        var before = await doc.GetSnapshotAsync();
        var lockRef = db.Collection("coordinacion").Document("turnos");
        var capacity = await lockRef.GetSnapshotAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Cancelar("started"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Cancelar("missing"));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(capacity.UpdateTime, (await lockRef.GetSnapshotAsync()).UpdateTime);
    }

    [LocalFirestoreFact]
    public async Task ConcurrentCancellationAndBookingNeverOversubscribe()
    {
        var (db, s, request) = await AuthoritativeSetup();
        await ConfigureCancellation(db);
        Assert.True(await s.Reservar(request with { Id = "old", ClaveIdempotencia = "old" }));
        var cancel = s.Cancelar("old");
        var replacement = request with { Id = "new", ClaveIdempotencia = "new" };
        var booking = s.Reservar(replacement);
        await Task.WhenAll(cancel, booking);
        Assert.Equal("Cancelado", (await db.Collection("turnos").Document("old").GetSnapshotAsync()).GetValue<string>("Estado"));
        Assert.Equal(booking.Result ? 1 : 0, (await db.Collection("turnos").WhereEqualTo("Estado", "Pendiente").GetSnapshotAsync()).Count);
        if (!booking.Result) Assert.True(await s.Reservar(replacement));
    }

    [LocalFirestoreFact]
    public async Task InvalidAndUnavailableDoNotWrite()
    {
        var (db, s) = await Setup();
        var invalid = Booking("invalid"); invalid.Vehiculos[0].Servicios[0].Precio = -1;
        await Assert.ThrowsAsync<ArgumentException>(() => Create(s, invalid));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Create(s, Booking("off-grid", 601)));
        Assert.Equal("La creación directa de turnos no está permitida.", error.Message);
        Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
        Assert.False((await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).Exists);
        Assert.False((await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync()).Exists);
    }
}

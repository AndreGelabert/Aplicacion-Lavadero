using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;

namespace Firebase.Tests.Integration;

public class TurnoWebBookingAdapterTests
{
    [LocalFirestoreFact]
    public async Task ReservesUsingAuthoritativeValuesAndStableRetry()
    {
        Assert.Equal("127.0.0.1:8190", Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST"));
        var db = new FirestoreDbBuilder { ProjectId = "demo-web-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("empleados").Document("e").SetAsync(new { Estado = "Activo" });
        await db.Collection("clientes").Document("c").SetAsync(new { Id = "c" });
        await db.Collection("vehiculos").Document("v").SetAsync(new Vehiculo { Id = "v", ClienteId = "c", TipoVehiculo = "Auto", Patente = "ABC123", Marca = "a", Modelo = "b", Color = "c" });
        await db.Collection("tiposVehiculos").Document("t").SetAsync(new TipoVehiculo { Id = "t", Nombre = "Auto", FormatoPatente = "lllnnn", CantidadEmpleadosRequeridos = 1 });
        await db.Collection("servicios").Document("s").SetAsync(new Servicio { Id = "s", Nombre = "Lavado", Tipo = "Lavado", TipoVehiculo = "Auto", Descripcion = "x", Estado = "Activo", Precio = 123, TiempoEstimado = 30 });
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion { CapacidadMaximaConcurrente = 1, HorariosOperacion = new() { ["Martes"] = "09:00-18:00" } });
        var adapter = new TurnoWebBookingAdapter(new TurnoService(db));
        var request = new TurnoWebBookingRequest("id", "key", "c", "v", new DateTime(2030, 1, 1, 9, 0, 0), true, [new("s", null)]);
        Assert.True(await adapter.Reservar(request));
        await db.Collection("servicios").Document("s").UpdateAsync("Precio", 999d);
        Assert.True(await adapter.Reservar(request));
        var turno = (await db.Collection("turnos").Document("id").GetSnapshotAsync()).ConvertTo<Turno>();
        Assert.Equal(new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc), turno.InicioUtc);
        Assert.Equal(123m, turno.PrecioAcordado);
        Assert.Equal(30, turno.Vehiculos.Single().Servicios.Single().TiempoEstimado);
        Assert.True(turno.AceptaAdelanto);
        Assert.Equal("Web", turno.Origen);
        Assert.Single((await db.Collection("turnos").GetSnapshotAsync()).Documents);
        Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.Reservar(request with { Id = "other" }));
        Assert.False(await adapter.Reservar(request with { Id = "other", ClaveIdempotencia = "other" }));
        Assert.Single((await db.Collection("turnos").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task PackageProvenanceAndAgreedValuesPersistThroughReplay()
    {
        Assert.Equal("127.0.0.1:8190", Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST"));
        var db = new FirestoreDbBuilder { ProjectId = "demo-web-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("empleados").Document("e").SetAsync(new { Estado = "Activo" });
        await db.Collection("clientes").Document("c").SetAsync(new { Id = "c" });
        await db.Collection("vehiculos").Document("v").SetAsync(new Vehiculo { Id = "v", ClienteId = "c", TipoVehiculo = "Auto", Patente = "ABC123", Marca = "a", Modelo = "b", Color = "c" });
        await db.Collection("tiposVehiculos").Document("t").SetAsync(new TipoVehiculo { Id = "t", Nombre = "Auto", FormatoPatente = "lllnnn", CantidadEmpleadosRequeridos = 1 });
        foreach (var (id, price, minutes) in new[] { ("s1", 100m, 20), ("s2", 50m, 30), ("s3", 20m, 10) })
            await db.Collection("servicios").Document(id).SetAsync(new Servicio { Id = id, Nombre = id, Tipo = "Lavado", TipoVehiculo = "Auto", Descripcion = "x", Estado = "Activo", Precio = price, TiempoEstimado = minutes });
        await db.Collection("paquetes_servicios").Document("p").SetAsync(new PaqueteServicio { Id = "p", Nombre = "Pack", Estado = "Activo", TipoVehiculo = "Auto", ServiciosIds = ["s1", "s2"], PorcentajeDescuento = 10 });
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion { CapacidadMaximaConcurrente = 1, HorariosOperacion = new() { ["Martes"] = "09:00-18:00" } });
        var adapter = new TurnoWebBookingAdapter(new TurnoService(db));
        var request = new TurnoWebBookingRequest("id", "key", "c", "v", new DateTime(2030, 1, 1, 9, 0, 0), false, [new("s1", "p"), new("s2", "p"), new("s3", null)], Descuento: 20);
        Assert.True(await adapter.Reservar(request));
        var reference = db.Collection("turnos").Document("id");
        var before = await reference.GetSnapshotAsync();
        var marker = Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        var capacity = await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
        var membership = await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync();
        void AssertSnapshot(Turno turno)
        {
            var services = turno.Vehiculos.Single().Servicios;
            Assert.Equal(new string?[] { "p", "p", null }, services.Select(s => s.PaqueteId));
            Assert.Equal(new decimal[] { 100m, 50m, 20m }, services.Select(s => s.Precio));
            Assert.Equal(new[] { 20, 30, 10 }, services.Select(s => s.TiempoEstimado));
            Assert.Equal(124m, turno.PrecioAcordado);
            Assert.Equal(TimeSpan.FromMinutes(60), turno.FinUtc - turno.InicioUtc);
        }
        AssertSnapshot(before.ConvertTo<Turno>());
        await db.Collection("servicios").Document("s1").UpdateAsync("Precio", 999d);
        Assert.True(await adapter.Reservar(request));
        var after = await reference.GetSnapshotAsync();
        AssertSnapshot(after.ConvertTo<Turno>());
        Assert.Equal(before.UpdateTime, after.UpdateTime);
        Assert.Equal(marker.UpdateTime, (await marker.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(capacity.UpdateTime, (await capacity.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(membership.UpdateTime, (await membership.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Single((await db.Collection("turnos").GetSnapshotAsync()).Documents);
        Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task InvalidInputsNeverCreateReservationOrCounters()
    {
        Assert.Equal("127.0.0.1:8190", Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST"));
        var db = new FirestoreDbBuilder { ProjectId = "demo-web-invalid-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var adapter = new TurnoWebBookingAdapter(new TurnoService(db));
        var valid = new TurnoWebBookingRequest("id", "key", "c", "v", new DateTime(2030, 1, 1, 9, 0, 0), false, [new("s", null)]);
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");
        var invalid = new DateTime(2008, 10, 19, 0, 30, 0);
        var ambiguous = new DateTime(2009, 3, 14, 23, 30, 0);
        Assert.True(zone.IsInvalidTime(invalid), "Expected the Buenos Aires spring transition gap.");
        Assert.False(zone.IsAmbiguousTime(invalid));
        Assert.True(zone.IsAmbiguousTime(ambiguous), "Expected the Buenos Aires autumn transition overlap.");
        Assert.False(zone.IsInvalidTime(ambiguous));
        var badInputs = new[] {
            valid with { ClaveIdempotencia = null! }, valid with { ClaveIdempotencia = "" }, valid with { ClaveIdempotencia = " " },
            valid with { Id = null! }, valid with { Id = "" }, valid with { Id = "a/b" },
            valid with { ClienteId = null! }, valid with { ClienteId = "" }, valid with { VehiculoId = null! }, valid with { VehiculoId = "" },
            valid with { Servicios = null! }, valid with { Servicios = [] }, valid with { Servicios = [null!] },
            valid with { Servicios = [new(null!, null)] }, valid with { Servicios = [new("", null)] }, valid with { Servicios = [new("s", "")] },
            valid with { InicioLocal = DateTime.SpecifyKind(valid.InicioLocal, DateTimeKind.Utc) }, valid with { InicioLocal = invalid }, valid with { InicioLocal = ambiguous }
        };
        await Assert.ThrowsAsync<ArgumentNullException>(() => adapter.Reservar(null!));
        foreach (var bad in badInputs)
        {
            await Assert.ThrowsAsync<ArgumentException>(() => adapter.Reservar(bad));
            Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
            Assert.Empty((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
            Assert.False((await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).Exists);
            Assert.False((await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync()).Exists);
        }
        Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        Assert.False((await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).Exists);
        Assert.False((await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync()).Exists);
    }
}

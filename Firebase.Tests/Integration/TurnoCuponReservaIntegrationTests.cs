using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;

namespace Firebase.Tests.Integration;

public class TurnoCuponReservaIntegrationTests
{
    private static async Task<(FirestoreDb db, TurnoService service, TurnoService.SnapshotRequest request)> Setup()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-cupon-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("empleados").Document("e").SetAsync(new { Estado = "Activo" });
        await db.Collection("clientes").Document("c").SetAsync(new { Id = "c" });
        await db.Collection("vehiculos").Document("v").SetAsync(new Vehiculo { Id = "v", Patente = "ABC123", TipoVehiculo = "Auto", Marca = "a", Modelo = "b", Color = "c", ClienteId = "c" });
        await db.Collection("tiposVehiculos").Document("t").SetAsync(new TipoVehiculo { Id = "t", Nombre = "Auto", FormatoPatente = "lllnnn", CantidadEmpleadosRequeridos = 1 });
        await db.Collection("servicios").Document("s").SetAsync(new Servicio { Id = "s", Nombre = "Lavado", Tipo = "Lavado", TipoVehiculo = "Auto", Descripcion = "x", Estado = "Activo", Precio = 100, TiempoEstimado = 30 });
        await db.Collection("paquetes_servicios").Document("p").SetAsync(new PaqueteServicio { Id = "p", Nombre = "Pack", Estado = "Activo", TipoVehiculo = "Auto", ServiciosIds = ["s"], PorcentajeDescuento = 10 });
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion { CapacidadMaximaConcurrente = 1, HorariosOperacion = new() { ["Martes"] = "09:00-18:00" } });
        await db.Collection("turno_cupones").Document("coupon").SetAsync(new { Owner = "c", Percentage = 20d, ExpiresUtc = DateTime.UtcNow.AddDays(1), Consumed = false });
        return (db, new TurnoService(db), new("id", "c", "v", "Web", "key", new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc), false, [new("s", "p")], CuponId: "coupon"));
    }

    [LocalFirestoreFact]
    public async Task DuplicateServiceSelectionsRejectWithoutCouponOrCoordinationWrites()
    {
        foreach (var selections in new TurnoService.SnapshotSelection[][] {
            [new("s", null), new("s", null)], [new("s", "p"), new("s", null)],
            [new("s", "p"), new("s", "p")], [new("s", "p"), new("s", "p2")] })
        {
            var (db, service, request) = await Setup();
            await db.Collection("paquetes_servicios").Document("p2").SetAsync(new PaqueteServicio { Id = "p2", Nombre = "Pack 2", Estado = "Activo", TipoVehiculo = "Auto", ServiciosIds = ["s"], PorcentajeDescuento = 5 });
            var coupon = await db.Collection("turno_cupones").Document("coupon").GetSnapshotAsync();
            var capacity = db.Collection("coordinacion").Document("turnos");
            var membership = db.Collection("coordinacion").Document("empleados");
            await capacity.SetAsync(new { Version = 7 }); await membership.SetAsync(new { Version = 9 });
            var beforeCapacity = await capacity.GetSnapshotAsync(); var beforeMembership = await membership.GetSnapshotAsync();
            var error = await Assert.ThrowsAsync<ArgumentException>(() => service.Reservar(request with { Servicios = selections }));
            Assert.Equal("Datos de turno inválidos. (Parameter 'input')", error.Message);
            Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
            Assert.Empty((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
            Assert.Equal(coupon.UpdateTime, (await coupon.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.False((await coupon.Reference.GetSnapshotAsync()).GetValue<bool>("Consumed"));
            Assert.Equal(beforeCapacity.UpdateTime, (await capacity.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(beforeMembership.UpdateTime, (await membership.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(7L, (await capacity.GetSnapshotAsync()).GetValue<long>("Version"));
            Assert.Equal(9L, (await membership.GetSnapshotAsync()).GetValue<long>("Version"));
        }
    }

    [LocalFirestoreFact]
    public async Task LegacyCrearRejectsCouponWithoutWritesAndPreservesNonCouponReplay()
    {
        var (db, service, request) = await Setup();
        var start = request.InicioUtc;
        var turno = new Turno { Id = request.Id, ClienteId = request.ClienteId, Estado = "Pendiente", Origen = "internal",
            ClaveIdempotencia = request.ClaveIdempotencia, InicioUtc = start, FinUtc = start.AddMinutes(30), CuponId = "coupon",
            Vehiculos = [new() { VehiculoId = "v", CantidadEmpleadosRequeridos = 1,
                Servicios = [new() { ServicioId = "s", TiempoEstimado = 30, Precio = 100m }] }] };
        var openings = new[] { new TurnoOpening(start.Date, start.Date.AddDays(1)) };
        var coupon = await db.Collection("turno_cupones").Document("coupon").GetSnapshotAsync();
        var capacity = db.Collection("coordinacion").Document("turnos");
        var membership = db.Collection("coordinacion").Document("empleados");
        await capacity.SetAsync(new { Version = 7 });
        await membership.SetAsync(new { Version = 9 });
        var capacityBefore = await capacity.GetSnapshotAsync();
        var membershipBefore = await membership.GetSnapshotAsync();
        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.Crear(turno, 1, openings, start.Date, start.Date));
        Assert.Equal("Cupón no admitido. (Parameter 'turno')", error.Message);
        Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        Assert.Equal(coupon.UpdateTime, (await coupon.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.False((await coupon.Reference.GetSnapshotAsync()).GetValue<bool>("Consumed"));
        Assert.Equal(capacityBefore.UpdateTime, (await capacity.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(membershipBefore.UpdateTime, (await membership.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(7L, (await capacity.GetSnapshotAsync()).GetValue<long>("Version"));
        Assert.Equal(9L, (await membership.GetSnapshotAsync()).GetValue<long>("Version"));

        turno.CuponId = null;
        turno.PrecioAcordado = 100m;
        turno.DesglosePrecio = new() { Version = 1, Subtotal = 100m, Total = 100m };
        turno.RecetaOperativa = new() { Version = 1, Servicios = [new() { ServicioId = "s", Nombre = "Lavado", Tipo = "Lavado" }] };
        turno.InsumosForecast = [];
        await db.Collection("turnos").Document(turno.Id).SetAsync(turno);
        var markerId = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(turno.ClaveIdempotencia)));
        await db.Collection("turno_solicitudes").Document(markerId).SetAsync(new Dictionary<string, object> { ["Solicitud"] = System.Text.Json.JsonSerializer.Serialize(turno) });
        var appointment = await db.Collection("turnos").Document(turno.Id).GetSnapshotAsync();
        var marker = Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        capacityBefore = await capacity.GetSnapshotAsync();
        membershipBefore = await membership.GetSnapshotAsync();
        Assert.True(await service.Crear(turno, 1, openings, start.Date, start.Date));
        Assert.Equal(appointment.UpdateTime, (await appointment.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(marker.UpdateTime, (await marker.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(capacityBefore.UpdateTime, (await capacity.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(membershipBefore.UpdateTime, (await membership.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(coupon.UpdateTime, (await coupon.Reference.GetSnapshotAsync()).UpdateTime);
    }

    [LocalFirestoreFact]
    public async Task FrozenCouponTermsSurviveCatalogMutationAndReplayWithoutWrites()
    {
        var (db, service, request) = await Setup();
        Assert.True(await service.Reservar(request));
        var marker = Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        Assert.Equal("ids-v4", marker.GetValue<string>("Esquema"));
        Assert.Equal("coupon", marker.GetValue<string>("CuponId"));
        Assert.Equal("c", marker.GetValue<string>("CuponOwner"));
        Assert.Equal(20d, marker.GetValue<double>("CuponPorcentaje"));
        var appointment = await db.Collection("turnos").Document(request.Id).GetSnapshotAsync();
        Assert.Equal(72m, appointment.ConvertTo<Turno>().PrecioAcordado);
        var couponRef = db.Collection("turno_cupones").Document("coupon");
        await couponRef.UpdateAsync(new Dictionary<string, object> { ["Percentage"] = 99d, ["Owner"] = "other" });
        var coupon = await couponRef.GetSnapshotAsync();
        var capacity = await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
        var membership = await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync();
        Assert.True(await service.Reservar(request));
        Assert.Equal(marker.UpdateTime, (await marker.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(appointment.UpdateTime, (await appointment.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(coupon.UpdateTime, (await couponRef.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(capacity.UpdateTime, (await capacity.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(membership.UpdateTime, (await membership.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(72m, (await appointment.Reference.GetSnapshotAsync()).ConvertTo<Turno>().PrecioAcordado);
    }

    [LocalFirestoreFact]
    public async Task BookingReplayRejectsMalformedAppointmentAndPreservesLifecycleAcrossSchemas()
    {
        foreach (var schema in new[] { "ids-v2", "ids-v3", "ids-v4" })
        foreach (var corruption in new[] { "missing", "nonstring", "unknown", "required" })
        {
            var (db, service, original) = await Setup();
            var request = schema == "ids-v2" ? original with { CuponId = null } : original;
            Assert.True(await service.Reservar(request));
            var marker = Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
            if (schema == "ids-v3") await marker.Reference.UpdateAsync("Esquema", schema);
            var reference = db.Collection("turnos").Document(request.Id);
            await reference.UpdateAsync(corruption == "required" ? "Vehiculos" : "Estado", corruption switch
            {
                "missing" => FieldValue.Delete, "nonstring" => (object)42, "unknown" => "Desconocido",
                _ => Array.Empty<object>()
            });
            var before = await reference.GetSnapshotAsync();
            marker = await marker.Reference.GetSnapshotAsync();
            var coupon = await db.Collection("turno_cupones").Document("coupon").GetSnapshotAsync();
            var capacity = await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
            var membership = await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync();
            await Assert.ThrowsAnyAsync<Exception>(() => service.Reservar(request));
            Assert.Equal(before.UpdateTime, (await reference.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(marker.UpdateTime, (await marker.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(coupon.UpdateTime, (await coupon.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(capacity.UpdateTime, (await capacity.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(membership.UpdateTime, (await membership.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Single((await db.Collection("turnos").GetSnapshotAsync()).Documents);
            Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        }
    }

    [LocalFirestoreFact]
    public async Task BookingReplayAllowsValidStatesAndMutablePreferencesAcrossSchemas()
    {
        foreach (var schema in new[] { "ids-v2", "ids-v3", "ids-v4" })
        foreach (var state in new[] { "Pendiente", "Cancelado", "Ausente", "EnProceso", "Completado" })
        {
            var (db, service, original) = await Setup();
            var request = schema == "ids-v2" ? original with { CuponId = null } : original;
            Assert.True(await service.Reservar(request));
            var marker = Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
            if (schema == "ids-v3") await marker.Reference.UpdateAsync("Esquema", schema);
            var reference = db.Collection("turnos").Document(request.Id);
            await reference.UpdateAsync(new Dictionary<string, object> { ["Estado"] = state, ["AceptaAdelanto"] = true });
            await db.Collection("servicios").Document("s").UpdateAsync("Precio", 999d);
            await db.Collection("paquetes_servicios").Document("p").UpdateAsync("PorcentajeDescuento", 50d);
            await db.Collection("clientes").Document("c").UpdateAsync("Estado", "Inactivo");
            var before = await reference.GetSnapshotAsync();
            marker = await marker.Reference.GetSnapshotAsync();
            var coupon = await db.Collection("turno_cupones").Document("coupon").GetSnapshotAsync();
            var capacity = await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
            var membership = await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync();
            Assert.True(await service.Reservar(request));
            Assert.Equal(before.UpdateTime, (await reference.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(marker.UpdateTime, (await marker.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(coupon.UpdateTime, (await coupon.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(capacity.UpdateTime, (await capacity.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(membership.UpdateTime, (await membership.Reference.GetSnapshotAsync()).UpdateTime);
        }
    }

    [LocalFirestoreFact]
    public async Task CorruptFrozenCouponTermsFailClosedWithoutWrites()
    {
        foreach (var corruption in new Dictionary<string, object>[] {
            new() { ["CuponId"] = "other" }, new() { ["CuponOwner"] = "other" },
            new() { ["CuponPorcentaje"] = -1d }, new() { ["CuponPorcentaje"] = 101d },
            new() { ["CuponPorcentaje"] = double.NaN }, new() { ["CuponPorcentaje"] = FieldValue.Delete } })
        {
            var (db, service, request) = await Setup();
            Assert.True(await service.Reservar(request));
            var marker = Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
            await marker.Reference.UpdateAsync(corruption);
            marker = await marker.Reference.GetSnapshotAsync();
            var appointment = await db.Collection("turnos").Document(request.Id).GetSnapshotAsync();
            var coupon = await db.Collection("turno_cupones").Document("coupon").GetSnapshotAsync();
            var capacity = await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
            var membership = await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.Reservar(request));
            Assert.Equal(marker.UpdateTime, (await marker.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(appointment.UpdateTime, (await appointment.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(coupon.UpdateTime, (await coupon.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(capacity.UpdateTime, (await capacity.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(membership.UpdateTime, (await membership.Reference.GetSnapshotAsync()).UpdateTime);
        }
    }

    [LocalFirestoreFact]
    public async Task LegacyV3ExactReplaySucceedsWithoutDerivingOrWritingTerms()
    {
        var (db, service, request) = await Setup();
        Assert.True(await service.Reservar(request));
        var marker = Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        await marker.Reference.UpdateAsync(new Dictionary<string, object> {
            ["Esquema"] = "ids-v3", ["CuponId"] = FieldValue.Delete,
            ["CuponOwner"] = FieldValue.Delete, ["CuponPorcentaje"] = FieldValue.Delete });
        marker = await marker.Reference.GetSnapshotAsync();
        var appointment = await db.Collection("turnos").Document(request.Id).GetSnapshotAsync();
        var couponRef = db.Collection("turno_cupones").Document("coupon");
        await couponRef.UpdateAsync("Percentage", 99d);
        var coupon = await couponRef.GetSnapshotAsync();
        var capacity = await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
        var membership = await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync();
        Assert.True(await service.Reservar(request));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Reservar(request with { CuponId = null }));
        Assert.Equal(marker.UpdateTime, (await marker.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(appointment.UpdateTime, (await appointment.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(coupon.UpdateTime, (await couponRef.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(capacity.UpdateTime, (await capacity.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(membership.UpdateTime, (await membership.Reference.GetSnapshotAsync()).UpdateTime);
    }

    [LocalFirestoreFact]
    public async Task CouponCatalogEditUsesFrozenTermsAndCurrentPackageWithoutReconsuming()
    {
        var (db, service, request) = await Setup();
        Assert.True(await service.Reservar(request));
        await db.Collection("servicios").Document("s").UpdateAsync(new Dictionary<string, object> { ["Precio"] = 200d, ["Receta"] = new[] { new RecetaInsumo { InsumoId = "soap", DosisTeorica = 1m } } });
        await db.Collection("paquetes_servicios").Document("p").UpdateAsync("PorcentajeDescuento", 25d);
        var couponRef = db.Collection("turno_cupones").Document("coupon");
        await couponRef.UpdateAsync(new Dictionary<string, object> { ["Owner"] = "other", ["Percentage"] = 99d });
        var coupon = await couponRef.GetSnapshotAsync();
        var edit = new TurnoService.EditRequest(request with { Servicios = [new("s", null)] }, "first");
        Assert.True(await service.Editar(edit));
        var packageEdit = new TurnoService.EditRequest(request, "second");
        Assert.True(await service.Editar(packageEdit));
        var appointment = await db.Collection("turnos").Document(request.Id).GetSnapshotAsync();
        var result = appointment.ConvertTo<Turno>();
        Assert.Equal(120m, result.PrecioAcordado);
        Assert.Equal(20m, result.Descuento);
        Assert.Equal("coupon", result.CuponId);
        Assert.NotEmpty(Assert.Single(result.RecetaOperativa.Servicios).Receta);
        Assert.Single(result.InsumosForecast);
        Assert.True(await service.Editar(packageEdit));
        Assert.Equal(appointment.UpdateTime, (await appointment.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(coupon.UpdateTime, (await couponRef.GetSnapshotAsync()).UpdateTime);
    }

    [LocalFirestoreFact]
    public async Task CouponRebuildRejectsUnavailableTermsAndInvalidCatalogWithoutWrites()
    {
        foreach (var mode in new[] { "v3", "missing", "corrupt", "catalog", "capacity", "version" })
        {
            var (db, service, request) = await Setup();
            Assert.True(await service.Reservar(request));
            var marker = Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
            if (mode == "v3") await marker.Reference.UpdateAsync("Esquema", "ids-v3");
            if (mode == "missing") await marker.Reference.UpdateAsync("CuponPorcentaje", FieldValue.Delete);
            if (mode == "corrupt") await marker.Reference.UpdateAsync("CuponPorcentaje", 101d);
            if (mode == "capacity")
                Assert.True(await service.Reservar(request with { Id = "other", ClaveIdempotencia = "other", CuponId = null, InicioUtc = request.InicioUtc.AddHours(1) }));
            var appointment = await db.Collection("turnos").Document(request.Id).GetSnapshotAsync();
            var coupon = await db.Collection("turno_cupones").Document("coupon").GetSnapshotAsync();
            marker = await marker.Reference.GetSnapshotAsync();
            var capacity = await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
            var membership = await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync();
            var updated = request with { Servicios = [new(mode == "catalog" ? "missing" : "s", null)], InicioUtc = mode == "capacity" ? request.InicioUtc.AddHours(1) : request.InicioUtc };
            var edit = new TurnoService.EditRequest(updated, "rebuild", mode == "version" ? Timestamp.FromDateTime(DateTime.SpecifyKind(new DateTime(2000, 1, 1), DateTimeKind.Utc)) : null);
            if (mode == "capacity") Assert.False(await service.Editar(edit));
            else await Assert.ThrowsAsync<InvalidOperationException>(() => service.Editar(edit));
            Assert.Equal(appointment.UpdateTime, (await appointment.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(marker.UpdateTime, (await marker.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(coupon.UpdateTime, (await coupon.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(capacity.UpdateTime, (await capacity.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(membership.UpdateTime, (await membership.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Empty((await db.Collection("turno_ediciones").GetSnapshotAsync()).Documents);
        }
    }

    [LocalFirestoreFact]
    public async Task CouponVehicleEditKeepsOwnerAndFrozenTerms()
    {
        var (db, service, request) = await Setup();
        await db.Collection("vehiculos").Document("v2").SetAsync(new Vehiculo { Id = "v2", Patente = "DEF456", TipoVehiculo = "Auto", Marca = "a", Modelo = "b", Color = "c", ClienteId = "c" });
        Assert.True(await service.Reservar(request));
        var coupon = await db.Collection("turno_cupones").Document("coupon").GetSnapshotAsync();
        Assert.True(await service.Editar(new(request with { VehiculoId = "v2" }, "vehicle")));
        var result = (await db.Collection("turnos").Document(request.Id).GetSnapshotAsync()).ConvertTo<Turno>();
        Assert.Equal("v2", result.Vehiculos[0].VehiculoId);
        Assert.Equal("c", result.ClienteId);
        Assert.Equal(72m, result.PrecioAcordado);
        Assert.Equal(coupon.UpdateTime, (await coupon.Reference.GetSnapshotAsync()).UpdateTime);
    }

    [LocalFirestoreFact]
    public async Task NonCouponScheduleEditRejectsInvalidPriceSnapshotWithoutWrites()
    {
        var (db, service, original) = await Setup();
        var request = original with { CuponId = null };
        Assert.True(await service.Reservar(request));
        var reference = db.Collection("turnos").Document(request.Id);
        await reference.UpdateAsync("PrecioAcordado", 1d);
        var before = await reference.GetSnapshotAsync();
        var capacity = await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
        var membership = await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => service.Editar(new(request with { InicioUtc = request.InicioUtc.AddMinutes(15) }, "invalid")));
        Assert.Equal(before.UpdateTime, (await reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(capacity.UpdateTime, (await capacity.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(membership.UpdateTime, (await membership.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Empty((await db.Collection("turno_ediciones").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task OmittedAssociatedPeopleDoNotOverwritePersistedSelections()
    {
        var (db, service, original) = await Setup();
        await db.Collection("clientes").Document("other").SetAsync(new { Id = "other" });
        await db.Collection("vehiculos").Document("v").UpdateAsync("ClientesIds", new[] { "other" });
        var request = original with { CuponId = null, ClienteTrajoId = "other", ClienteRetiraId = "other" };
        Assert.True(await service.Reservar(request));
        var edit = request with { ClienteTrajoId = null, ClienteRetiraId = null, InicioUtc = request.InicioUtc.AddMinutes(15) };
        Assert.True(await service.Editar(new(edit, "people")));
        var result = (await db.Collection("turnos").Document(request.Id).GetSnapshotAsync()).ConvertTo<Turno>();
        Assert.Equal("other", result.ClienteTrajoId);
        Assert.Equal("other", result.ClienteRetiraId);
    }

    [LocalFirestoreFact]
    public async Task FractionalCouponRebuildReplaysWithoutWrites()
    {
        var (db, service, request) = await Setup();
        await db.Collection("turno_cupones").Document("coupon").UpdateAsync("Percentage", 12.34d);
        await db.Collection("servicios").Document("s").UpdateAsync("Precio", 123.45d);
        Assert.True(await service.Reservar(request));
        var edit = new TurnoService.EditRequest(request with { Servicios = [new("s", null)] }, "fractional");
        Assert.True(await service.Editar(edit));
        var appointment = await db.Collection("turnos").Document(request.Id).GetSnapshotAsync();
        var marker = Assert.Single((await db.Collection("turno_ediciones").GetSnapshotAsync()).Documents);
        Assert.True(await service.Editar(edit));
        Assert.Equal(appointment.UpdateTime, (await appointment.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(marker.UpdateTime, (await marker.Reference.GetSnapshotAsync()).UpdateTime);
    }

    [LocalFirestoreFact]
    public async Task OldEditReplayAfterInterveningEditRejectsWithoutOverwriting()
    {
        var (db, service, original) = await Setup();
        var request = original with { CuponId = null };
        Assert.True(await service.Reservar(request));
        var appointmentRef = db.Collection("turnos").Document(request.Id);
        var initial = await appointmentRef.GetSnapshotAsync();
        var first = new TurnoService.EditRequest(request with { InicioUtc = request.InicioUtc.AddMinutes(15) }, "first", initial.UpdateTime);
        Assert.True(await service.Editar(first));
        var afterFirst = await appointmentRef.GetSnapshotAsync();
        var second = new TurnoService.EditRequest(first.Solicitud with { InicioUtc = request.InicioUtc.AddMinutes(30) }, "second", afterFirst.UpdateTime);
        Assert.True(await service.Editar(second));
        var before = await appointmentRef.GetSnapshotAsync();
        var markers = await db.Collection("turno_ediciones").GetSnapshotAsync();
        var capacity = await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
        var membership = await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync();
        var coupon = await db.Collection("turno_cupones").Document("coupon").GetSnapshotAsync();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.Editar(first));
        Assert.Equal("Edición incompatible.", error.Message);
        var after = await appointmentRef.GetSnapshotAsync();
        Assert.Equal(before.UpdateTime, after.UpdateTime);
        Assert.Equal(second.Solicitud.InicioUtc, after.ConvertTo<Turno>().InicioUtc);
        var markersAfter = await db.Collection("turno_ediciones").GetSnapshotAsync();
        Assert.Equal(2, markers.Count);
        Assert.Equal(markers.Documents.Select(d => (d.Id, d.UpdateTime)).OrderBy(x => x.Id), markersAfter.Documents.Select(d => (d.Id, d.UpdateTime)).OrderBy(x => x.Id));
        Assert.Equal(capacity.UpdateTime, (await capacity.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(membership.UpdateTime, (await membership.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(coupon.UpdateTime, (await coupon.Reference.GetSnapshotAsync()).UpdateTime);
    }

    [LocalFirestoreFact]
    public async Task DirectCrearWithoutCompleteSnapshotCannotBeScheduleEdited()
    {
        foreach (var partial in new[] { false, true })
        {
            var (db, service, original) = await Setup();
            var request = original with { CuponId = null, Servicios = [new("s", null)] };
            var start = request.InicioUtc;
            var turno = new Turno { Id = request.Id, ClienteId = request.ClienteId, Estado = "Pendiente", Origen = request.Origen,
                ClaveIdempotencia = request.ClaveIdempotencia, InicioUtc = start, FinUtc = start.AddMinutes(30),
                ClienteTrajoId = "c", ClienteRetiraId = "c", PrecioAcordado = 100m,
                Vehiculos = [new() { VehiculoId = "v", CantidadEmpleadosRequeridos = 1,
                    Servicios = [new() { ServicioId = "s", TiempoEstimado = 30, Precio = 100m }] }],
                DesglosePrecio = partial ? new() { Version = 1, Subtotal = 100m, Total = 100m } : null };
            Assert.Empty(turno.Validar());
            await Assert.ThrowsAnyAsync<Exception>(() => service.Crear(turno, 1, [new(start.Date, start.Date.AddDays(1))], start.Date, start.Date));
            Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
            Assert.Empty((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
            Assert.False((await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).Exists);
            Assert.False((await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync()).Exists);
        }
    }

    [LocalFirestoreFact]
    public async Task CouponAndPackageUseCatalogAndReplayDoesNotConsumeAgain()
    {
        var (db, service, request) = await Setup();
        Assert.True(await service.Reservar(request));
        var coupon = await db.Collection("turno_cupones").Document("coupon").GetSnapshotAsync();
        Assert.True(coupon.GetValue<bool>("Consumed"));
        var appointment = await db.Collection("turnos").Document("id").GetSnapshotAsync();
        Assert.Equal(72m, appointment.ConvertTo<Turno>().PrecioAcordado);
        await db.Collection("servicios").Document("s").UpdateAsync("Precio", 999d);
        Assert.True(await service.Reservar(request));
        Assert.Equal(coupon.UpdateTime, (await coupon.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(appointment.UpdateTime, (await appointment.Reference.GetSnapshotAsync()).UpdateTime);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Reservar(request with { CuponId = null }));
    }

    [LocalFirestoreFact]
    public async Task CouponScheduleAndPreferenceEditsPreserveAgreedDataAndReplay()
    {
        var (db, service, request) = await Setup();
        Assert.True(await service.Reservar(request));
        var appointment = db.Collection("turnos").Document(request.Id);
        var before = (await appointment.GetSnapshotAsync()).ConvertTo<Turno>();
        var coupon = await db.Collection("turno_cupones").Document("coupon").GetSnapshotAsync();
        await db.Collection("servicios").Document("s").UpdateAsync("Precio", 999d);
        var edit = new TurnoService.EditRequest(request with { InicioUtc = request.InicioUtc.AddMinutes(15) }, "schedule");
        Assert.True(await service.Editar(edit));
        var after = (await appointment.GetSnapshotAsync()).ConvertTo<Turno>();
        Assert.Equal(before.CuponId, after.CuponId);
        Assert.Equal(before.Descuento, after.Descuento);
        Assert.Equal(before.PrecioAcordado, after.PrecioAcordado);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before.Vehiculos), System.Text.Json.JsonSerializer.Serialize(after.Vehiculos));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before.RecetaOperativa), System.Text.Json.JsonSerializer.Serialize(after.RecetaOperativa));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before.InsumosForecast), System.Text.Json.JsonSerializer.Serialize(after.InsumosForecast));
        Assert.Equal(edit.Solicitud.InicioUtc, after.InicioUtc);
        var saved = await appointment.GetSnapshotAsync();
        Assert.True(await service.Editar(edit));
        Assert.Equal(saved.UpdateTime, (await appointment.GetSnapshotAsync()).UpdateTime);
        Assert.True(await service.Editar(new(edit.Solicitud with { AceptaAdelanto = true }, "preference")));
        Assert.True((await appointment.GetSnapshotAsync()).ConvertTo<Turno>().AceptaAdelanto);
        Assert.Equal(coupon.UpdateTime, (await coupon.Reference.GetSnapshotAsync()).UpdateTime);
    }

    [LocalFirestoreFact]
    public async Task CouponEditRejectsChangesAndUnavailableTimeWithoutWrites()
    {
        var (db, service, request) = await Setup();
        Assert.True(await service.Reservar(request));
        var appointment = await db.Collection("turnos").Document(request.Id).GetSnapshotAsync();
        var coupon = await db.Collection("turno_cupones").Document("coupon").GetSnapshotAsync();
        var capacity = await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
        var membership = await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync();
        foreach (var bad in new[] { request with { CuponId = "other" }, request with { CuponId = null }, request with { ClienteId = "other" }, request with { Descuento = 5 }, request with { VehiculoId = "other" }, request with { Servicios = [new("other", null)] } })
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.Editar(new(bad, Guid.NewGuid().ToString())));
        Assert.False(await service.Editar(new(request with { InicioUtc = request.InicioUtc.AddMinutes(1) }, "unavailable")));
        Assert.Equal(appointment.UpdateTime, (await appointment.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(coupon.UpdateTime, (await coupon.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(capacity.UpdateTime, (await capacity.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(membership.UpdateTime, (await membership.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Empty((await db.Collection("turno_ediciones").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task MalformedCouponAppointmentFailsClosedWithoutWrites()
    {
        var (db, service, request) = await Setup();
        Assert.True(await service.Reservar(request));
        var appointment = db.Collection("turnos").Document(request.Id);
        await appointment.UpdateAsync("Vehiculos", Array.Empty<object>());
        var before = await appointment.GetSnapshotAsync();
        var coupon = await db.Collection("turno_cupones").Document("coupon").GetSnapshotAsync();
        var capacity = await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
        var membership = await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.Editar(new(request, "malformed")));
        Assert.Equal("El turno no está pendiente o es inválido.", error.Message);
        Assert.Equal(before.UpdateTime, (await appointment.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(coupon.UpdateTime, (await coupon.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(capacity.UpdateTime, (await capacity.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(membership.UpdateTime, (await membership.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Empty((await db.Collection("turno_ediciones").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task OccupiedCapacityRejectsCouponEditWithoutWrites()
    {
        var (db, service, request) = await Setup();
        Assert.True(await service.Reservar(request));
        Assert.True(await service.Reservar(request with { Id = "second", ClaveIdempotencia = "second", CuponId = null, InicioUtc = request.InicioUtc.AddHours(1) }));
        var appointment = await db.Collection("turnos").Document(request.Id).GetSnapshotAsync();
        var other = await db.Collection("turnos").Document("second").GetSnapshotAsync();
        var coupon = await db.Collection("turno_cupones").Document("coupon").GetSnapshotAsync();
        var capacity = await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
        var membership = await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync();
        Assert.False(await service.Editar(new(request with { InicioUtc = request.InicioUtc.AddHours(1) }, "occupied")));
        Assert.Equal(appointment.UpdateTime, (await appointment.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(other.UpdateTime, (await other.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(coupon.UpdateTime, (await coupon.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(capacity.UpdateTime, (await capacity.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(membership.UpdateTime, (await membership.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Empty((await db.Collection("turno_ediciones").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task CouponEditFailsClosedWhenBookingMarkerIsMangled()
    {
        var (db, service, request) = await Setup();
        Assert.True(await service.Reservar(request));
        var marker = Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        await marker.Reference.UpdateAsync("Esquema", "corrupto");
        var markerBefore = await marker.Reference.GetSnapshotAsync();
        var appointment = await db.Collection("turnos").Document(request.Id).GetSnapshotAsync();
        var coupon = await db.Collection("turno_cupones").Document("coupon").GetSnapshotAsync();
        var capacity = await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
        var membership = await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync();

        var edit = request with { CuponId = null, Descuento = 5 };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.Editar(new(edit, "edit-corrupt-marker")));
        Assert.Equal("Edición de cupón no admitida.", error.Message);
        Assert.Equal(appointment.UpdateTime, (await appointment.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(markerBefore.UpdateTime, (await marker.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(coupon.UpdateTime, (await coupon.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.True((await coupon.Reference.GetSnapshotAsync()).GetValue<bool>("Consumed"));
        Assert.Equal(capacity.UpdateTime, (await capacity.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(membership.UpdateTime, (await membership.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Empty((await db.Collection("turno_ediciones").GetSnapshotAsync()).Documents);
        Assert.Equal("coupon", (await appointment.Reference.GetSnapshotAsync()).GetValue<string>("CuponId"));
    }

    [LocalFirestoreFact]
    public async Task InvalidCouponAndUnavailableCapacityLeaveNoWrites()
    {
        var (db, service, request) = await Setup();
        var coupon = db.Collection("turno_cupones").Document("coupon");
        foreach (var bad in new[] { request with { ClienteId = "other" }, request with { CuponId = "missing" }, request with { Descuento = 5 } })
            await Assert.ThrowsAnyAsync<Exception>(() => service.Reservar(bad));
        await coupon.UpdateAsync("ExpiresUtc", DateTime.UtcNow.AddDays(-1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Reservar(request));
        await coupon.UpdateAsync("ExpiresUtc", DateTime.UtcNow.AddDays(1));
        Assert.False(await service.Reservar(request with { InicioUtc = request.InicioUtc.AddMinutes(1) }));
        Assert.False((await coupon.GetSnapshotAsync()).GetValue<bool>("Consumed"));
        Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        Assert.False((await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).Exists);
        Assert.False((await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync()).Exists);
    }

    private static async Task AssertNoBookingWrites(FirestoreDb db, DocumentSnapshot coupon)
    {
        var after = await coupon.Reference.GetSnapshotAsync();
        Assert.Equal(coupon.UpdateTime, after.UpdateTime);
        Assert.False(after.GetValue<bool>("Consumed"));
        Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        Assert.False((await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).Exists);
        Assert.False((await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync()).Exists);
    }

    [LocalFirestoreFact]
    public async Task WrongPersistedOwnerAndCatalogFailureRollBack()
    {
        var (db, service, request) = await Setup();
        var coupon = db.Collection("turno_cupones").Document("coupon");
        await db.Collection("clientes").Document("other").SetAsync(new { Id = "other" });
        await coupon.UpdateAsync("Owner", "other");
        var before = await coupon.GetSnapshotAsync();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.Reservar(request));
        Assert.Equal("Cupón inválido.", error.Message);
        await AssertNoBookingWrites(db, before);
        await coupon.UpdateAsync("Owner", "c");
        before = await coupon.GetSnapshotAsync();
        error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.Reservar(request with { Servicios = [new("missing", null)] }));
        Assert.Equal("Servicio no encontrado.", error.Message);
        await AssertNoBookingWrites(db, before);
    }

    [LocalFirestoreFact]
    public async Task CancellationAndNoShowNeverRestoreConsumedCoupon()
    {
        foreach (var cancel in new[] { true, false })
        {
            var (db, service, request) = await Setup();
            var config = db.Collection("configuracion").Document("system_config");
            await config.UpdateAsync(new Dictionary<string, object> { ["CancelacionAnticipadaHorasMinimas"] = 24, ["CancelacionAnticipadaDescuento"] = 0, ["CancelacionAnticipadaValidezDias"] = 30, ["ToleranciaLlegadaTurnosMinutos"] = 15 });
            Assert.True(await service.Reservar(request));
            var before = await db.Collection("turno_cupones").Document("coupon").GetSnapshotAsync();
            if (cancel) await service.Cancelar(request.Id);
            else Assert.True(await service.MarcarAusencia(request.Id, request.InicioUtc.AddMinutes(16)));
            var after = await before.Reference.GetSnapshotAsync();
            Assert.True(after.GetValue<bool>("Consumed"));
            Assert.Equal(before.UpdateTime, after.UpdateTime);
            Assert.Equal(cancel ? "Cancelado" : "Ausente", (await db.Collection("turnos").Document(request.Id).GetSnapshotAsync()).GetValue<string>("Estado"));
        }
    }

    [LocalFirestoreFact]
    public async Task NoCouponMarkerReplaysWithoutWritesAndEditPreservesSnapshot()
    {
        var (db, service, couponRequest) = await Setup();
        var request = couponRequest with { CuponId = null };
        Assert.True(await service.Reservar(request));
        var marker = Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        Assert.Equal("ids-v2", marker.GetValue<string>("Esquema"));
        var appointment = await db.Collection("turnos").Document(request.Id).GetSnapshotAsync();
        var capacity = await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
        var membership = await db.Collection("coordinacion").Document("empleados").GetSnapshotAsync();
        await db.Collection("servicios").Document("s").UpdateAsync("Precio", 999d);
        Assert.True(await service.Reservar(request));
        Assert.Equal(marker.UpdateTime, (await marker.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(appointment.UpdateTime, (await appointment.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(capacity.UpdateTime, (await capacity.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(membership.UpdateTime, (await membership.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.True(await service.Editar(new(request with { InicioUtc = request.InicioUtc.AddMinutes(15) }, "edit")));
        var edited = (await appointment.Reference.GetSnapshotAsync()).ConvertTo<Turno>();
        Assert.Equal(90m, edited.PrecioAcordado);
        Assert.Equal(request.InicioUtc.AddMinutes(15), edited.InicioUtc);
        Assert.False((await db.Collection("turno_cupones").Document("coupon").GetSnapshotAsync()).GetValue<bool>("Consumed"));
    }

    [LocalFirestoreFact]
    public async Task ConcurrentReuseAllowsOnlyOneBooking()
    {
        var (db, service, request) = await Setup();
        async Task<bool> TryBook(TurnoService.SnapshotRequest input)
        {
            try { return await service.Reservar(input); } catch (InvalidOperationException) { return false; }
        }
        var results = await Task.WhenAll(TryBook(request), TryBook(request with { Id = "other", ClaveIdempotencia = "other", InicioUtc = request.InicioUtc.AddHours(1) }));
        Assert.Single(results.Where(x => x));
        Assert.Single((await db.Collection("turnos").GetSnapshotAsync()).Documents);
        Assert.Single((await db.Collection("turno_solicitudes").GetSnapshotAsync()).Documents);
        Assert.True((await db.Collection("turno_cupones").Document("coupon").GetSnapshotAsync()).GetValue<bool>("Consumed"));
        Assert.Equal(1L, (await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).GetValue<long>("Version"));
    }
}

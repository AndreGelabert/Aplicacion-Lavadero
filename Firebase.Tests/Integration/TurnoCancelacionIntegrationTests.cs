using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;

namespace Firebase.Tests.Integration;

public class TurnoCancelacionPropietarioIntegrationTests
{
    private static readonly DateTime Now = new(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static async Task<(FirestoreDb db, TurnoService service)> Setup()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-owner-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("turnos").Document("t").SetAsync(new { Estado = "Pendiente", ClienteId = "owner", InicioUtc = Now.AddDays(2), FinUtc = Now.AddDays(2).AddMinutes(30) });
        await db.Collection("coordinacion").Document("turnos").SetAsync(new { Version = 7L });
        await db.Collection("configuracion").Document("system_config").SetAsync(new { CancelacionAnticipadaHorasMinimas = 24, CancelacionAnticipadaDescuento = 10d, CancelacionAnticipadaValidezDias = 30 });
        return (db, new TurnoService(db) { CancellationUtcNow = () => Now });
    }

    private static Task CancelarComo(TurnoService service, string owner) => service.CancelarDePropietario("t", owner);

    private static async Task<string[]> State(FirestoreDb db)
    {
        var state = new List<string>();
        foreach (var name in new[] { "turnos", "coordinacion", "turno_cupones", "turno_cascadas" })
        {
            var docs = await db.Collection(name).GetSnapshotAsync();
            state.Add($"{name}:{docs.Count}");
            state.AddRange(docs.Documents.Select(d => $"{name}/{d.Id}:{d.UpdateTime}:{System.Text.Json.JsonSerializer.Serialize(d.ToDictionary())}"));
        }
        return state.OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    [LocalFirestoreFact]
    public async Task WrongOwnerCannotCancelOrReplayAndOwnerCanReplayWithoutWrites()
    {
        var (db, service) = await Setup();
        var before = await State(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => CancelarComo(service, "attacker"));
        Assert.Equal(before, await State(db));
        Assert.Empty((await db.Collection("turno_cupones").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);
        await CancelarComo(service, "owner");
        Assert.Equal("Cancelado", (await db.Collection("turnos").Document("t").GetSnapshotAsync()).GetValue<string>("Estado"));
        Assert.Equal("owner", Assert.Single((await db.Collection("turno_cupones").GetSnapshotAsync()).Documents).GetValue<string>("Owner"));
        Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);
        Assert.Equal(8L, (await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).GetValue<long>("Version"));
        var after = await State(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => CancelarComo(service, "attacker"));
        Assert.Equal(after, await State(db));
        await CancelarComo(service, "owner");
        Assert.Equal(after, await State(db));
    }

    [LocalFirestoreFact]
    public async Task StaffCancellationStillIssuesCouponAndReplaysWithoutWrites()
    {
        var (db, service) = await Setup();
        await service.Cancelar("t");
        Assert.Equal("Cancelado", (await db.Collection("turnos").Document("t").GetSnapshotAsync()).GetValue<string>("Estado"));
        Assert.Equal("owner", Assert.Single((await db.Collection("turno_cupones").GetSnapshotAsync()).Documents).GetValue<string>("Owner"));
        Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);
        Assert.Equal(8L, (await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).GetValue<long>("Version"));
        var after = await State(db);
        await service.Cancelar("t");
        Assert.Equal(after, await State(db));
    }
}

public class TurnoCancelacionIntegrationTests
{
    private static readonly DateTime Now = new(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static async Task<(FirestoreDb db, TurnoService service)> Setup(DateTime start, bool config = true)
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-cancel-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("turnos").Document("t").SetAsync(new { Estado = "Pendiente", ClienteId = "client", InicioUtc = start, FinUtc = start.AddMinutes(30) });
        await db.Collection("coordinacion").Document("turnos").SetAsync(new { Version = 7L });
        if (config) await db.Collection("configuracion").Document("system_config").SetAsync(new { CancelacionAnticipadaHorasMinimas = 24, CancelacionAnticipadaDescuento = 10d, CancelacionAnticipadaValidezDias = 30 });
        return (db, new TurnoService(db) { CancellationUtcNow = () => Now });
    }

    [LocalFirestoreFact]
    public async Task EligibleAndExactThresholdIssueOnceAndPreserveCascadeOnReplay()
    {
        foreach (var start in new[] { Now.AddHours(25), Now.AddHours(24) })
        {
            var (db, service) = await Setup(start);
            await service.Cancelar("t");
            var appointment = await db.Collection("turnos").Document("t").GetSnapshotAsync();
            Assert.Equal("Cancelado", appointment.GetValue<string>("Estado"));
            var coupon = Assert.Single((await db.Collection("turno_cupones").GetSnapshotAsync()).Documents);
            Assert.Equal("client", coupon.GetValue<string>("Owner"));
            Assert.Equal(10d, coupon.GetValue<double>("Percentage"));
            Assert.Equal(Now.AddDays(30), coupon.GetValue<DateTime>("ExpiresUtc"));
            Assert.False(coupon.GetValue<bool>("Consumed"));
            var cascade = Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);
            Assert.Equal(start, cascade.GetValue<DateTime>("VacancyStartUtc"));
            Assert.Equal(start.AddMinutes(30), cascade.GetValue<DateTime>("VacancyEndUtc"));
            var capacity = await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
            Assert.Equal(8L, capacity.GetValue<long>("Version"));
            await service.Cancelar("t");
            Assert.Equal(appointment.UpdateTime, (await appointment.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(coupon.UpdateTime, (await coupon.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(cascade.UpdateTime, (await cascade.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(capacity.UpdateTime, (await capacity.Reference.GetSnapshotAsync()).UpdateTime);
        }
    }

    [LocalFirestoreFact]
    public async Task LateAndLegacyCancelledDoNotReceiveCoupon()
    {
        var (db, service) = await Setup(Now.AddHours(24).AddTicks(-10));
        await service.Cancelar("t");
        Assert.Empty((await db.Collection("turno_cupones").GetSnapshotAsync()).Documents);
        Assert.Equal("Cancelado", (await db.Collection("turnos").Document("t").GetSnapshotAsync()).GetValue<string>("Estado"));
        Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);
        Assert.Equal(8L, (await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).GetValue<long>("Version"));
        await db.Collection("turnos").Document("legacy").SetAsync(new { Estado = "Cancelado" });
        await service.Cancelar("legacy");
        Assert.Empty((await db.Collection("turno_cupones").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task MissingAndInvalidConfigurationLeaveEverythingUnchanged()
    {
        var (db, service) = await Setup(Now.AddDays(2), false);
        var config = db.Collection("configuracion").Document("system_config");
        var appointmentBefore = await db.Collection("turnos").Document("t").GetSnapshotAsync();
        var capacityBefore = await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
        var missing = await Assert.ThrowsAsync<InvalidOperationException>(() => service.Cancelar("t"));
        Assert.Equal("Configuración de cancelación incompleta.", missing.Message);
        Assert.Equal(appointmentBefore.UpdateTime, (await appointmentBefore.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(capacityBefore.UpdateTime, (await capacityBefore.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Empty((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_cupones").GetSnapshotAsync()).Documents);
        await config.SetAsync(new { CancelacionAnticipadaHorasMinimas = 0, CancelacionAnticipadaDescuento = 10d, CancelacionAnticipadaValidezDias = 30 });
        await AssertRejected(db, service);
        await config.SetAsync(new { CancelacionAnticipadaHorasMinimas = 24, CancelacionAnticipadaDescuento = 10d });
        await AssertRejected(db, service);
    }

    [LocalFirestoreFact]
    public async Task CouponCollisionAndVersionOverflowRollback()
    {
        var (db, service) = await Setup(Now.AddDays(2));
        var coupon = db.Collection("turno_cupones").Document(TurnoService.CancellationCouponId("t"));
        await coupon.SetAsync(new { Owner = "other" });
        await AssertRejected(db, service);
        Assert.Equal("other", (await coupon.GetSnapshotAsync()).GetValue<string>("Owner"));
        var (db2, service2) = await Setup(Now.AddDays(2));
        await db2.Collection("coordinacion").Document("turnos").SetAsync(new { Version = long.MaxValue });
        await AssertRejected(db2, service2);
    }

    private static async Task AssertRejected(FirestoreDb db, TurnoService service)
    {
        var appointment = await db.Collection("turnos").Document("t").GetSnapshotAsync();
        var capacity = await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
        var coupons = await db.Collection("turno_cupones").GetSnapshotAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => service.Cancelar("t"));
        Assert.Equal(appointment.UpdateTime, (await appointment.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(capacity.UpdateTime, (await capacity.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Empty((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);
        var after = await db.Collection("turno_cupones").GetSnapshotAsync();
        Assert.Equal(coupons.Count, after.Count);
        foreach (var before in coupons.Documents) Assert.Equal(before.UpdateTime, (await before.Reference.GetSnapshotAsync()).UpdateTime);
    }
}

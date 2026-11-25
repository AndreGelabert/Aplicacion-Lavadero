using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;

namespace Firebase.Tests.Integration;

public class WhatsAppCancellationAuthorizationIntegrationTests
{
    [LocalFirestoreFact]
    public async Task StaffCancellationStillWorksAndReplays()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-wa-staff-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var now = DateTime.UtcNow;
        var appointment = db.Collection("turnos").Document("staff");
        await appointment.SetAsync(new { Estado = "Pendiente", ClienteId = "owner", InicioUtc = now.AddDays(2), FinUtc = now.AddDays(2).AddMinutes(30) });
        await db.Collection("coordinacion").Document("turnos").SetAsync(new { Version = 7L });
        await db.Collection("configuracion").Document("system_config").SetAsync(new { CancelacionAnticipadaHorasMinimas = 24, CancelacionAnticipadaDescuento = 10d, CancelacionAnticipadaValidezDias = 30 });
        var service = new TurnoService(db);
        await service.Cancelar("staff");
        var cancelled = await appointment.GetSnapshotAsync();
        Assert.Equal("Cancelado", cancelled.GetValue<string>("Estado"));
        Assert.Equal(8L, (await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).GetValue<long>("Version"));
        Assert.Single((await db.Collection("turno_cupones").GetSnapshotAsync()).Documents);
        Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);
        await service.Cancelar("staff");
        Assert.Equal(cancelled.UpdateTime, (await appointment.GetSnapshotAsync()).UpdateTime);
    }

    [LocalFirestoreFact]
    public async Task CancellationRequiresAndConsumesCurrentSessionCapability()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-wa-cancel-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var now = DateTime.UtcNow;
        var appointment = db.Collection("turnos").Document("t");
        var session = db.Collection("whatsapp_sessions").Document("5491112345678");
        await appointment.SetAsync(new { Estado = "Pendiente", ClienteId = "owner", InicioUtc = now.AddDays(2), FinUtc = now.AddDays(2).AddMinutes(30) });
        await db.Collection("coordinacion").Document("turnos").SetAsync(new { Version = 7L });
        await db.Collection("configuracion").Document("system_config").SetAsync(new { CancelacionAnticipadaHorasMinimas = 24, CancelacionAnticipadaDescuento = 10d, CancelacionAnticipadaValidezDias = 30 });
        await session.SetAsync(new { Id = "5491112345678", ClienteId = "owner", CurrentState = "TURNO_CONFIRMAR", Revision = 4L, IncarnationId = "incarnation", SelectionId = "choice", CancellationAppointmentId = "t" });
        var service = new TurnoService(db);
        Task Cancel(string incarnation)
        {
            var method = typeof(TurnoService).GetMethod("CancelarWhatsApp", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(method);
            return (Task)method.Invoke(service, new object[] { "t", "owner", "5491112345678", incarnation, 4L, "choice" })!;
        }
        var selected = await appointment.GetSnapshotAsync();
        await session.UpdateAsync("CancellationAppointmentVersion", selected.UpdateTime!);
        await appointment.UpdateAsync(new Dictionary<string, object> { ["InicioUtc"] = now.AddDays(3), ["FinUtc"] = now.AddDays(3).AddMinutes(30) });
        var changed = await appointment.GetSnapshotAsync();
        var unchangedSession = await session.GetSnapshotAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Cancel("incarnation"));
        Assert.Equal(changed.UpdateTime, (await appointment.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(unchangedSession.UpdateTime, (await session.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(7L, (await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).GetValue<long>("Version"));
        Assert.Empty((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_cupones").GetSnapshotAsync()).Documents);
        await session.UpdateAsync("CancellationAppointmentVersion", changed.UpdateTime!);
        var before = await appointment.GetSnapshotAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Cancel("old-incarnation"));
        Assert.Equal(before.UpdateTime, (await appointment.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(7L, (await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).GetValue<long>("Version"));
        Assert.Empty((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_cupones").GetSnapshotAsync()).Documents);
        await Cancel("incarnation");
        Assert.Equal("Cancelado", (await appointment.GetSnapshotAsync()).GetValue<string>("Estado"));
        Assert.Equal(8L, (await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).GetValue<long>("Version"));
        var consumed = await session.GetSnapshotAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Cancel("incarnation"));
        Assert.Equal(consumed.UpdateTime, (await session.GetSnapshotAsync()).UpdateTime);
    }
}

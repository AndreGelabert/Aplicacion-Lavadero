using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;

namespace Firebase.Tests.Integration;

public class EmailResponseReplayTests
{
    private static readonly DateTime T = new(2030, 1, 8, 12, 0, 0, DateTimeKind.Utc);
    [LocalFirestoreFact]
    public async Task SameActionReplaysWithoutWritesAndInvalidRequestsFail()
    {
        foreach (var accept in new[] { true, false }) await Exercise(accept, null);
    }

    [LocalFirestoreFact]
    public async Task MissingOrMismatchedDurableEvidenceFailsWithoutWrites()
    {
        foreach (var accept in new[] { true, false })
            foreach (var corruption in new[] { "missing", "mismatch", "malformed" })
                await Exercise(accept, corruption);
    }

    private static async Task Exercise(bool accept, string? corruption)
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-replay-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("turnos").Document("appointment").SetAsync(new Turno { Id = "appointment", ClienteId = "owner", Estado = "Pendiente", Origen = "test", ClaveIdempotencia = "appointment", AceptaAdelanto = true, InicioUtc = T.AddDays(2), FinUtc = T.AddDays(2).AddMinutes(30), Vehiculos = [new() { VehiculoId = "vehicle", CantidadEmpleadosRequeridos = 1, Servicios = [new() { ServicioId = "service", TiempoEstimado = 30 }] }] });
        await db.Collection("clientes").Document("owner").SetAsync(new { Id = "owner", Estado = "Activo", Email = "owner@example.com" });
        await db.Collection("empleados").Document("one").SetAsync(new { Estado = "Activo" });
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion { CapacidadMaximaConcurrente = 1, HorariosOperacion = new() { ["Miércoles"] = "00:00-23:59", ["Jueves"] = "00:00-23:59", ["Viernes"] = "00:00-23:59", ["Sábado"] = "00:00-23:59", ["Domingo"] = "00:00-23:59", ["Lunes"] = "00:00-23:59", ["Martes"] = "00:00-23:59" } });
        var offers = new TurnoOfertaPersistence(db);
        await offers.Create(new("cascade", "offer", "appointment", T, T.AddMinutes(10), T.AddDays(2), T.AddDays(1)));
        var email = new TurnoOfertaEmailCapability(db);
        var token = await email.Issue("cascade", "offer", "owner", T, T.AddMinutes(5));
        await email.RespondHttp("cascade", "offer", "owner", token, T.AddMinutes(1), accept, default);
        var collections = new[] { "turnos", "turno_ofertas", "turno_cascadas", "turno_ofertas_email_capabilities", "turno_ofertas_pendientes", "turno_aceptaciones", "turno_ofertas_terminales", "coordinacion" };
        async Task<string[]> Snapshot() => (await Task.WhenAll(collections.Select(async c => (await db.Collection(c).GetSnapshotAsync()).Documents.Select(d => $"{c}/{d.Id}:{d.UpdateTime}").OrderBy(x => x).ToArray()))).SelectMany(x => x).ToArray();
        if (corruption != null)
        {
            var proof = accept ? db.Collection("turno_aceptaciones").Document("offer") :
                (await db.Collection("turno_ofertas_terminales").GetSnapshotAsync()).Documents.Single().Reference;
            if (corruption == "missing") await proof.DeleteAsync();
            else await proof.UpdateAsync(accept ? "CascadeId" : "OfferId", corruption == "mismatch" ? "other" : 42);
            var damaged = await Snapshot();
            await Assert.ThrowsAnyAsync<InvalidOperationException>(() => email.RespondHttp("cascade", "offer", "owner", token, T.AddMinutes(2), accept, default));
            Assert.Equal(damaged, await Snapshot());
            return;
        }
        var before = await Snapshot();
        await email.RespondHttp("cascade", "offer", "owner", token, T.AddMinutes(2), accept, default);
        Assert.Equal(before, await Snapshot());
        foreach (var bad in new[] { ("cascade", "offer", "owner", token, !accept, T.AddMinutes(2)), ("cascade", "offer", "other", token, accept, T.AddMinutes(2)), ("other", "offer", "owner", token, accept, T.AddMinutes(2)), ("cascade", "other", "owner", token, accept, T.AddMinutes(2)), ("cascade", "offer", "owner", "bad", accept, T.AddMinutes(2)), ("cascade", "offer", "owner", new string('A', 64), accept, T.AddMinutes(2)), ("cascade", "offer", "owner", token, accept, T.AddMinutes(5)) })
        {
            await Assert.ThrowsAsync<TurnoOfertaEmailInvalidException>(() => email.RespondHttp(bad.Item1, bad.Item2, bad.Item3, bad.Item4, bad.Item6, bad.Item5, default));
            Assert.Equal(before, await Snapshot());
        }
    }
}

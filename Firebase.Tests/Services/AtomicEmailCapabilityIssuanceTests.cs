using System.Security.Cryptography;
using System.Text;
using Firebase.Services;
using Firebase.Tests.Integration;
using Google.Api.Gax;
using Google.Cloud.Firestore;

namespace Firebase.Tests.Services;

public class AtomicEmailCapabilityIssuanceTests
{
    private static readonly DateTime Now = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static async Task<FirestoreDb> Setup()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-atomic-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("turno_ofertas").Document("offer").SetAsync(new { CascadeId = "cascade", ClienteId = "client", Estado = "Pendiente", CreatedUtc = Now, ExpiresUtc = Now.AddMinutes(10), AppointmentId = "appointment" });
        await db.Collection("turnos").Document("appointment").SetAsync(new { Id = "appointment", ClienteId = "client" });
        await db.Collection("turno_cascadas").Document("cascade").SetAsync(new { PendingId = "offer" });
        await db.Collection("turno_ofertas_pendientes").Document("appointment").SetAsync(new { OfferId = "offer", CascadeId = "cascade" });
        return db;
    }

    [LocalFirestoreFact]
    public async Task TransactionIssuesHashAndRollsBackTogetherWithOtherWrites()
    {
        var db = await Setup();
        var service = new TurnoOfertaEmailCapability(db);
        var token = TurnoOfertaEmailCapability.GenerateSecret();
        var marker = db.Collection("atomic_test").Document("marker");
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.RunTransactionAsync(async tx => {
            await service.IssueInTransaction(tx, "cascade", "offer", "client", Now, Now.AddMinutes(5), token);
            tx.Create(marker, new { Value = 1 });
            throw new InvalidOperationException("rollback");
#pragma warning disable CS0162
            return true;
#pragma warning restore CS0162
        }));
        Assert.False((await marker.GetSnapshotAsync()).Exists);
        Assert.False((await db.Collection("turno_ofertas_email_capabilities").Document("offer").GetSnapshotAsync()).Exists);
        await db.RunTransactionAsync(async tx => {
            await service.IssueInTransaction(tx, "cascade", "offer", "client", Now, Now.AddMinutes(5), token);
            tx.Create(marker, new { Value = 1 });
            return true;
        });
        var stored = await db.Collection("turno_ofertas_email_capabilities").Document("offer").GetSnapshotAsync();
        Assert.True((await marker.GetSnapshotAsync()).Exists);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))), stored.GetValue<string>("Hash"));
        Assert.DoesNotContain(token, stored.ToDictionary().Values.OfType<string>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.RunTransactionAsync(async tx => {
            await service.IssueInTransaction(tx, "cascade", "offer", "client", Now, Now.AddMinutes(5), token);
            return true;
        }));
    }

    [LocalFirestoreFact]
    public async Task ExistingIssueStillRejectsInvalidExpiryAndIssuesOnce()
    {
        var db = await Setup();
        var service = new TurnoOfertaEmailCapability(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Issue("cascade", "offer", "client", Now, Now.AddMinutes(11)));
        Assert.False((await db.Collection("turno_ofertas_email_capabilities").Document("offer").GetSnapshotAsync()).Exists);
        var token = await service.Issue("cascade", "offer", "client", Now, Now.AddMinutes(5));
        var snapshot = await db.Collection("turno_ofertas_email_capabilities").Document("offer").GetSnapshotAsync();
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))), snapshot.GetValue<string>("Hash"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Issue("cascade", "offer", "client", Now, Now.AddMinutes(5)));
        Assert.Equal(snapshot.UpdateTime, (await snapshot.Reference.GetSnapshotAsync()).UpdateTime);
    }
}

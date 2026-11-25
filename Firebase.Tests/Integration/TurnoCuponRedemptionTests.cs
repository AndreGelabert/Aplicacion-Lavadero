using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;

namespace Firebase.Tests.Integration;

public class TurnoCuponRedemptionTests
{
    [LocalFirestoreFact]
    public async Task PreparationValidatesPersistedTermsWithoutWriting()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-coupon-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var reference = db.Collection("turno_cupones").Document("coupon");
        var percentage = Math.BitIncrement(10d);
        await reference.SetAsync(new { Owner = "client", Percentage = percentage, ExpiresUtc = DateTime.UtcNow.AddDays(1), Consumed = false });
        var before = await reference.GetSnapshotAsync();
        var prepared = await db.RunTransactionAsync(tx => TurnoCuponRedemption.Preparar(tx, reference, "client", false));
        Assert.Equal(BitConverter.DoubleToInt64Bits(percentage), BitConverter.DoubleToInt64Bits(prepared));
        foreach (var (owner, stacking) in new[] { ("other", false), ("client", true) })
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.RunTransactionAsync(tx => TurnoCuponRedemption.Preparar(tx, reference, owner, stacking)));
        Assert.Equal(before.UpdateTime, (await reference.GetSnapshotAsync()).UpdateTime);
        await reference.UpdateAsync("Consumed", true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.RunTransactionAsync(tx => TurnoCuponRedemption.Preparar(tx, reference, "client", false)));
        await reference.UpdateAsync(new Dictionary<string, object> { ["Consumed"] = false, ["ExpiresUtc"] = DateTime.UtcNow.AddDays(-1) });
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.RunTransactionAsync(tx => TurnoCuponRedemption.Preparar(tx, reference, "client", false)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.RunTransactionAsync(tx => TurnoCuponRedemption.Preparar(tx, db.Collection("turno_cupones").Document("missing"), "client", false)));
    }
}

using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;

namespace Firebase.Tests.Integration;

public class TurnoAdelantoCandidateQueryTests
{
    private static readonly DateTime Vacancy = new(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [LocalFirestoreFact]
    public async Task ReadsOrderedCandidatesAndAnchorsWithoutWrites()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-candidates-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var collection = db.Collection("turnos");
        async Task Put(string id, DateTime start, string state = "Pendiente", bool opted = true)
        {
            await collection.Document(id).SetAsync(new { InicioUtc = start, Estado = state, AceptaAdelanto = opted });
            await Task.Delay(20);
        }
        await Put("older", Vacancy.AddHours(1));
        await Put("newer", Vacancy.AddHours(1));
        await Put("near", Vacancy.AddMinutes(30));
        await Put("boundary", Vacancy.AddDays(7));
        await Put("outside", Vacancy.AddDays(7).AddMinutes(1));
        await Put("same", Vacancy);
        await Put("out", Vacancy.AddMinutes(1), opted: false);
        await Put("cancelled", Vacancy.AddMinutes(1), "Cancelado");
        await Put("anchored", Vacancy.AddHours(2));
        await db.Collection("turno_adelanto_anclas").Document("anchored").SetAsync(new { AnchorUtc = Vacancy.AddDays(7).AddMinutes(1) });
        var before = await collection.GetSnapshotAsync();
        var anchorBefore = await db.Collection("turno_adelanto_anclas").GetSnapshotAsync();
        var older = before.Documents.Single(d => d.Id == "older");
        var newer = before.Documents.Single(d => d.Id == "newer");
        Assert.NotNull(older.CreateTime);
        Assert.NotNull(newer.CreateTime);
        Assert.True(older.CreateTime < newer.CreateTime);

        var result = await new TurnoAdelantoCandidateQuery(db).Find(Vacancy);

        Assert.Equal(new[] { "near", "older", "newer", "boundary" }, result.Select(x => x.Id));
        Assert.Equal(older.CreateTime!.Value.ToDateTime(), result.Single(x => x.Id == "older").BookingCreatedUtc);
        var after = await collection.GetSnapshotAsync();
        Assert.Equal(before.Documents.Select(d => (d.Id, d.UpdateTime)), after.Documents.Select(d => (d.Id, d.UpdateTime)));
        var anchorAfter = await db.Collection("turno_adelanto_anclas").GetSnapshotAsync();
        Assert.Equal(anchorBefore.Documents.Select(d => (d.Id, d.UpdateTime)), anchorAfter.Documents.Select(d => (d.Id, d.UpdateTime)));
        Assert.Empty((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);
    }
}

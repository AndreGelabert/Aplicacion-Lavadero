using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Grpc.Core;

namespace Firebase.Tests.Integration;

public class TurnoOutboxPersistenceTests
{
    private static FirestoreDb Setup() => new FirestoreDbBuilder { ProjectId = "demo-outbox-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
    private static TurnoOutboxPersistence.Request Request(string id = "event-1") => new(id, TurnoOutboxPersistence.Event.Booking, TurnoOutboxPersistence.Channel.WhatsApp, "offer-1");

    [LocalFirestoreFact]
    public async Task GenericBeginRejectsOfferWithoutChangingClaim()
    {
        var db = Setup(); var now = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await db.RunTransactionAsync(tx => { TurnoOutboxPersistence.Enqueue(db, tx, Request() with { EventType = TurnoOutboxPersistence.Event.Offer }); return Task.FromResult(0); });
        var claim = (await TurnoOutboxPersistence.ClaimBound(db, "event-1", "a", now, TimeSpan.FromMinutes(1)))!;
        var doc = db.Collection("turno_outbox").Document("event-1"); var before = await doc.GetSnapshotAsync();
        Assert.False(await TurnoOutboxPersistence.BeginSend(db, "event-1", "a", claim.Generation, now));
        Assert.False(await TurnoOutboxPersistence.BeginSendBound(db, "event-1", "a", claim, now));
        var after = await doc.GetSnapshotAsync();
        Assert.Equal(before.UpdateTime, after.UpdateTime);
        Assert.Equal("Claimed", after.GetValue<string>("State"));
        Assert.Equal(claim.Generation, after.GetValue<long>("Generation"));
    }

    [LocalFirestoreFact]
    public async Task LeaseFencesStaleWorkersAndUncertainSendIsNeverReclaimed()
    {
        var db = Setup(); var now = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await db.RunTransactionAsync(tx => { TurnoOutboxPersistence.Enqueue(db, tx, Request()); return Task.FromResult(0); });
        var first = await TurnoOutboxPersistence.Claim(db, "event-1", "same-token", now, TimeSpan.FromMinutes(1));
        Assert.Equal(1L, first);
        Assert.Null(await TurnoOutboxPersistence.Claim(db, "event-1", "other-token", now, TimeSpan.FromMinutes(1)));
        var second = await TurnoOutboxPersistence.Claim(db, "event-1", "same-token", now.AddMinutes(1), TimeSpan.FromMinutes(1));
        Assert.Equal(2L, second);
        Assert.False(await TurnoOutboxPersistence.BeginSend(db, "event-1", "same-token", first!.Value, now.AddMinutes(1)));
        Assert.True(await TurnoOutboxPersistence.BeginSend(db, "event-1", "same-token", second!.Value, now.AddMinutes(1)));
        Assert.Null(await TurnoOutboxPersistence.Claim(db, "event-1", "other-token", now.AddDays(1), TimeSpan.FromMinutes(1)));
        Assert.False(await TurnoOutboxPersistence.Complete(db, "event-1", "same-token", first.Value));
        Assert.True(await TurnoOutboxPersistence.Complete(db, "event-1", "same-token", second.Value));
        Assert.True(await TurnoOutboxPersistence.Complete(db, "event-1", "same-token", second.Value));
        Assert.Null(await TurnoOutboxPersistence.Claim(db, "event-1", "other-token", now.AddDays(2), TimeSpan.FromMinutes(1)));
    }

    [LocalFirestoreFact]
    public async Task BoundClaimRejectsChangedMetadataBeforeSend()
    {
        var db = Setup(); var now = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await db.RunTransactionAsync(tx => { TurnoOutboxPersistence.Enqueue(db, tx, Request()); return Task.FromResult(0); });
        var claim = (await TurnoOutboxPersistence.ClaimBound(db, "event-1", "a", now, TimeSpan.FromMinutes(1)))!;
        Assert.Equal(TurnoOutboxPersistence.Event.Booking, claim.EventType);
        Assert.Equal(TurnoOutboxPersistence.Channel.WhatsApp, claim.DeliveryChannel);
        Assert.Equal("offer-1", claim.ReferenceId);
        var doc = db.Collection("turno_outbox").Document("event-1");
        foreach (var change in new[] { new Dictionary<string, object> { ["Event"] = "Offer" }, new Dictionary<string, object> { ["Channel"] = "Email" }, new Dictionary<string, object> { ["ReferenceId"] = "other" } })
        {
            await doc.UpdateAsync(change); var before = await doc.GetSnapshotAsync();
            Assert.False(await TurnoOutboxPersistence.BeginSendBound(db, "event-1", "a", claim, now));
            Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
            await doc.UpdateAsync(new Dictionary<string, object> { ["Event"] = "Booking", ["Channel"] = "WhatsApp", ["ReferenceId"] = "offer-1" });
        }
        Assert.True(await TurnoOutboxPersistence.BeginSendBound(db, "event-1", "a", claim, now));
    }

    [LocalFirestoreFact]
    public async Task ReleaseIsFencedAndAllowsImmediateReclaimWithoutReusingGeneration()
    {
        var db = Setup(); var now = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await db.RunTransactionAsync(tx => { TurnoOutboxPersistence.Enqueue(db, tx, Request()); return Task.FromResult(0); });
        var claim = (await TurnoOutboxPersistence.ClaimBound(db, "event-1", "a", now, TimeSpan.FromMinutes(1)))!;
        var doc = db.Collection("turno_outbox").Document("event-1");
        foreach (var (token, binding, time) in new[] { ("b", claim, now), ("a", claim with { Generation = 2 }, now), ("a", claim with { ReferenceId = "other" }, now), ("a", claim with { EventType = TurnoOutboxPersistence.Event.Offer }, now), ("a", claim with { DeliveryChannel = TurnoOutboxPersistence.Channel.Email }, now), ("a", claim, now.AddMinutes(1)) })
        {
            var before = await doc.GetSnapshotAsync();
            Assert.False(await TurnoOutboxPersistence.ReleaseBound(db, "event-1", token, binding, time));
            Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        }
        Assert.True(await TurnoOutboxPersistence.ReleaseBound(db, "event-1", "a", claim, now));
        var next = (await TurnoOutboxPersistence.ClaimBound(db, "event-1", "a", now, TimeSpan.FromMinutes(1)))!;
        Assert.Equal(2, next.Generation);
        Assert.True(await TurnoOutboxPersistence.BeginSendBound(db, "event-1", "a", next, now));
        var indeterminate = await doc.GetSnapshotAsync();
        Assert.False(await TurnoOutboxPersistence.ReleaseBound(db, "event-1", "a", next, now));
        Assert.Equal(indeterminate.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        Assert.Null(await TurnoOutboxPersistence.ClaimBound(db, "event-1", "b", now.AddDays(1), TimeSpan.FromMinutes(1)));
    }

    [LocalFirestoreFact]
    public async Task ConcurrentClaimsHaveOneWinner()
    {
        var db = Setup(); var now = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await db.RunTransactionAsync(tx => { TurnoOutboxPersistence.Enqueue(db, tx, Request()); return Task.FromResult(0); });
        var claims = await Task.WhenAll(TurnoOutboxPersistence.Claim(db, "event-1", "a", now, TimeSpan.FromHours(1)), TurnoOutboxPersistence.Claim(db, "event-1", "b", now, TimeSpan.FromHours(1)));
        Assert.Single(claims.Where(x => x == 1)); Assert.Single(claims.Where(x => x == null));
    }

    [LocalFirestoreFact]
    public async Task InvalidGenerationStatesFailClosedWithoutWrites()
    {
        var db = Setup(); var now = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var doc = db.Collection("turno_outbox").Document("event-1");
        foreach (var fields in new[] {
            new Dictionary<string, object> { ["State"] = "Pending" },
            new Dictionary<string, object> { ["State"] = "Pending", ["Generation"] = "bad" },
            new Dictionary<string, object> { ["State"] = "Pending", ["Generation"] = -1L },
            new Dictionary<string, object> { ["State"] = "Claimed", ["Generation"] = long.MaxValue, ["ClaimToken"] = "a", ["LeaseUntil"] = Timestamp.FromDateTime(now) },
            new Dictionary<string, object> { ["State"] = "Claimed", ["Generation"] = 0L, ["ClaimToken"] = "a", ["LeaseUntil"] = Timestamp.FromDateTime(now) },
            new Dictionary<string, object> { ["State"] = "Claimed", ["Generation"] = 1L, ["ClaimToken"] = "a", ["LeaseUntil"] = "bad" },
            new Dictionary<string, object> { ["State"] = "Indeterminate", ["ClaimToken"] = "a" },
            new Dictionary<string, object> { ["State"] = "Completed", ["ClaimToken"] = "a", ["Generation"] = "bad" }
        })
        {
            fields["Event"] = "Offer"; fields["Channel"] = "WhatsApp"; fields["ReferenceId"] = "offer-1";
            await doc.SetAsync(fields); var before = await doc.GetSnapshotAsync();
            Assert.Null(await TurnoOutboxPersistence.Claim(db, "event-1", "a", now, TimeSpan.FromTicks(10)));
            Assert.False(await TurnoOutboxPersistence.BeginSend(db, "event-1", "a", 1, now));
            Assert.False(await TurnoOutboxPersistence.Complete(db, "event-1", "a", 1));
            Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        }
    }

    [LocalFirestoreFact]
    public async Task MaximumGenerationCanBeUsedButNotIncremented()
    {
        var db = Setup(); var now = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var doc = db.Collection("turno_outbox").Document("event-1");
        await doc.SetAsync(new Dictionary<string, object> { ["Event"] = "Booking", ["Channel"] = "WhatsApp", ["ReferenceId"] = "offer-1", ["State"] = "Claimed", ["Generation"] = long.MaxValue - 1, ["ClaimToken"] = "a", ["LeaseUntil"] = Timestamp.FromDateTime(now) });
        Assert.Equal(long.MaxValue, await TurnoOutboxPersistence.Claim(db, "event-1", "a", now, TimeSpan.FromHours(1)));
        var before = await doc.GetSnapshotAsync();
        Assert.Null(await TurnoOutboxPersistence.Claim(db, "event-1", "a", now.AddHours(1), TimeSpan.FromHours(1)));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        Assert.True(await TurnoOutboxPersistence.BeginSend(db, "event-1", "a", long.MaxValue, now));
        Assert.True(await TurnoOutboxPersistence.Complete(db, "event-1", "a", long.MaxValue));
    }

    [LocalFirestoreFact]
    public async Task MalformedPersistedMetadataNeverChangesAnyLifecycleState()
    {
        var db = Setup(); var now = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var doc = db.Collection("turno_outbox").Document("event-1");
        foreach (var state in new[] { "Pending", "Claimed", "Indeterminate", "Completed" })
        foreach (var key in new[] { "Event", "Channel", "ReferenceId" })
        foreach (var invalid in new object?[] { null, "invalid/value", 42L })
        {
            var fields = new Dictionary<string, object> { ["Event"] = "Offer", ["Channel"] = "WhatsApp", ["ReferenceId"] = "offer-1", ["State"] = state, ["Generation"] = state == "Pending" ? 0L : 1L };
            if (state != "Pending") { fields["ClaimToken"] = "a"; fields["LeaseUntil"] = Timestamp.FromDateTime(now); }
            if (invalid == null) fields.Remove(key); else fields[key] = invalid;
            await doc.SetAsync(fields); var before = await doc.GetSnapshotAsync();
            Assert.Null(await TurnoOutboxPersistence.Claim(db, "event-1", "a", now, TimeSpan.FromMinutes(1)));
            Assert.False(await TurnoOutboxPersistence.BeginSend(db, "event-1", "a", 1, now.AddTicks(-10)));
            Assert.False(await TurnoOutboxPersistence.Complete(db, "event-1", "a", 1));
            Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        }
    }

    [LocalFirestoreFact]
    public async Task ClockAndLeaseValidationRejectsWithoutWrites()
    {
        var db = Setup(); var now = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await db.RunTransactionAsync(tx => { TurnoOutboxPersistence.Enqueue(db, tx, Request()); return Task.FromResult(0); });
        var doc = db.Collection("turno_outbox").Document("event-1"); var before = await doc.GetSnapshotAsync();
        foreach (var (clock, lease) in new[] { (DateTime.SpecifyKind(now, DateTimeKind.Unspecified), TimeSpan.FromMinutes(1)), (now.AddTicks(1), TimeSpan.FromMinutes(1)), (now, TimeSpan.Zero), (now, TimeSpan.FromTicks(1)), (now, TimeSpan.FromHours(1).Add(TimeSpan.FromTicks(10))), (DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc), TimeSpan.FromHours(1)) })
            await Assert.ThrowsAsync<ArgumentException>(() => TurnoOutboxPersistence.Claim(db, "event-1", "a", clock, lease));
        await Assert.ThrowsAsync<ArgumentException>(() => TurnoOutboxPersistence.BeginSend(db, "event-1", "a", 1, now.AddTicks(1)));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(1L, await TurnoOutboxPersistence.Claim(db, "event-1", "a", now, TimeSpan.FromTicks(10)));
        Assert.False(await TurnoOutboxPersistence.BeginSend(db, "event-1", "a", 1, now.AddTicks(10)));
    }

    [LocalFirestoreFact]
    public async Task CommitAndAbortAreAtomic()
    {
        var db = Setup(); var domain = db.Collection("synthetic_domain").Document("one");
        await db.RunTransactionAsync(tx => { tx.Create(domain, new { Value = 1 }); TurnoOutboxPersistence.Enqueue(db, tx, Request()); return Task.FromResult(0); });
        var entry = await db.Collection("turno_outbox").Document("event-1").GetSnapshotAsync();
        Assert.True((await domain.GetSnapshotAsync()).Exists);
        Assert.Equal("Booking", entry.GetValue<string>("Event"));
        Assert.Equal("WhatsApp", entry.GetValue<string>("Channel"));
        Assert.Equal("offer-1", entry.GetValue<string>("ReferenceId"));
        Assert.Equal("Pending", entry.GetValue<string>("State"));
        Assert.Equal(0L, entry.GetValue<long>("Generation"));
        Assert.Equal(5, entry.ToDictionary().Count);
        var aborted = db.Collection("synthetic_domain").Document("aborted");
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.RunTransactionAsync<int>(tx => { tx.Create(aborted, new { Value = 1 }); TurnoOutboxPersistence.Enqueue(db, tx, Request("event-2")); throw new InvalidOperationException("abort"); }));
        Assert.False((await aborted.GetSnapshotAsync()).Exists);
        Assert.False((await db.Collection("turno_outbox").Document("event-2").GetSnapshotAsync()).Exists);
    }

    [LocalFirestoreFact]
    public async Task DuplicateIdFailsEvenForSameRequestAndDoesNotCommitDomain()
    {
        var db = Setup(); var request = Request();
        await db.RunTransactionAsync(tx => { TurnoOutboxPersistence.Enqueue(db, tx, request); return Task.FromResult(0); });
        var entry = db.Collection("turno_outbox").Document(request.Id); var before = await entry.GetSnapshotAsync();
        var domain = db.Collection("synthetic_domain").Document("duplicate");
        var ex = await Assert.ThrowsAsync<RpcException>(() => db.RunTransactionAsync(tx => { tx.Create(domain, new { Value = 1 }); TurnoOutboxPersistence.Enqueue(db, tx, request); return Task.FromResult(0); }));
        Assert.Equal(StatusCode.AlreadyExists, ex.StatusCode);
        Assert.False((await domain.GetSnapshotAsync()).Exists);
        Assert.Equal(before.UpdateTime, (await entry.GetSnapshotAsync()).UpdateTime);
        Assert.Single((await db.Collection("turno_outbox").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task MalformedMetadataRejectsBeforeAnyTransactionWritesCommit()
    {
        var db = Setup(); var good = Request();
        var invalid = new[] { good with { Id = "" }, good with { Id = "a/b" }, good with { Id = new string('a', 129) }, good with { Id = "__reserved__" }, good with { ReferenceId = "" }, good with { ReferenceId = "x@y.com" }, good with { ReferenceId = new string('a', 129) }, good with { EventType = (TurnoOutboxPersistence.Event)99 }, good with { DeliveryChannel = (TurnoOutboxPersistence.Channel)99 } };
        foreach (var request in invalid)
        {
            var domain = db.Collection("synthetic_domain").Document("invalid");
            await Assert.ThrowsAsync<ArgumentException>(() => db.RunTransactionAsync(tx => { TurnoOutboxPersistence.Enqueue(db, tx, request); tx.Create(domain, new { Value = 1 }); return Task.FromResult(0); }));
            Assert.False((await domain.GetSnapshotAsync()).Exists);
            Assert.Empty((await db.Collection("turno_outbox").GetSnapshotAsync()).Documents);
        }
    }
}

using Firebase.Models.WhatsApp;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Firebase.Tests.Integration;

public class WhatsAppSessionTransitionIntegrationTests
{
    [LocalFirestoreFact]
    public async Task IncarnationCasWritesDeleteAndLegacyFailClosed()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-session-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var service = new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance);
        const string phone = "5491112345678";
        var initial = await service.GetOrCreateSession(phone);
        Assert.True(await service.TryUpdateState(phone, initial.IncarnationId!, 0, "next"));
        Assert.True(await service.TrySaveTemporaryData(phone, initial.IncarnationId!, 1, "key", "value"));
        Assert.True(await service.TryAssociateClient(phone, initial.IncarnationId!, 2, "client"));
        var current = await service.GetOrCreateSession(phone);
        Assert.Equal(3, current.Revision);
        Assert.Equal("value", current.TemporaryData["key"]);
        Assert.Equal("client", current.ClienteId);
        Assert.Equal(WhatsAppFlowStates.MENU_CLIENTE_AUTENTICADO, current.CurrentState);
        var doc = db.Collection("whatsapp_sessions").Document(phone);
        var before = await doc.GetSnapshotAsync();
        Assert.False(await service.TryUpdateState(phone, initial.IncarnationId!, 0, "stale"));
        Assert.False(await service.TryDeleteSession(phone, initial.IncarnationId!, 0));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        Assert.True(await service.TryDeleteSession(phone, initial.IncarnationId!, 3));
        var replacement = await service.GetOrCreateSession(phone);
        Assert.NotEqual(initial.IncarnationId, replacement.IncarnationId);
        before = await doc.GetSnapshotAsync();
        Assert.False(await service.TryUpdateState(phone, initial.IncarnationId!, 0, "stale"));
        Assert.False(await service.TrySaveTemporaryData(phone, initial.IncarnationId!, 0, "key", "stale"));
        Assert.False(await service.TryAssociateClient(phone, initial.IncarnationId!, 0, "stale"));
        Assert.False(await service.TryDeleteSession(phone, initial.IncarnationId!, 0));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        await doc.UpdateAsync("IncarnationId", FieldValue.Delete);
        before = await doc.GetSnapshotAsync();
        Assert.False(await service.TryUpdateState(phone, replacement.IncarnationId!, 0, "stale"));
        Assert.False(await service.TrySaveTemporaryData(phone, replacement.IncarnationId!, 0, "key", "stale"));
        Assert.False(await service.TryAssociateClient(phone, replacement.IncarnationId!, 0, "stale"));
        Assert.False(await service.TryDeleteSession(phone, replacement.IncarnationId!, 0));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
    }

    [LocalFirestoreFact]
    public async Task TransitionRejectsRecreatedAndLegacySessionsWithoutWrites()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-session-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var service = new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance);
        const string phone = "5491112345678";
        var initial = await service.GetOrCreateSession(phone);
        await service.ClearSession(phone);
        var replacement = await service.GetOrCreateSession(phone);
        Assert.NotEqual(initial.IncarnationId, replacement.IncarnationId);
        var doc = db.Collection("whatsapp_sessions").Document(phone);
        var before = await doc.GetSnapshotAsync();
        Assert.False(await service.TryTransition(phone, initial.IncarnationId, 0, "INICIO", null, "done", null));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        Assert.True(await service.TryTransition(phone, replacement.IncarnationId, 0, "INICIO", null, "done", null));
        await doc.UpdateAsync("IncarnationId", FieldValue.Delete);
        before = await doc.GetSnapshotAsync();
        Assert.False(await service.TryTransition(phone, replacement.IncarnationId, 1, "done", null, "next", null));
        Assert.False(await service.TryTransition(phone, null, 1, "done", null, "next", null));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
    }

    [LocalFirestoreFact]
    public async Task ConcurrentChoicesAndReplayAreGuardedWithoutWrites()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-session-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var service = new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance);
        const string phone = "5491112345678";
        var incarnation = (await service.GetOrCreateSession(phone)).IncarnationId;
        Assert.True(await service.TryTransition(phone, incarnation, 0, WhatsAppFlowStates.INICIO, null, "confirm", "opaque-choice"));
        var doc = db.Collection("whatsapp_sessions").Document(phone);
        var before = await doc.GetSnapshotAsync();
        Assert.False(await service.TryTransition(phone, incarnation, 1, "confirm", "wrong-choice", "done", null));
        Assert.False(await service.TryTransition(phone, incarnation, 0, WhatsAppFlowStates.INICIO, null, "confirm", "opaque-choice"));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        var results = await Task.WhenAll(
            service.TryTransition(phone, incarnation, 1, "confirm", "opaque-choice", "accepted", null),
            service.TryTransition(phone, incarnation, 1, "confirm", "opaque-choice", "rejected", null));
        Assert.Single(results.Where(x => x));
        var after = await doc.GetSnapshotAsync();
        var session = after.ConvertTo<WhatsAppSession>();
        Assert.Equal(2, session.Revision);
        Assert.Contains(session.CurrentState, new[] { "accepted", "rejected" });
        Assert.Null(session.SelectionId);
        Assert.False(await service.TryTransition(phone, incarnation, 1, "confirm", "opaque-choice", "accepted", null));
        Assert.Equal(after.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
    }

    [LocalFirestoreFact]
    public async Task InvalidInputsDoNotWrite()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-session-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var service = new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance);
        const string phone = "5491112345678";
        var incarnation = (await service.GetOrCreateSession(phone)).IncarnationId;
        var doc = db.Collection("whatsapp_sessions").Document(phone);
        var before = await doc.GetSnapshotAsync();
        foreach (var invalid in new[] { "", "123", "+5491112345678", "5491112345678/", "5491112345678 ", "１２３４５６７８９０" })
            await Assert.ThrowsAsync<ArgumentException>(() => service.TryTransition(invalid, incarnation, 0, "INICIO", null, "done", null));
        foreach (var invalid in new[] { "", "bad state", "bad\nstate", new string('x', 513) })
        {
            await Assert.ThrowsAsync<ArgumentException>(() => service.TryTransition(phone, incarnation, 0, invalid, null, "done", null));
            await Assert.ThrowsAsync<ArgumentException>(() => service.TryTransition(phone, incarnation, 0, "INICIO", null, invalid, null));
            await Assert.ThrowsAsync<ArgumentException>(() => service.TryTransition(phone, incarnation, 0, "INICIO", invalid, "done", null));
            await Assert.ThrowsAsync<ArgumentException>(() => service.TryTransition(phone, incarnation, 0, "INICIO", null, "done", invalid));
        }
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        Assert.Single((await db.Collection("whatsapp_sessions").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task CorruptDocumentsFailClosedAndLegacyDocumentTransitions()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-session-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var service = new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance);
        const string phone = "5491112345678";
        var doc = db.Collection("whatsapp_sessions").Document(phone);
        const string incarnation = "test-incarnation";
        var original = new Dictionary<string, object> {
            ["IncarnationId"] = incarnation, ["Id"] = phone, ["CurrentState"] = "INICIO", ["TemporaryData"] = new Dictionary<string, string>(),
            ["CreatedAt"] = Timestamp.FromDateTime(DateTime.UtcNow), ["LastInteraction"] = Timestamp.FromDateTime(DateTime.UtcNow)
        };
        foreach (var (field, value) in new (string, object)[] {
            ("Id", "other"), ("Id", 1L), ("CurrentState", "bad state"), ("CurrentState", 1L),
            ("Revision", -1L), ("Revision", "0"), ("SelectionId", "bad selection"), ("SelectionId", 1L),
            ("TemporaryData", "bad"), ("CreatedAt", "bad"), ("LastInteraction", "bad"), ("ClienteId", 1L)
        })
        {
            var corrupt = new Dictionary<string, object>(original) { [field] = value };
            await doc.SetAsync(corrupt);
            var before = await doc.GetSnapshotAsync();
            Assert.False(await service.TryTransition(phone, incarnation, 0, "INICIO", null, "done", null));
            Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        }
        foreach (var field in new[] { "Id", "CurrentState", "TemporaryData", "CreatedAt", "LastInteraction" })
        {
            var corrupt = new Dictionary<string, object>(original); corrupt.Remove(field);
            await doc.SetAsync(corrupt);
            var before = await doc.GetSnapshotAsync();
            Assert.False(await service.TryTransition(phone, incarnation, 0, "INICIO", null, "done", null));
            Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        }
        await doc.SetAsync(original);
        Assert.True(await service.TryTransition(phone, incarnation, 0, "INICIO", null, "done", "opaque-choice"));
        var after = await doc.GetSnapshotAsync();
        Assert.Equal(1L, after.GetValue<long>("Revision"));
        Assert.Equal("opaque-choice", after.GetValue<string>("SelectionId"));
    }

    [LocalFirestoreFact]
    public async Task TemporaryDataWritesSerializeWithTransitionAndEachOther()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-session-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var service = new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance);
        const string phone = "5491112345678";
        var incarnation = (await service.GetOrCreateSession(phone)).IncarnationId;
        var transition = service.TryTransition(phone, incarnation, 0, "INICIO", null, "confirm", "choice");
        var save = service.SaveTemporaryData(phone, "first.key", "one");
        await Task.WhenAll(transition, save);
        var session = await service.GetOrCreateSession(phone);
        Assert.Equal("one", session.TemporaryData["first.key"]);
        Assert.Equal(transition.Result ? 2 : 1, session.Revision);
        Assert.Equal(transition.Result ? "confirm" : "INICIO", session.CurrentState);
        Assert.Equal(transition.Result ? "choice" : null, session.SelectionId);
        Assert.False(await service.TryTransition(phone, incarnation, 0, "INICIO", null, "confirm", "choice"));
        await Task.WhenAll(service.SaveTemporaryData(phone, "second", "two"), service.SaveTemporaryData(phone, "third", "three"));
        session = await service.GetOrCreateSession(phone);
        Assert.Equal("one", session.TemporaryData["first.key"]);
        Assert.Equal("two", session.TemporaryData["second"]);
        Assert.Equal("three", session.TemporaryData["third"]);
        Assert.Equal(transition.Result ? 4 : 3, session.Revision);
    }

    [LocalFirestoreFact]
    public async Task TemporaryDataCreatesAndUpdatesLegacySessions()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-session-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var service = new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance);
        const string phone = "5491112345678";
        await service.SaveTemporaryData(phone, "key", "initial");
        var session = await service.GetOrCreateSession(phone);
        Assert.Equal("INICIO", session.CurrentState);
        Assert.Equal("initial", session.TemporaryData["key"]);
        Assert.Equal(1, session.Revision);
        var doc = db.Collection("whatsapp_sessions").Document(phone);
        await doc.UpdateAsync(new Dictionary<string, object> { ["Revision"] = FieldValue.Delete });
        await service.SaveTemporaryData(phone, "key", "updated");
        session = await service.GetOrCreateSession(phone);
        Assert.Equal("updated", session.TemporaryData["key"]);
        Assert.Equal(1, session.Revision);
    }

    [LocalFirestoreFact]
    public async Task MissingLookupDoesNotOverwriteConcurrentSave()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-session-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var service = new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance);
        const string phone = "5491112345678";
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        service.AfterMissingSessionRead = async () => { reached.TrySetResult(); await resume.Task; };
        var lookup = service.GetOrCreateSession(phone);
        try
        {
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await service.SaveTemporaryData(phone, "key", "value");
        }
        finally { resume.TrySetResult(); }
        var result = await lookup;
        Assert.Equal("value", result.TemporaryData["key"]);
        Assert.Equal(1, result.Revision);
        var persisted = await service.GetOrCreateSession(phone);
        Assert.Equal("value", persisted.TemporaryData["key"]);
        Assert.Equal(1, persisted.Revision);
    }

    [LocalFirestoreFact]
    public async Task SaveThenStaleTransitionDoesNotWrite()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-session-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var service = new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance);
        const string phone = "5491112345678";
        var initial = await service.GetOrCreateSession(phone);
        Assert.Equal("INICIO", initial.CurrentState);
        Assert.Equal(0, initial.Revision);
        await service.SaveTemporaryData(phone, "key", "value");
        var doc = db.Collection("whatsapp_sessions").Document(phone);
        var before = await doc.GetSnapshotAsync();
        Assert.False(await service.TryTransition(phone, initial.IncarnationId, initial.Revision, initial.CurrentState, null, "confirm", "choice"));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        var current = await service.GetOrCreateSession(phone);
        Assert.Equal(1, current.Revision);
        Assert.Equal("value", current.TemporaryData["key"]);
        Assert.Equal("INICIO", current.CurrentState);
    }

    [LocalFirestoreFact]
    public async Task SaveRejectsCorruptRevisionWithoutWriting()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-session-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var service = new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance);
        const string phone = "5491112345678";
        await service.GetOrCreateSession(phone);
        var doc = db.Collection("whatsapp_sessions").Document(phone);
        foreach (var revision in new object[] { -1L, long.MaxValue, "invalid" })
        {
            await doc.UpdateAsync("Revision", revision);
            var before = await doc.GetSnapshotAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveTemporaryData(phone, "key", "value"));
            Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        }
    }

    [LocalFirestoreFact]
    public async Task LegacyWritersInvalidateStaleRevisionAndPreserveFields()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-session-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var service = new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance);
        const string phone = "5491112345678";
        var incarnation = (await service.GetOrCreateSession(phone)).IncarnationId;
        await service.UpdateSessionState(phone, "INICIO");
        var doc = db.Collection("whatsapp_sessions").Document(phone);
        var before = await doc.GetSnapshotAsync();
        Assert.False(await service.TryTransition(phone, incarnation, 0, "INICIO", null, "done", null));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        await service.AssociateClienteToSession(phone, "client");
        var session = await service.GetOrCreateSession(phone);
        Assert.Equal(2, session.Revision);
        Assert.Equal("client", session.ClienteId);
        Assert.Equal(WhatsAppFlowStates.MENU_CLIENTE_AUTENTICADO, session.CurrentState);
        before = await doc.GetSnapshotAsync();
        Assert.False(await service.TryTransition(phone, incarnation, 1, WhatsAppFlowStates.MENU_CLIENTE_AUTENTICADO, null, "done", null));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
    }

    [LocalFirestoreFact]
    public async Task LegacyWritersAndTransitionHaveSerializableRevision()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-session-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var service = new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance);
        foreach (var associate in new[] { false, true })
        {
            var phone = associate ? "5491112345678" : "5491112345679";
            var incarnation = (await service.GetOrCreateSession(phone)).IncarnationId;
            var transition = service.TryTransition(phone, incarnation, 0, "INICIO", null, "chosen", "choice");
            var writer = associate ? service.AssociateClienteToSession(phone, "client") : service.UpdateSessionState(phone, "legacy");
            await Task.WhenAll(transition, writer);
            var session = await service.GetOrCreateSession(phone);
            Assert.Equal(transition.Result ? 2 : 1, session.Revision);
            Assert.Equal(associate ? WhatsAppFlowStates.MENU_CLIENTE_AUTENTICADO : "legacy", session.CurrentState);
            Assert.Equal(associate ? "client" : null, session.ClienteId);
            Assert.Equal(transition.Result ? "choice" : null, session.SelectionId);
        }
    }

    [LocalFirestoreFact]
    public async Task CleanupUsesStrictThirtyMinuteBoundary()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-session-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var now = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var service = new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance) { CleanupUtcNow = () => now };
        var collection = db.Collection("whatsapp_sessions");
        var boundary = now.AddMinutes(-30);
        foreach (var (id, activity) in new[] { ("before", boundary.AddMilliseconds(1)), ("exact", boundary), ("after", boundary.AddMilliseconds(-1)) })
        {
            await collection.Document(id).SetAsync(new Dictionary<string, object> {
                ["Id"] = id, ["CurrentState"] = "INICIO", ["TemporaryData"] = new Dictionary<string, string>(),
                ["CreatedAt"] = Timestamp.FromDateTime(now.AddHours(-1)), ["LastInteraction"] = Timestamp.FromDateTime(activity)
            });
        }
        var before = await collection.Document("before").GetSnapshotAsync();
        var exact = await collection.Document("exact").GetSnapshotAsync();
        await service.CleanupInactiveSessions();
        var beforeAfter = await collection.Document("before").GetSnapshotAsync();
        var exactAfter = await collection.Document("exact").GetSnapshotAsync();
        Assert.True(beforeAfter.Exists);
        Assert.True(exactAfter.Exists);
        Assert.Equal(before.UpdateTime, beforeAfter.UpdateTime);
        Assert.Equal(exact.UpdateTime, exactAfter.UpdateTime);
        Assert.False((await collection.Document("after").GetSnapshotAsync()).Exists);
    }

    [LocalFirestoreFact]
    public async Task CleanupRechecksActivityAndMissingCandidatesAndDeletesExpired()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-session-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var service = new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance);
        const string active = "5491112345678", expired = "5491112345679", missing = "5491112345680", recent = "5491112345681";
        var collection = db.Collection("whatsapp_sessions");
        foreach (var phone in new[] { active, expired, missing, recent })
            await service.GetOrCreateSession(phone);
        foreach (var phone in new[] { active, expired, missing })
            await collection.Document(phone).UpdateAsync("LastInteraction", DateTime.UtcNow.AddMinutes(-31));
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        service.AfterCleanupSnapshot = async () => { reached.TrySetResult(); await resume.Task; };
        var cleanup = service.CleanupInactiveSessions();
        DocumentSnapshot before;
        try
        {
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await service.SaveTemporaryData(active, "activity", "preserved");
            before = await collection.Document(active).GetSnapshotAsync();
            await service.ClearSession(missing);
        }
        finally { resume.TrySetResult(); }
        await cleanup;
        var after = await collection.Document(active).GetSnapshotAsync();
        Assert.True(after.Exists);
        Assert.Equal(before.UpdateTime, after.UpdateTime);
        Assert.Equal("preserved", after.ConvertTo<WhatsAppSession>().TemporaryData["activity"]);
        Assert.False((await collection.Document(expired).GetSnapshotAsync()).Exists);
        Assert.False((await collection.Document(missing).GetSnapshotAsync()).Exists);
        Assert.True((await collection.Document(recent).GetSnapshotAsync()).Exists);
    }

    [LocalFirestoreFact]
    public async Task CleanupDoesNotDeleteRecreatedExpiredLegacySession()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-session-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var service = new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance);
        const string phone = "5491112345678";
        var doc = db.Collection("whatsapp_sessions").Document(phone);
        var legacy = new Dictionary<string, object> {
            ["Id"] = phone, ["CurrentState"] = "INICIO", ["TemporaryData"] = new Dictionary<string, string>(),
            ["CreatedAt"] = Timestamp.FromDateTime(DateTime.UtcNow.AddHours(-2)),
            ["LastInteraction"] = Timestamp.FromDateTime(DateTime.UtcNow.AddHours(-1))
        };
        await doc.SetAsync(legacy);
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        service.AfterCleanupSnapshot = async () => { reached.TrySetResult(); await resume.Task; };
        var cleanup = service.CleanupInactiveSessions();
        DocumentSnapshot replacement;
        try
        {
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await service.ClearSession(phone);
            await doc.CreateAsync(legacy);
            replacement = await doc.GetSnapshotAsync();
        }
        finally { resume.TrySetResult(); }
        await cleanup;
        var after = await doc.GetSnapshotAsync();
        Assert.True(after.Exists);
        Assert.Equal(replacement.UpdateTime, after.UpdateTime);
        Assert.Equal("INICIO", after.ConvertTo<WhatsAppSession>().CurrentState);
    }

    [LocalFirestoreFact]
    public async Task CleanupSkipsMalformedCandidatesAndContinues()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-session-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var service = new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance);
        var collection = db.Collection("whatsapp_sessions");
        var legacy = new Dictionary<string, object> {
            ["Id"] = "legacy", ["CurrentState"] = "INICIO", ["TemporaryData"] = new Dictionary<string, string>(),
            ["CreatedAt"] = Timestamp.FromDateTime(DateTime.UtcNow.AddHours(-2)),
            ["LastInteraction"] = Timestamp.FromDateTime(DateTime.UtcNow.AddHours(-1))
        };
        var badTime = new Dictionary<string, object>(legacy) { ["LastInteraction"] = "bad" };
        var missingState = new Dictionary<string, object>(legacy); missingState.Remove("CurrentState");
        await collection.Document("a").SetAsync(badTime);
        await collection.Document("b").SetAsync(missingState);
        await collection.Document("z").SetAsync(legacy);
        var a = await collection.Document("a").GetSnapshotAsync();
        var b = await collection.Document("b").GetSnapshotAsync();
        await service.CleanupInactiveSessions();
        Assert.Equal(a.UpdateTime, (await collection.Document("a").GetSnapshotAsync()).UpdateTime);
        Assert.Equal(b.UpdateTime, (await collection.Document("b").GetSnapshotAsync()).UpdateTime);
        Assert.False((await collection.Document("z").GetSnapshotAsync()).Exists);
    }

    [LocalFirestoreFact]
    public async Task StateAndAssociationMissingDocumentsFailWithoutCreating()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-session-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var service = new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance);
        const string phone = "5491112345678";
        var doc = db.Collection("whatsapp_sessions").Document(phone);
        await Assert.ThrowsAnyAsync<Exception>(() => service.UpdateSessionState(phone, "state"));
        Assert.False((await doc.GetSnapshotAsync()).Exists);
        await Assert.ThrowsAnyAsync<Exception>(() => service.AssociateClienteToSession(phone, "client"));
        Assert.False((await doc.GetSnapshotAsync()).Exists);
    }

    [LocalFirestoreFact]
    public async Task StateAndAssociationPreserveOtherSessionFields()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-session-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var service = new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance);
        const string phone = "5491112345678";
        var incarnation = (await service.GetOrCreateSession(phone)).IncarnationId;
        await service.SaveTemporaryData(phone, "key", "value");
        Assert.True(await service.TryTransition(phone, incarnation, 1, "INICIO", null, "chosen", "selection"));
        var initial = await service.GetOrCreateSession(phone);
        await service.UpdateSessionState(phone, "next");
        var state = await service.GetOrCreateSession(phone);
        Assert.Equal(initial.Id, state.Id);
        Assert.Equal(initial.CreatedAt, state.CreatedAt);
        Assert.Equal(initial.SelectionId, state.SelectionId);
        Assert.Equal("value", state.TemporaryData["key"]);
        Assert.Equal(initial.ClienteId, state.ClienteId);
        Assert.Equal("next", state.CurrentState);
        Assert.Equal(initial.Revision + 1, state.Revision);
        await service.AssociateClienteToSession(phone, "client");
        var associated = await service.GetOrCreateSession(phone);
        Assert.Equal(initial.Id, associated.Id);
        Assert.Equal(initial.CreatedAt, associated.CreatedAt);
        Assert.Equal(initial.SelectionId, associated.SelectionId);
        Assert.Equal("value", associated.TemporaryData["key"]);
        Assert.Equal("client", associated.ClienteId);
        Assert.Equal(WhatsAppFlowStates.MENU_CLIENTE_AUTENTICADO, associated.CurrentState);
        Assert.Equal(state.Revision + 1, associated.Revision);
    }

    [LocalFirestoreFact]
    public async Task ConcurrentStateAndAssociationSerialize()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-session-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var service = new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance);
        const string phone = "5491112345678";
        await service.GetOrCreateSession(phone);
        await service.SaveTemporaryData(phone, "key", "value");
        var initial = await service.GetOrCreateSession(phone);
        await Task.WhenAll(service.UpdateSessionState(phone, "next"), service.AssociateClienteToSession(phone, "client"));
        var result = await service.GetOrCreateSession(phone);
        Assert.Equal(initial.Revision + 2, result.Revision);
        Assert.Equal("client", result.ClienteId);
        Assert.Contains(result.CurrentState, new[] { "next", WhatsAppFlowStates.MENU_CLIENTE_AUTENTICADO });
        Assert.Equal(initial.Id, result.Id);
        Assert.Equal(initial.CreatedAt, result.CreatedAt);
        Assert.Equal(initial.SelectionId, result.SelectionId);
        Assert.Equal("value", result.TemporaryData["key"]);
    }

    [LocalFirestoreFact]
    public async Task ExistingStateWriterStillWorks()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-session-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var service = new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance);
        await service.GetOrCreateSession("123");
        await service.UpdateSessionState("123", "legacy");
        Assert.Equal("legacy", (await service.GetOrCreateSession("123")).CurrentState);
    }
}

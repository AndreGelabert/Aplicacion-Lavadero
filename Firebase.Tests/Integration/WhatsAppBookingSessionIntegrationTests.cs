using Firebase.Models.WhatsApp;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Firebase.Tests.Integration;

public class WhatsAppBookingSessionIntegrationTests
{
    [LocalFirestoreFact]
    public async Task BookingTransitionsAreSequentialAndRejectStaleOrWrongOwnerWithoutWrites()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-book-session-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var service = new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance);
        const string phone = "5491112345678";
        await service.GetOrCreateSession(phone);
        await service.AssociateClienteToSession(phone, "client");
        var initial = await service.GetOrCreateSession(phone);
        var doc = db.Collection("whatsapp_sessions").Document(phone);
        Assert.True(await service.TryBookingTransition(phone, initial, "choose", "cap1", "draft1", 10, new() { ["opaque1"] = "vehicle" }));
        var current = await service.GetOrCreateSession(phone);
        Assert.Equal(initial.Revision + 1, current.Revision);
        Assert.Equal("choose", current.CurrentState);
        Assert.Equal("cap1", current.SelectionId);
        Assert.Equal("draft1", current.BookingRequest);
        Assert.Equal(10, current.BookingTolerance);
        Assert.Equal("vehicle", current.BookingChoices!["opaque1"]);
        var before = await doc.GetSnapshotAsync();
        Assert.False(await service.TryBookingTransition(phone, initial, "bad", null, null, null, null));
        var wrong = await service.GetOrCreateSession(phone); wrong.ClienteId = "other";
        Assert.False(await service.TryBookingTransition(phone, wrong, "bad", null, null, null, null));
        var wrongSelection = await service.GetOrCreateSession(phone); wrongSelection.SelectionId = "wrong";
        Assert.False(await service.TryBookingTransition(phone, wrongSelection, "bad", null, null, null, null));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        Assert.True(await service.TryBookingTransition(phone, current, "confirm", "cap2", "draft2", 0, new() { ["opaque2"] = "service" }));
        var result = await service.GetOrCreateSession(phone);
        Assert.Equal(current.Revision + 1, result.Revision);
        Assert.Equal("cap2", result.SelectionId);
        Assert.Equal("draft2", result.BookingRequest);
        Assert.Equal(0, result.BookingTolerance);
        Assert.Equal("service", result.BookingChoices!["opaque2"]);
        await service.ClearSession(phone);
        await service.GetOrCreateSession(phone);
        await service.AssociateClienteToSession(phone, "client");
        before = await doc.GetSnapshotAsync();
        Assert.False(await service.TryBookingTransition(phone, initial, "bad", null, null, null, null));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
    }

    [LocalFirestoreFact]
    public async Task RejectsOversizedInputsAndUnversionedBookingChangesWithoutWriting()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-book-session-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var service = new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance);
        const string phone = "5491112345678";
        await service.GetOrCreateSession(phone);
        await service.AssociateClienteToSession(phone, "client");
        var expected = await service.GetOrCreateSession(phone);
        var doc = db.Collection("whatsapp_sessions").Document(phone);
        var before = await doc.GetSnapshotAsync();
        Assert.False(await service.TryBookingTransition(phone, expected, "choose", "cap", "draft", 525601, null));
        Assert.False(await service.TryBookingTransition(phone, expected, "choose", "cap", new string('x', 1048488), 10, null));
        Assert.False(await service.TryBookingTransition(phone, expected, "choose", "cap", "draft", 10, new() { ["opaque"] = new string('x', 1048488) }));
        Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
        foreach (var (field, value) in new (string, object)[] { ("BookingRequest", "other"), ("BookingTolerance", 12), ("BookingChoices", new Dictionary<string, string> { ["x"] = "other" }) })
        {
            await doc.UpdateAsync(field, value);
            before = await doc.GetSnapshotAsync();
            Assert.False(await service.TryBookingTransition(phone, expected, "choose", "cap", "draft", 10, null));
            Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
            await doc.UpdateAsync(field, FieldValue.Delete);
        }
    }

    [LocalFirestoreFact]
    public async Task ExistingStateUpdateStillPreservesSessionData()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-book-session-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var service = new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance);
        const string phone = "5491112345678";
        await service.GetOrCreateSession(phone);
        await service.SaveTemporaryData(phone, "key", "value");
        await service.UpdateSessionState(phone, "next");
        var current = await service.GetOrCreateSession(phone);
        Assert.Equal("next", current.CurrentState);
        Assert.Equal(2, current.Revision);
        Assert.Equal("value", current.TemporaryData["key"]);
    }

    [LocalFirestoreFact]
    public async Task LegacyAndCorruptSessionsFailClosed()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-book-session-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var service = new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance);
        const string phone = "5491112345678";
        await service.GetOrCreateSession(phone);
        await service.AssociateClienteToSession(phone, "client");
        var expected = await service.GetOrCreateSession(phone);
        var doc = db.Collection("whatsapp_sessions").Document(phone);
        foreach (var (field, value) in new (string, object)[] { ("Revision", FieldValue.Delete), ("IncarnationId", FieldValue.Delete), ("TemporaryData", "bad"), ("BookingTolerance", "bad"), ("BookingChoices", "bad") })
        {
            var original = (await doc.GetSnapshotAsync()).ToDictionary();
            await doc.UpdateAsync(field, value);
            var before = await doc.GetSnapshotAsync();
            Assert.False(await service.TryBookingTransition(phone, expected, "choose", "cap", "draft", 10, new() { ["opaque"] = "value" }));
            Assert.Equal(before.UpdateTime, (await doc.GetSnapshotAsync()).UpdateTime);
            await doc.SetAsync(original);
        }
    }
}

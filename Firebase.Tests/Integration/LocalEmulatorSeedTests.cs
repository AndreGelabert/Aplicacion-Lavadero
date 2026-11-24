using Firebase.Security;
using FirebaseAdmin;
using Google.Api.Gax;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;

namespace Firebase.Tests.Integration;

public class LocalEmulatorSeedIntegrationTests
{
    [LocalSeedFact]
    public async Task SeedCoordinatesNewEmployeesAndPreservesExistingDocumentsOnRepeat()
    {
        Assert.Equal("127.0.0.1:8190", Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST"));
        Assert.Equal("127.0.0.1:9199", Environment.GetEnvironmentVariable("FIREBASE_AUTH_EMULATOR_HOST"));
        var db = new FirestoreDbBuilder { ProjectId = "demo-local", EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var app = FirebaseApp.Create(new AppOptions { ProjectId = "demo-local", Credential = GoogleCredential.FromAccessToken("fixture") }, "seed-test-" + Guid.NewGuid());
        try
        {
            var existing = db.Collection("empleados").Document(LocalEmulatorSeed.UserId);
            await existing.CreateAsync(new Dictionary<string, object> { ["Estado"] = "Inactivo", ["Marker"] = "preserve" });
            var original = await existing.GetSnapshotAsync();
            await LocalEmulatorSeed.RunAsync(db, app, true);
            var versionRef = db.Collection("coordinacion").Document("empleados");
            Assert.Equal(1L, (await versionRef.GetSnapshotAsync()).GetValue<long>("Version"));
            var after = await existing.GetSnapshotAsync();
            Assert.Equal(original.UpdateTime, after.UpdateTime);
            Assert.Equal("Inactivo", after.GetValue<string>("Estado"));
            Assert.Equal("preserve", after.GetValue<string>("Marker"));
            var created = await db.Collection("empleados").Document(LocalEmulatorSeed.CreateOnlyUserId).GetSnapshotAsync();
            Assert.Equal("Activo", created.GetValue<string>("Estado"));
            var role = await db.Collection("roles").Document(LocalEmulatorSeed.CreateOnlyRoleId).GetSnapshotAsync();
            Assert.True(role.Exists);
            await LocalEmulatorSeed.RunAsync(db, app, true);
            Assert.Equal(1L, (await versionRef.GetSnapshotAsync()).GetValue<long>("Version"));
            Assert.Equal(created.UpdateTime, (await created.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(role.UpdateTime, (await role.Reference.GetSnapshotAsync()).UpdateTime);
        }
        finally { app.Delete(); }
    }
}

using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Configuration;
using Moq;

namespace Firebase.Tests.Integration;

public class EmployeeMembershipVersionIntegrationTests
{
    private static FirestoreDb Database()
    {
        Assert.Equal("127.0.0.1:8190", Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST"));
        return new FirestoreDbBuilder { ProjectId = "demo-member-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
    }

    private static DocumentReference Version(FirestoreDb db) => db.Collection("coordinacion").Document("empleados");
    private static async Task<long> ReadVersion(FirestoreDb db) => (await Version(db).GetSnapshotAsync()).GetValue<long>("Version");

    [LocalSeedFact]
    public async Task StateChangesAdvanceVersionAndMissingEmployeeDoesNot()
    {
        var db = Database();
        await db.Collection("empleados").Document("a").SetAsync(new Dictionary<string, object> { ["Estado"] = "Activo" });
        var service = new PersonalService(db);
        await service.CambiarEstadoEmpleado("a", "Inactivo");
        Assert.Equal("Inactivo", (await db.Collection("empleados").Document("a").GetSnapshotAsync()).GetValue<string>("Estado"));
        Assert.Equal(1, await ReadVersion(db));
        await service.CambiarEstadoEmpleado("a", "Activo");
        Assert.Equal(2, await ReadVersion(db));
        await Assert.ThrowsAnyAsync<Exception>(() => service.CambiarEstadoEmpleado("missing", "Activo"));
        Assert.Equal(2, await ReadVersion(db));
    }

    [LocalSeedFact]
    public async Task GoogleCreateAndMigrationAdvanceSameVersion()
    {
        var db = Database();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Firebase:ApiKey"] = "fixture" }).Build();
        var uid = "first";
        using var client = new HttpClient();
        var service = new AuthenticationService(config, db, Mock.Of<IAuditWriter>(), client, _ => Task.FromResult((uid, "fixture@example.invalid", "Fixture")));
        Assert.True((await service.AuthenticateWithGoogleAsync("fixture")).IsSuccess);
        Assert.Equal(1, await ReadVersion(db));
        uid = "second";
        Assert.True((await service.AuthenticateWithGoogleAsync("fixture")).IsSuccess);
        Assert.Equal(2, await ReadVersion(db));
        Assert.False((await db.Collection("empleados").Document("first").GetSnapshotAsync()).Exists);
        Assert.True((await db.Collection("empleados").Document("second").GetSnapshotAsync()).Exists);
    }
}

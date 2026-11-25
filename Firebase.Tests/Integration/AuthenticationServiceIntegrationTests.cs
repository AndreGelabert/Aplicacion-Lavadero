using Firebase.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Moq;
using System.Reflection;

namespace Firebase.Tests.Integration;

public class AuthenticationServiceIntegrationTests
{
    [Fact]
    public void ProductionConstructionDoesNotExposeAnInjectableVerifier()
    {
        var constructors = typeof(AuthenticationService).GetConstructors();
        var constructor = Assert.Single(constructors);
        Assert.Equal(new[] { typeof(IConfiguration), typeof(Google.Cloud.Firestore.FirestoreDb), typeof(IAuditWriter), typeof(HttpClient) },
            constructor.GetParameters().Select(p => p.ParameterType));
    }

    [Fact]
    public void DuplicateProductionRegistrationsResolveFirebaseVerifier()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(Configuration(false));
        services.AddSingleton<FirestoreDb>(_ => null!);
        services.AddSingleton<IAuditWriter>(Mock.Of<IAuditWriter>());
        services.AddHttpClient<AuthenticationService>();
        services.AddScoped<AuthenticationService>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AuthenticationService>();
        var verifier = Assert.IsType<Func<string, Task<(string Uid, string Email, string Name)>>>(
            typeof(AuthenticationService).GetField("_verifyGoogleToken", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(service));
        Assert.Equal("VerifyGoogleTokenAsync", verifier.Method.Name);
        Assert.Equal(typeof(AuthenticationService), verifier.Method.DeclaringType);
        Assert.Null(verifier.Target);
    }

    private static IConfiguration Configuration(bool emulator) => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["LocalEmulators:Enabled"] = emulator ? "true" : "false", ["Firebase:ApiKey"] = "test-only" }).Build();

    private static FirestoreDb Database()
    {
        Assert.Equal("127.0.0.1:8190", Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST"));
        return new FirestoreDbBuilder { ProjectId = "demo-auth-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
    }

    [LocalSeedFact]
    public async Task RejectedTokenDoesNotWriteEmployeeOrAudit()
    {
        var db = Database();
        var audit = new Mock<IAuditWriter>(MockBehavior.Strict);
        using var client = new HttpClient();
        var service = new AuthenticationService(Configuration(false), db, audit.Object, client,
            _ => Task.FromException<(string Uid, string Email, string Name)>(new InvalidOperationException("rejected")));
        var result = await service.AuthenticateWithGoogleAsync("untrusted");
        Assert.False(result.IsSuccess);
        Assert.Equal("Error al autenticar con Google. Por favor, intente de nuevo.", result.ErrorMessage);
        Assert.Empty((await db.Collection("empleados").GetSnapshotAsync()).Documents);
        audit.VerifyNoOtherCalls();
    }

    [LocalSeedFact]
    public async Task VerifiedIdentityCreatesEmployeeAndMigratesActiveExistingIdentity()
    {
        var db = Database();
        var audit = new Mock<IAuditWriter>();
        using var client = new HttpClient();
        var uid = "new-uid";
        var service = new AuthenticationService(Configuration(false), db, audit.Object, client,
            _ => Task.FromResult((uid, "fixture@example.invalid", "Fixture")));
        var created = await service.AuthenticateWithGoogleAsync("verified-fixture");
        Assert.True(created.IsSuccess, created.ErrorMessage);
        Assert.Equal(uid, created.UserInfo!.Uid);
        Assert.Equal("Empleado", created.UserInfo.Role);
        var original = await db.Collection("empleados").Document(uid).GetSnapshotAsync();
        Assert.Equal("Activo", original.GetValue<string>("Estado"));
        uid = "migrated-uid";
        var migrated = await service.AuthenticateWithGoogleAsync("verified-fixture");
        Assert.True(migrated.IsSuccess, migrated.ErrorMessage);
        Assert.Equal(uid, migrated.UserInfo!.Uid);
        Assert.Equal("Empleado", migrated.UserInfo.Role);
        Assert.False((await original.Reference.GetSnapshotAsync()).Exists);
        var replacement = await db.Collection("empleados").Document(uid).GetSnapshotAsync();
        Assert.Equal("Activo", replacement.GetValue<string>("Estado"));
        Assert.Equal("fixture@example.invalid", replacement.GetValue<string>("Email"));
        audit.Verify(a => a.LogEvent(uid, "fixture@example.invalid", "Migración a Google Auth", uid, "Empleado", It.IsAny<Dictionary<string, string>>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()), Times.Once);
    }

    [LocalSeedFact]
    public async Task ConcurrentNewIdentitiesWithSameEmailDoNotCreateDuplicates()
    {
        var db = Database();
        var audit = new Mock<IAuditWriter>();
        using var client1 = new HttpClient();
        using var client2 = new HttpClient();
        var one = new AuthenticationService(Configuration(false), db, audit.Object, client1,
            _ => Task.FromResult(("first", "race@example.invalid", "First")));
        var two = new AuthenticationService(Configuration(false), db, audit.Object, client2,
            _ => Task.FromResult(("second", "race@example.invalid", "Second")));
        var arrived = 0;
        var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task WaitBoth()
        {
            if (Interlocked.Increment(ref arrived) == 2) barrier.TrySetResult();
            await barrier.Task.WaitAsync(TimeSpan.FromSeconds(20));
        }
        one.BeforeGoogleProvisioningTransaction = WaitBoth;
        two.BeforeGoogleProvisioningTransaction = async () => { await WaitBoth(); await firstFinished.Task.WaitAsync(TimeSpan.FromSeconds(20)); };
        var first = one.AuthenticateWithGoogleAsync("one");
        var second = two.AuthenticateWithGoogleAsync("two");
        try { await first; } finally { firstFinished.TrySetResult(); }
        var results = await Task.WhenAll(first, second);
        Assert.Single(results.Where(r => r.IsSuccess));
        var employees = await db.Collection("empleados").WhereEqualTo("Email", "race@example.invalid").GetSnapshotAsync();
        var employee = Assert.Single(employees.Documents);
        Assert.Equal("Activo", employee.GetValue<string>("Estado"));
        audit.Verify(a => a.LogEvent(It.IsAny<string>(), "race@example.invalid", "Registro con Google", It.IsAny<string>(), "Empleado", It.IsAny<Dictionary<string, string>>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()), Times.Once);
        audit.Verify(a => a.LogSessionEvent(It.IsAny<string>(), "race@example.invalid", "Inicio de sesión con Google", It.IsAny<string>(), "Empleado"), Times.Once);
    }

    [Fact]
    public async Task EmulatorGuardRejectsGoogleBeforeInjectedVerifierRuns()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["LocalEmulators:Enabled"] = "true"
        }).Build();
        var calls = 0;
        using var client = new HttpClient();
        var service = new AuthenticationService(configuration, null!, null!, client, token =>
        {
            calls++;
            return Task.FromResult((Uid: "uid", Email: "person@example.invalid", Name: "Person"));
        });

        var result = await service.AuthenticateWithGoogleAsync("untrusted-token");

        Assert.False(result.IsSuccess);
        Assert.Equal("El inicio de sesión con Google no está disponible en el entorno local.", result.ErrorMessage);
        Assert.Equal(0, calls);
    }
}

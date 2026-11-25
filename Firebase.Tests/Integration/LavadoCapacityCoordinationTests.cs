using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Caching.Memory;

namespace Firebase.Tests.Integration;

public class LavadoCapacityCoordinationTests
{
    private static Lavado Wash() => new() { Id = "", Estado = "Pendiente", ClienteId = "client", VehiculoId = "vehicle", CantidadEmpleadosRequeridos = 1,
        Servicios = new() { new ServicioEnLavado { ServicioId = "service", Estado = "Pendiente" } } };

    private sealed class Fixture : IDisposable
    {
        public readonly FirestoreDb Db = new FirestoreDbBuilder { ProjectId = "demo-cap-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        private readonly MemoryCache cache = new(new MemoryCacheOptions());
        public LavadoService Service { get; }
        public Fixture()
        {
            cache.Set("system_configuration", new Configuracion { HorariosOperacion = new(), CapacidadMaximaConcurrente = 10 });
            var services = new ServicioService(Db);
            Service = new LavadoService(Db, services, new PaqueteServicioService(Db, services, cache), new PersonalService(Db), new ConfiguracionService(Db, cache));
        }
        public Task SetCapacity(int capacity) => Db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion { HorariosOperacion = new(), CapacidadMaximaConcurrente = capacity });
        public void Dispose() => cache.Dispose();
    }

    [LocalFirestoreFact]
    public async Task BothPathsUseCurrentCapacityAndRejectWithoutWrites()
    {
        foreach (var directFirst in new[] { true, false })
        {
            using var f = new Fixture();
            await f.SetCapacity(1);
            var first = Wash();
            if (directFirst) await f.Service.CrearLavado(first);
            else await f.Service.CrearLavadoConInicio(first);
            var before = await f.Db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => directFirst
                ? f.Service.CrearLavadoConInicio(Wash()) : f.Service.CrearLavado(Wash()));
            Assert.Equal("Se ha alcanzado la capacidad máxima de 1 lavados concurrentes.", error.Message);
            Assert.Single((await f.Db.Collection("lavados").GetSnapshotAsync()).Documents);
            var after = await f.Db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
            Assert.True(before.Exists);
            Assert.Equal(before.UpdateTime, after.UpdateTime);
            Assert.Equal(1L, after.GetValue<long>("Version"));
            var stored = (await f.Service.ObtenerLavado(first.Id))!;
            Assert.Empty(stored.EmpleadosAsignadosIds);
            Assert.Equal(directFirst ? "Pendiente" : "EnProceso", stored.Servicios[0].Estado);
        }
    }

    [LocalFirestoreFact]
    public async Task CreationPreservesCoordinatorFieldsAndAdvancesExistingVersion()
    {
        using var f = new Fixture();
        await f.SetCapacity(10);
        var reference = f.Db.Collection("coordinacion").Document("turnos");
        await reference.SetAsync(new Dictionary<string, object> { ["Version"] = 7L, ["Sentinel"] = "preserve" });
        await f.Service.CrearLavado(Wash());
        var afterDirect = await reference.GetSnapshotAsync();
        Assert.Equal(8L, afterDirect.GetValue<long>("Version"));
        Assert.Equal("preserve", afterDirect.GetValue<string>("Sentinel"));
        await f.Service.CrearLavadosConInicio(new[] { (Wash(), (IReadOnlyDictionary<int, string>?)null), (Wash(), (IReadOnlyDictionary<int, string>?)null) });
        var afterBatch = await reference.GetSnapshotAsync();
        Assert.Equal(9L, afterBatch.GetValue<long>("Version"));
        Assert.Equal("preserve", afterBatch.GetValue<string>("Sentinel"));
        Assert.Equal(3, (await f.Db.Collection("lavados").GetSnapshotAsync()).Count);
    }

    [LocalFirestoreFact]
    public async Task AbsentCoordinatorIsCreatedByBothPaths()
    {
        foreach (var direct in new[] { true, false })
        {
            using var f = new Fixture();
            await f.SetCapacity(10);
            var reference = f.Db.Collection("coordinacion").Document("turnos");
            Assert.False((await reference.GetSnapshotAsync()).Exists);
            if (direct) await f.Service.CrearLavado(Wash());
            else await f.Service.CrearLavadosConInicio(new[] { (Wash(), (IReadOnlyDictionary<int, string>?)null) });
            var coordinator = await reference.GetSnapshotAsync();
            Assert.True(coordinator.Exists);
            Assert.Equal(1L, coordinator.GetValue<long>("Version"));
            Assert.Single((await f.Db.Collection("lavados").GetSnapshotAsync()).Documents);
        }
    }

    [LocalFirestoreFact]
    public async Task InvalidOrOverflowVersionFailsWithoutWrites()
    {
        foreach (var direct in new[] { true, false })
        foreach (var version in new object[] { "invalid", long.MaxValue })
        {
            using var f = new Fixture();
            await f.SetCapacity(10);
            var reference = f.Db.Collection("coordinacion").Document("turnos");
            await reference.SetAsync(new Dictionary<string, object> { ["Version"] = version, ["Sentinel"] = "preserve" });
            var before = await reference.GetSnapshotAsync();
            var wash = Wash();
            var error = await Record.ExceptionAsync(() => direct
                ? f.Service.CrearLavado(wash)
                : f.Service.CrearLavadosConInicio(new[] { (wash, (IReadOnlyDictionary<int, string>?)null) }));
            Assert.NotNull(error);
            if (version is long) Assert.IsType<OverflowException>(error);
            var after = await reference.GetSnapshotAsync();
            Assert.Equal(before.UpdateTime, after.UpdateTime);
            Assert.Equal(version, after.GetValue<object>("Version"));
            Assert.Equal("preserve", after.GetValue<string>("Sentinel"));
            Assert.Empty((await f.Db.Collection("lavados").GetSnapshotAsync()).Documents);
            Assert.Empty((await f.Db.Collection("reservasHerramientas").GetSnapshotAsync()).Documents);
        }
    }

    [LocalFirestoreFact]
    public async Task ExistingCoordinatorWithoutVersionFailsWithoutWrites()
    {
        foreach (var direct in new[] { true, false })
        {
            using var f = new Fixture();
            await f.SetCapacity(10);
            var reference = f.Db.Collection("coordinacion").Document("turnos");
            await reference.SetAsync(new Dictionary<string, object> { ["Sentinel"] = "preserve" });
            var before = await reference.GetSnapshotAsync();
            var wash = Wash();
            var error = await Record.ExceptionAsync(() => direct
                ? f.Service.CrearLavado(wash)
                : f.Service.CrearLavadosConInicio(new[] { (wash, (IReadOnlyDictionary<int, string>?)null) }));
            Assert.NotNull(error);
            var after = await reference.GetSnapshotAsync();
            Assert.Equal(before.UpdateTime, after.UpdateTime);
            Assert.Equal(before.ToDictionary(), after.ToDictionary());
            Assert.Empty((await f.Db.Collection("lavados").GetSnapshotAsync()).Documents);
            Assert.Empty((await f.Db.Collection("reservasHerramientas").GetSnapshotAsync()).Documents);
        }
    }

    [LocalFirestoreFact]
    public async Task FullCapacityTakesPrecedenceOverInvalidWashWithoutWrites()
    {
        foreach (var direct in new[] { true, false })
        {
            using var f = new Fixture();
            await f.SetCapacity(10);
            for (var i = 0; i < 10; i++) await f.Service.CrearLavado(Wash());
            var before = await f.Db.Collection("lavados").GetSnapshotAsync();
            var lockBefore = await f.Db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
            var invalid = Wash();
            invalid.ClienteId = "";
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => direct
                ? f.Service.CrearLavado(invalid) : f.Service.CrearLavadoConInicio(invalid));
            Assert.Equal("Se ha alcanzado la capacidad máxima de 10 lavados concurrentes.", error.Message);
            var after = await f.Db.Collection("lavados").GetSnapshotAsync();
            Assert.Equal(before.Documents.Select(x => (x.Id, x.UpdateTime)), after.Documents.Select(x => (x.Id, x.UpdateTime)));
            var lockAfter = await f.Db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
            Assert.Equal(lockBefore.UpdateTime, lockAfter.UpdateTime);
            Assert.Equal(lockBefore.GetValue<long>("Version"), lockAfter.GetValue<long>("Version"));
            Assert.Empty((await f.Db.Collection("reservasHerramientas").GetSnapshotAsync()).Documents);
        }
    }

    [LocalFirestoreFact]
    public async Task UpdateUpsertsAndOverwritesWhileAdvancingCoordinator()
    {
        using var f = new Fixture();
        var wash = Wash();
        wash.Id = "wash";
        var coordinator = f.Db.Collection("coordinacion").Document("turnos");
        var reference = f.Db.Collection("lavados").Document(wash.Id);
        await f.Service.ActualizarLavado(wash);
        Assert.True((await reference.GetSnapshotAsync()).Exists);
        Assert.Equal(1L, (await coordinator.GetSnapshotAsync()).GetValue<long>("Version"));
        await coordinator.UpdateAsync("Sentinel", "preserve");
        await reference.UpdateAsync("Extra", "discard");
        wash.Estado = "EnProceso";
        await f.Service.ActualizarLavado(wash);
        var updated = await reference.GetSnapshotAsync();
        Assert.False(updated.ContainsField("Extra"));
        Assert.Equal("EnProceso", updated.GetValue<string>("Estado"));
        var lockAfter = await coordinator.GetSnapshotAsync();
        Assert.Equal(2L, lockAfter.GetValue<long>("Version"));
        Assert.Equal("preserve", lockAfter.GetValue<string>("Sentinel"));
    }

    [LocalFirestoreFact]
    public async Task UpdateRejectsAnyPersistedTurnoIdWithoutWrites()
    {
        foreach (var link in new object?[] { "turno-1", null, 42L })
        {
            using var f = new Fixture();
            var wash = Wash(); wash.Id = "wash";
            var reference = f.Db.Collection("lavados").Document(wash.Id);
            var coordinator = f.Db.Collection("coordinacion").Document("turnos");
            await reference.SetAsync(new Dictionary<string, object?> { ["TurnoId"] = link, ["Extra"] = "preserve" });
            await coordinator.SetAsync(new Dictionary<string, object> { ["Version"] = 7L, ["Sentinel"] = "preserve" });
            var before = await reference.GetSnapshotAsync();
            var lockBefore = await coordinator.GetSnapshotAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.ActualizarLavado(wash));
            var after = await reference.GetSnapshotAsync();
            var lockAfter = await coordinator.GetSnapshotAsync();
            Assert.Equal(before.UpdateTime, after.UpdateTime);
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before.ToDictionary()), System.Text.Json.JsonSerializer.Serialize(after.ToDictionary()));
            Assert.Equal(lockBefore.UpdateTime, lockAfter.UpdateTime);
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(lockBefore.ToDictionary()), System.Text.Json.JsonSerializer.Serialize(lockAfter.ToDictionary()));
        }
    }

    [LocalFirestoreFact]
    public async Task UpdateInvalidCoordinatorRollsBackExistingAndMissingWash()
    {
        foreach (var version in new object[] { "invalid", long.MaxValue })
        foreach (var exists in new[] { true, false })
        {
            using var f = new Fixture();
            var wash = Wash(); wash.Id = "wash";
            var reference = f.Db.Collection("lavados").Document(wash.Id);
            if (exists) await reference.SetAsync(new Dictionary<string, object> { ["Sentinel"] = "original" });
            var before = await reference.GetSnapshotAsync();
            var coordinator = f.Db.Collection("coordinacion").Document("turnos");
            await coordinator.SetAsync(new Dictionary<string, object> { ["Version"] = version, ["Sentinel"] = "preserve" });
            var lockBefore = await coordinator.GetSnapshotAsync();
            var error = await Record.ExceptionAsync(() => f.Service.ActualizarLavado(wash));
            Assert.NotNull(error);
            if (version is long) Assert.IsType<OverflowException>(error);
            var after = await reference.GetSnapshotAsync();
            Assert.Equal(before.Exists, after.Exists);
            if (exists) { Assert.Equal(before.UpdateTime, after.UpdateTime); Assert.Equal("original", after.GetValue<string>("Sentinel")); }
            var lockAfter = await coordinator.GetSnapshotAsync();
            Assert.Equal(lockBefore.UpdateTime, lockAfter.UpdateTime);
            Assert.Equal(version, lockAfter.GetValue<object>("Version"));
            Assert.Equal("preserve", lockAfter.GetValue<string>("Sentinel"));
        }
    }

    [LocalFirestoreFact]
    public async Task AssignedEmployeesAreRevalidatedAcrossCreationBatchAndUpdates()
    {
        using var f = new Fixture();
        await f.SetCapacity(10);
        await f.Db.Collection("empleados").Document("active").SetAsync(new { Estado = "Activo" });
        await f.Db.Collection("empleados").Document("inactive").SetAsync(new { Estado = "Inactivo" });
        Lavado Assigned(string id) { var w = Wash(); w.EmpleadosAsignadosIds.Add(id); return w; }
        var first = Assigned("active");
        await f.Service.CrearLavado(first);
        var coordinator = f.Db.Collection("coordinacion").Document("turnos");
        var before = await coordinator.GetSnapshotAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.CrearLavadoConInicio(Assigned("active")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.CrearLavado(Assigned("inactive")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.CrearLavado(Assigned("missing")));
        Assert.Single((await f.Db.Collection("lavados").GetSnapshotAsync()).Documents);
        Assert.Equal(before.UpdateTime, (await coordinator.GetSnapshotAsync()).UpdateTime);
        await f.Service.ActualizarLavado(first); // self is not a collision
        var inactiveWash = Assigned("active"); inactiveWash.Id = "inactive-wash"; inactiveWash.Estado = "Realizado";
        await f.Service.ActualizarLavado(inactiveWash);
        var conflicting = Assigned("active"); conflicting.Id = "conflict"; conflicting.Estado = "EnProceso";
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.ActualizarLavado(conflicting));
        Assert.False((await f.Db.Collection("lavados").Document("conflict").GetSnapshotAsync()).Exists);
    }

    [LocalFirestoreFact]
    public async Task BatchDuplicateRollsBackAndEmptyLegacyAssignmentsRemainAllowed()
    {
        using var f = new Fixture();
        await f.SetCapacity(10);
        await f.Db.Collection("empleados").Document("active").SetAsync(new { Estado = "Activo" });
        Lavado Assigned() { var w = Wash(); w.EmpleadosAsignadosIds.Add("active"); return w; }
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.CrearLavadosConInicio(new[] {
            (Assigned(), (IReadOnlyDictionary<int, string>?)null), (Assigned(), (IReadOnlyDictionary<int, string>?)null) }));
        Assert.Empty((await f.Db.Collection("lavados").GetSnapshotAsync()).Documents);
        Assert.False((await f.Db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).Exists);
        await f.Service.CrearLavado(Wash());
        await f.Service.CrearLavadoConInicio(Wash());
        Assert.Equal(2, (await f.Db.Collection("lavados").GetSnapshotAsync()).Count);
    }

    [LocalFirestoreFact]
    public async Task ConcurrentAssignedCreationsHaveAtMostOneWinner()
    {
        using var f = new Fixture();
        await f.SetCapacity(10);
        await f.Db.Collection("empleados").Document("active").SetAsync(new { Estado = "Activo" });
        Lavado Assigned() { var w = Wash(); w.EmpleadosAsignadosIds.Add("active"); return w; }
        var results = await Task.WhenAll(Record.ExceptionAsync(() => f.Service.CrearLavado(Assigned())),
            Record.ExceptionAsync(() => f.Service.CrearLavadoConInicio(Assigned())));
        Assert.Single(results, x => x == null);
        Assert.Single((await f.Db.Collection("lavados").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task ConcurrentDifferentPathsHaveOneWinnerAndNoPartialWash()
    {
        using var f = new Fixture();
        await f.SetCapacity(1);
        async Task<Exception?> Attempt(bool direct) => await Record.ExceptionAsync(() => direct
            ? f.Service.CrearLavado(Wash()) : f.Service.CrearLavadoConInicio(Wash()));
        var results = await Task.WhenAll(Attempt(true), Attempt(false));
        Assert.Single(results, x => x == null);
        var failure = Assert.IsType<InvalidOperationException>(Assert.Single(results, x => x != null));
        Assert.Equal("Se ha alcanzado la capacidad máxima de 1 lavados concurrentes.", failure.Message);
        Assert.Single((await f.Db.Collection("lavados").GetSnapshotAsync()).Documents);
        Assert.Equal(1L, (await f.Db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).GetValue<long>("Version"));
        Assert.Empty((await f.Db.Collection("reservasHerramientas").GetSnapshotAsync()).Documents);
    }
}

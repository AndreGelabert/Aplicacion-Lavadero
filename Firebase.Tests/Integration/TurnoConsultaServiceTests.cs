using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;

namespace Firebase.Tests.Integration;

public class TurnoConsultaServiceTests
{
    [LocalFirestoreFact]
    public async Task DetailUsesOnlyReciprocalWashAssignmentsAndAvailableClientNames()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-consulta-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var turno = db.Collection("turnos").Document("t");
        var wash = db.Collection("lavados").Document("w");
        await turno.SetAsync(new Turno { Id = "t", ClienteId = "c", Estado = "EnProceso", InicioUtc = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc), FinUtc = new DateTime(2030, 1, 1, 13, 0, 0, DateTimeKind.Utc) });
        await turno.UpdateAsync("LavadoId", "w");
        await db.Collection("clientes").Document("c").SetAsync(new { Nombre = "Ana" });
        await db.Collection("empleados").Document("e").SetAsync(new { Nombre = "Empleado Real" });
        await wash.SetAsync(new { TurnoId = "otro", EmpleadosAsignadosIds = new[] { "e" } });
        var reader = new TurnoConsultaService(db);
        var mismatch = (await reader.Obtener("t"))!;
        Assert.Equal("Ana", mismatch.Cliente);
        Assert.Empty(mismatch.EmpleadosAsignados!);
        await wash.SetAsync(new { TurnoId = "t", EmpleadosAsignadosIds = new[] { "e", "missing", "bad/id" } });
        var before = await turno.GetSnapshotAsync();
        var detail = (await reader.Obtener("t"))!;
        Assert.Equal(new[] { "Empleado Real" }, detail.EmpleadosAsignados);
        Assert.Equal(before.UpdateTime, (await turno.GetSnapshotAsync()).UpdateTime);
        await db.Collection("clientes").Document("c").SetAsync(new { Apellido = "Perez" });
        Assert.Equal("Perez", (await reader.Obtener("t"))!.Cliente);
        await turno.UpdateAsync("LavadoId", "missing");
        Assert.Empty((await reader.Obtener("t"))!.EmpleadosAsignados!);
    }

    [LocalFirestoreFact]
    public async Task UpcomingQuery_FiltersOwnerStatusAndTime_AndLimitsOrderedResults()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-consulta-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var start = new DateTime(2035, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var batch = db.StartBatch();
        void Add(string id, string owner, string status, DateTime time) => batch.Set(db.Collection("turnos").Document(id),
            new Turno { Id = id, ClienteId = owner, Estado = status, InicioUtc = time, FinUtc = time.AddHours(1) });
        for (var i = 14; i >= 0; i--) Add($"a{i:D2}", "owner-a", "Pendiente", start.AddMinutes(i));
        Add("past", "owner-a", "Pendiente", start.AddMinutes(-1));
        Add("cancelled", "owner-a", "Cancelado", start);
        Add("other", "owner-b", "Pendiente", start);
        await batch.CommitAsync();
        var reader = new TurnoConsultaService(db);
        var a = await reader.ProximosDeCliente("owner-a", start);
        Assert.Equal(Enumerable.Range(0, 11).Select(i => $"a{i:D2}"), a.Select(t => t.Id));
        Assert.Equal("other", Assert.Single(await reader.ProximosDeCliente("owner-b", start)).Id);
        Assert.Empty(await reader.ProximosDeCliente("owner-c", start));
    }

    [LocalFirestoreFact]
    public async Task DetailAndListExposePersistedEditSelectionsWithoutConfidentialFields()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-consulta-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var reference = db.Collection("turnos").Document("edit");
        await reference.SetAsync(new Turno
        {
            Id = "edit", ClienteId = "customer", Estado = "Pendiente", Origen = "secret-origin",
            ClaveIdempotencia = "secret-key", Notas = "secret-notes", CuponId = "secret-coupon",
            InicioUtc = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            FinUtc = new DateTime(2030, 1, 1, 13, 0, 0, DateTimeKind.Utc), Descuento = 12.5m,
            Vehiculos = new() { new VehiculoEnTurno { VehiculoId = "vehicle-missing", Servicios = new()
            {
                new ServicioEnTurno { ServicioId = "service-missing", PaqueteId = "package-missing" },
                new ServicioEnTurno { ServicioId = "other-service", PaqueteId = null },
                new ServicioEnTurno { ServicioId = "service-missing", PaqueteId = "package-missing" }
            } } }
        });
        var before = await reference.GetSnapshotAsync();
        var reader = new TurnoConsultaService(db);
        var detail = Assert.IsType<TurnoConsultaViewModel>(await reader.Obtener("edit"));
        var listItem = Assert.Single(await reader.Listar());
        foreach (var item in new[] { detail, listItem })
        {
            Assert.Equal(12.5m, item.Descuento);
            var vehicle = Assert.Single(item.Vehiculos);
            Assert.Equal("vehicle-missing", vehicle.VehiculoId);
            Assert.Equal(new[] { "service-missing", "other-service", "service-missing" }, vehicle.Servicios.Select(s => s.ServicioId));
            Assert.Equal(new string?[] { "package-missing", null, "package-missing" }, vehicle.Servicios.Select(s => s.PaqueteId));
            Assert.Equal("No disponible", vehicle.Patente);
            var json = System.Text.Json.JsonSerializer.Serialize(item);
            Assert.DoesNotContain("secret-", json);
        }
        Assert.NotNull(detail.VersionEsperada);
        Assert.Null(listItem.VersionEsperada);
        Assert.Equal(before.UpdateTime, (await reference.GetSnapshotAsync()).UpdateTime);
    }

    [LocalFirestoreFact]
    public async Task ProjectionPreservesOrderAndDuplicatesAndHandlesMissingDocumentsWithoutWrites()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-consulta-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var customer = db.Collection("clientes").Document("c");
        var vehicle = db.Collection("vehiculos").Document("v");
        var service = db.Collection("servicios").Document("s");
        await customer.SetAsync(new { Nombre = "Ana", Apellido = "Perez", Email = "secret@example.invalid" });
        await vehicle.SetAsync(new { Patente = "ABC123" });
        await service.SetAsync(new { Nombre = "Lavado" });
        Turno Entry(string id, int day, string client, string vehicleId) => new() {
            Id = id, ClienteId = client, Estado = "Pendiente", InicioUtc = new DateTime(2030, 1, day, 12, 0, 0, DateTimeKind.Utc), FinUtc = new DateTime(2030, 1, day, 13, 0, 0, DateTimeKind.Utc),
            Vehiculos = new() { new VehiculoEnTurno { VehiculoId = vehicleId, Servicios = new() { new ServicioEnTurno { ServicioId = "s" }, new ServicioEnTurno { ServicioId = "missing" }, new ServicioEnTurno { ServicioId = "s" } } } }
        };
        var a = db.Collection("turnos").Document("a"); var b = db.Collection("turnos").Document("b"); var c = db.Collection("turnos").Document("c");
        await a.SetAsync(Entry("a", 1, "c", "v")); await b.SetAsync(Entry("b", 2, "c", "v")); await c.SetAsync(Entry("c", 3, "missing", "missing"));
        var refs = new[] { customer, vehicle, service, a, b, c };
        var before = await db.GetAllSnapshotsAsync(refs);
        var reader = new TurnoConsultaService(db);
        var list = await reader.Listar();
        Assert.Equal(new[] { "c", "b", "a" }, list.Select(x => x.Id));
        Assert.Equal("No disponible", list[0].Cliente);
        Assert.Equal("No disponible", list[0].Vehiculos[0].Patente);
        Assert.Equal("Ana Perez", list[1].Cliente);
        Assert.Equal("ABC123", list[1].Vehiculos[0].Patente);
        Assert.Equal(new[] { "Lavado", "No disponible", "Lavado" }, list[1].Vehiculos[0].Servicios.Select(x => x.Nombre));
        Assert.Equal("Ana Perez", (await reader.Obtener("a"))!.Cliente);
        Assert.Null(await reader.Obtener("missing")); Assert.Null(await reader.Obtener("bad/id"));
        var after = await db.GetAllSnapshotsAsync(refs);
        Assert.Equal(before.Select(x => x.UpdateTime), after.Select(x => x.UpdateTime));
    }
}

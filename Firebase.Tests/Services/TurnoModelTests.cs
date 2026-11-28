using Firebase.Models;

namespace Firebase.Tests.Services;

public class TurnoModelTests
{
    private static Turno Valid() => new()
    {
        Id = "t1", ClienteId = "c1", Estado = "Pendiente", Origen = "Web", ClaveIdempotencia = "request-1",
        InicioUtc = new DateTime(2030, 1, 1, 10, 0, 0, DateTimeKind.Utc),
        FinUtc = new DateTime(2030, 1, 1, 11, 0, 0, DateTimeKind.Utc), AceptaAdelanto = true,
        Vehiculos = [new() { VehiculoId = "v1", CantidadEmpleadosRequeridos = 2,
            Servicios = [new() { ServicioId = "s1", Precio = 100, TiempoEstimado = 20 },
                new() { ServicioId = "s2", Precio = 0, TiempoEstimado = 40 }] }]
    };

    [Fact]
    public void WebFormRequiresIdsSelectionsLocalDateTimeAndValidDiscount()
    {
        var form = new TurnoWebForm();
        Assert.NotEmpty(form.Validar());
        form.ClienteId = "c"; form.VehiculoId = "v"; form.ClienteTrajoId = "c"; form.ClienteRetiraId = "c";
        form.Fecha = new DateOnly(2030, 1, 1); form.Hora = new TimeOnly(9, 0);
        form.Servicios = [new TurnoWebFormServicio { ServicioId = "s" }];
        Assert.Empty(form.Validar());
        form.Descuento = 101;
        Assert.Contains(nameof(form.Descuento), form.Validar());
        form.Descuento = -1;
        Assert.Contains(nameof(form.Descuento), form.Validar());
        form.Descuento = 0; form.Servicios[0].ServicioId = "a/b";
        Assert.NotEmpty(form.Validar());
    }

    [Fact]
    public void ValidSnapshotsAndPreferenceAreRetainedWithoutPersistence()
    {
        var turno = Valid();
        Assert.Empty(turno.Validar());
        Assert.Equal("request-1", turno.ClaveIdempotencia);
        Assert.Equal("Web", turno.Origen);
        Assert.True(turno.AceptaAdelanto);
        Assert.Equal(2, turno.Vehiculos[0].CantidadEmpleadosRequeridos);
        Assert.Equal(100m, turno.Vehiculos[0].Servicios[0].Precio);
        Assert.Null(turno.Vehiculos[0].Servicios[0].PaqueteId);
        Assert.Equal(60, turno.Vehiculos[0].Servicios.Sum(s => s.TiempoEstimado));
    }

    [Fact]
    public void RequiredFieldsAndCollectionsAreValidated()
    {
        var turno = Valid();
        turno.Id = " "; turno.ClienteId = ""; turno.Estado = " "; turno.Origen = ""; turno.ClaveIdempotencia = " ";
        turno.Vehiculos[0].VehiculoId = ""; turno.Vehiculos[0].Servicios[0].ServicioId = " ";
        var errors = turno.Validar();
        foreach (var field in new[] { "Id", "ClienteId", "Estado", "Origen", "ClaveIdempotencia", "VehiculoId", "ServicioId" })
            Assert.Contains(field, errors);
        turno.Vehiculos[0].Servicios.Clear();
        Assert.Contains("Servicios", turno.Validar());
        turno.Vehiculos.Clear();
        Assert.Contains("Vehiculos", turno.Validar());
    }

    [Fact]
    public void RejectsInvalidSnapshotsAndIntervals()
    {
        var turno = Valid();
        turno.Vehiculos[0].CantidadEmpleadosRequeridos = 0;
        turno.Vehiculos[0].Servicios[0].TiempoEstimado = 0;
        turno.Vehiculos[0].Servicios[0].Precio = -1;
        turno.InicioUtc = DateTime.SpecifyKind(turno.InicioUtc, DateTimeKind.Unspecified);
        turno.FinUtc = turno.InicioUtc;
        var errors = turno.Validar();
        foreach (var field in new[] { "CantidadEmpleadosRequeridos", "TiempoEstimado", "Precio", "InicioUtc", "FinUtc" })
            Assert.Contains(field, errors);
        turno.FinUtc = DateTime.SpecifyKind(turno.FinUtc.AddMinutes(-1), DateTimeKind.Local);
        Assert.Contains("FinUtc", turno.Validar());
    }

    [Fact]
    public void IntervalMustEqualSumOfOneVehiclesServiceMinutes()
    {
        var turno = Valid();
        Assert.Empty(turno.Validar());
        turno.FinUtc = turno.FinUtc.AddTicks(-1);
        Assert.Contains("FinUtc", turno.Validar());
        turno.FinUtc = turno.FinUtc.AddTicks(2);
        Assert.Contains("FinUtc", turno.Validar());
        turno.FinUtc = turno.InicioUtc.AddMinutes(60);
        Assert.Empty(turno.Validar());
    }

    [Fact]
    public void MultipleVehiclesRequireSeparateAppointments()
    {
        var turno = Valid();
        turno.Vehiculos.Add(new() { VehiculoId = "v2", CantidadEmpleadosRequeridos = 1,
            Servicios = [new() { ServicioId = "s3", Precio = 1, TiempoEstimado = 60 }] });
        Assert.Contains("Vehiculos", turno.Validar());
    }

    [Firebase.Tests.Integration.LocalFirestoreFact]
    public async Task FirestoreRoundtripPreservesAgreedAndServicePricesAndUtcInstants()
    {
        var db = new Google.Cloud.Firestore.FirestoreDbBuilder { ProjectId = "demo-turno-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = Google.Api.Gax.EmulatorDetection.EmulatorOnly }.Build();
        var reference = db.Collection("turnos").Document("snapshot");
        var original = Valid();
        original.PrecioAcordado = 123.45625m;
        original.Vehiculos[0].Servicios[0].Precio = 100.125m;
        original.Vehiculos[0].Servicios[0].PaqueteId = "pack";
        original.Vehiculos[0].Servicios[1].Precio = 0.25m;
        await reference.SetAsync(original);
        var actual = (await reference.GetSnapshotAsync()).ConvertTo<Turno>();
        Assert.Equal(original.PrecioAcordado, actual.PrecioAcordado);
        Assert.Equal("pack", actual.Vehiculos[0].Servicios[0].PaqueteId);
        Assert.Null(actual.Vehiculos[0].Servicios[1].PaqueteId);
        Assert.Equal(new decimal[] { 100.125m, 0.25m }, actual.Vehiculos[0].Servicios.Select(s => s.Precio));
        Assert.Equal(original.InicioUtc, actual.InicioUtc);
        Assert.Equal(original.FinUtc, actual.FinUtc);
        Assert.Equal(DateTimeKind.Utc, actual.InicioUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, actual.FinUtc.Kind);
        Assert.Empty(actual.Validar());
    }

    [Firebase.Tests.Integration.LocalFirestoreFact]
    public async Task FirestoreLegacyDocumentWithoutPackageIdDoesNotInventProvenance()
    {
        var db = new Google.Cloud.Firestore.FirestoreDbBuilder { ProjectId = "demo-turno-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = Google.Api.Gax.EmulatorDetection.EmulatorOnly }.Build();
        var reference = db.Collection("turnos").Document("legacy");
        await reference.SetAsync(new { Vehiculos = new[] { new { VehiculoId = "v", CantidadEmpleadosRequeridos = 1, Servicios = new[] { new { ServicioId = "s", Precio = 10.0, TiempoEstimado = 30 } } } } });
        var actual = (await reference.GetSnapshotAsync()).ConvertTo<Turno>();
        Assert.Null(actual.Vehiculos.Single().Servicios.Single().PaqueteId);
        Assert.Equal(10m, actual.Vehiculos.Single().Servicios.Single().Precio);
        Assert.Equal(30, actual.Vehiculos.Single().Servicios.Single().TiempoEstimado);
    }

    [Fact]
    public void ValidationIsRepeatableAndDoesNotMutateSnapshots()
    {
        var turno = Valid();
        turno.Vehiculos[0].Servicios[0].TiempoEstimado = -1;
        var first = turno.Validar();
        Assert.Equal(first, turno.Validar());
        Assert.Equal(-1, turno.Vehiculos[0].Servicios[0].TiempoEstimado);
    }

    [Fact]
    public void FechaCreacionUtcAcceptsNullOrUtcRejectsInvalidAndOmitsNullInJson()
    {
        var turno = Valid();
        Assert.Null(turno.FechaCreacionUtc);
        Assert.Empty(turno.Validar());
        Assert.DoesNotContain("FechaCreacionUtc", System.Text.Json.JsonSerializer.Serialize(turno));

        turno.FechaCreacionUtc = new DateTime(2029, 12, 31, 15, 0, 0, DateTimeKind.Utc);
        Assert.Empty(turno.Validar());

        turno.FechaCreacionUtc = default(DateTime);
        Assert.Contains(nameof(Turno.FechaCreacionUtc), turno.Validar());

        turno.FechaCreacionUtc = DateTime.SpecifyKind(new DateTime(2029, 12, 31, 15, 0, 0), DateTimeKind.Local);
        Assert.Contains(nameof(Turno.FechaCreacionUtc), turno.Validar());

        turno.FechaCreacionUtc = DateTime.SpecifyKind(new DateTime(2029, 12, 31, 15, 0, 0), DateTimeKind.Unspecified);
        Assert.Contains(nameof(Turno.FechaCreacionUtc), turno.Validar());
    }
}

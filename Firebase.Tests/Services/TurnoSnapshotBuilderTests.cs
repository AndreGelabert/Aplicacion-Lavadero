using Firebase.Models;
using Firebase.Services;

namespace Firebase.Tests.Services;

public class TurnoSnapshotBuilderTests
{
    private static readonly DateTime Start = new(2030, 1, 1, 10, 0, 0, DateTimeKind.Utc);
    private static Servicio Service(string id, decimal price, int minutes) => new() { Id = id, Nombre = id, Tipo = "Limpieza", TipoVehiculo = "Auto", Descripcion = "x", Estado = "Activo", Precio = price, TiempoEstimado = minutes };
    private static Vehiculo Vehicle() => new() { Id = "v", Patente = "ABC123", TipoVehiculo = "auto", Marca = "a", Modelo = "b", Color = "c" };
    private static TipoVehiculo Type() => new() { Id = "t", Nombre = "Auto", FormatoPatente = "lllnnn", CantidadEmpleadosRequeridos = 4 };
    private static TurnoSnapshotBuilder.Entrada Input() => new("t", "c", "Web", "key", Start, true, Vehicle(), Type(),
        [new(Service("s1", 100, 20), "p"), new(Service("s2", 50, 40), "p"), new(Service("s3", 20, 5), null)],
        [new PaqueteServicio { Id = "p", Nombre = "Pack", Estado = "Activo", TipoVehiculo = "Auto", ServiciosIds = ["s1", "s2"], PorcentajeDescuento = 10 }], 20);

    [Fact]
    public void BuildsOneVehicleWithExactDurationStaffAndLavadoPrice()
    {
        var input = Input(); var result = TurnoSnapshotBuilder.Build(input);
        Assert.Empty(result.Validar());
        Assert.Equal(Start.AddMinutes(65), result.FinUtc);
        Assert.Equal(4, result.Vehiculos.Single().CantidadEmpleadosRequeridos);
        Assert.Equal(124m, result.PrecioAcordado);
        Assert.Equal(1, result.DesglosePrecio!.Version);
        Assert.Equal(155m, result.DesglosePrecio.Subtotal);
        Assert.Equal(124m, result.DesglosePrecio.Total);
        Assert.Equal(new decimal[] { 100, 50, 20 }, result.Vehiculos.Single().Servicios.Select(s => s.Precio));
        Assert.Equal(new string?[] { "p", "p", null }, result.Vehiculos.Single().Servicios.Select(s => s.PaqueteId));
        Assert.True(result.AceptaAdelanto);
        input.Servicios[0].Servicio.Precio = 1;
        Assert.Equal(100m, result.Vehiculos[0].Servicios[0].Precio);
    }

    [Fact]
    public void OperationalSnapshotCopiesAllNestedCatalogObjects()
    {
        var source = Service("s", 100, 20);
        source.Receta = [new RecetaInsumo { Modalidad = "Tipo", TipoId = "t", NombreTipo = "Tipo", Categoria = "Volumen", InsumoId = "i", NombreInsumo = "Insumo", UnidadMedida = "ml", DosisTeorica = 1.25m }];
        source.Etapas = [new Etapa { Id = "e", Nombre = "Etapa", Receta = [new RecetaInsumo { Modalidad = "Producto", TipoId = "t2", NombreTipo = "Tipo 2", Categoria = "Peso", InsumoId = "i2", NombreInsumo = "Insumo 2", UnidadMedida = "g", DosisTeorica = 2.5m }] }];
        var snapshot = TurnoSnapshotBuilder.CapturarRecetaOperativa([source]);
        source.Nombre = "Cambiado"; source.Tipo = "Otro";
        source.Receta[0].TipoId = "cambiado"; source.Receta[0].DosisTeorica = 9;
        source.Etapas[0].Id = "otro"; source.Etapas[0].Nombre = "Otra";
        source.Etapas[0].Receta[0].NombreInsumo = "Otro"; source.Etapas[0].Receta[0].DosisTeorica = 8;
        source.Receta.Add(new()); source.Etapas[0].Receta.Add(new()); source.Etapas.Add(new());
        var captured = Assert.Single(snapshot.Servicios);
        Assert.Equal("s", captured.ServicioId); Assert.Equal("s", captured.Nombre); Assert.Equal("Limpieza", captured.Tipo);
        var item = Assert.Single(captured.Receta);
        Assert.Equal(("Tipo", "t", "Tipo", "Volumen", "i", "Insumo", "ml", 1.25m), (item.Modalidad, item.TipoId, item.NombreTipo, item.Categoria, item.InsumoId, item.NombreInsumo, item.UnidadMedida, item.DosisTeorica));
        var stage = Assert.Single(captured.Etapas); Assert.Equal("e", stage.Id); Assert.Equal("Etapa", stage.Nombre);
        item = Assert.Single(stage.Receta);
        Assert.Equal(("Producto", "t2", "Tipo 2", "Peso", "i2", "Insumo 2", "g", 2.5m), (item.Modalidad, item.TipoId, item.NombreTipo, item.Categoria, item.InsumoId, item.NombreInsumo, item.UnidadMedida, item.DosisTeorica));
    }

    [Fact]
    public void SnapshotRetainsDeliveryPickupDiscountAndNotes()
    {
        var input = Input() with { ClienteTrajoId = "delivery", ClienteRetiraId = "pickup", Notas = "Nota" };
        var result = TurnoSnapshotBuilder.Build(input);
        Assert.Equal("delivery", result.ClienteTrajoId);
        Assert.Equal("pickup", result.ClienteRetiraId);
        Assert.Equal("Nota", result.Notas);
        Assert.Equal(20m, result.Descuento);
        Assert.Equal(124m, result.PrecioAcordado);
    }

    [Fact]
    public void SnapshotRetainsDenormalizedClientAndVehicleFields()
    {
        var input = Input() with
        {
            ClienteNombre = "Juan Perez",
            ClienteTrajoNombre = "Pedro Gomez",
            ClienteRetiraNombre = "Maria Lopez"
        };
        var result = TurnoSnapshotBuilder.Build(input);
        Assert.Equal("Juan Perez", result.ClienteNombre);
        Assert.Equal("Pedro Gomez", result.ClienteTrajoNombre);
        Assert.Equal("Maria Lopez", result.ClienteRetiraNombre);
        Assert.Equal("ABC123", result.VehiculoPatente);
        Assert.Equal("auto", result.TipoVehiculo);
        Assert.Equal("ABC123", result.Vehiculos[0].Patente);
        Assert.Equal("auto", result.Vehiculos[0].TipoVehiculo);
    }

    [Fact]
    public void BookingContractsCarryAllLavadoFields()
    {
        var input = new TurnoService.SnapshotRequest("id", "c", "v", "Web", "key", Start, true,
            [new("s", null)], "delivery", "pickup", 12.5m, "nota");
        var web = new TurnoWebBookingRequest("id", "key", "c", "v", DateTime.SpecifyKind(Start, DateTimeKind.Unspecified), true,
            [new("s", null)], "delivery", "pickup", 12.5m, "nota");
        Assert.Equal((input.ClienteTrajoId, input.ClienteRetiraId, input.Descuento, input.Notas),
            (web.ClienteTrajoId, web.ClienteRetiraId, web.Descuento, web.Notas));
    }

    [Fact]
    public void IdempotencyMarkersMatchOnlyExactVersionAndLegacyDefaults()
    {
        var request = new TurnoService.SnapshotRequest("id", "c", "v", "Web", "key", Start, true, [new("s", null)]);
        const string old = "{\"Id\":\"id\",\"ClienteId\":\"c\",\"VehiculoId\":\"v\",\"Origen\":\"Web\",\"ClaveIdempotencia\":\"key\",\"InicioUtc\":\"2030-01-01T10:00:00Z\",\"AceptaAdelanto\":true,\"Servicios\":[{\"ServicioId\":\"s\",\"PaqueteId\":null}]}";
        Assert.True(TurnoService.MatchesMarker("ids-v1", old, request));
        Assert.False(TurnoService.MatchesMarker("ids-v1", old.Replace(",\"PaqueteId\":null", ""), request));
        Assert.False(TurnoService.MatchesMarker("ids-v1", old, request with { Descuento = 1 }));
        Assert.False(TurnoService.MatchesMarker("ids-v1", old, request with { ClienteTrajoId = "c" }));
        Assert.False(TurnoService.MatchesMarker("ids-v1", old, request with { ClienteRetiraId = "c" }));
        Assert.False(TurnoService.MatchesMarker("ids-v1", old, request with { Notas = "x" }));
        Assert.False(TurnoService.MatchesMarker("ids-v1", old, request with { Id = "other" }));
        Assert.False(TurnoService.MatchesMarker("ids-v1", old.Replace("\"Id\":\"id\"", "\"Id\":\"id\",\"Extra\":0"), request));
        Assert.False(TurnoService.MatchesMarker("ids-v1", old.Replace("\"Id\":\"id\"", "\"Id\":\"id\",\"Id\":\"id\""), request));
        Assert.False(TurnoService.MatchesMarker("ids-v1", "{}", request));
        Assert.False(TurnoService.MatchesMarker("unknown", old, request));
        var json = System.Text.Json.JsonSerializer.Serialize(request);
        Assert.True(TurnoService.MatchesMarker("ids-v2", json, request));
        Assert.False(TurnoService.MatchesMarker("ids-v2", json, request with { Notas = "x" }));
        Assert.False(TurnoService.MatchesMarker("ids-v2", old, request));
    }

    [Fact]
    public void IndividualAndPartialPackageSelectionsUseExistingPricing()
    {
        var input = Input();
        var partial = input with { Servicios = [input.Servicios[0], input.Servicios[2]], Descuento = 0 };
        Assert.Equal(110m, TurnoSnapshotBuilder.Build(partial).PrecioAcordado);
        var individual = input with { Servicios = [new(input.Servicios[2].Servicio, null)], Paquetes = [], Descuento = 0 };
        Assert.Equal(20m, TurnoSnapshotBuilder.Build(individual).PrecioAcordado);
    }

    [Theory]
    [InlineData("0", "0", "170")]
    [InlineData("100", "0", "20")]
    [InlineData("0", "100", "0")]
    [InlineData("100", "100", "0")]
    [InlineData("12.5", "7.5", "139.90625")]
    public void DiscountBoundariesAndFractionalPrices(string package, string overall, string expected)
    {
        var input = Input();
        input.Paquetes[0].PorcentajeDescuento = decimal.Parse(package, System.Globalization.CultureInfo.InvariantCulture);
        var result = TurnoSnapshotBuilder.Build(input with { Descuento = decimal.Parse(overall, System.Globalization.CultureInfo.InvariantCulture) });
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), result.PrecioAcordado);
        Assert.Equal(result.PrecioAcordado, result.DesglosePrecio!.Total);
        Assert.Equal(150m - 150m * decimal.Parse(package, System.Globalization.CultureInfo.InvariantCulture) / 100m + 20m, result.DesglosePrecio.Subtotal);
        Assert.Equal(new decimal[] { 100, 50, 20 }, result.Vehiculos.Single().Servicios.Select(s => s.Precio));
    }

    [Firebase.Tests.Integration.LocalFirestoreFact]
    public void FractionalPricingMatchesPublicLavadoCalculation()
    {
        var db = new Google.Cloud.Firestore.FirestoreDbBuilder { ProjectId = "demo-snapshot-pricing", EmulatorDetection = Google.Api.Gax.EmulatorDetection.EmulatorOnly }.Build();
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var services = new ServicioService(db);
        var lavado = new LavadoService(db, services, new PaqueteServicioService(db, services, cache),
            new PersonalService(db), new ConfiguracionService(db, cache));
        var input = Input();
        input.Servicios[0].Servicio.Precio = 17.33m;
        input.Servicios[1].Servicio.Precio = 23.47m;
        input.Servicios[2].Servicio.Precio = 6.19m;
        input.Paquetes[0].PorcentajeDescuento = 12.75m;
        input = input with { Descuento = 7.25m };
        var washServices = input.Servicios.Select(s => new ServicioEnLavado { ServicioId = s.Servicio.Id, Precio = s.Servicio.Precio, PaqueteId = s.PaqueteId }).ToList();
        var expected = lavado.CalcularPrecioCreacion(washServices, input.Paquetes.ToDictionary(p => p.Id), input.Descuento);
        Assert.Null(expected.Error);
        var snapshot = TurnoSnapshotBuilder.Build(input);
        Assert.Equal(expected.Precio, snapshot.PrecioAcordado);
        Assert.Equal(expected.PrecioOriginal, snapshot.DesglosePrecio!.Subtotal);
        Assert.Equal(expected.Precio, snapshot.DesglosePrecio.Total);
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("100.01")]
    public void RejectsOutOfRangeDiscounts(string value)
    {
        var number = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        var input = Input();
        Assert.Throws<ArgumentException>(() => TurnoSnapshotBuilder.Build(input with { Descuento = number }));
        input.Paquetes[0].PorcentajeDescuento = number;
        Assert.Throws<ArgumentException>(() => TurnoSnapshotBuilder.Build(input));
    }

    [Fact]
    public void RejectsInvalidDomainInputs()
    {
        var input = Input();
        input.Servicios[0].Servicio.Estado = "Inactivo";
        Assert.Throws<ArgumentException>(() => TurnoSnapshotBuilder.Build(input));
        input = Input(); input.Servicios[0].Servicio.TipoVehiculo = "Camion";
        Assert.Throws<ArgumentException>(() => TurnoSnapshotBuilder.Build(input));
        input = Input(); input.TipoVehiculo.CantidadEmpleadosRequeridos = 0;
        Assert.Throws<ArgumentException>(() => TurnoSnapshotBuilder.Build(input));
        input = Input(); input.Servicios[0].Servicio.TiempoEstimado = 0;
        Assert.Throws<ArgumentException>(() => TurnoSnapshotBuilder.Build(input));
        input = Input(); input.Servicios[0].Servicio.Precio = -1;
        Assert.Throws<ArgumentException>(() => TurnoSnapshotBuilder.Build(input));
        input = Input(); input.Paquetes[0].ServiciosIds.Remove("s1");
        Assert.Throws<ArgumentException>(() => TurnoSnapshotBuilder.Build(input));
        Assert.Throws<ArgumentException>(() => TurnoSnapshotBuilder.Build(Input() with { Descuento = 101 }));
        Assert.Throws<ArgumentException>(() => TurnoSnapshotBuilder.Build(Input() with { InicioUtc = DateTime.SpecifyKind(Start, DateTimeKind.Unspecified) }));
    }
}

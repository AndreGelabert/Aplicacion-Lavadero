using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Firebase.Tests.Integration;

public class LavadoFinalizacionTransactionTests
{
    [LocalFirestoreFact]
    public async Task InicioParaleloRechazaSinModificarLavado()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-wash-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var servicios = new ServicioService(db);
        var service = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache), new PersonalService(db), new ConfiguracionService(db, cache));
        var lavado = new Lavado { Id = "wash", Estado = "EnProceso", ClienteId = "client", VehiculoId = "vehicle", FechaCreacion = DateTime.UtcNow,
            Servicios = [new ServicioEnLavado { ServicioId = "service", Estado = "EnProceso", Etapas = [
                new EtapaEnLavado { EtapaId = "first", Nombre = "Enjuage inicial", Estado = "EnProceso" },
                new EtapaEnLavado { EtapaId = "second", Nombre = "Segunda", Estado = "Pendiente" } ] }] };
        await service.ActualizarLavado(lavado);
        var reference = db.Collection("lavados").Document("wash");
        var before = await reference.GetSnapshotAsync();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.IniciarEtapa("wash", "service", "second"));
        Assert.Equal("No se puede iniciar esta etapa porque la etapa 'Enjuage inicial' está en proceso. Las etapas deben completarse en orden.", error.Message);
        var after = await reference.GetSnapshotAsync();
        Assert.Equal(before.UpdateTime, after.UpdateTime);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before.ToDictionary()), System.Text.Json.JsonSerializer.Serialize(after.ToDictionary()));
    }

    [LocalFirestoreFact]
    public async Task IdInvalidoConservaErrorOriginalYSinEscrituras()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-wash-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var servicios = new ServicioService(db);
        var service = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache), new PersonalService(db), new ConfiguracionService(db, cache));
        var reference = db.Collection("lavados").Document("sentinel");
        await reference.SetAsync(new Dictionary<string, object> { ["CampoAjeno"] = "conservar" });
        var before = await reference.GetSnapshotAsync();
        foreach (var id in new string?[] { null, "", " \t " })
        {
            var error = await Record.ExceptionAsync(() => service.FinalizarLavado(id!));
            var after = await reference.GetSnapshotAsync();
            var all = await db.Collection("lavados").GetSnapshotAsync();
            Assert.Equal(before.UpdateTime, after.UpdateTime);
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before.ToDictionary()), System.Text.Json.JsonSerializer.Serialize(after.ToDictionary()));
            Assert.Single(all.Documents);
            var argument = Assert.IsType<ArgumentException>(error);
            Assert.Equal("id", argument.ParamName);
            Assert.Equal(new ArgumentException("El ID del lavado no puede estar vacío", "id").Message, argument.Message);
        }
    }

    [LocalFirestoreFact]
    public async Task FinalizacionPreservaCamposYPagoYPersonal()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-wash-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var servicios = new ServicioService(db);
        var service = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache), new PersonalService(db), new ConfiguracionService(db, cache));
        await db.Collection("empleados").Document("staff").SetAsync(new Dictionary<string, object> { ["Uid"] = "staff", ["Nombre"] = "Staff", ["Estado"] = "Activo" });
        var lavado = new Lavado { Id = "wash", Estado = "EnProceso", ClienteId = "client", VehiculoId = "vehicle", Precio = 100, FechaCreacion = DateTime.UtcNow, EmpleadosAsignadosIds = new() { "staff" }, EmpleadosAsignadosNombres = new() { "Staff" }, Servicios = new() { new ServicioEnLavado { ServicioId = "service", Estado = "Realizado" } } };
        await service.ActualizarLavado(lavado);
        await service.RegistrarPago(lavado.Id, 100, "Efectivo");
        var reference = db.Collection("lavados").Document(lavado.Id);
        await reference.UpdateAsync("CampoAjeno", "conservar");
        var before = (await reference.GetSnapshotAsync()).ToDictionary();
        before.Remove("Estado"); before.Remove("TiempoFinalizacion");
        await service.FinalizarLavado(lavado.Id);
        var result = (await service.ObtenerLavado(lavado.Id))!;
        Assert.Equal("Realizado", result.Estado);
        Assert.NotNull(result.TiempoFinalizacion);
        var after = (await reference.GetSnapshotAsync()).ToDictionary();
        after.Remove("Estado"); after.Remove("TiempoFinalizacion");
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before), System.Text.Json.JsonSerializer.Serialize(after));
        await service.FinalizarLavado(lavado.Id);
        var again = (await service.ObtenerLavado(lavado.Id))!;
        Assert.Equal("Realizado", again.Estado);
        Assert.True(again.TiempoFinalizacion >= result.TiempoFinalizacion);
    }

    [LocalFirestoreFact]
    public async Task PendienteSinCanceladosRechazaSinEscribir()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-wash-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var servicios = new ServicioService(db);
        var service = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache), new PersonalService(db), new ConfiguracionService(db, cache));
        var lavado = new Lavado { Id = "wash", Estado = "EnProceso", ClienteId = "client", VehiculoId = "vehicle", Precio = 100, FechaCreacion = DateTime.UtcNow, Servicios = new() { new ServicioEnLavado { ServicioId = "service", Estado = "Pendiente" } } };
        await service.ActualizarLavado(lavado);
        var reference = db.Collection("lavados").Document(lavado.Id);
        var before = await reference.GetSnapshotAsync();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.FinalizarLavado(lavado.Id));
        Assert.Equal("No se puede finalizar el lavado porque hay servicios pendientes o en proceso.", error.Message);
        var after = await reference.GetSnapshotAsync();
        Assert.Equal(before.UpdateTime, after.UpdateTime);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before.ToDictionary()), System.Text.Json.JsonSerializer.Serialize(after.ToDictionary()));
    }

    [LocalFirestoreFact]
    public async Task CanceladoConPendienteProduceParcialComoAntes()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-wash-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var servicios = new ServicioService(db);
        var service = new LavadoService(db, servicios, new PaqueteServicioService(db, servicios, cache), new PersonalService(db), new ConfiguracionService(db, cache));
        var lavado = new Lavado { Id = "wash", Estado = "EnProceso", ClienteId = "client", VehiculoId = "vehicle", Precio = 100, FechaCreacion = DateTime.UtcNow, Servicios = new() { new ServicioEnLavado { ServicioId = "one", Estado = "Pendiente" }, new ServicioEnLavado { ServicioId = "two", Estado = "Cancelado" } } };
        await service.ActualizarLavado(lavado);
        await service.FinalizarLavado(lavado.Id);
        var result = (await service.ObtenerLavado(lavado.Id))!;
        Assert.Equal("RealizadoParcialmente", result.Estado);
        Assert.NotNull(result.TiempoFinalizacion);
        Assert.Equal(new[] { "Pendiente", "Cancelado" }, result.Servicios.Select(s => s.Estado));
    }
}

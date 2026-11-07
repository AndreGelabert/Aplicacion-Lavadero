using System.Security.Claims;
using System.Text.Json;
using Firebase.Models;
using Firebase.Services;
using FirebaseLoginCustom.Controllers;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Firebase.Tests.Integration;

public class AuditWashCaptureTests
{
    [LocalFirestoreFact]
    public async Task PaymentAndLifecycleCapturePersistedSnapshotsAndRejectInvalidPayment()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-wash-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var services = new ServicioService(db); var packages = new PaqueteServicioService(db, services, cache); var staff = new PersonalService(db); var config = new ConfiguracionService(db, cache);
        var washes = new LavadoService(db, services, packages, staff, config);
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "actor"), new Claim(ClaimTypes.Email, "actor@example.invalid") }, "test");
        var controller = new LavadosController(NullLogger<LavadosController>.Instance, new AuditService(db), washes, services, packages, new ClienteService(db), new VehiculoService(db), staff, config, new TipoVehiculoService(db), new TipoDocumentoService(db), null!) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } } };
        var wash = new Lavado { Id = "wash", Estado = "EnProceso", ClienteId = "client", VehiculoId = "vehicle", VehiculoPatente = "ABC123", Precio = 100, FechaCreacion = DateTime.UtcNow, Servicios = new() { new ServicioEnLavado { ServicioId = "service", Estado = "Pendiente" } } };
        await washes.ActualizarLavado(wash);
        var initial = AuditCampos.CapturarLavado((await washes.ObtenerLavado(wash.Id))!);
        Assert.False(Success(await controller.RegistrarPago(wash.Id, 0, "Efectivo", null)));
        Assert.Equal(initial.OrderBy(x => x.Key), AuditCampos.CapturarLavado((await washes.ObtenerLavado(wash.Id))!).OrderBy(x => x.Key));
        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
        Assert.True(Success(await controller.IniciarServicio(wash.Id, "service")));
        var started = AuditCampos.CapturarLavado((await washes.ObtenerLavado(wash.Id))!);
        await Event(db, "Inicio de servicio en lavado", initial, started, 1);
        Assert.True(Success(await controller.RegistrarPago(wash.Id, 100, "Efectivo", "nota")));
        var paid = AuditCampos.CapturarLavado((await washes.ObtenerLavado(wash.Id))!);
        await Event(db, "Registro de pago en lavado", started, paid, 2);
        Assert.True(Success(await controller.FinalizarServicio(wash.Id, "service")));
        var finished = AuditCampos.CapturarLavado((await washes.ObtenerLavado(wash.Id))!);
        await Event(db, "Finalización de servicio en lavado", paid, finished, 3);
        Assert.True(Success(await controller.RegistrarRetiro(wash.Id)));
        var pickedUp = AuditCampos.CapturarLavado((await washes.ObtenerLavado(wash.Id))!);
        await Event(db, "Retiro de vehículo", finished, pickedUp, 4);
    }
    [LocalFirestoreFact]
    public async Task PickupRejectsMissingUnpaidAndRepeatedButAllowsCancelledWithoutPayment()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-wash-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var services = new ServicioService(db); var packages = new PaqueteServicioService(db, services, cache); var staff = new PersonalService(db); var config = new ConfiguracionService(db, cache);
        var washes = new LavadoService(db, services, packages, staff, config);
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "actor"), new Claim(ClaimTypes.Email, "actor@example.invalid") }, "test");
        var controller = new LavadosController(NullLogger<LavadosController>.Instance, new AuditService(db), washes, services, packages, new ClienteService(db), new VehiculoService(db), staff, config, new TipoVehiculoService(db), new TipoDocumentoService(db), null!) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } } };
        async Task AssertFailure(string id, string message)
        {
            var json = JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(await controller.RegistrarRetiro(id)).Value);
            Assert.False(json.GetProperty("success").GetBoolean());
            Assert.Equal(message, json.GetProperty("message").GetString());
        }
        await AssertFailure("absent", "Lavado no encontrado.");
        Assert.Null(await washes.ObtenerLavado("absent"));
        var wash = new Lavado { Id = "wash", Estado = "EnProceso", ClienteId = "client", VehiculoId = "vehicle", VehiculoPatente = "ABC123", FechaCreacion = DateTime.UtcNow, Servicios = new() { new ServicioEnLavado { ServicioId = "service", Estado = "Pendiente" } } };
        await washes.ActualizarLavado(wash);
        var initial = AuditCampos.CapturarLavado((await washes.ObtenerLavado(wash.Id))!);
        await AssertFailure(wash.Id, "El lavado debe estar pagado antes de permitir el retiro del vehículo.");
        Assert.Equal(initial.OrderBy(x => x.Key), AuditCampos.CapturarLavado((await washes.ObtenerLavado(wash.Id))!).OrderBy(x => x.Key));
        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
        wash.Estado = "Cancelado";
        await washes.ActualizarLavado(wash);
        var before = AuditCampos.CapturarLavado((await washes.ObtenerLavado(wash.Id))!);
        var start = DateTime.UtcNow;
        var result = JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(await controller.RegistrarRetiro(wash.Id)).Value);
        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.Equal("Retiro registrado correctamente.", result.GetProperty("message").GetString());
        var persisted = (await washes.ObtenerLavado(wash.Id))!;
        Assert.Equal(Lavado.EstadosRetiro.Retirado, persisted.EstadoRetiro);
        Assert.InRange(persisted.FechaRetiro!.Value, start.AddMilliseconds(-1), DateTime.UtcNow);
        var after = AuditCampos.CapturarLavado(persisted);
        await Event(db, "Retiro de vehículo", before, after, 1);
        await AssertFailure(wash.Id, "El vehículo ya fue retirado.");
        Assert.Equal(after.OrderBy(x => x.Key), AuditCampos.CapturarLavado((await washes.ObtenerLavado(wash.Id))!).OrderBy(x => x.Key));
        await Event(db, "Retiro de vehículo", before, after, 1);
    }
    private static bool Success(IActionResult result) => JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(result).Value).GetProperty("success").GetBoolean();
    private static async Task Event(FirestoreDb db, string action, Dictionary<string,string> before, Dictionary<string,string> after, int count)
    {
        var docs = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents;
        Assert.Equal(count, docs.Count);
        var doc = Assert.Single(docs, d => d.GetValue<string>("Action") == action);
        Assert.Equal("Lavado", doc.GetValue<string>("TargetType"));
        Assert.Equal("wash", doc.GetValue<string>("TargetId"));
        Assert.Equal("ABC123", doc.GetValue<string>("NombreHistorico"));
        Assert.Equal(before.OrderBy(x => x.Key), doc.GetValue<Dictionary<string,string>>("Antes").OrderBy(x => x.Key));
        Assert.Equal(after.OrderBy(x => x.Key), doc.GetValue<Dictionary<string,string>>("Despues").OrderBy(x => x.Key));
    }
}

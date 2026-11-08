using System.Security.Claims;
using System.Text.Json;
using Firebase.Models;
using Firebase.Services;
using FirebaseLoginCustom.Controllers;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Firebase.Tests.Integration;

public class LavadoAssociatedClientRefactorTests
{
    [LocalFirestoreFact]
    public async Task RejectionsPreserveClientsVehicleAndAudit()
    {
        await using var f = await Fixture.Create();
        var cases = new (Action<LavadosController.RegistroClienteAsociadoRequest> Change, string Message)[]
        {
            (r => r.VehiculoId = " ", "Selecciona un vehículo e ingresa su clave de asociación."),
            (r => r.ClaveAsociacion = " ", "Selecciona un vehículo e ingresa su clave de asociación."),
            (r => r.Nombre = " ", "Completa todos los campos obligatorios."),
            (r => r.VehiculoId = "missing", "El vehículo seleccionado ya no está disponible."),
            (r => r.VehiculoId = "inactive", "El vehículo seleccionado ya no está disponible."),
            (r => r.ClaveAsociacion = "incorrecta", "La clave de asociación no es válida para este vehículo."),
            (r => r.VehiculoId = "nokey", "La clave de asociación no es válida para este vehículo."),
            (r => r.TipoDocumento = "missing", "Selecciona un tipo de documento válido."),
            (r => r.TipoDocumento = "Vacio", "Selecciona un tipo de documento válido."),
            (r => r.NumeroDocumento = "12x", "El número de documento debe respetar el formato: nnn."),
            (r => r.NumeroDocumento = "123", "Ya existe un cliente con este documento."),
            (r => r.Nombre = "12a", "El nombre debe contener al menos 3 letras")
        };
        var before = await f.State();
        await AssertRejected(f, null!, "Selecciona un vehículo e ingresa su clave de asociación.", before);
        foreach (var (change, message) in cases)
        {
            var request = Request();
            change(request);
            await AssertRejected(f, request, message, before);
        }
        foreach (var field in new[] { "TipoDocumento", "NumeroDocumento", "Apellido", "Telefono", "Email" })
        {
            var request = Request();
            typeof(LavadosController.RegistroClienteAsociadoRequest).GetProperty(field)!.SetValue(request, " ");
            await AssertRejected(f, request, "Completa todos los campos obligatorios.", before);
        }
    }

    [LocalFirestoreFact]
    public async Task SuccessPersistsBothSidesAndAuditAndReturnsProjection()
    {
        await using var f = await Fixture.Create();
        var request = Request();
        request.Nombre = " Ana "; request.Apellido = " Perez "; request.Telefono = " 1234567890 "; request.Email = " ana@example.com ";
        var json = Json(await f.Controller.RegistrarClienteAsociado(request));
        Assert.Equal(new[] { "success", "message", "cliente" }, json.EnumerateObject().Select(p => p.Name));
        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.Equal("Cliente registrado y asociado correctamente.", json.GetProperty("message").GetString());
        var projection = json.GetProperty("cliente");
        Assert.Equal(new[] { "id", "nombre", "documento" }, projection.EnumerateObject().Select(p => p.Name));
        var id = projection.GetProperty("id").GetString()!;
        Assert.False(string.IsNullOrWhiteSpace(id));
        Assert.Equal("Ana Perez", projection.GetProperty("nombre").GetString());
        Assert.Equal("DNI: 456", projection.GetProperty("documento").GetString());
        var client = await new ClienteService(f.Db).ObtenerCliente(id);
        Assert.NotNull(client);
        Assert.Equal("DNI", client.TipoDocumento); Assert.Equal("456", client.NumeroDocumento);
        Assert.Equal("Ana", client.Nombre); Assert.Equal("Perez", client.Apellido);
        Assert.Equal("1234567890", client.Telefono); Assert.Equal("ana@example.com", client.Email);
        Assert.Equal("Activo", client.Estado); Assert.Equal(new[] { "vehicle" }, client.VehiculosIds);
        var vehicle = await new VehiculoService(f.Db).ObtenerVehiculo("vehicle");
        Assert.Equal("owner", vehicle.ClienteId);
        Assert.Equal(new[] { "owner", id }, vehicle.ClientesIds);
        Assert.Equal(2, (await f.Db.Collection("clientes").GetSnapshotAsync()).Count);
        var auditDoc = Assert.Single((await f.Db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
        var audit = await new AuditService(f.Db).ObtenerDetalle(auditDoc.Id);
        Assert.Equal("actor", audit.UserId); Assert.Equal("actor@example.com", audit.UserEmail);
        Assert.Equal("Creacion de cliente y asociacion a vehiculo", audit.Action);
        Assert.Equal(id, audit.TargetId); Assert.Equal("Cliente", audit.TargetType);
        Assert.Empty(audit.Antes);
        Assert.Equal(AuditCampos.CapturarCliente(client).OrderBy(x => x.Key), audit.Despues.OrderBy(x => x.Key));
    }

    private static async Task AssertRejected(Fixture f, LavadosController.RegistroClienteAsociadoRequest request, string message, string before)
    {
        var json = Json(await f.Controller.RegistrarClienteAsociado(request));
        Assert.Equal(new[] { "success", "message" }, json.EnumerateObject().Select(p => p.Name));
        Assert.False(json.GetProperty("success").GetBoolean());
        Assert.Equal(message, json.GetProperty("message").GetString());
        Assert.Equal(before, await f.State());
    }

    private static LavadosController.RegistroClienteAsociadoRequest Request() => new()
    {
        VehiculoId = "vehicle", ClaveAsociacion = "clave", TipoDocumento = "DNI", NumeroDocumento = "456",
        Nombre = "Ana", Apellido = "Perez", Telefono = "1234567890", Email = "ana@example.com"
    };
    private static JsonElement Json(IActionResult result) => JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(result).Value);

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly MemoryCache cache = new(new MemoryCacheOptions());
        public FirestoreDb Db { get; }
        public LavadosController Controller { get; }
        private Fixture(FirestoreDb db)
        {
            Db = db;
            var services = new ServicioService(db); var packages = new PaqueteServicioService(db, services, cache);
            var staff = new PersonalService(db); var config = new ConfiguracionService(db, cache);
            var washes = new LavadoService(db, services, packages, staff, config);
            var clients = new ClienteService(db); var vehicles = new VehiculoService(db); var types = new TipoVehiculoService(db);
            Controller = new LavadosController(NullLogger<LavadosController>.Instance, new AuditService(db), washes,
                services, packages, clients, vehicles, staff, config, types, new TipoDocumentoService(db),
                new LavadoCreationService(clients, vehicles, services, packages, types, washes),
                new LavadoAssociatedClientService(clients, vehicles, new TipoDocumentoService(db)))
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "actor"), new Claim(ClaimTypes.Email, "actor@example.com")
                }, "test")) } }
            };
        }
        public static async Task<Fixture> Create()
        {
            var f = new Fixture(new FirestoreDbBuilder { ProjectId = "demo-associated-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build());
            await f.Db.Collection("clientes").Document("owner").SetAsync(new Cliente { Id = "owner", TipoDocumento = "DNI", NumeroDocumento = "123", Nombre = "Juan", Apellido = "Perez", Telefono = "1234567890", Email = "juan@example.com" });
            foreach (var (id, state, hash) in new[] { ("vehicle", "Activo", VehiculoService.HashClaveAsociacion("clave")), ("inactive", "Inactivo", VehiculoService.HashClaveAsociacion("clave")), ("nokey", "Activo", "") })
                await f.Db.Collection("vehiculos").Document(id).SetAsync(new Vehiculo { Id = id, Patente = "ABC123", TipoVehiculo = "Auto", Marca = "Marca", Modelo = "Modelo", Color = "Azul", ClienteId = "owner", ClientesIds = new() { "owner" }, Estado = state, ClaveAsociacionHash = hash });
            await f.Db.Collection("tiposDocumento").Document("dni").SetAsync(new Dictionary<string, object> { ["Nombre"] = "DNI", ["Formato"] = "nnn" });
            await f.Db.Collection("tiposDocumento").Document("empty").SetAsync(new Dictionary<string, object> { ["Nombre"] = "Vacio", ["Formato"] = "" });
            return f;
        }
        public async Task<string> State()
        {
            var state = new SortedDictionary<string, object>();
            foreach (var name in new[] { "clientes", "vehiculos", "registros_auditoria" })
                foreach (var doc in (await Db.Collection(name).GetSnapshotAsync()).Documents.OrderBy(d => d.Id))
                    state[name + "/" + doc.Id] = doc.ToDictionary();
            return JsonSerializer.Serialize(state);
        }
        public async ValueTask DisposeAsync()
        {
            foreach (var name in new[] { "clientes", "vehiculos", "tiposDocumento", "registros_auditoria" })
                foreach (var doc in (await Db.Collection(name).GetSnapshotAsync()).Documents)
                    await doc.Reference.DeleteAsync();
            cache.Dispose();
        }
    }
}

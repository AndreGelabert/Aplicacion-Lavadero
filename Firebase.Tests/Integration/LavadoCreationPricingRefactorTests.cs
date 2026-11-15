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

public class LavadoCreationPricingRefactorTests
{
    [LocalFirestoreFact]
    public async Task CreationPreservesIndividualAndSelectedPackagePrices()
    {
        foreach (var (items, discount, original, final) in new[]
        {
            (new[] { Item("one"), Item("two") }, 0m, 150.25m, 150.25m),
            (new[] { Item("one", "pack"), Item("two") }, 12.5m, 130.20m, 113.925m),
            (new[] { Item("one", "pack"), Item("two", "pack"), Item("three") }, 10m, 145.20m, 130.68m)
        })
        {
            using var fixture = await Fixture.Create();
            var response = Json(await fixture.Controller.CrearLavadoAjax(Request(items, discount)));
            Assert.True(response.GetProperty("success").GetBoolean(), response.GetRawText());
            Assert.Equal("Lavado creado correctamente.", response.GetProperty("message").GetString());
            var id = Assert.Single(response.GetProperty("lavadosIds").EnumerateArray()).GetString();
            var lavado = await fixture.Washes.ObtenerLavado(id!);
            Assert.NotNull(lavado);
            Assert.Equal(original, lavado.PrecioOriginal);
            Assert.Equal(final, lavado.Precio);
            Assert.Equal(discount, lavado.Descuento);
            Assert.Equal(items.Select(x => x.ServicioId), lavado.Servicios.Select(x => x.ServicioId));
            Assert.Equal(items.Select(x => x.PaqueteId ?? string.Empty), lavado.Servicios.Select(x => x.PaqueteId));
            Assert.Equal("EnProceso", lavado.Servicios[0].Estado);
            Assert.Equal(1, (await fixture.Db.Collection("lavados").GetSnapshotAsync()).Count);
        }
    }

    [LocalFirestoreFact]
    public async Task InvalidPackageSelectionsKeepExistingErrorAndDoNotPersist()
    {
        using var fixture = await Fixture.Create();
        foreach (var items in new[]
        {
            new[] { Item("one", "missing") },
            new[] { Item("three", "pack") },
            new[] { Item("one", "pack"), Item("one", "pack") }
        })
        {
            var response = Json(await fixture.Controller.CrearLavadoAjax(Request(items, 10m)));
            Assert.False(response.GetProperty("success").GetBoolean());
            Assert.Equal(items[0].PaqueteId == "missing"
                ? "Uno de los paquetes seleccionados ya no está disponible."
                : "Los servicios seleccionados no corresponden al paquete indicado.", response.GetProperty("message").GetString());
            Assert.Empty((await fixture.Db.Collection("lavados").GetSnapshotAsync()).Documents);
        }
    }

    [LocalFirestoreFact]
    public async Task CreationPersistsDefaultCappedAndWithinLimitStaffAssignments()
    {
        foreach (var (requirement, maximum, expected) in new (int?, int, int)[]
        {
            (null, 3, 1),
            (4, 2, 2),
            (2, 3, 2),
            (2, 2, 2)
        })
        {
            using var fixture = await Fixture.Create();
            fixture.SetStaffMaximum(maximum);
            if (requirement.HasValue)
                await fixture.Db.Collection("tiposVehiculos").Document("auto").SetAsync(new Dictionary<string, object>
                {
                    ["Nombre"] = "Auto", ["CantidadEmpleadosRequeridos"] = requirement.Value
                });
            var staff = new Dictionary<string, string> { ["staff"] = "Empleado", ["staff2"] = "Segundo", ["staff3"] = "Tercero" };
            foreach (var id in new[] { "staff2", "staff3" })
                await fixture.Db.Collection("empleados").Document(id).SetAsync(new Dictionary<string, object>
                {
                    ["Nombre"] = staff[id], ["Email"] = id + "@example.com", ["Rol"] = "Empleado", ["Estado"] = "Activo"
                });

            var response = Json(await fixture.Controller.CrearLavadoAjax(Request(new[] { Item("one") }, 0m)));
            Assert.True(response.GetProperty("success").GetBoolean(), response.GetRawText());
            Assert.Equal("Lavado creado correctamente.", response.GetProperty("message").GetString());
            var idCreated = Assert.Single(response.GetProperty("lavadosIds").EnumerateArray()).GetString();
            var lavado = await fixture.Washes.ObtenerLavado(idCreated!);
            Assert.NotNull(lavado);
            Assert.Equal(expected, lavado.CantidadEmpleadosRequeridos);
            Assert.Equal(expected, lavado.EmpleadosAsignadosIds.Count);
            Assert.Equal(expected, lavado.EmpleadosAsignadosNombres.Count);
            Assert.Equal(expected, lavado.EmpleadosAsignadosIds.Distinct().Count());
            for (var i = 0; i < expected; i++)
            {
                Assert.True(staff.TryGetValue(lavado.EmpleadosAsignadosIds[i], out var name));
                Assert.Equal(name, lavado.EmpleadosAsignadosNombres[i]);
            }
            Assert.Equal(1, (await fixture.Db.Collection("lavados").GetSnapshotAsync()).Count);
        }
    }

    [LocalFirestoreFact]
    public async Task CreationCopiesOrderedServicesAndStagesAndStartsOnlyTheFirst()
    {
        foreach (var firstHasStages in new[] { false, true })
        {
            using var fixture = await Fixture.Create();
            fixture.Controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "actor"), new Claim(ClaimTypes.Email, "actor@example.com")
            }, "test"));
            var stagedId = firstHasStages ? "two" : "one";
            await fixture.Db.Collection("servicios").Document(stagedId).UpdateAsync(new Dictionary<string, object>
            {
                ["TiempoEstimado"] = 23,
                ["Etapas"] = new[]
                {
                    new Dictionary<string, object> { ["Id"] = "z-stage", ["Nombre"] = "Primera" },
                    new Dictionary<string, object> { ["Id"] = "a-stage", ["Nombre"] = "Segunda" }
                }
            });
            var before = (await fixture.Db.Collection("servicios").Document(stagedId).GetSnapshotAsync()).UpdateTime;
            var response = Json(await fixture.Controller.CrearLavadoAjax(Request(new[] { Item("two", "pack"), Item("one") }, 0m)));
            Assert.True(response.GetProperty("success").GetBoolean(), response.GetRawText());
            Assert.Equal("Lavado creado correctamente.", response.GetProperty("message").GetString());
            var id = Assert.Single(response.GetProperty("lavadosIds").EnumerateArray()).GetString();
            var lavado = await fixture.Washes.ObtenerLavado(id!);
            Assert.NotNull(lavado);
            Assert.Equal("EnProceso", lavado.Estado);
            Assert.Equal(33, lavado.TiempoEstimado);
            Assert.Equal(2, lavado.Servicios.Count);
            var first = lavado.Servicios[0];
            var second = lavado.Servicios[1];
            Assert.Equal(new[] { "two", "one" }, lavado.Servicios.Select(s => s.ServicioId));
            Assert.Equal(new[] { "two", "one" }, lavado.Servicios.Select(s => s.ServicioNombre));
            Assert.Equal(new[] { "two", "one" }, lavado.Servicios.Select(s => s.TipoServicio));
            Assert.Equal(new[] { 50m, 100.25m }, lavado.Servicios.Select(s => s.Precio));
            Assert.Equal(new[] { firstHasStages ? 23 : 10, firstHasStages ? 10 : 23 }, lavado.Servicios.Select(s => s.TiempoEstimado));
            Assert.Equal(new[] { 0, 1 }, lavado.Servicios.Select(s => s.Orden));
            Assert.Equal(new[] { "pack", "" }, lavado.Servicios.Select(s => s.PaqueteId));
            Assert.Equal(new[] { "Paquete", "" }, lavado.Servicios.Select(s => s.PaqueteNombre));
            Assert.Equal("EnProceso", first.Estado);
            Assert.NotNull(first.TiempoInicio);
            Assert.Null(first.TiempoFinalizacion);
            Assert.Equal("Pendiente", second.Estado);
            Assert.Null(second.TiempoInicio);
            Assert.Null(second.TiempoFinalizacion);
            var staged = firstHasStages ? first : second;
            Assert.Empty((firstHasStages ? second : first).Etapas);
            Assert.Equal(new[] { "z-stage", "a-stage" }, staged.Etapas.Select(e => e.EtapaId));
            Assert.Equal(new[] { "Primera", "Segunda" }, staged.Etapas.Select(e => e.Nombre));
            Assert.Equal(firstHasStages ? "EnProceso" : "Pendiente", staged.Etapas[0].Estado);
            if (firstHasStages) Assert.NotNull(staged.Etapas[0].TiempoInicio);
            else Assert.Null(staged.Etapas[0].TiempoInicio);
            Assert.Equal("Pendiente", staged.Etapas[1].Estado);
            Assert.Null(staged.Etapas[1].TiempoInicio);
            Assert.All(staged.Etapas, e => Assert.Null(e.TiempoFinalizacion));
            Assert.Equal(before, (await fixture.Db.Collection("servicios").Document(stagedId).GetSnapshotAsync()).UpdateTime);
            Assert.Equal(1, (await fixture.Db.Collection("lavados").GetSnapshotAsync()).Count);
            var auditDoc = Assert.Single((await fixture.Db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
            var audit = await new AuditService(fixture.Db).ObtenerDetalle(auditDoc.Id);
            Assert.Equal("actor", audit.UserId);
            Assert.Equal("actor@example.com", audit.UserEmail);
            Assert.Equal("Creación de lavado", audit.Action);
            Assert.Equal(id, audit.TargetId);
            Assert.Equal("Lavado", audit.TargetType);
            Assert.Empty(audit.Antes);
            Assert.Equal(AuditCampos.CapturarLavado(lavado).OrderBy(x => x.Key), audit.Despues.OrderBy(x => x.Key));
        }
    }

    [LocalFirestoreFact]
    public async Task CreationGuardsReturnExactErrorsWithoutWritesOrAudit()
    {
        foreach (var (vehicle, service, message) in new[]
        {
            ("missing", "one", "Vehículo con ID missing no encontrado."),
            ("vehicle", "missing", "Servicio con ID missing no encontrado."),
            ("vehicle", "incompatible", "El servicio 'incompatible' no es compatible con el tipo de vehículo 'Auto'.")
        })
        {
            using var fixture = await Fixture.Create();
            SetAuditActor(fixture);
            await fixture.Db.Collection("servicios").Document("incompatible").SetAsync(new Dictionary<string, object>
            {
                ["Nombre"] = "incompatible", ["Tipo"] = "incompatible", ["TipoVehiculo"] = "moto",
                ["Precio"] = 10.0, ["TiempoEstimado"] = 10, ["Descripcion"] = "Servicio", ["Estado"] = "Activo"
            });
            var request = Request(new[] { Item(service) }, 0m);
            request.VehiculosServicios[0].VehiculoId = vehicle;
            AssertError(Json(await fixture.Controller.CrearLavadoAjax(request)), message);
            Assert.Empty((await fixture.Db.Collection("lavados").GetSnapshotAsync()).Documents);
            Assert.Empty((await fixture.Db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
        }
    }

    [LocalFirestoreFact]
    public async Task SecondVehicleFailureLeavesNoWashOrAudit()
    {
        using var fixture = await Fixture.Create();
        SetAuditActor(fixture);
        var request = Request(new[] { Item("one") }, 0m);
        request.VehiculosServicios.Add(new VehiculoServiciosRequest { VehiculoId = "missing", Servicios = new() { Item("one") } });

        AssertError(Json(await fixture.Controller.CrearLavadoAjax(request)), "Vehículo con ID missing no encontrado.");
        Assert.Empty((await fixture.Db.Collection("lavados").GetSnapshotAsync()).Documents);
        Assert.Empty((await fixture.Db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task MissingPackageOnSecondVehicleRejectsBeforeAnyWrite()
    {
        using var fixture = await Fixture.Create();
        SetAuditActor(fixture);
        var request = Request(new[] { Item("one") }, 0m);
        request.VehiculosServicios.Add(new VehiculoServiciosRequest { VehiculoId = "missing", Servicios = new() { Item("one", "missing") } });

        AssertError(Json(await fixture.Controller.CrearLavadoAjax(request)), "Uno de los paquetes seleccionados ya no está disponible.");
        Assert.Empty((await fixture.Db.Collection("lavados").GetSnapshotAsync()).Documents);
        Assert.Empty((await fixture.Db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task CreationRejectsUnavailableInitialResourcesWithoutPartialReservation()
    {
        foreach (var staged in new[] { false, true })
        foreach (var shortage in new[] { false, true })
        {
            using var fixture = await Fixture.Create();
            var stock = new StockService(fixture.Db);
            var insumos = new InsumoService(fixture.Db);
            await new TipoInsumoService(fixture.Db).AsegurarTipoHerramienta("fixture", "fixture");
            var tool = new Insumo { Nombre = "Hidro", Tipo = "Herramienta", UnidadMedida = "unidad" };
            var chemical = new Insumo { Nombre = "Jabon", Tipo = "Quimico" };
            Assert.True((await insumos.Guardar(tool, true)).EsExitoso);
            Assert.True((await insumos.Guardar(chemical, true)).EsExitoso);
            Assert.True((await stock.Ajustar(tool.Id, 1, true, "Ingreso", "actor")).EsExitoso);
            if (!shortage)
            {
                Assert.True((await stock.Ajustar(chemical.Id, 1, true, "Ingreso", "actor")).EsExitoso);
                Assert.True((await stock.ReservarHerramienta("ocupada", tool.Id, 1)).EsExitoso);
            }
            var recipe = new[] { new RecetaInsumo { InsumoId = tool.Id, DosisTeorica = 1 }, new RecetaInsumo { InsumoId = chemical.Id, DosisTeorica = 1 } };
            await fixture.Db.Collection("servicios").Document("one").UpdateAsync(staged
                ? new Dictionary<string, object> { ["Etapas"] = new[] { new Etapa { Id = "e", Nombre = "Etapa", Receta = recipe.ToList() } } }
                : new Dictionary<string, object> { ["Receta"] = recipe });
            var auditsPrevios = (await fixture.Db.Collection("registros_auditoria").GetSnapshotAsync()).Documents;
            Assert.Equal(shortage ? 4 : 5, auditsPrevios.Count);
            Assert.Single(auditsPrevios.Where(d => d.GetValue<string>("TargetType") == "TipoInsumo"));
            Assert.Equal(shortage ? 3 : 4, auditsPrevios.Count(d => d.GetValue<string>("TargetType") == "Insumo"));
            var response = Json(await fixture.Controller.CrearLavadoAjax(Request(new[] { Item("one") }, 0m)));
            AssertError(response, "No se pudo crear el lavado. Inténtalo nuevamente.");
            Assert.Empty((await fixture.Db.Collection("lavados").GetSnapshotAsync()).Documents);
            var auditsPost = (await fixture.Db.Collection("registros_auditoria").GetSnapshotAsync()).Documents;
            Assert.Equal(auditsPrevios.Select(d => (d.Id, d.GetValue<string>("Action"), d.GetValue<string>("TargetId"))).OrderBy(x => x.Id),
                auditsPost.Select(d => (d.Id, d.GetValue<string>("Action"), d.GetValue<string>("TargetId"))).OrderBy(x => x.Id));
            Assert.DoesNotContain(auditsPost, d => d.GetValue<string>("TargetType") == "Lavado");
            Assert.Equal(shortage ? 0 : 1, (await fixture.Db.Collection("reservasHerramientas").GetSnapshotAsync()).Count);
            Assert.Equal(shortage ? 0 : 1, (await insumos.Obtener(chemical.Id))!.StockActual);
            Assert.Equal(1, (await insumos.Obtener(tool.Id))!.StockActual);
            if (shortage) Assert.True((await stock.ReservarHerramienta("libre", tool.Id, 1)).EsExitoso);
            else Assert.False((await stock.ReservarHerramienta("extra", tool.Id, 1)).EsExitoso);
        }
    }

    [LocalFirestoreFact]
    public async Task BatchAssignsDistinctAvailableEmployees()
    {
        using var fixture = await Fixture.Create();
        await fixture.AddSecondVehicle();
        await fixture.AddSecondEmployee();
        var response = Json(await fixture.Controller.CrearLavadoAjax(TwoVehicles()));
        Assert.True(response.GetProperty("success").GetBoolean(), response.GetRawText());
        var ids = response.GetProperty("lavadosIds").EnumerateArray().Select(x => x.GetString()!).ToArray();
        Assert.Equal(2, ids.Length);
        var lavados = new[] { (await fixture.Washes.ObtenerLavado(ids[0]))!, (await fixture.Washes.ObtenerLavado(ids[1]))! };
        Assert.Equal(new[] { "staff", "staff2" }, lavados.SelectMany(l => l.EmpleadosAsignadosIds).OrderBy(x => x));
        Assert.All(lavados, l => Assert.Single(l.EmpleadosAsignadosIds));
        Assert.Equal(2, (await fixture.Db.Collection("lavados").GetSnapshotAsync()).Count);
    }

    [LocalFirestoreFact]
    public async Task BatchRejectsInsufficientEmployeesWithoutEffects()
    {
        using var fixture = await Fixture.Create();
        SetAuditActor(fixture);
        await fixture.AddSecondVehicle();
        var response = Json(await fixture.Controller.CrearLavadoAjax(TwoVehicles()));
        Assert.False(response.GetProperty("success").GetBoolean());
        Assert.Empty((await fixture.Db.Collection("lavados").GetSnapshotAsync()).Documents);
        Assert.Empty((await fixture.Db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
        Assert.Empty((await fixture.Db.Collection("reservasHerramientas").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task BatchAuditFailureReportsCreatedAndContinuesOtherAudits()
    {
        using var fixture = await Fixture.Create(failFirstAudit: true);
        SetAuditActor(fixture);
        await fixture.AddSecondVehicle();
        await fixture.AddSecondEmployee();
        var response = Json(await fixture.Controller.CrearLavadoAjax(TwoVehicles()));
        Assert.True(response.GetProperty("success").GetBoolean(), response.GetRawText());
        Assert.Contains("auditoría", response.GetProperty("message").GetString()!, StringComparison.OrdinalIgnoreCase);
        var ids = response.GetProperty("lavadosIds").EnumerateArray().Select(x => x.GetString()!).ToArray();
        Assert.Equal(2, ids.Length);
        Assert.Equal(2, ids.Distinct().Count());
        Assert.Equal(ids.OrderBy(x => x), (await fixture.Db.Collection("lavados").GetSnapshotAsync()).Documents.Select(x => x.Id).OrderBy(x => x));
        var auditoria = Assert.Single((await fixture.Db.Collection("registros_auditoria").GetSnapshotAsync()).Documents).ConvertTo<AuditLog>();
        Assert.Equal(ids[1], auditoria.TargetId);
        Assert.Equal("actor", auditoria.UserId);
        Assert.Equal("actor@example.com", auditoria.UserEmail);
        Assert.Equal("Creación de lavado", auditoria.Action);
    }

    private static CrearLavadoRequest TwoVehicles()
    {
        var request = Request(new[] { Item("one") }, 0m);
        request.VehiculosServicios.Add(new VehiculoServiciosRequest { VehiculoId = "vehicle2", Servicios = new() { Item("one") } });
        return request;
    }

    private static void SetAuditActor(Fixture fixture) => fixture.Controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.NameIdentifier, "actor"), new Claim(ClaimTypes.Email, "actor@example.com")
    }, "test"));

    private static void AssertError(JsonElement response, string message)
    {
        Assert.Equal(new[] { "success", "message" }, response.EnumerateObject().Select(property => property.Name));
        Assert.False(response.GetProperty("success").GetBoolean());
        Assert.Equal(message, response.GetProperty("message").GetString());
    }

    private static ServicioItemRequest Item(string id, string? package = null) => new() { ServicioId = id, PaqueteId = package, PaqueteNombre = package == null ? null : "Paquete" };
    private static CrearLavadoRequest Request(ServicioItemRequest[] items, decimal discount) => new()
    {
        ClienteId = "client", Descuento = discount,
        VehiculosServicios = new() { new VehiculoServiciosRequest { VehiculoId = "vehicle", Servicios = items.ToList() } }
    };
    private static JsonElement Json(IActionResult result) => JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(result).Value);

    private sealed class FailFirstAudit(IAuditWriter inner) : IAuditWriter
    {
        private bool _failed;
        public Task LogEvent(string userId, string userEmail, string action, string targetId, string targetType) => inner.LogEvent(userId, userEmail, action, targetId, targetType);
        public Task LogSessionEvent(string userId, string userEmail, string action, string targetId, string targetType) => inner.LogSessionEvent(userId, userEmail, action, targetId, targetType);
        public Task LogEvent(string userId, string userEmail, string action, string targetId, string targetType, Dictionary<string, string> antes, Dictionary<string, string> despues, string nombreHistorico = null!)
        {
            if (!_failed) { _failed = true; throw new InvalidOperationException("Fallo simulado de auditoría"); }
            return inner.LogEvent(userId, userEmail, action, targetId, targetType, antes, despues, nombreHistorico);
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());
        public FirestoreDb Db { get; }
        public LavadoService Washes { get; }
        public LavadosController Controller { get; }

        private Fixture(FirestoreDb db, bool failFirstAudit)
        {
            Db = db;
            var services = new ServicioService(db);
            var packages = new PaqueteServicioService(db, services, _cache);
            var staff = new PersonalService(db);
            var config = new ConfiguracionService(db, _cache);
            _cache.Set("system_configuration", new Configuracion { HorariosOperacion = new(), CapacidadMaximaConcurrente = 10 });
            Washes = new LavadoService(db, services, packages, staff, config);
            IAuditWriter audit = new AuditService(db);
            if (failFirstAudit) audit = new FailFirstAudit(audit);
            Controller = new LavadosController(NullLogger<LavadosController>.Instance, audit, Washes,
                services, packages, new ClienteService(db), new VehiculoService(db), staff, config,
                new TipoVehiculoService(db), new TipoDocumentoService(db),
                new LavadoCreationService(new ClienteService(db), new VehiculoService(db), services, packages, new TipoVehiculoService(db), Washes),
                new LavadoAssociatedClientService(new ClienteService(db), new VehiculoService(db), new TipoDocumentoService(db)))
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "actor") }, "test"))
                } }
            };
        }

        public void SetStaffMaximum(int maximum) => _cache.Set("system_configuration", new Configuracion
        {
            HorariosOperacion = new(), CapacidadMaximaConcurrente = 10, EmpleadosMaximosPorLavado = maximum
        });

        public Task AddSecondVehicle() => Db.Collection("vehiculos").Document("vehicle2").SetAsync(new Vehiculo { Id = "vehicle2", Patente = "DEF456", TipoVehiculo = "Auto", Marca = "Marca", Modelo = "Modelo", Color = "Azul", ClienteId = "client" });
        public Task AddSecondEmployee() => Db.Collection("empleados").Document("staff2").SetAsync(new Dictionary<string, object> { ["Nombre"] = "Segundo", ["Email"] = "staff2@example.com", ["Rol"] = "Empleado", ["Estado"] = "Activo" });

        public static async Task<Fixture> Create(bool failFirstAudit = false)
        {
            var fixture = new Fixture(new FirestoreDbBuilder { ProjectId = "demo-washprice-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build(), failFirstAudit);
            var db = fixture.Db;
            await db.Collection("clientes").Document("client").SetAsync(new Cliente { Id = "client", TipoDocumento = "DNI", NumeroDocumento = "123", Nombre = "Ana", Apellido = "Perez", Telefono = "1234567890", Email = "ana@example.com" });
            await db.Collection("vehiculos").Document("vehicle").SetAsync(new Vehiculo { Id = "vehicle", Patente = "ABC123", TipoVehiculo = "Auto", Marca = "Marca", Modelo = "Modelo", Color = "Azul", ClienteId = "client" });
            await db.Collection("empleados").Document("staff").SetAsync(new Dictionary<string, object> { ["Nombre"] = "Empleado", ["Email"] = "staff@example.com", ["Rol"] = "Empleado", ["Estado"] = "Activo" });
            foreach (var (id, price) in new[] { ("one", 100.25), ("two", 50.0), ("three", 25.0) })
                await db.Collection("servicios").Document(id).SetAsync(new Dictionary<string, object> { ["Nombre"] = id, ["Tipo"] = id, ["TipoVehiculo"] = "auto", ["Precio"] = price, ["TiempoEstimado"] = 10, ["Descripcion"] = "Servicio", ["Estado"] = "Activo" });
            await db.Collection("paquetes_servicios").Document("pack").SetAsync(new Dictionary<string, object> { ["Nombre"] = "Paquete", ["Estado"] = "Activo", ["TipoVehiculo"] = "Auto", ["Precio"] = 999.0, ["PorcentajeDescuento"] = 20.0, ["ServiciosIds"] = new[] { "one", "two" } });
            return fixture;
        }

        public void Dispose() => _cache.Dispose();
    }
}

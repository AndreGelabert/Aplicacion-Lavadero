using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Firebase.Models;
using Firebase.Security;
using Firebase.Services;
using FirebaseLoginCustom.Controllers;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Firebase.Tests.Integration;

public sealed class LavadoInsumosHttpWorkflowIntegrationTests
{
    [LocalFirestoreFact]
    public async Task InicioYFinalizacionHttpReservanYConsumenProductoElegido()
    {
        await using var f = await Fixture.Start();
        await f.Seed(1m, false);
        var stock = new StockService(f.Db);
        var antes = await f.Snapshot();
        await f.Post("IniciarEtapa", new() { ["lavadoId"] = "l", ["servicioId"] = "s", ["etapaId"] = "e", ["seleccionJson"] = "{\"0\":\"insuficiente\"}" }, false, "No se pudo iniciar la etapa. Inténtalo nuevamente.");
        Assert.Equal(antes, await f.Snapshot());
        await f.Post("IniciarEtapa", new() { ["lavadoId"] = "l", ["servicioId"] = "s", ["etapaId"] = "e", ["seleccionJson"] = "{\"0\":\"i\"}" }, true, "Etapa iniciada correctamente.");
        Assert.Equal(1m, (await new InsumoService(f.Db).Obtener("i"))!.StockActual);
        Assert.Equal(0.25m, (await stock.ObtenerReservasInventario(new[] { "i" }))["i"]);
        Assert.Empty(await stock.ObtenerMovimientos("i"));
        Assert.Equal("i", (await f.Washes.ObtenerLavado("l"))!.Servicios[0].Etapas[0].Receta[0].InsumoId);
        await f.Post("FinalizarEtapa", new() { ["lavadoId"] = "l", ["servicioId"] = "s", ["etapaId"] = "e" }, true, "Etapa finalizada correctamente.");
        Assert.Equal(0.75m, (await new InsumoService(f.Db).Obtener("i"))!.StockActual);
        Assert.Equal(0m, (await stock.ObtenerReservasInventario(new[] { "i" }))["i"]);
        var mov = Assert.Single(await stock.ObtenerMovimientos("i"));
        Assert.Equal("ConsumoEtapa", mov.Tipo); Assert.Equal(-0.25m, mov.Cantidad);
        var audits = await f.Db.Collection("registros_auditoria").GetSnapshotAsync();
        Assert.Contains(audits.Documents, d => d.GetValue<string>("Action") == "Inicio de etapa en lavado");
        Assert.Contains(audits.Documents, d => d.GetValue<string>("Action") == "Finalización de etapa en lavado");
    }

    [LocalFirestoreFact]
    public async Task CancelacionHttpConConsumoRegistradoYAdicionalNoDuplicaStock()
    {
        await using var f = await Fixture.Start(); await f.Seed(2m, true);
        var stock = new StockService(f.Db);
        async Task Inicio(string etapa) => await f.Post("IniciarEtapa", new() { ["lavadoId"] = "l", ["servicioId"] = "s", ["etapaId"] = etapa, ["seleccionJson"] = "{\"0\":\"i\"}" }, true, "Etapa iniciada correctamente.");
        await Inicio("e");
        await f.Post("FinalizarEtapa", new() { ["lavadoId"] = "l", ["servicioId"] = "s", ["etapaId"] = "e" }, true, "Etapa finalizada correctamente.");
        await Inicio("e2");
        var fields = new Dictionary<string,string> { ["lavadoId"] = "l", ["servicioId"] = "s", ["motivo"] = "motivo", ["consumosNoRegistradosJson"] = "{\"i\":0.2}" };
        await f.Post("CancelarServicio", fields, true, "Servicio cancelado correctamente.");
        Assert.Equal(1.55m, (await new InsumoService(f.Db).Obtener("i"))!.StockActual);
        Assert.Equal(0m, (await stock.ObtenerReservasInventario(new[] { "i" }))["i"]);
        var movimientos = await stock.ObtenerMovimientos("i");
        Assert.Equal(-0.25m, Assert.Single(movimientos.Where(m => m.Tipo == "ConsumoEtapa")).Cantidad);
        Assert.Equal(new[] { -0.2m, 0m }, movimientos.Where(m => m.Tipo == "MermaCancelacion").Select(m => m.Cantidad).OrderBy(x => x).ToArray());
        await f.Post("CancelarServicio", fields, true, "Servicio cancelado correctamente.");
        Assert.Equal(1.55m, (await new InsumoService(f.Db).Obtener("i"))!.StockActual);
        var repetidos = await stock.ObtenerMovimientos("i");
        Assert.Equal(movimientos.Select(m => m.Id).OrderBy(x => x), repetidos.Select(m => m.Id).OrderBy(x => x));
        Assert.Equal(0m, (await stock.ObtenerReservasInventario(new[] { "i" }))["i"]);
        Assert.Equal("Cancelado", (await f.Washes.ObtenerLavado("l"))!.Servicios[0].Estado);
    }

    [LocalFirestoreFact]
    public async Task AltaHttpMultivehiculoRechazaDemandaAcumuladaSinEfectos()
    {
        await using var f = await Fixture.Start();
        await f.SeedAlta(0.4m);
        var before = await f.Snapshot();
        var resultado = await f.CrearLote();
        Assert.False(resultado.GetProperty("success").GetBoolean());
        Assert.Equal("No se pudo crear el lavado. Inténtalo nuevamente.", resultado.GetProperty("message").GetString());
        Assert.Equal(before, await f.Snapshot());
        var stock = new StockService(f.Db);
        Assert.Equal(0.4m, (await new InsumoService(f.Db).Obtener("i"))!.StockActual);
        Assert.Equal(0m, (await stock.ObtenerReservasInventario(new[] { "i" }))["i"]);
        Assert.Empty(await stock.ObtenerMovimientos("i"));
        Assert.Empty((await f.Db.Collection("lavados").GetSnapshotAsync()).Documents);
        Assert.Empty((await f.Db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task AltaHttpMultivehiculoReservaYAsignaEmpleadosDistintos()
    {
        await using var f = await Fixture.Start();
        await f.SeedAlta(1m);
        var resultado = await f.CrearLote();
        Assert.True(resultado.GetProperty("success").GetBoolean(), resultado.GetRawText());
        Assert.Equal("Se crearon 2 lavados correctamente.", resultado.GetProperty("message").GetString());
        var ids = resultado.GetProperty("lavadosIds").EnumerateArray().Select(x => x.GetString()!).ToArray();
        Assert.Equal(2, ids.Distinct().Count());
        var lavados = new List<Lavado>();
        foreach (var id in ids) lavados.Add((await f.Washes.ObtenerLavado(id))!);
        Assert.All(lavados, l => { Assert.Equal("EnProceso", l.Estado); Assert.Equal("i", Assert.Single(l.Servicios).Receta[0].InsumoId); Assert.Single(l.EmpleadosAsignadosIds); });
        Assert.Equal(new[] { "v1", "v2" }, lavados.Select(l => l.VehiculoId).OrderBy(x => x).ToArray());
        Assert.Equal(2, lavados.SelectMany(l => l.EmpleadosAsignadosIds).Distinct().Count());
        var stock = new StockService(f.Db);
        Assert.Equal(1m, (await new InsumoService(f.Db).Obtener("i"))!.StockActual);
        Assert.Equal(0.5m, (await stock.ObtenerReservasInventario(new[] { "i" }))["i"]);
        Assert.Empty(await stock.ObtenerMovimientos("i"));
        var audits = await f.Db.Collection("registros_auditoria").GetSnapshotAsync();
        Assert.Equal(2, audits.Documents.Count(d => d.GetValue<string>("Action") == "Creación de lavado"));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly WebApplication app; private readonly HttpClient client; private readonly string token; private readonly AsyncServiceScope scope;
        public FirestoreDb Db { get; } public LavadoService Washes { get; }
        private Fixture(WebApplication app, HttpClient client, string token, FirestoreDb db, AsyncServiceScope scope, LavadoService washes) { this.app = app; this.client = client; this.token = token; this.scope = scope; Db = db; Washes = washes; }
        public static async Task<Fixture> Start()
        {
            if (Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST") != "127.0.0.1:8245") throw new InvalidOperationException("Disposable local emulator required.");
            var db = new FirestoreDbBuilder { ProjectId = "demo-wash-http-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
            builder.Services.AddControllersWithViews().AddApplicationPart(typeof(LavadosController).Assembly);
            builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, InsumosPermisosHttpIntegrationTests.HeaderAuth>("Test", _ => { });
            builder.Services.AddAuthorization(); builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
            builder.Services.AddSingleton(db); builder.Services.AddMemoryCache();
            builder.Services.AddScoped<ServicioService>(); builder.Services.AddScoped<PaqueteServicioService>(); builder.Services.AddScoped<PersonalService>(); builder.Services.AddScoped<ConfiguracionService>(); builder.Services.AddScoped<StockService>(); builder.Services.AddScoped<InsumoService>(); builder.Services.AddScoped<LavadoService>();
            builder.Services.AddScoped<ClienteService>(); builder.Services.AddScoped<VehiculoService>(); builder.Services.AddScoped<TipoVehiculoService>(); builder.Services.AddScoped<TipoDocumentoService>(); builder.Services.AddScoped<LavadoCreationService>(); builder.Services.AddScoped<LavadoAssociatedClientService>(); builder.Services.AddScoped<IAuditWriter, AuditService>();
            var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization();
            app.MapGet("/test-token", (HttpContext context, IAntiforgery anti) => anti.GetAndStoreTokens(context).RequestToken!).RequireAuthorization();
            app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"); await app.StartAsync();
            var client = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            client.DefaultRequestHeaders.Add("X-Test-Permissions", "Lavados.Crear,Lavados.Modificar,Lavados.Consultar");
            var token = await client.GetStringAsync("/test-token");
            var scope = app.Services.CreateAsyncScope();
            try { return new Fixture(app, client, token, db, scope, scope.ServiceProvider.GetRequiredService<LavadoService>()); }
            catch { await scope.DisposeAsync(); client.Dispose(); await app.StopAsync(); await app.DisposeAsync(); throw; }
        }
        public async Task Seed(decimal cantidad, bool dosEtapas)
        {
            await Db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "i", TipoId = "tipo", Tipo = "Jabon", UnidadMedida = "l", StockActual = cantidad });
            await Db.Collection("insumos").Document("insuficiente").SetAsync(new Insumo { Id = "insuficiente", Nombre = "insuficiente", TipoId = "tipo", Tipo = "Jabon", UnidadMedida = "l", StockActual = 0.1m });
            EtapaEnLavado Etapa(string id) => new() { EtapaId = id, Receta = new() { new RecetaInsumo { Modalidad = "Tipo", TipoId = "tipo", UnidadMedida = "ml", DosisTeorica = 250m } } };
            var etapas = new List<EtapaEnLavado> { Etapa("e") }; if (dosEtapas) etapas.Add(Etapa("e2"));
            await Washes.ActualizarLavado(new Lavado { Id = "l", Estado = "EnProceso", ClienteId = "c", VehiculoId = "v", FechaCreacion = DateTime.UtcNow, Servicios = new() { new ServicioEnLavado { ServicioId = "s", Estado = "EnProceso", Etapas = etapas } } });
        }
        // Direct fixture setup mirrors the existing local integration fixture; creation itself is HTTP.
        public async Task SeedAlta(decimal cantidad)
        {
            scope.ServiceProvider.GetRequiredService<IMemoryCache>().Set("system_configuration", new Configuracion { HorariosOperacion = new(), CapacidadMaximaConcurrente = 10 });
            await Db.Collection("clientes").Document("c").SetAsync(new Cliente { Id = "c", Nombre = "Ana", Apellido = "Perez", TipoDocumento = "DNI", NumeroDocumento = "123", Telefono = "1234567890", Email = "ana@example.invalid" });
            foreach (var id in new[] { "v1", "v2" }) await Db.Collection("vehiculos").Document(id).SetAsync(new Vehiculo { Id = id, TipoVehiculo = "Auto", Patente = id, Marca = "Marca", Modelo = "Modelo", Color = "Azul" });
            foreach (var id in new[] { "p1", "p2" }) await Db.Collection("empleados").Document(id).SetAsync(new Dictionary<string, object> { ["Nombre"] = id, ["Email"] = id + "@example.invalid", ["Rol"] = "Empleado", ["Estado"] = "Activo" });
            await Db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "i", TipoId = "tipo", Tipo = "Jabon", UnidadMedida = "l", StockActual = cantidad });
            await Db.Collection("servicios").Document("s").SetAsync(new Dictionary<string, object> { ["Nombre"] = "Servicio", ["Tipo"] = "Lavado", ["TipoVehiculo"] = "Auto", ["Estado"] = "Activo", ["Precio"] = 10.0, ["TiempoEstimado"] = 10, ["Descripcion"] = "Servicio", ["Receta"] = new[] { new RecetaInsumo { Modalidad = "Tipo", TipoId = "tipo", UnidadMedida = "ml", DosisTeorica = 250m } } });
        }
        public async Task<JsonElement> CrearLote()
        {
            var request = new LavadosController.CrearLavadoConSeleccionRequest {
                ClienteId = "c", VehiculosServicios = new() {
                    new VehiculoServiciosRequest { VehiculoId = "v1", Servicios = new() { new ServicioItemRequest { ServicioId = "s" } } },
                    new VehiculoServiciosRequest { VehiculoId = "v2", Servicios = new() { new ServicioItemRequest { ServicioId = "s" } } }
                }, Selecciones = new() { ["v1"] = new() { [0] = "i" }, ["v2"] = new() { [0] = "i" } }
            };
            using var message = new HttpRequestMessage(HttpMethod.Post, "/Lavados/CrearLavadoAjax") { Content = JsonContent.Create(request) };
            message.Headers.Add("RequestVerificationToken", token);
            using var response = await client.SendAsync(message); var text = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.OK, text);
            using var json = JsonDocument.Parse(text); return json.RootElement.Clone();
        }
        public async Task Post(string action, Dictionary<string,string> fields, bool success, string message)
        {
            var values = new Dictionary<string,string>(fields) { ["__RequestVerificationToken"] = token };
            using var response = await client.PostAsync("/Lavados/" + action, new FormUrlEncodedContent(values));
            var text = await response.Content.ReadAsStringAsync(); Assert.True(response.StatusCode == HttpStatusCode.OK, text);
            using var json = JsonDocument.Parse(text); Assert.Equal(success, json.RootElement.GetProperty("success").GetBoolean()); Assert.Equal(message, json.RootElement.GetProperty("message").GetString());
        }
        public async Task<string[]> Snapshot()
        {
            var result = new List<string>();
            foreach (var name in new[] { "lavados", "insumos", "movimientosStock", "registros_auditoria", "empleados" })
            {
                var snapshot = await Db.Collection(name).GetSnapshotAsync(); result.Add(name + ":" + snapshot.Count);
                result.AddRange(snapshot.Documents.OrderBy(d => d.Id).Select(d => name + "/" + d.Id + ":" + d.UpdateTime));
            }
            return result.ToArray();
        }
        public async ValueTask DisposeAsync() { client.Dispose(); await scope.DisposeAsync(); await app.StopAsync(); await app.DisposeAsync(); }
    }
}

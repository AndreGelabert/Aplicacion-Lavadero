using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
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
using Moq;

namespace Firebase.Tests.Integration;

public class TurnoInicioHttpTests
{
    [LocalFirestoreFact]
    public async Task InicioRequiresBothPermissionsAntiforgeryAndValidInputAndDelegates()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-inicio-http-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var now = DateTime.UtcNow;
        await db.Collection("turnos").Document("turno").SetAsync(new Turno { Id = "turno", ClienteId = "c", Estado = "Pendiente", Origen = "web", ClaveIdempotencia = "k", InicioUtc = now, FinUtc = now.AddMinutes(10), PrecioAcordado = 72, Descuento = 10, DesglosePrecio = new() { Version = 1, Subtotal = 80, Total = 72 }, Vehiculos = [new() { VehiculoId = "v", CantidadEmpleadosRequeridos = 1, Servicios = [new() { ServicioId = "s", Precio = 80, TiempoEstimado = 10 }] }], RecetaOperativa = new() { Version = 1, Servicios = [new() { ServicioId = "s", Nombre = "Congelado", Tipo = "Lavado", Receta = [new() { Modalidad = "Tipo", TipoId = "tipo", UnidadMedida = "ml", DosisTeorica = 250 }] }] } });
        await db.Collection("insumos").Document("jabon").SetAsync(new Insumo { Id = "jabon", Nombre = "Jabon", TipoId = "tipo", Tipo = "Jabon", UnidadMedida = "l", StockActual = 1, Estado = "Activo" });
        await db.Collection("insumos").Document("incompatible").SetAsync(new Insumo { Id = "incompatible", Nombre = "Otro", TipoId = "otro", Tipo = "Otro", UnidadMedida = "l", StockActual = 1, Estado = "Activo" });
        await db.Collection("empleados").Document("e").SetAsync(new { Estado = "Activo" });
        await db.Collection("configuracion").Document("system_config").SetAsync(new { CapacidadMaximaConcurrente = 1, ToleranciaLlegadaTurnosMinutos = 15 });
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(TurnosController).Assembly.GetName().Name, EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews();
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, InsumosPermisosHttpIntegrationTests.HeaderAuth>("Test", _ => { });
        builder.Services.AddAuthorization(); builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        var consulta = new Mock<ITurnoConsultaService>();
        consulta.Setup(x => x.Obtener("turno")).ReturnsAsync(new TurnoConsultaViewModel("turno", "Cliente", "Pendiente", now, now.AddMinutes(10), false, 72, Array.Empty<VehiculoConsultaViewModel>()));
        builder.Services.AddSingleton(db); builder.Services.AddMemoryCache(); builder.Services.AddSingleton(consulta.Object);
        builder.Services.AddSingleton<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());
        builder.Services.AddScoped<TurnoInicioRecursosService>();
        builder.Services.AddScoped<InsumoService>();
        builder.Services.AddScoped<ServicioService>(); builder.Services.AddScoped<PaqueteServicioService>(); builder.Services.AddScoped<PersonalService>(); builder.Services.AddScoped<ConfiguracionService>(); builder.Services.AddScoped<LavadoService>();
        await using var app = builder.Build();
        app.UseDeveloperExceptionPage(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
        app.MapGet("/token", (HttpContext c, IAntiforgery a) => a.GetAndStoreTokens(c).RequestToken!).RequireAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"); await app.StartAsync();
        try
        {
            var address = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
            using var client = new HttpClient(new HttpClientHandler { UseCookies = true }) { BaseAddress = address };
            async Task<string[]> State()
            {
                var result = new List<string>();
                foreach (var n in new[] { "turnos", "lavados", "coordinacion", "insumos", "movimientosStock", "reservasHerramientas", "reservasHerramientasTotales" })
                {
                    var docs = await db.Collection(n).GetSnapshotAsync();
                    result.Add($"{n}:count={docs.Count}");
                    result.AddRange(docs.Documents.Select(d => $"{n}/{d.Id}:{d.UpdateTime}:{System.Text.Json.JsonSerializer.Serialize(d.ToDictionary())}"));
                }
                return result.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            }
            var before = await State();
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/Turnos/Iniciar", new { turnoId = "turno" })).StatusCode);
            foreach (var p in new[] { "Turnos.Modificar", "Lavados.Crear", "Turnos.Consultar", "Turnos.Modificar,Lavados.Crear", "Turnos.Modificar,Lavados.Modificar", "Lavados.Crear,Lavados.Modificar" })
            {
                client.DefaultRequestHeaders.Remove("X-Test-Permissions"); client.DefaultRequestHeaders.Add("X-Test-Permissions", p);
                var token = await client.GetStringAsync("/token"); client.DefaultRequestHeaders.Remove("RequestVerificationToken"); client.DefaultRequestHeaders.Add("RequestVerificationToken", token);
                using var denied = await client.PostAsJsonAsync("/Turnos/Iniciar", new { turnoId = "turno", seleccion = new Dictionary<int, string> { [0] = "jabon" } });
                Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
                Assert.Equal("", await denied.Content.ReadAsStringAsync());
                Assert.Equal(before, await State());
            }
            client.DefaultRequestHeaders.Remove("X-Test-Permissions"); client.DefaultRequestHeaders.Add("X-Test-Permissions", "Turnos.Modificar,Lavados.Crear,Lavados.Modificar");
            client.DefaultRequestHeaders.Remove("RequestVerificationToken");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/Turnos/Iniciar", new { turnoId = "turno" })).StatusCode); Assert.Equal(before, await State());
            client.DefaultRequestHeaders.Add("RequestVerificationToken", "invalid-token");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/Turnos/Iniciar", new { turnoId = "turno" })).StatusCode); Assert.Equal(before, await State());
            client.DefaultRequestHeaders.Remove("RequestVerificationToken");
            client.DefaultRequestHeaders.Remove("X-Test-Permissions"); client.DefaultRequestHeaders.Add("X-Test-Permissions", "Turnos.Consultar,Turnos.Modificar,Lavados.Crear,Lavados.Modificar");
            using var detail = await client.GetAsync("/Turnos/Detalle/turno");
            var html = await detail.Content.ReadAsStringAsync();
            Assert.True(detail.StatusCode == HttpStatusCode.OK, $"Detail returned {detail.StatusCode}: {html}");
            Assert.Contains("data-turno-inicio", html);
            Assert.Contains("data-turno-id=\"turno\"", html);
            Assert.Contains("data-recursos-url=\"/Turnos/RecursosInicio\"", html);
            Assert.Contains("data-inicio-url=\"/Turnos/Iniciar\"", html);
            Assert.Contains("turno-inicio.js", html);
            var tokenMatch = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            Assert.True(tokenMatch.Success);
            client.DefaultRequestHeaders.Add("RequestVerificationToken", WebUtility.HtmlDecode(tokenMatch.Groups[1].Value));
            using (var resources = await client.GetAsync("/Turnos/RecursosInicio?id=turno"))
            {
                Assert.Equal(HttpStatusCode.OK, resources.StatusCode);
                using var json = JsonDocument.Parse(await resources.Content.ReadAsStringAsync());
                var item = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray());
                Assert.Equal(0, item.GetProperty("indice").GetInt32());
                Assert.Equal("jabon", Assert.Single(item.GetProperty("opciones").EnumerateArray()).GetProperty("id").GetString());
            }
            Assert.Equal(before, await State());
            foreach (var selection in new[] { new Dictionary<int, string>(), new Dictionary<int, string> { [0] = "incompatible" }, new Dictionary<int, string> { [0] = "desconocido" } })
            {
                using var rejectedSelection = await client.PostAsJsonAsync("/Turnos/Iniciar", new { turnoId = "turno", seleccion = selection });
                Assert.Equal(HttpStatusCode.Conflict, rejectedSelection.StatusCode);
                Assert.Equal("{\"success\":false,\"message\":\"No se pudo iniciar el turno.\"}", await rejectedSelection.Content.ReadAsStringAsync());
                Assert.Equal(before, await State());
            }
            using (var missing = await client.PostAsJsonAsync("/Turnos/Iniciar", new { turnoId = "no-existe" }))
            {
                Assert.Equal(HttpStatusCode.Conflict, missing.StatusCode);
                Assert.Equal("{\"success\":false,\"message\":\"No se pudo iniciar el turno.\"}", await missing.Content.ReadAsStringAsync());
                Assert.Equal(before, await State());
            }
            foreach (var body in new[] { "{}", "{\"turnoId\":\"../x\"}", "{\"turnoId\":\"turno\",\"seleccion\":{\"-1\":\"x\"}}", "{\"turnoId\":\"turno\",\"seleccion\":{\"0\":\"a/b\"}}", "{\"turnoId\":\"turno\",\"precio\":1}", "{", "{\"turnoId\":12}", "{\"turnoId\":\"turno\",\"seleccion\":{\"abc\":\"jabon\"}}" })
            {
                using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
                using var rejected = await client.PostAsync("/Turnos/Iniciar", content);
                Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
                using var json = System.Text.Json.JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
                Assert.Equal(2, json.RootElement.EnumerateObject().Count());
                Assert.False(json.RootElement.GetProperty("success").GetBoolean());
                Assert.Equal("Datos de inicio inválidos.", json.RootElement.GetProperty("message").GetString());
                Assert.Equal(before, await State());
            }
            using var response = await client.PostAsJsonAsync("/Turnos/Iniciar", new { turnoId = "turno", seleccion = new Dictionary<int, string> { [0] = "jabon" } });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var wash = Assert.Single((await db.Collection("lavados").GetSnapshotAsync()).Documents);
            Assert.Equal("turno", wash.GetValue<string>("TurnoId")); Assert.Equal(72d, wash.GetValue<double>("Precio"));
            Assert.Equal(wash.Id, (await db.Collection("turnos").Document("turno").GetSnapshotAsync()).GetValue<string>("LavadoId"));
            var mapped = (await app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope().ServiceProvider.GetRequiredService<LavadoService>().ObtenerLavado(wash.Id))!;
            Assert.Equal("EnProceso", mapped.Servicios[0].Estado);
            Assert.Equal("jabon", mapped.Servicios[0].Receta[0].InsumoId);
            Assert.Equal(0.25m, (await new StockService(db).ObtenerReservasInventario(new[] { "jabon" }))["jabon"]);
            Assert.Equal(1m, (await db.Collection("insumos").Document("jabon").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
            var after = await State(); Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/Turnos/Iniciar", new { turnoId = "turno" })).StatusCode); Assert.Equal(after, await State());
        }
        finally { await app.StopAsync(); }
    }
}

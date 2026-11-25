using System.Net;
using System.Text.Json;
using Firebase.Models;
using Firebase.Security;
using Firebase.Services;
using FirebaseLoginCustom.Controllers;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Firebase.Tests.Integration;

public class TurnoInicioRecursosHttpTests
{
    [LocalFirestoreFact]
    public async Task OpcionesUsanSoloRecetaCongeladaYNoEscriben()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-recursos-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var now = DateTime.UtcNow;
        var turno = new Turno { Id = "t", Estado = "Pendiente", InicioUtc = now, FinUtc = now.AddMinutes(10), PrecioAcordado = 10, DesglosePrecio = new() { Version = 1, Subtotal = 10, Total = 10 }, Vehiculos = [new() { VehiculoId = "v", CantidadEmpleadosRequeridos = 1, Servicios = [new() { ServicioId = "s", Precio = 10, TiempoEstimado = 10 }] }], RecetaOperativa = new() { Version = 1, Servicios = [new() { ServicioId = "s", Nombre = "Congelado", Tipo = "Lavado", Receta = [new() { Modalidad = "Producto", InsumoId = "fijo", UnidadMedida = "ml", DosisTeorica = 1 }, new() { Modalidad = "Tipo", TipoId = "tipo", NombreTipo = "Congelado", UnidadMedida = "ml", DosisTeorica = 250 }] }] } };
        var doc = db.Collection("turnos").Document("t"); await doc.SetAsync(turno);
        await db.Collection("servicios").Document("s").SetAsync(new { Nombre = "Catálogo cambiado" });
        await db.Collection("insumos").Document("jabon").SetAsync(new Insumo { Id = "jabon", Nombre = "Jabón", TipoId = "tipo", UnidadMedida = "l", Estado = "Activo" });
        await db.Collection("insumos").Document("otro").SetAsync(new Insumo { Id = "otro", Nombre = "Otro", TipoId = "otro", UnidadMedida = "l", Estado = "Activo" });
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(TurnosController).Assembly.GetName().Name, EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews(); builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, InsumosPermisosHttpIntegrationTests.HeaderAuth>("Test", _ => { });
        builder.Services.AddAuthorization(); builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db); builder.Services.AddSingleton(Mock.Of<ITurnoConsultaService>()); builder.Services.AddScoped<TurnoInicioRecursosService>();
        await using var app = builder.Build(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"); await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            async Task<string[]> State()
            {
                var result = new List<string>();
                foreach (var collection in new[] { "turnos", "lavados", "coordinacion", "insumos", "servicios", "movimientosStock", "reservasHerramientas", "reservasHerramientasTotales" })
                {
                    var snapshots = await db.Collection(collection).GetSnapshotAsync();
                    result.Add($"{collection}:count={snapshots.Count}");
                    result.AddRange(snapshots.Documents.Select(d => $"{collection}/{d.Id}:{d.UpdateTime}:{JsonSerializer.Serialize(d.ToDictionary())}"));
                }
                return result.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            }
            async Task Check(HttpStatusCode status, string url = "/Turnos/RecursosInicio?id=t")
            {
                var state = await State();
                using var response = await client.GetAsync(url);
                Assert.Equal(status, response.StatusCode);
                Assert.Equal(state, await State());
            }
            var before = await State();
            using (var anonymous = await client.GetAsync("/Turnos/RecursosInicio?id=t")) Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            foreach (var permissions in new[] { "Turnos.Modificar", "Lavados.Crear,Lavados.Modificar", "Turnos.Modificar,Lavados.Crear", "Turnos.Modificar,Lavados.Modificar" })
            {
                client.DefaultRequestHeaders.Remove("X-Test-Permissions"); client.DefaultRequestHeaders.Add("X-Test-Permissions", permissions);
                using var denied = await client.GetAsync("/Turnos/RecursosInicio?id=t"); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            }
            client.DefaultRequestHeaders.Remove("X-Test-Permissions"); client.DefaultRequestHeaders.Add("X-Test-Permissions", "Turnos.Modificar,Lavados.Crear,Lavados.Modificar");
            using (var response = await client.GetAsync("/Turnos/RecursosInicio?id=t"))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var item = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray());
                Assert.Equal(1, item.GetProperty("indice").GetInt32()); Assert.Equal("Congelado", item.GetProperty("nombre").GetString());
                Assert.Equal("jabon", Assert.Single(item.GetProperty("opciones").EnumerateArray()).GetProperty("id").GetString());
            }
            Assert.Equal(before, await State());
            var operational = turno.RecetaOperativa!.Servicios[0];
            operational.Etapas = [new Etapa { Id = "e1", Nombre = "Primera", Receta = [
                new() { Modalidad = "Producto", InsumoId = "fijo", UnidadMedida = "ml", DosisTeorica = 1 },
                new() { Modalidad = "Tipo", TipoId = "tipo", NombreTipo = "Etapa", UnidadMedida = "ml", DosisTeorica = 50 },
                new() { Modalidad = "Tipo", TipoId = "tipo", NombreTipo = "Segunda opción", UnidadMedida = "ml", DosisTeorica = 20 }] },
                new Etapa { Id = "e2", Nombre = "Segunda", Receta = [new() { Modalidad = "Tipo", TipoId = "tipo", NombreTipo = "No iniciar", UnidadMedida = "ml", DosisTeorica = 10 }] }];
            await doc.SetAsync(turno);
            var stageBefore = await State();
            using (var response = await client.GetAsync("/Turnos/RecursosInicio?id=t"))
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var items = json.RootElement.GetProperty("items").EnumerateArray().ToArray();
                Assert.Equal(2, items.Length);
                Assert.Equal(1, items[0].GetProperty("indice").GetInt32()); Assert.Equal("Etapa", items[0].GetProperty("nombre").GetString());
                Assert.Equal(2, items[1].GetProperty("indice").GetInt32()); Assert.Equal("Segunda opción", items[1].GetProperty("nombre").GetString());
                foreach (var item in items) Assert.Equal("jabon", Assert.Single(item.GetProperty("opciones").EnumerateArray()).GetProperty("id").GetString());
            }
            Assert.Equal(stageBefore, await State());
            operational.ServicioId = "distinto"; await doc.SetAsync(turno); await Check(HttpStatusCode.Conflict);
            operational.ServicioId = "s"; operational.Etapas[0].Receta[1].DosisTeorica = 0; await doc.SetAsync(turno); await Check(HttpStatusCode.Conflict);
            operational.Etapas[0].Receta[1].DosisTeorica = 50; turno.Estado = "Cancelado"; await doc.SetAsync(turno); await Check(HttpStatusCode.Conflict);
            turno.Estado = "Pendiente"; await doc.SetAsync(turno); await doc.UpdateAsync("LavadoId", "inconsistente"); await Check(HttpStatusCode.Conflict);
            await doc.UpdateAsync(new Dictionary<string, object> { ["LavadoId"] = FieldValue.Delete, ["RecetaOperativa"] = FieldValue.Delete });
            await Check(HttpStatusCode.Conflict);
            await Check(HttpStatusCode.Conflict, "/Turnos/RecursosInicio?id=desconocido");
            foreach (var id in new[] { " t", "t ", "t\u0001x", "a/b", ".", "..", new string('a', 1501), new string('é', 751) })
            {
                var state = await State();
                using var invalid = await client.GetAsync("/Turnos/RecursosInicio?id=" + Uri.EscapeDataString(id));
                Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
                Assert.Equal("{\"success\":false,\"message\":\"Datos de inicio inválidos.\"}", await invalid.Content.ReadAsStringAsync());
                Assert.Equal(state, await State());
            }
        }
        finally { await app.StopAsync(); }
    }
}

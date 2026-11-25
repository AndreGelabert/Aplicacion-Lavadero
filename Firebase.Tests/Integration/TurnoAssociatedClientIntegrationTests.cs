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
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Firebase.Tests.Integration;

public class TurnoAssociatedClientIntegrationTests
{
    [LocalFirestoreFact]
    public async Task TurnoFormAndLavadoRegistrationEnforcePermissionsCsrfValidationAndProjection()
    {
        Assert.Equal("127.0.0.1:8190", Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST"));
        var db = new FirestoreDbBuilder { ProjectId = "demo-turno-assoc-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("clientes").Document("owner").SetAsync(new Cliente { Id = "owner", TipoDocumento = "DNI", NumeroDocumento = "123", Nombre = "Juan", Apellido = "Perez", Telefono = "1234567890", Email = "owner@example.invalid", Estado = "Activo" });
        await db.Collection("vehiculos").Document("vehicle").SetAsync(new Vehiculo { Id = "vehicle", Patente = "ABC123", TipoVehiculo = "Auto", Marca = "Marca", Modelo = "Modelo", Color = "Azul", ClienteId = "owner", ClientesIds = new() { "owner" }, Estado = "Activo", ClaveAsociacionHash = VehiculoService.HashClaveAsociacion("clave") });
        await db.Collection("tiposDocumento").Document("dni").SetAsync(new { Nombre = "DNI", Formato = "nnn" });
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(TurnosController).Assembly.GetName().Name, EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews();
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, InsumosPermisosHttpIntegrationTests.HeaderAuth>("Test", _ => { });
        builder.Services.AddAuthorization(); builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db); builder.Services.AddMemoryCache();
        builder.Services.AddSingleton(Mock.Of<ITurnoConsultaService>()); builder.Services.AddSingleton<TurnoFormIdentity>();
        builder.Services.AddScoped<ITurnoSelectorService, TurnoSelectorService>();
        builder.Services.AddScoped<ClienteService>(); builder.Services.AddScoped<VehiculoService>(); builder.Services.AddScoped<TipoDocumentoService>();
        builder.Services.AddScoped<ServicioService>(); builder.Services.AddScoped<PaqueteServicioService>(); builder.Services.AddScoped<PersonalService>();
        builder.Services.AddScoped<ConfiguracionService>(); builder.Services.AddScoped<InsumoService>(); builder.Services.AddScoped<TipoVehiculoService>();
        builder.Services.AddScoped<LavadoService>(); builder.Services.AddScoped<LavadoCreationService>(); builder.Services.AddScoped<LavadoAssociatedClientService>();
        builder.Services.AddScoped<AuditService>(); builder.Services.AddScoped<IAuditWriter>(p => p.GetRequiredService<AuditService>());
        await using var app = builder.Build(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"); await app.StartAsync();
        try
        {
            var address = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
            using var client = new HttpClient(new HttpClientHandler { UseCookies = true, AllowAutoRedirect = false }) { BaseAddress = address };
            async Task<string[]> State()
            {
                var result = new List<string>();
                foreach (var collection in new[] { "clientes", "vehiculos", "registros_auditoria", "turnos" })
                    result.AddRange((await db.Collection(collection).GetSnapshotAsync()).Documents.OrderBy(d => d.Id).Select(d => $"{collection}/{d.Id}:{d.UpdateTime}"));
                return result.ToArray();
            }
            void Permission(string value) { client.DefaultRequestHeaders.Remove("X-Test-Permissions"); client.DefaultRequestHeaders.Add("X-Test-Permissions", value); }
            var request = new LavadosController.RegistroClienteAsociadoRequest { VehiculoId = "vehicle", ClaveAsociacion = "clave", TipoDocumento = "DNI", NumeroDocumento = "456", Nombre = "Ana", Apellido = "Perez", Telefono = "1234567890", Email = "ana@example.invalid" };
            const string path = "/Lavados/RegistrarClienteAsociado";
            var before = await State();
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(path, request)).StatusCode); Assert.Equal(before, await State());
            Permission("Turnos.Crear");
            var deniedPage = await client.GetStringAsync("/Turnos/Crear");
            Assert.DoesNotContain("registroClienteModal", deniedPage);
            var deniedToken = WebUtility.HtmlDecode(Regex.Match(deniedPage, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
            Assert.NotEmpty(deniedToken); client.DefaultRequestHeaders.Add("RequestVerificationToken", deniedToken);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path, request)).StatusCode); Assert.Equal(before, await State());
            Permission("Turnos.Crear,Clientes.Crear");
            var page = await client.GetStringAsync("/Turnos/Crear");
            Assert.Contains("registroClienteModal", page); Assert.Contains("data-formato=\"nnn\"", page);
            var token = WebUtility.HtmlDecode(Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
            Assert.NotEmpty(token); client.DefaultRequestHeaders.Remove("RequestVerificationToken");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, request)).StatusCode); Assert.Equal(before, await State());
            client.DefaultRequestHeaders.Add("RequestVerificationToken", token);
            using (var search = await client.GetAsync("/Turnos/BuscarVehiculosPorPatente?search=ABC123")) { Assert.Equal(HttpStatusCode.OK, search.StatusCode); Assert.Contains("vehicle", await search.Content.ReadAsStringAsync()); }
            foreach (var invalid in new[] { new LavadosController.RegistroClienteAsociadoRequest { VehiculoId = "vehicle", ClaveAsociacion = "wrong", TipoDocumento = "DNI", NumeroDocumento = "456", Nombre = "Ana", Apellido = "Perez", Telefono = "1234567890", Email = "ana@example.invalid" }, new LavadosController.RegistroClienteAsociadoRequest { VehiculoId = "vehicle", ClaveAsociacion = "clave", TipoDocumento = "DNI", NumeroDocumento = "xyz", Nombre = "Ana", Apellido = "Perez", Telefono = "1234567890", Email = "ana@example.invalid" } })
            {
                using var rejected = await client.PostAsJsonAsync(path, invalid); Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
                var json = await rejected.Content.ReadFromJsonAsync<JsonElement>(); Assert.False(json.GetProperty("success").GetBoolean());
                Assert.Equal(new[] { "success", "message" }, json.EnumerateObject().Select(p => p.Name)); Assert.Equal(before, await State());
            }
            using var response = await client.PostAsJsonAsync(path, request); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.True(result.GetProperty("success").GetBoolean());
            Assert.Equal(new[] { "success", "message", "cliente" }, result.EnumerateObject().Select(p => p.Name));
            var projection = result.GetProperty("cliente"); Assert.Equal(new[] { "id", "nombre", "documento" }, projection.EnumerateObject().Select(p => p.Name));
            Assert.Equal("Ana Perez", projection.GetProperty("nombre").GetString()); Assert.Equal("DNI: 456", projection.GetProperty("documento").GetString());
            var id = projection.GetProperty("id").GetString()!;
            var saved = await new ClienteService(db).ObtenerCliente(id); Assert.NotNull(saved); Assert.Contains("vehicle", saved.VehiculosIds);
            Assert.Contains(id, (await new VehiculoService(db).ObtenerVehiculo("vehicle")).ClientesIds);
            var audit = Assert.Single((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents); Assert.Equal(id, audit.GetValue<string>("TargetId"));
            using var associated = await client.GetAsync("/Turnos/ObtenerClientesVehiculo?vehiculoId=vehicle&patente=ABC123");
            Assert.Equal(HttpStatusCode.OK, associated.StatusCode); var text = await associated.Content.ReadAsStringAsync(); Assert.Contains(id, text); Assert.DoesNotContain("ana@example.invalid", text); Assert.DoesNotContain("1234567890", text);
        }
        finally { await app.StopAsync(); }
    }
}

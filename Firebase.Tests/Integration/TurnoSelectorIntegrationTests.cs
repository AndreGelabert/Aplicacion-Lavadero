using System.Net;
using System.Text.Json;
using Firebase.Models;
using Firebase.Security;
using Firebase.Services;
using FirebaseLoginCustom.Controllers;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace Firebase.Tests.Integration;

public class TurnoSelectorIntegrationTests
{
    [LocalFirestoreFact]
    public async Task PopulatedSelectorsEnforcePermissionsAndDoNotWrite()
    {
        Assert.Equal("127.0.0.1:8190", Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST"));
        var db = new FirestoreDbBuilder { ProjectId = "demo-selector-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        async Task Put(string collection, string id, object data) => await db.Collection(collection).Document(id).SetAsync(data);
        await Put("clientes", "c1", new Cliente { Id = "c1", Nombre = "Ana", Apellido = "Perez", TipoDocumento = "DNI", NumeroDocumento = "123", Email = "private@example.test", Telefono = "1234567890" });
        await Put("clientes", "c2", new Cliente { Id = "c2", Nombre = "Luis", Apellido = "Diaz", TipoDocumento = "DNI", NumeroDocumento = "456", Email = "other@example.test", Telefono = "1234567890", Estado = "Inactivo" });
        await Put("vehiculos", "v1", new Vehiculo { Id = "v1", Patente = "ABC123", Marca = "Ford", Modelo = "Focus", Color = "Rojo", TipoVehiculo = "Auto", Estado = "Activo", ClienteId = "c1", ClientesIds = new() { "c1", "c2" } });
        await Put("vehiculos", "v2", new Vehiculo { Id = "v2", Patente = "ABC999", Marca = "Ford", Modelo = "Fiesta", Color = "Azul", TipoVehiculo = "Auto", Estado = "Inactivo" });
        foreach (var (id, tipo, estado, precio, minutos) in new[] { ("s1", "Auto", "Activo", 100d, 30), ("s2", "Auto", "Activo", 50d, 15), ("s3", "Auto", "Inactivo", 10d, 5), ("s4", "Moto", "Activo", 20d, 10) })
            await Put("servicios", id, new { Nombre = id, Tipo = id, TipoVehiculo = tipo, Estado = estado, Precio = precio, TiempoEstimado = minutos, Descripcion = "desc" });
        foreach (var (id, tipo, estado) in new[] { ("p1", "Auto", "Activo"), ("p2", "Auto", "Inactivo"), ("p3", "Moto", "Activo") })
            await Put("paquetes_servicios", id, new { Nombre = id, TipoVehiculo = tipo, Estado = estado, PorcentajeDescuento = 20d, ServiciosIds = new[] { "s1", "s2" } });
        foreach (var (id, discount) in new[] { ("p-zero", 0d), ("p-full", 100d), ("p-fraction", 12.5d) })
            await Put("paquetes_servicios", id, new { Nombre = id, TipoVehiculo = "Moto", Estado = "Activo", PorcentajeDescuento = discount, ServiciosIds = new[] { "m1", "m2" } });
        await Put("servicios", "m1", new { Nombre = "m1", Tipo = "m1", TipoVehiculo = "Moto", Estado = "Activo", Precio = 101.25d, TiempoEstimado = 21, Descripcion = "desc" });
        await Put("servicios", "m2", new { Nombre = "m2", Tipo = "m2", TipoVehiculo = "Moto", Estado = "Activo", Precio = 48.75d, TiempoEstimado = 14, Descripcion = "desc" });
        foreach (var second in new[] { "missing", "s1", "s3", "s4" })
            await Put("paquetes_servicios", "invalid-" + second, new { Nombre = "invalid", TipoVehiculo = "Auto", Estado = "Activo", PorcentajeDescuento = 20d, ServiciosIds = new[] { "s1", second } });
        var collections = new[] { "clientes", "vehiculos", "servicios", "paquetes_servicios", "turnos", "turno_solicitudes", "coordinacion" };
        async Task<string[]> State() => (await Task.WhenAll(collections.Select(async c => (await db.Collection(c).GetSnapshotAsync()).Documents.Select(d => $"{c}/{d.Id}:{d.UpdateTime}").ToArray()))).SelectMany(x => x).OrderBy(x => x).ToArray();
        var before = await State();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(TurnosController).Assembly.GetName().Name, EnvironmentName = "Development" });
        builder.Host.UseDefaultServiceProvider(o => o.ValidateOnBuild = false);
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews().AddControllersAsServices();
        builder.Services.AddTransient(sp => new LavadosController(null!, null!, null!, sp.GetRequiredService<ServicioService>(), sp.GetRequiredService<PaqueteServicioService>(), null!, null!, null!, null!, null!, null!, null!, null!));
        builder.Services.AddAuthentication("Test").AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, InsumosPermisosHttpIntegrationTests.HeaderAuth>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db); builder.Services.AddMemoryCache();
        builder.Services.AddScoped<ClienteService>(); builder.Services.AddScoped<VehiculoService>(); builder.Services.AddScoped<ServicioService>(); builder.Services.AddScoped<PaqueteServicioService>();
        builder.Services.AddScoped<ITurnoSelectorService, TurnoSelectorService>();
        builder.Services.AddSingleton(Mock.Of<ITurnoConsultaService>());
        await using var app = builder.Build();
        app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            const string search = "/Turnos/BuscarVehiculosPorPatente?search=";
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(search + "ABC123")).StatusCode);
            client.DefaultRequestHeaders.Add("X-Test-Permissions", "Lavados.Consultar");
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(search + "ABC123")).StatusCode);
            client.DefaultRequestHeaders.Remove("X-Test-Permissions");
            async Task<JsonElement> Get(string path) { using var response = await client.GetAsync(path); Assert.Equal(HttpStatusCode.OK, response.StatusCode); return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone(); }
            client.DefaultRequestHeaders.Add("X-Test-Permissions", "Turnos.Crear");
            Assert.Empty((await Get(search + "ABC")).EnumerateArray());
            var vehicle = Assert.Single((await Get(search + "abc123")).EnumerateArray());
            Assert.Equal("v1", vehicle.GetProperty("id").GetString()); Assert.Equal("ABC123", vehicle.GetProperty("patente").GetString());
            Assert.False(vehicle.TryGetProperty("clienteId", out _)); Assert.False(vehicle.TryGetProperty("claveAsociacionHash", out _));
            Assert.Empty((await Get(search + "ABC999")).EnumerateArray());
            const string linked = "/Turnos/ObtenerClientesVehiculo?vehiculoId=v1";
            Assert.Empty((await Get(linked)).EnumerateArray());
            Assert.Empty((await Get(linked + "&patente=ABC999")).EnumerateArray());
            var customer = Assert.Single((await Get(linked + "&patente=abc123")).EnumerateArray());
            Assert.Equal("c1", customer.GetProperty("id").GetString()); Assert.Equal("Ana Perez", customer.GetProperty("nombre").GetString()); Assert.Equal("DNI: 123", customer.GetProperty("documento").GetString());
            Assert.False(customer.TryGetProperty("email", out _)); Assert.False(customer.TryGetProperty("telefono", out _));
            foreach (var permission in new[] { "Turnos.Crear", "Turnos.Consultar" })
            {
                client.DefaultRequestHeaders.Remove("X-Test-Permissions"); client.DefaultRequestHeaders.Add("X-Test-Permissions", permission);
                var services = (await Get("/Turnos/ObtenerServiciosPorTipoVehiculo?tipoVehiculo=Auto")).EnumerateArray().ToArray();
                Assert.Equal(new[] { "s1", "s2" }, services.Select(x => x.GetProperty("id").GetString()));
                Assert.Equal(100m, services[0].GetProperty("precio").GetDecimal()); Assert.Equal(30, services[0].GetProperty("tiempoEstimado").GetInt32());
                var package = Assert.Single((await Get("/Turnos/ObtenerPaquetesPorTipoVehiculo?tipoVehiculo=Auto")).EnumerateArray());
                Assert.Equal("p1", package.GetProperty("id").GetString()); Assert.Equal(150m, package.GetProperty("precioOriginal").GetDecimal());
                Assert.Equal(120m, package.GetProperty("precio").GetDecimal()); Assert.Equal(45, package.GetProperty("tiempoEstimado").GetInt32()); Assert.Equal(20m, package.GetProperty("descuento").GetDecimal());
                Assert.Equal(new[] { "s1", "s2" }, package.GetProperty("servicios").EnumerateArray().Select(x => x.GetProperty("id").GetString()));
            }
            var turnosPackages = (await Get("/Turnos/ObtenerPaquetesPorTipoVehiculo?tipoVehiculo=Moto")).EnumerateArray().ToDictionary(x => x.GetProperty("id").GetString()!);
            client.DefaultRequestHeaders.Remove("X-Test-Permissions"); client.DefaultRequestHeaders.Add("X-Test-Permissions", "Lavados.Crear");
            var lavadoPackages = (await Get("/Lavados/ObtenerPaquetesPorTipoVehiculo?tipoVehiculo=Moto")).EnumerateArray().ToDictionary(x => x.GetProperty("id").GetString()!);
            foreach (var (id, expected) in new[] { ("p-zero", 150m), ("p-full", 0m), ("p-fraction", 131.25m) })
            {
                var turno = turnosPackages[id]; var lavado = lavadoPackages[id];
                Assert.Equal(expected, turno.GetProperty("precio").GetDecimal());
                Assert.Equal(expected, lavado.GetProperty("precio").GetDecimal());
                Assert.Equal(35, turno.GetProperty("tiempoEstimado").GetInt32());
                Assert.Equal(35, lavado.GetProperty("tiempoEstimado").GetInt32());
                Assert.Equal(turno.GetProperty("precioOriginal").GetDecimal(), lavado.GetProperty("precioOriginal").GetDecimal());
                Assert.Equal(turno.GetProperty("descuento").GetDecimal(), lavado.GetProperty("descuento").GetDecimal());
                Assert.Equal(turno.GetProperty("servicios").GetRawText(), lavado.GetProperty("servicios").GetRawText());
            }
            client.DefaultRequestHeaders.Remove("X-Test-Permissions"); client.DefaultRequestHeaders.Add("X-Test-Permissions", "Turnos.Consultar");
            Assert.Single((await Get(search + "ABC")).EnumerateArray());
            Assert.Single((await Get(linked)).EnumerateArray());
            Assert.Empty((await Get("/Turnos/ObtenerClientesVehiculo?vehiculoId=v2")).EnumerateArray());
            Assert.Equal(before, await State());
        }
        finally { await app.StopAsync(); }
    }
}

using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Firebase.Controllers;
using Firebase.Models;
using Firebase.Security;
using Firebase.Services;
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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Firebase.Tests.Integration;

public sealed class ClienteHttpIntegrationTests
{
    public sealed class HeaderAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers["X-Test-Permissions"].ToString();
            if (string.IsNullOrEmpty(header))
                header = Request.Headers["X-Test-Permission"].ToString();

            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "test-user-id"), new(ClaimTypes.Email, "test@example.invalid") };
            claims.AddRange(header.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(p => new Claim("permiso", p)));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }

    private static async Task<(WebApplication App, HttpClient Client)> CreateTestServerAsync(FirestoreDb db)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(ClienteController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, HeaderAuth>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db);
        builder.Services.AddMemoryCache();
        builder.Services.AddScoped<ConfiguracionService>();
        builder.Services.AddScoped<InsumoService>();
        builder.Services.AddScoped<StockService>();
        builder.Services.AddScoped<ClienteService>();
        builder.Services.AddScoped<TipoDocumentoService>();
        builder.Services.AddScoped<VehiculoService>();
        builder.Services.AddScoped<IAuditWriter, AuditService>();
        builder.Services.AddScoped<AuditService>();
        builder.Services.AddScoped<ClienteLifecycleService>();
        builder.Services.AddScoped<ClienteVehicleService>();
        builder.Services.AddSingleton(Mock.Of<IEmailSender>());
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/test-token", (HttpContext context, IAntiforgery anti) => anti.GetAndStoreTokens(context).RequestToken!).RequireAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();

        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() })
        {
            BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single())
        };
        return (app, client);
    }

    [LocalFirestoreFact]
    public async Task ClienteHttp_CrearClienteAjaxConVehiculoNuevo_PersisteBidireccionalYAudita()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-cli-http-crear-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var (app, client) = await CreateTestServerAsync(db);
        try
        {
            client.DefaultRequestHeaders.Add("X-Test-Permissions", "Clientes.Consultar,Clientes.Crear,Vehiculos.Crear");
            var token = await client.GetStringAsync("/test-token");

            var vehiculosPayload = JsonSerializer.Serialize(new[]
            {
                new ClienteVehiculoInput
                {
                    Patente = "AA123BB",
                    Marca = "Toyota",
                    Modelo = "Corolla",
                    TipoVehiculo = "Auto",
                    Color = "Gris",
                    EsNuevo = true,
                    MarcaManual = true,
                    ModeloManual = true
                }
            });

            var form = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Nombre"] = "Carlos",
                ["Apellido"] = "Gomez",
                ["TipoDocumento"] = "DNI",
                ["NumeroDocumento"] = "35123456",
                ["Telefono"] = "1144556677",
                ["Email"] = "carlos@example.invalid",
                ["NotificarPorEmail"] = "true",
                ["NotificarPorWhatsApp"] = "false",
                ["vehiculosDataJson"] = vehiculosPayload
            };

            var response = await client.PostAsync("/Cliente/CrearClienteAjax", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.Contains("X-Form-Valid"));
            Assert.Equal("true", response.Headers.GetValues("X-Form-Valid").Single());

            // Verificar cliente en Firestore
            var clientsSnapshot = await db.Collection("clientes").WhereEqualTo("NumeroDocumento", "35123456").GetSnapshotAsync();
            var clientDoc = Assert.Single(clientsSnapshot.Documents);
            var cliente = clientDoc.ConvertTo<Cliente>();
            Assert.Equal("Carlos", cliente.Nombre);
            Assert.Equal("Gomez", cliente.Apellido);
            Assert.Single(cliente.VehiculosIds);

            // Verificar vehiculo en Firestore con clave SHA-256
            var vehSnapshot = await db.Collection("vehiculos").WhereEqualTo("Patente", "AA123BB").GetSnapshotAsync();
            var vehDoc = Assert.Single(vehSnapshot.Documents);
            var vehiculo = vehDoc.ConvertTo<Vehiculo>();
            Assert.Equal(cliente.Id, vehiculo.ClienteId);
            Assert.Contains(cliente.Id, vehiculo.ClientesIds);
            Assert.Equal(cliente.VehiculosIds[0], vehiculo.Id);
            Assert.NotEmpty(vehiculo.ClaveAsociacionHash);

            // Verificar auditoria
            var auditSnapshot = await db.Collection("registros_auditoria").GetSnapshotAsync();
            Assert.NotEmpty(auditSnapshot.Documents);
            Assert.Contains(auditSnapshot.Documents, d => d.GetValue<string>("TargetType") == "Cliente");
            Assert.Contains(auditSnapshot.Documents, d => d.GetValue<string>("TargetType") == "Vehiculo");
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [LocalFirestoreFact]
    public async Task ClienteHttp_CrearClienteAjax_SinPermisoVehiculosCrear_RetornaForbidden()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-cli-http-forbid-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var (app, client) = await CreateTestServerAsync(db);
        try
        {
            // Solo Clientes.Crear, sin Vehiculos.Crear
            client.DefaultRequestHeaders.Add("X-Test-Permissions", "Clientes.Consultar,Clientes.Crear");
            var token = await client.GetStringAsync("/test-token");

            var vehiculosPayload = JsonSerializer.Serialize(new[]
            {
                new ClienteVehiculoInput
                {
                    Patente = "BB999CC",
                    Marca = "Ford",
                    Modelo = "Fiesta",
                    TipoVehiculo = "Auto",
                    Color = "Rojo",
                    EsNuevo = true,
                    MarcaManual = true,
                    ModeloManual = true
                }
            });

            var form = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Nombre"] = "Sin",
                ["Apellido"] = "Permiso",
                ["TipoDocumento"] = "DNI",
                ["NumeroDocumento"] = "99887766",
                ["Telefono"] = "1199887766",
                ["Email"] = "sinpermiso@example.invalid",
                ["NotificarPorEmail"] = "true",
                ["NotificarPorWhatsApp"] = "false",
                ["vehiculosDataJson"] = vehiculosPayload
            };

            var response = await client.PostAsync("/Cliente/CrearClienteAjax", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

            // Nada persistido
            var clientsSnapshot = await db.Collection("clientes").GetSnapshotAsync();
            Assert.Empty(clientsSnapshot.Documents);
            var vehSnapshot = await db.Collection("vehiculos").GetSnapshotAsync();
            Assert.Empty(vehSnapshot.Documents);
            var auditSnapshot = await db.Collection("registros_auditoria").GetSnapshotAsync();
            Assert.Empty(auditSnapshot.Documents);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [LocalFirestoreFact]
    public async Task ClienteHttp_ActualizarClienteAjaxConAsociacionPorClave_VinculaCorrectamente()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-cli-http-assoc-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var clientService = new ClienteService(db);
        var vehService = new VehiculoService(db);

        // Pre-insertar cliente titular con vehículo propio
        var titular = new Cliente
        {
            Id = "titular-1",
            Nombre = "Titular",
            Apellido = "Uno",
            TipoDocumento = "DNI",
            NumeroDocumento = "11111111",
            Telefono = "1100000001",
            Email = "titular@example.invalid",
            Estado = "Activo",
            NotificarPorEmail = true
        };
        await clientService.CrearCliente(titular);

        const string claveAsociacion = "SECRET123";
        var vehTitular = new Vehiculo
        {
            Id = "veh-titular-1",
            Patente = "AB222CD",
            Marca = "Toyota",
            Modelo = "Yaris",
            TipoVehiculo = "Auto",
            Color = "Azul",
            Estado = "Activo",
            ClienteId = titular.Id,
            ClientesIds = new List<string> { titular.Id },
            ClaveAsociacionHash = VehiculoService.HashClaveAsociacion(claveAsociacion)
        };
        await vehService.CrearVehiculo(vehTitular);
        titular.VehiculosIds = new List<string> { vehTitular.Id };
        await clientService.ActualizarCliente(titular);

        // Pre-insertar cliente 2 que tiene su propio auto
        var cliente2 = new Cliente
        {
            Id = "cliente-2",
            Nombre = "Maria",
            Apellido = "Asociada",
            TipoDocumento = "DNI",
            NumeroDocumento = "22222222",
            Telefono = "1100000002",
            Email = "maria@example.invalid",
            Estado = "Activo",
            NotificarPorEmail = true
        };
        await clientService.CrearCliente(cliente2);

        var vehPropio = new Vehiculo
        {
            Id = "veh-propio-2",
            Patente = "XY888ZZ",
            Marca = "Fiat",
            Modelo = "Cronos",
            TipoVehiculo = "Auto",
            Color = "Blanco",
            Estado = "Activo",
            ClienteId = cliente2.Id,
            ClientesIds = new List<string> { cliente2.Id },
            ClaveAsociacionHash = VehiculoService.HashClaveAsociacion("CLAVEPROPIA")
        };
        await vehService.CrearVehiculo(vehPropio);
        cliente2.VehiculosIds = new List<string> { vehPropio.Id };
        await clientService.ActualizarCliente(cliente2);

        var (app, client) = await CreateTestServerAsync(db);
        try
        {
            client.DefaultRequestHeaders.Add("X-Test-Permissions", "Clientes.Consultar,Clientes.Modificar,Vehiculos.Crear");
            var token = await client.GetStringAsync("/test-token");

            // Payload para actualizar Cliente 2: conserva su vehiculo y asocia el vehiculo del titular via clave
            var vehiculosPayload = JsonSerializer.Serialize(new[]
            {
                new ClienteVehiculoInput
                {
                    Id = vehPropio.Id,
                    Patente = vehPropio.Patente,
                    Marca = vehPropio.Marca,
                    Modelo = vehPropio.Modelo,
                    TipoVehiculo = vehPropio.TipoVehiculo,
                    Color = vehPropio.Color,
                    EsNuevo = false,
                    EsAsociacion = false
                },
                new ClienteVehiculoInput
                {
                    Patente = "AB222CD",
                    Marca = "Toyota",
                    Modelo = "Yaris",
                    TipoVehiculo = "Auto",
                    Color = "Azul",
                    EsNuevo = false,
                    EsAsociacion = true,
                    ClaveAsociacion = claveAsociacion
                }
            });

            var form = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = cliente2.Id,
                ["Nombre"] = "Maria",
                ["Apellido"] = "Asociada",
                ["TipoDocumento"] = "DNI",
                ["NumeroDocumento"] = "22222222",
                ["Telefono"] = "1100000002",
                ["Email"] = "maria@example.invalid",
                ["NotificarPorEmail"] = "true",
                ["NotificarPorWhatsApp"] = "false",
                ["vehiculosDataJson"] = vehiculosPayload
            };

            var response = await client.PostAsync("/Cliente/ActualizarClienteAjax", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.Contains("X-Form-Valid"));
            Assert.Equal("true", response.Headers.GetValues("X-Form-Valid").Single());

            // Verificar vinculación en Firestore
            var vehActualizado = (await vehService.ObtenerVehiculo(vehTitular.Id))!;
            Assert.Equal(titular.Id, vehActualizado.ClienteId); // Titular original conservado
            Assert.Contains(titular.Id, vehActualizado.ClientesIds);
            Assert.Contains(cliente2.Id, vehActualizado.ClientesIds);

            var cliente2Actualizado = (await clientService.ObtenerCliente(cliente2.Id))!;
            Assert.Contains(vehTitular.Id, cliente2Actualizado.VehiculosIds);
            Assert.Contains(vehPropio.Id, cliente2Actualizado.VehiculosIds);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [LocalFirestoreFact]
    public async Task ClienteHttp_DeactivateYReactivateCliente_PreservaCompartidosYActualizaEstados()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-cli-http-deact-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var clientService = new ClienteService(db);
        var vehService = new VehiculoService(db);

        var clienteA = new Cliente { Id = "cli-a", Nombre = "Pedro", Apellido = "Alfa", TipoDocumento = "DNI", NumeroDocumento = "44444441", Telefono = "1111111111", Email = "a@example.invalid", Estado = "Activo", NotificarPorEmail = true };
        var clienteB = new Cliente { Id = "cli-b", Nombre = "Ana", Apellido = "Beta", TipoDocumento = "DNI", NumeroDocumento = "44444442", Telefono = "1122222222", Email = "b@example.invalid", Estado = "Activo", NotificarPorEmail = true };
        await clientService.CrearCliente(clienteA);
        await clientService.CrearCliente(clienteB);

        var vehExclusivo = new Vehiculo { Id = "veh-exc", Patente = "EX111AA", Marca = "VW", Modelo = "Gol", TipoVehiculo = "Auto", Color = "Blanco", Estado = "Activo", ClienteId = clienteA.Id, ClientesIds = new() { clienteA.Id } };
        var vehCompartido = new Vehiculo { Id = "veh-sha", Patente = "SH222BB", Marca = "Renault", Modelo = "Clio", TipoVehiculo = "Auto", Color = "Negro", Estado = "Activo", ClienteId = clienteA.Id, ClientesIds = new() { clienteA.Id, clienteB.Id } };
        await vehService.CrearVehiculo(vehExclusivo);
        await vehService.CrearVehiculo(vehCompartido);

        clienteA.VehiculosIds = new() { vehExclusivo.Id, vehCompartido.Id };
        clienteB.VehiculosIds = new() { vehCompartido.Id };
        await clientService.ActualizarCliente(clienteA);
        await clientService.ActualizarCliente(clienteB);

        var (app, client) = await CreateTestServerAsync(db);
        try
        {
            client.DefaultRequestHeaders.Add("X-Test-Permissions", "Clientes.Consultar,Clientes.Eliminar,Clientes.Modificar");
            var token = await client.GetStringAsync("/test-token");

            // 1. Desactivar Cliente A
            var deactForm = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = clienteA.Id
            };
            var deactResp = await client.PostAsync("/Cliente/DeactivateCliente", new FormUrlEncodedContent(deactForm));
            Assert.Equal(HttpStatusCode.OK, deactResp.StatusCode);
            var deactJson = JsonSerializer.Deserialize<JsonElement>(await deactResp.Content.ReadAsStringAsync());
            Assert.True(deactJson.GetProperty("success").GetBoolean());

            // Verificar estados en DB: Cliente A inactivo, Exclusivo inactivo, Compartido ACTIVO
            var cAAfterDeact = (await clientService.ObtenerCliente(clienteA.Id))!;
            Assert.Equal("Inactivo", cAAfterDeact.Estado);
            var vExcAfterDeact = (await vehService.ObtenerVehiculo(vehExclusivo.Id))!;
            Assert.Equal("Inactivo", vExcAfterDeact.Estado);
            var vShaAfterDeact = (await vehService.ObtenerVehiculo(vehCompartido.Id))!;
            Assert.Equal("Activo", vShaAfterDeact.Estado);

            // 2. Reactivar Cliente A
            var token2 = await client.GetStringAsync("/test-token");
            var reactForm = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token2,
                ["id"] = clienteA.Id
            };
            var reactResp = await client.PostAsync("/Cliente/ReactivateCliente", new FormUrlEncodedContent(reactForm));
            Assert.Equal(HttpStatusCode.OK, reactResp.StatusCode);
            var reactJson = JsonSerializer.Deserialize<JsonElement>(await reactResp.Content.ReadAsStringAsync());
            Assert.True(reactJson.GetProperty("success").GetBoolean());

            // Verificar estados en DB: Cliente A activo, Exclusivo reactivado a Activo, Compartido sigue Activo
            var cAAfterReact = (await clientService.ObtenerCliente(clienteA.Id))!;
            Assert.Equal("Activo", cAAfterReact.Estado);
            var vExcAfterReact = (await vehService.ObtenerVehiculo(vehExclusivo.Id))!;
            Assert.Equal("Activo", vExcAfterReact.Estado);
            var vShaAfterReact = (await vehService.ObtenerVehiculo(vehCompartido.Id))!;
            Assert.Equal("Activo", vShaAfterReact.Estado);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [LocalFirestoreFact]
    public async Task ClienteHttp_CrearClienteAjax_ConTelefonoDuplicado_RetornaError()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-cli-dup-tel-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var clientService = new ClienteService(db);
        var existingClient = new Cliente
        {
            Id = "cli-existente-tel",
            Nombre = "Existente",
            Apellido = "Primero",
            TipoDocumento = "DNI",
            NumeroDocumento = "11111111",
            Telefono = "1144556677",
            Email = "primero@example.invalid",
            Estado = "Activo",
            NotificarPorEmail = true
        };
        await clientService.CrearCliente(existingClient);

        var (app, client) = await CreateTestServerAsync(db);
        try
        {
            client.DefaultRequestHeaders.Add("X-Test-Permissions", "Clientes.Consultar,Clientes.Crear,Vehiculos.Crear");
            var token = await client.GetStringAsync("/test-token");

            var vehiculosPayload = JsonSerializer.Serialize(new[]
            {
                new ClienteVehiculoInput
                {
                    Patente = "BB999CC",
                    Marca = "Ford",
                    Modelo = "Fiesta",
                    TipoVehiculo = "Auto",
                    Color = "Rojo",
                    EsNuevo = true,
                    MarcaManual = true,
                    ModeloManual = true
                }
            });

            var form = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Nombre"] = "Segundo",
                ["Apellido"] = "Nuevo",
                ["TipoDocumento"] = "DNI",
                ["NumeroDocumento"] = "22222222",
                ["Telefono"] = "1144556677", // Mismo teléfono
                ["Email"] = "segundo@example.invalid",
                ["NotificarPorEmail"] = "true",
                ["NotificarPorWhatsApp"] = "false",
                ["vehiculosDataJson"] = vehiculosPayload
            };

            var response = await client.PostAsync("/Cliente/CrearClienteAjax", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.Contains("X-Form-Valid"));
            Assert.Equal("false", response.Headers.GetValues("X-Form-Valid").Single());

            var html = await response.Content.ReadAsStringAsync();
            var decodedHtml = WebUtility.HtmlDecode(html);
            Assert.Contains("Ya existe un cliente con este número de teléfono.", decodedHtml);

            // No se debe haber creado el segundo cliente
            var clientsSnapshot = await db.Collection("clientes").WhereEqualTo("NumeroDocumento", "22222222").GetSnapshotAsync();
            Assert.Empty(clientsSnapshot.Documents);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [LocalFirestoreFact]
    public async Task ClienteHttp_ActualizarClienteAjax_ConTelefonoDuplicadoDeOtroCliente_RetornaError()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-cli-edit-tel-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var clientService = new ClienteService(db);
        var vehService = new VehiculoService(db);

        var clienteA = new Cliente
        {
            Id = "cli-edit-a",
            Nombre = "Pedro",
            Apellido = "Alfa",
            TipoDocumento = "DNI",
            NumeroDocumento = "11111111",
            Telefono = "1111111111",
            Email = "a@example.invalid",
            Estado = "Activo",
            NotificarPorEmail = true
        };
        await clientService.CrearCliente(clienteA);

        var vehA = new Vehiculo { Id = "veh-a", Patente = "AA111AA", TipoVehiculo = "Auto", Marca = "Fiat", Modelo = "Palio", Color = "Blanco", Estado = "Activo", ClienteId = clienteA.Id, ClientesIds = new List<string> { clienteA.Id } };
        await vehService.CrearVehiculo(vehA);
        clienteA.VehiculosIds = new List<string> { vehA.Id };
        await clientService.ActualizarCliente(clienteA);

        var clienteB = new Cliente
        {
            Id = "cli-edit-b",
            Nombre = "Maria",
            Apellido = "Beta",
            TipoDocumento = "DNI",
            NumeroDocumento = "22222222",
            Telefono = "2222222222",
            Email = "b@example.invalid",
            Estado = "Activo",
            NotificarPorEmail = true
        };
        await clientService.CrearCliente(clienteB);

        var (app, client) = await CreateTestServerAsync(db);
        try
        {
            client.DefaultRequestHeaders.Add("X-Test-Permissions", "Clientes.Consultar,Clientes.Modificar,Vehiculos.Crear");
            var token = await client.GetStringAsync("/test-token");

            var vehiculosPayload = JsonSerializer.Serialize(new[]
            {
                new ClienteVehiculoInput
                {
                    Id = vehA.Id,
                    Patente = vehA.Patente,
                    Marca = vehA.Marca,
                    Modelo = vehA.Modelo,
                    TipoVehiculo = vehA.TipoVehiculo,
                    Color = vehA.Color
                }
            });

            // Intentar cambiar teléfono de clienteA al teléfono de clienteB
            var form = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = clienteA.Id,
                ["Nombre"] = clienteA.Nombre,
                ["Apellido"] = clienteA.Apellido,
                ["TipoDocumento"] = clienteA.TipoDocumento,
                ["NumeroDocumento"] = clienteA.NumeroDocumento,
                ["Telefono"] = clienteB.Telefono, // Conflicto
                ["Email"] = clienteA.Email,
                ["NotificarPorEmail"] = "true",
                ["NotificarPorWhatsApp"] = "false",
                ["vehiculosDataJson"] = vehiculosPayload
            };

            var response = await client.PostAsync("/Cliente/ActualizarClienteAjax", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.Contains("X-Form-Valid"));
            Assert.Equal("false", response.Headers.GetValues("X-Form-Valid").Single());

            var html = await response.Content.ReadAsStringAsync();
            var decodedHtml = WebUtility.HtmlDecode(html);
            Assert.Contains("Ya existe un cliente con este número de teléfono.", decodedHtml);

            // Verificar que no se modificó en DB
            var cAEnDb = (await clientService.ObtenerCliente(clienteA.Id))!;
            Assert.Equal("1111111111", cAEnDb.Telefono);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [LocalFirestoreFact]
    public async Task ClienteHttp_VerificarTelefono_DetectaDuplicadosCorrectamente()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-cli-check-tel-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var clientService = new ClienteService(db);
        var cliente = new Cliente
        {
            Id = "cli-verif-1",
            Nombre = "Verif",
            Apellido = "Test",
            TipoDocumento = "DNI",
            NumeroDocumento = "55555555",
            Telefono = "1133334444",
            Email = "verif@example.invalid",
            Estado = "Activo",
            NotificarPorEmail = true
        };
        await clientService.CrearCliente(cliente);

        var (app, client) = await CreateTestServerAsync(db);
        try
        {
            client.DefaultRequestHeaders.Add("X-Test-Permissions", "Clientes.Crear,Clientes.Modificar");

            // Teléfono existente sin pasar ID -> existe: true
            var resp1 = await client.GetAsync("/Cliente/VerificarTelefono?telefono=1133334444");
            Assert.Equal(HttpStatusCode.OK, resp1.StatusCode);
            var json1 = JsonSerializer.Deserialize<JsonElement>(await resp1.Content.ReadAsStringAsync());
            Assert.True(json1.GetProperty("existe").GetBoolean());

            // Teléfono existente pasando el mismo ID del dueño -> existe: false (permite mantener su propio teléfono)
            var resp2 = await client.GetAsync($"/Cliente/VerificarTelefono?telefono=1133334444&id={cliente.Id}");
            Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);
            var json2 = JsonSerializer.Deserialize<JsonElement>(await resp2.Content.ReadAsStringAsync());
            Assert.False(json2.GetProperty("existe").GetBoolean());

            // Teléfono no registrado -> existe: false
            var resp3 = await client.GetAsync("/Cliente/VerificarTelefono?telefono=9999999999");
            Assert.Equal(HttpStatusCode.OK, resp3.StatusCode);
            var json3 = JsonSerializer.Deserialize<JsonElement>(await resp3.Content.ReadAsStringAsync());
            Assert.False(json3.GetProperty("existe").GetBoolean());
        }
        finally
        {
            await app.StopAsync();
        }
    }
}

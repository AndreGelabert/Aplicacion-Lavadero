using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Firebase.Controllers;
using Firebase.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Moq;

namespace Firebase.Tests.Integration;

public sealed class InsumoCatalogIntegrationTests
{
    private static FirestoreDb Db() => new FirestoreDbBuilder
    {
        ProjectId = "demo-catalogo-" + Guid.NewGuid().ToString("N")[..16],
        EmulatorDetection = EmulatorDetection.EmulatorOnly
    }.Build();

    private static async Task<SolicitudCotizacion> EmitirSolicitud(ComprasService compras, string proveedorId, string insumoId)
    {
        var anteriores = (await compras.ObtenerSolicitudes()).Select(x => x.Id).ToHashSet();
        Assert.True((await compras.CrearSolicitudes(new() { proveedorId }, new() { new OrdenCompraItem { InsumoId = insumoId, Cantidad = 1 } }, null)).EsExitoso);
        return Assert.Single((await compras.ObtenerSolicitudes()).Where(x => !anteriores.Contains(x.Id)));
    }

    private static IEmailSender TransporteAceptado()
    {
        var sender = new Mock<IEmailSender>();
        sender.Setup(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ResultadoEnvioEmail(true, "Aceptado"));
        return sender.Object;
    }

    public sealed class Auth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(new[] {
                new Claim(ClaimTypes.NameIdentifier, "test"), new Claim(ClaimTypes.Email, "test@example.invalid"),
                new Claim("permiso", "Insumos.Crear"), new Claim("permiso", "Insumos.Modificar") }, Scheme.Name)), Scheme.Name)));
    }

    [LocalFirestoreFact]
    public async Task FormularioHttpVinculaToggleYMultiplesProveedoresEnAltaYEdicion()
    {
        var db = Db();
        // Explicit audited fixture setup; GET must not initialize catalog types.
        var tipos = new TipoInsumoService(db);
        var herramienta = await tipos.AsegurarTipoHerramienta("test", "test@example.invalid");
        var quimico = new TipoInsumo { Nombre = "Quimico", Categoria = TipoInsumo.Categorias.Volumen, UnidadResumen = "ml" };
        Assert.True((await tipos.Guardar(quimico, true, "test", "test@example.invalid")).EsExitoso);
        var proveedores = new ProveedorService(db);
        var p1 = new Proveedor { RazonSocial = "Proveedor Uno", IdentificacionTributaria = "30-00000001-5", Contacto = "1234567890", EmailOrdenes = "uno@example.invalid" };
        var p2 = new Proveedor { RazonSocial = "Proveedor Dos", IdentificacionTributaria = "30-00000002-3", Contacto = "1234567891", EmailOrdenes = "dos@example.invalid" };
        Assert.True((await proveedores.Guardar(p1, true)).EsExitoso);
        Assert.True((await proveedores.Guardar(p2, true)).EsExitoso);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(InsumosController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, Auth>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db);
        builder.Services.AddMemoryCache(); builder.Services.AddScoped<ConfiguracionService>();
        builder.Services.AddScoped<InsumoService>(); builder.Services.AddScoped<ProveedorService>();
        builder.Services.AddScoped<StockService>(); builder.Services.AddScoped<ComprasService>();
        builder.Services.AddSingleton(tipos);
        builder.Services.AddSingleton(Mock.Of<IAuditWriter>()); builder.Services.AddSingleton(Mock.Of<IEmailSender>());
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(address) };
            async Task<string> Page() { var r = await client.GetAsync("/Insumos/Index"); var html = await r.Content.ReadAsStringAsync(); Assert.True(r.StatusCode == HttpStatusCode.OK, html); return html; }
            var page = await Page();
            Assert.Contains("Reabastecimiento automático", page); Assert.Contains("Proveedor Uno", page); Assert.Contains("Proveedor Dos", page);
            Assert.Matches($"(?s)<select[^>]*name=\"TipoId\"[^>]*>.*?<option value=\"{Regex.Escape(herramienta.Id)}\"[^>]*>Herramienta</option>.*?</select>", page);
            var token = Regex.Match(page, "name=\\\"__RequestVerificationToken\\\"[^>]*value=\\\"([^\\\"]+)\\\"").Groups[1].Value;
            Assert.NotEmpty(token);
            async Task Post(string id, string name, bool crear, bool automatico, params string[] ids)
            {
                var fields = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", token), new("crear", crear.ToString().ToLowerInvariant()), new("Id", id), new("Nombre", name), new("TipoId", herramienta.Id), new("Tipo", "Herramienta"), new("UnidadMedida", "unidad"), new("StockMinimo", "0"), new("CantidadReabastecimiento", "0") };
                if (automatico) fields.Add(new("ReabastecimientoAutomatico", "true"));
                fields.AddRange(ids.Select(i => new KeyValuePair<string, string>("ProveedoresIds", i)));
                var response = await client.PostAsync("/Insumos/Guardar", new FormUrlEncodedContent(fields));
                Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            }
            var service = new InsumoService(db);
            await Post("", "Hidro", true, true);
            Assert.Empty(await service.ObtenerTodos());
            page = await Page(); Assert.Contains("Seleccione al menos un proveedor", page);
            await Post("", "Hidro", true, false);
            var item = Assert.Single(await service.ObtenerTodos());
            Assert.False(item.ReabastecimientoAutomatico); Assert.Empty(item.ProveedoresIds); Assert.Equal(0, item.StockActual);
            await Post(item.Id, "Hidro editada", false, true);
            Assert.False((await service.Obtener(item.Id))!.ReabastecimientoAutomatico);
            await Post(item.Id, "Hidro editada", false, true, p1.Id, p2.Id);
            item = (await service.Obtener(item.Id))!;
            Assert.True(item.ReabastecimientoAutomatico); Assert.Equal(new[] { p1.Id, p2.Id }, item.ProveedoresIds);
            page = await Page(); Assert.Contains("Hidro editada", page); Assert.Contains("Editar", page);
            await Post(item.Id, "Hidro editada", false, false);
            item = (await service.Obtener(item.Id))!; Assert.False(item.ReabastecimientoAutomatico); Assert.Empty(item.ProveedoresIds);
            await Post("", "Otro", true, true, p1.Id);
            Assert.Single((await service.ObtenerTodos()).Where(x => x.Nombre == "Otro" && x.ReabastecimientoAutomatico && x.ProveedoresIds.Contains(p1.Id)));
            Assert.True((await service.Guardar(new Insumo { Nombre = "Quimico exclusivo", TipoId = quimico.Id, Tipo = "Quimico", UnidadMedida = "ml" }, true)).EsExitoso);
            var filtered = await client.GetAsync("/Insumos/Index?tipo=Herramienta");
            Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
            var html = await filtered.Content.ReadAsStringAsync();
            Assert.Contains("Hidro editada", html); Assert.Contains("Otro", html); Assert.DoesNotContain("Quimico exclusivo", html);
            Assert.Matches("<input[^>]*name=\"tipos\"[^>]*value=\"Herramienta\"[^>]*checked", html);
            filtered = await client.GetAsync("/Insumos/Index?tipo=Quimico");
            Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
            html = await filtered.Content.ReadAsStringAsync();
            Assert.Contains("Quimico exclusivo", html); Assert.DoesNotContain("Hidro editada", html);
        }
        finally { await app.StopAsync(); }
    }

    [LocalFirestoreFact]
    public async Task DesactivacionRespetaEstadosDeOrdenes()
    {
        var db = Db();
        var insumos = new InsumoService(db);
        var compras = new ComprasService(db);
        foreach (var estado in new[] { "PendienteAprobacion", "RevisionManual", "Enviada", "Confirmada", "Despachada", "Recibida", "Cancelada" })
        {
            var item = new Insumo { Nombre = "Insumo " + estado };
            Assert.True((await insumos.Guardar(item, true)).EsExitoso);
            await db.Collection("ordenesCompra").Document(estado).SetAsync(new OrdenCompra { Id = estado, Estado = estado, Items = new() { new OrdenCompraItem { InsumoId = item.Id, Cantidad = 1 } } });
            var resultado = await insumos.CambiarEstado(item.Id, "Inactivo");
            var abierto = estado is not ("Recibida" or "Cancelada");
            Assert.Equal(!abierto, resultado.EsExitoso);
            if (abierto) Assert.Equal("No se puede desactivar un insumo con órdenes pendientes.", resultado.MensajeError);
            Assert.Equal(abierto ? "Activo" : "Inactivo", (await insumos.Obtener(item.Id))!.Estado);
            Assert.Equal(estado, (await compras.ObtenerOrdenes()).Single(x => x.Id == estado).Estado);
        }
    }

    [LocalFirestoreFact]
    public async Task AdjudicacionNoCreaOrdenParaInsumoDesactivado()
    {
        var db = Db();
        var insumos = new InsumoService(db);
        var compras = new ComprasService(db);
        var proveedor = new Proveedor { RazonSocial = "Proveedor", IdentificacionTributaria = "30-00000001-5", Contacto = "1234567890", EmailOrdenes = "p@example.invalid" };
        Assert.True((await new ProveedorService(db).Guardar(proveedor, true)).EsExitoso);
        var item = new Insumo { Nombre = "Insumo" };
        Assert.True((await insumos.Guardar(item, true)).EsExitoso);
        var solicitud = await EmitirSolicitud(compras, proveedor.Id, item.Id);
        Assert.True((await compras.RegistrarRespuesta(new CotizacionRespuesta { SolicitudId = solicitud.Id, PlazoEntrega = "1", Items = new() { new OrdenCompraItem { InsumoId = item.Id, PrecioUnitario = 1 } } })).EsExitoso);
        Assert.True((await insumos.CambiarEstado(item.Id, "Inactivo")).EsExitoso);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => compras.Adjudicar(solicitud.Id));
        Assert.Equal("El insumo no existe o está inactivo.", error.Message);
        Assert.Equal("Inactivo", (await insumos.Obtener(item.Id))!.Estado);
        Assert.Empty(await compras.ObtenerOrdenes());
        Assert.Equal("Respondida", Assert.Single(await compras.ObtenerSolicitudes()).Estado);
    }

    [LocalFirestoreFact]
    public async Task DesactivacionYCreacionConcurrentesNoDejanOrdenAbiertaConInsumoInactivo()
    {
        var db = Db();
        var insumos = new InsumoService(db);
        var compras = new ComprasService(db);
        var proveedor = new Proveedor { RazonSocial = "Proveedor", IdentificacionTributaria = "30-00000001-5", Contacto = "1234567890", EmailOrdenes = "p@example.invalid" };
        Assert.True((await new ProveedorService(db).Guardar(proveedor, true)).EsExitoso);
        for (var n = 0; n < 20; n++)
        {
            var item = new Insumo { Nombre = "Insumo " + n };
            Assert.True((await insumos.Guardar(item, true)).EsExitoso);
            var solicitud = await EmitirSolicitud(compras, proveedor.Id, item.Id);
            Assert.True((await compras.RegistrarRespuesta(new CotizacionRespuesta { SolicitudId = solicitud.Id, PlazoEntrega = "1", Items = new() { new OrdenCompraItem { InsumoId = item.Id, PrecioUnitario = 1 } } })).EsExitoso);
            async Task<bool> Adjudicar() { try { return (await compras.Adjudicar(solicitud.Id, null, TransporteAceptado())).EsExitoso; } catch (InvalidOperationException e) { Assert.Equal("El insumo no existe o está inactivo.", e.Message); return false; } }
            var adjudicar = Adjudicar();
            var desactivar = insumos.CambiarEstado(item.Id, "Inactivo");
            await Task.WhenAll(adjudicar, desactivar);
            Assert.NotEqual(desactivar.Result.EsExitoso, adjudicar.Result);
            var creadas = (await compras.ObtenerOrdenes()).Where(x => x.Items.Any(i => i.InsumoId == item.Id)).ToList();
            Assert.Equal(adjudicar.Result ? "Activo" : "Inactivo", (await insumos.Obtener(item.Id))!.Estado);
            Assert.Equal(adjudicar.Result ? 1 : 0, creadas.Count);
            if (creadas.Count != 0) Assert.Equal("Enviada", creadas[0].Estado);
        }
    }

    [LocalFirestoreFact]
    public async Task AdjudicacionYDesactivacionConcurrentesNoDejanOrdenConInsumoInactivo()
    {
        var db = Db();
        var insumos = new InsumoService(db);
        var compras = new ComprasService(db);
        var proveedor = new Proveedor { RazonSocial = "Proveedor", IdentificacionTributaria = "30-00000001-5", Contacto = "1234567890", EmailOrdenes = "p@example.invalid" };
        Assert.True((await new ProveedorService(db).Guardar(proveedor, true)).EsExitoso);
        for (var n = 0; n < 10; n++)
        {
            var item = new Insumo { Nombre = "Insumo " + n };
            Assert.True((await insumos.Guardar(item, true)).EsExitoso);
            var solicitud = await EmitirSolicitud(compras, proveedor.Id, item.Id);
            Assert.True((await compras.RegistrarRespuesta(new CotizacionRespuesta { SolicitudId = solicitud.Id, PlazoEntrega = "1", Items = new() { new OrdenCompraItem { InsumoId = item.Id, PrecioUnitario = 1 } } })).EsExitoso);
            async Task<bool> Adjudicar() { try { return (await compras.Adjudicar(solicitud.Id, null, TransporteAceptado())).EsExitoso; } catch (InvalidOperationException e) { Assert.Equal("El insumo no existe o está inactivo.", e.Message); return false; } }
            var adjudicar = Adjudicar();
            var desactivar = insumos.CambiarEstado(item.Id, "Inactivo");
            await Task.WhenAll(adjudicar, desactivar);
            Assert.NotEqual(adjudicar.Result, desactivar.Result.EsExitoso);
            Assert.Equal(adjudicar.Result ? "Activo" : "Inactivo", (await insumos.Obtener(item.Id))!.Estado);
            var ordenes = (await compras.ObtenerOrdenes()).Where(o => o.Items.Any(i => i.InsumoId == item.Id)).ToList();
            Assert.Equal(adjudicar.Result ? 1 : 0, ordenes.Count);
            Assert.Equal(adjudicar.Result ? "Adjudicada" : "Respondida", (await compras.ObtenerSolicitudes()).Single(s => s.Id == solicitud.Id).Estado);
        }
    }

    [LocalFirestoreFact]
    public async Task AltaYEdicionExigenProveedorSoloConReabastecimientoActivo()
    {
        var db = Db();
        await new TipoInsumoService(db).AsegurarTipoHerramienta();
        var service = new InsumoService(db);
        var item = new Insumo { Nombre = "Herramienta", Tipo = "Herramienta", UnidadMedida = "unidad", ReabastecimientoAutomatico = true };
        var rechazo = await service.Guardar(item, true);
        Assert.False(rechazo.EsExitoso);
        Assert.Equal("Seleccione al menos un proveedor para el reabastecimiento automático.", rechazo.MensajeError);
        Assert.Empty(await service.ObtenerTodos());

        item.ReabastecimientoAutomatico = false;
        Assert.True((await service.Guardar(item, true)).EsExitoso);
        Assert.Equal(0, (await service.Obtener(item.Id))!.StockActual);
        item.ReabastecimientoAutomatico = true;
        rechazo = await service.Guardar(item, false);
        Assert.False(rechazo.EsExitoso);
        Assert.Equal("Seleccione al menos un proveedor para el reabastecimiento automático.", rechazo.MensajeError);
        Assert.False((await service.Obtener(item.Id))!.ReabastecimientoAutomatico);
        item.ProveedoresIds = new() { "p1", "p2" };
        Assert.True((await service.Guardar(item, false)).EsExitoso);
        var actual = (await service.Obtener(item.Id))!;
        Assert.True(actual.ReabastecimientoAutomatico);
        Assert.Equal(new[] { "p1", "p2" }, actual.ProveedoresIds);
        var segundo = new Insumo { Nombre = "Otro", ReabastecimientoAutomatico = true, ProveedoresIds = new() { "p1" } };
        Assert.True((await service.Guardar(segundo, true)).EsExitoso);
        Assert.Equal("p1", Assert.Single((await service.Obtener(segundo.Id))!.ProveedoresIds));
        item.ReabastecimientoAutomatico = false;
        item.ProveedoresIds.Clear();
        Assert.True((await service.Guardar(item, false)).EsExitoso);
        Assert.Empty((await service.Obtener(item.Id))!.ProveedoresIds);
    }

    [LocalFirestoreFact]
    public async Task CambiarEstadoInsumoHttpRegistraAuditoriaEnDesactivacionYReactivacionYSinEventoEnRechazo()
    {
        var db = Db();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(InsumosController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, ProveedorCatalogIntegrationTests.HeaderAuth>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db);
        builder.Services.AddMemoryCache(); builder.Services.AddScoped<ConfiguracionService>();
        builder.Services.AddScoped<InsumoService>(); builder.Services.AddScoped<ProveedorService>();
        builder.Services.AddScoped<StockService>(); builder.Services.AddScoped<ComprasService>();
        builder.Services.AddScoped<IAuditWriter, AuditService>();
        var emailSender = new Mock<IEmailSender>();
        emailSender.Setup(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoEnvioEmail(true, "Fake ok"));
        builder.Services.AddSingleton(emailSender.Object);
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(address) };
            client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Consultar,Insumos.Crear,Insumos.Modificar");

            async Task<string> Page(string path = "/Insumos/Index")
            {
                var r = await client.GetAsync(path);
                Assert.Equal(HttpStatusCode.OK, r.StatusCode);
                return WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync());
            }

            var page = await Page();
            var token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.NotEmpty(token);

            async Task<HttpResponseMessage> PostForm(string action, params (string Key, string Value)[] fields)
            {
                var data = fields.Select(x => new KeyValuePair<string, string>(x.Key, x.Value)).Append(new("__RequestVerificationToken", token));
                return await client.PostAsync("/Insumos/" + action, new FormUrlEncodedContent(data));
            }

            Assert.Equal(HttpStatusCode.Redirect, (await PostForm("GuardarProveedor",
                ("crear", "true"),
                ("IdentificacionTributaria", "30-00000001-5"),
                ("RazonSocial", "Proveedor Insumo"),
                ("Contacto", "1234567890"),
                ("EmailOrdenes", "insumo@example.invalid"))).StatusCode);
            var proveedor = Assert.Single(await new ProveedorService(db).ObtenerTodos());

            Assert.Equal(HttpStatusCode.Redirect, (await PostForm("Guardar",
                ("crear", "true"),
                ("Nombre", "Shampoo Neutro"),
                ("Tipo", "Quimico"),
                ("UnidadMedida", "ml"),
                ("StockMinimo", "5"),
                ("CantidadReabastecimiento", "10"),
                ("ReabastecimientoAutomatico", "true"),
                ("ProveedoresIds", proveedor.Id))).StatusCode);

            var insumoService = new InsumoService(db);
            var insumo = Assert.Single(await insumoService.ObtenerTodos());

            var esperadoActivo = new Dictionary<string, string>
            {
                ["Nombre"] = "Shampoo Neutro",
                ["Tipo"] = "Quimico",
                ["UnidadMedida"] = "ml",
                ["StockActual"] = "0",
                ["StockMinimo"] = "5",
                ["CantidadReabastecimiento"] = "10",
                ["ReabastecimientoAutomatico"] = "True",
                ["ProveedoresIds"] = System.Text.Json.JsonSerializer.Serialize(new[] { proveedor.Id }),
                ["Estado"] = "Activo"
            };
            var esperadoInactivo = new Dictionary<string, string>(esperadoActivo) { ["Estado"] = "Inactivo" };

            async Task AssertEstadoAuditEvents(params (string Action, string TargetId, string NombreHistorico, Dictionary<string, string> Antes, Dictionary<string, string> Despues)[] expected)
            {
                var docs = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents
                    .Where(d => d.GetValue<string>("TargetType") == "Insumo" && d.GetValue<string>("Action") != "Creacion de insumo")
                    .ToList();
                Assert.Equal(expected.Length, docs.Count);
                foreach (var e in expected)
                {
                    var doc = Assert.Single(docs, d => d.GetValue<string>("Action") == e.Action && d.GetValue<string>("TargetId") == e.TargetId);
                    Assert.Equal("test", doc.GetValue<string>("UserId"));
                    Assert.Equal("test@example.invalid", doc.GetValue<string>("UserEmail"));
                    Assert.Equal("Insumo", doc.GetValue<string>("TargetType"));
                    Assert.Equal(e.NombreHistorico, doc.GetValue<string>("NombreHistorico"));
                    Assert.Equal(e.Antes.OrderBy(x => x.Key), doc.GetValue<Dictionary<string, string>>("Antes").OrderBy(x => x.Key));
                    Assert.Equal(e.Despues.OrderBy(x => x.Key), doc.GetValue<Dictionary<string, string>>("Despues").OrderBy(x => x.Key));
                    var json = System.Text.Json.JsonSerializer.Serialize(doc.ToDictionary());
                    Assert.DoesNotContain("Password", json, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("Token", json, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("Secret", json, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("ApiKey", json, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("Clave", json, StringComparison.OrdinalIgnoreCase);
                }
            }

            // Desactivación exitosa
            Assert.Equal(HttpStatusCode.Redirect, (await PostForm("CambiarEstado", ("id", insumo.Id), ("estado", "Inactivo"))).StatusCode);
            page = await Page("/Insumos/Index?estados=Activo&estados=Inactivo");
            Assert.Contains("Insumo desactivado.", page);
            Assert.Contains("Reactivar", page);
            Assert.Equal("Inactivo", (await insumoService.Obtener(insumo.Id))!.Estado);
            await AssertEstadoAuditEvents(
                ("Desactivacion de insumo", insumo.Id, "Shampoo Neutro", esperadoActivo, esperadoInactivo));

            // Reactivación exitosa
            Assert.Equal(HttpStatusCode.Redirect, (await PostForm("CambiarEstado", ("id", insumo.Id), ("estado", "Activo"))).StatusCode);
            page = await Page();
            Assert.Contains("Insumo reactivado.", page);
            Assert.Contains("Desactivar", page);
            Assert.Equal("Activo", (await insumoService.Obtener(insumo.Id))!.Estado);
            await AssertEstadoAuditEvents(
                ("Desactivacion de insumo", insumo.Id, "Shampoo Neutro", esperadoActivo, esperadoInactivo),
                ("Reactivacion de insumo", insumo.Id, "Shampoo Neutro", esperadoInactivo, esperadoActivo));

            // Rechazo por insumo inexistente no emite auditoría
            Assert.Equal(HttpStatusCode.Redirect, (await PostForm("CambiarEstado", ("id", "no-existe"), ("estado", "Inactivo"))).StatusCode);
            page = await Page();
            Assert.Contains("El insumo no existe.", page);
            await AssertEstadoAuditEvents(
                ("Desactivacion de insumo", insumo.Id, "Shampoo Neutro", esperadoActivo, esperadoInactivo),
                ("Reactivacion de insumo", insumo.Id, "Shampoo Neutro", esperadoInactivo, esperadoActivo));

            // Rechazo por orden pendiente no emite auditoría ni muta estado
            await db.Collection("ordenesCompra").Document("orden-pendiente").SetAsync(new OrdenCompra
            {
                Id = "orden-pendiente",
                ProveedorId = proveedor.Id,
                Estado = "PendienteAprobacion",
                Items = new() { new() { InsumoId = insumo.Id, Cantidad = 3 } }
            });
            Assert.Equal(HttpStatusCode.Redirect, (await PostForm("CambiarEstado", ("id", insumo.Id), ("estado", "Inactivo"))).StatusCode);
            page = await Page();
            Assert.Contains("No se puede desactivar un insumo con órdenes pendientes.", page);
            Assert.Equal("Activo", (await insumoService.Obtener(insumo.Id))!.Estado);
            await AssertEstadoAuditEvents(
                ("Desactivacion de insumo", insumo.Id, "Shampoo Neutro", esperadoActivo, esperadoInactivo),
                ("Reactivacion de insumo", insumo.Id, "Shampoo Neutro", esperadoInactivo, esperadoActivo));

            // Rechazo por permisos insuficientes (403) no emite auditoría ni muta estado
            foreach (var permiso in new[] { "Insumos.Consultar", "Insumos.Crear" })
            {
                client.DefaultRequestHeaders.Remove("X-Test-Permission");
                client.DefaultRequestHeaders.Add("X-Test-Permission", permiso);
                Assert.Equal(HttpStatusCode.Forbidden, (await PostForm("CambiarEstado", ("id", insumo.Id), ("estado", "Inactivo"))).StatusCode);
            }
            Assert.Equal("Activo", (await insumoService.Obtener(insumo.Id))!.Estado);
            await AssertEstadoAuditEvents(
                ("Desactivacion de insumo", insumo.Id, "Shampoo Neutro", esperadoActivo, esperadoInactivo),
                ("Reactivacion de insumo", insumo.Id, "Shampoo Neutro", esperadoInactivo, esperadoActivo));
        }
        finally { await app.StopAsync(); }
    }

    [LocalFirestoreFact]
    public async Task GuardarInsumoHttpRegistraAuditoriaBeforeAfterEnAltaYEdicionYSinEventoEnRechazoYPermisos()
    {
        var db = Db();
        var proveedores = new ProveedorService(db);
        var p1 = new Proveedor { RazonSocial = "Proveedor Alfa", IdentificacionTributaria = "30-00000001-5", Contacto = "1234567890", EmailOrdenes = "alfa@example.invalid" };
        var p2 = new Proveedor { RazonSocial = "Proveedor Beta", IdentificacionTributaria = "30-00000002-3", Contacto = "1234567891", EmailOrdenes = "beta@example.invalid" };
        Assert.True((await proveedores.Guardar(p1, true)).EsExitoso);
        Assert.True((await proveedores.Guardar(p2, true)).EsExitoso);

        var rolPersonalizado = new Rol
        {
            Id = "rol-custom-insumos",
            Nombre = "Supervisor Insumos",
            Descripcion = "Rol personalizado preservado",
            Permisos = new() { "Insumos.Consultar", "Insumos.Crear" },
            Estado = "Activo",
            FechaCreacion = new DateTime(2025, 1, 10, 12, 0, 0, DateTimeKind.Utc)
        };
        await db.Collection("roles").Document(rolPersonalizado.Id).SetAsync(rolPersonalizado);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(InsumosController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, ProveedorCatalogIntegrationTests.HeaderAuth>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db);
        builder.Services.AddMemoryCache();
        builder.Services.AddScoped<ConfiguracionService>();
        builder.Services.AddScoped<InsumoService>();
        builder.Services.AddScoped<ProveedorService>();
        builder.Services.AddScoped<StockService>();
        builder.Services.AddScoped<ComprasService>();
        builder.Services.AddScoped<IAuditWriter, AuditService>();
        var emailSender = new Mock<IEmailSender>();
        emailSender.Setup(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoEnvioEmail(true, "Fake ok"));
        builder.Services.AddSingleton(emailSender.Object);

        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(address) };

            void SetPermissions(string permissions)
            {
                client.DefaultRequestHeaders.Remove("X-Test-Permission");
                if (!string.IsNullOrEmpty(permissions))
                    client.DefaultRequestHeaders.Add("X-Test-Permission", permissions);
            }

            SetPermissions("Insumos.Consultar,Insumos.Crear");
            async Task<string> Page(string path = "/Insumos/Index")
            {
                var r = await client.GetAsync(path);
                Assert.Equal(HttpStatusCode.OK, r.StatusCode);
                return WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync());
            }

            var page = await Page();
            var token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.NotEmpty(token);

            async Task<HttpResponseMessage> PostGuardar(params (string Key, string Value)[] fields)
            {
                var data = fields.Select(x => new KeyValuePair<string, string>(x.Key, x.Value)).Append(new("__RequestVerificationToken", token));
                return await client.PostAsync("/Insumos/Guardar", new FormUrlEncodedContent(data));
            }

            async Task AssertAuditEvents(params (string Action, string TargetId, string NombreHistorico, Dictionary<string, string> Antes, Dictionary<string, string> Despues)[] expected)
            {
                var expectedActions = expected.Select(e => e.Action).ToHashSet();
                var docs = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents
                    .Where(d => d.GetValue<string>("TargetType") == "Insumo" && expectedActions.Contains(d.GetValue<string>("Action")))
                    .ToList();
                Assert.Equal(expected.Length, docs.Count);
                foreach (var e in expected)
                {
                    var doc = Assert.Single(docs, d => d.GetValue<string>("Action") == e.Action && d.GetValue<string>("TargetId") == e.TargetId);
                    Assert.Equal("test", doc.GetValue<string>("UserId"));
                    Assert.Equal("test@example.invalid", doc.GetValue<string>("UserEmail"));
                    Assert.Equal("Insumo", doc.GetValue<string>("TargetType"));
                    Assert.Equal(e.NombreHistorico, doc.GetValue<string>("NombreHistorico"));
                    Assert.True(doc.ContainsField("Antes"));
                    Assert.True(doc.ContainsField("Despues"));
                    var antes = doc.GetValue<Dictionary<string, string>>("Antes");
                    var despues = doc.GetValue<Dictionary<string, string>>("Despues");
                    Assert.NotNull(antes);
                    Assert.NotNull(despues);
                    Assert.Equal(e.Antes.OrderBy(x => x.Key), antes.OrderBy(x => x.Key));
                    Assert.Equal(e.Despues.OrderBy(x => x.Key), despues.OrderBy(x => x.Key));
                    Assert.Equal("ConCambios", AuditCampos.ObtenerEstadoHistorial(antes, despues));
                    Assert.DoesNotContain("Id", antes.Keys);
                    Assert.DoesNotContain("Id", despues.Keys);
                    var json = System.Text.Json.JsonSerializer.Serialize(doc.ToDictionary());
                    Assert.DoesNotContain("Password", json, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("Token", json, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("Secret", json, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("ApiKey", json, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("Clave", json, StringComparison.OrdinalIgnoreCase);
                }
            }

            // 1. Alta exitosa con solo Insumos.Crear (incluye nombre con espacios y proveedores desordenados)
            SetPermissions("Insumos.Crear");
            var altaResp = await PostGuardar(
                ("crear", "true"),
                ("Nombre", " Shampoo Neutro "),
                ("Tipo", "Quimico"),
                ("UnidadMedida", "ml"),
                ("StockMinimo", "5"),
                ("CantidadReabastecimiento", "10"),
                ("ReabastecimientoAutomatico", "true"),
                ("ProveedoresIds", p2.Id),
                ("ProveedoresIds", p1.Id));
            Assert.Equal(HttpStatusCode.Redirect, altaResp.StatusCode);

            var insumoService = new InsumoService(db);
            var shampoo = Assert.Single(await insumoService.ObtenerTodos());
            Assert.Equal("Shampoo Neutro", shampoo.Nombre);
            Assert.Equal(0m, shampoo.StockActual);
            Assert.Equal("Activo", shampoo.Estado);

            var ordenadosIds = new[] { p1.Id, p2.Id }.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var esperadoShampooInicial = new Dictionary<string, string>
            {
                ["Nombre"] = "Shampoo Neutro",
                ["Tipo"] = "Quimico",
                ["UnidadMedida"] = "ml",
                ["StockActual"] = "0",
                ["StockMinimo"] = "5",
                ["CantidadReabastecimiento"] = "10",
                ["ReabastecimientoAutomatico"] = "True",
                ["ProveedoresIds"] = System.Text.Json.JsonSerializer.Serialize(ordenadosIds),
                ["Estado"] = "Activo"
            };
            await AssertAuditEvents(
                ("Creacion de insumo", shampoo.Id, "Shampoo Neutro", new(), esperadoShampooInicial));

            // 2. Rechazos en alta no crean insumo ni emiten auditoría
            SetPermissions("Insumos.Consultar,Insumos.Crear,Insumos.Modificar");
            foreach (var (fields, expectedError) in new[]
            {
                (new[] { ("crear", "true"), ("Nombre", "   "), ("Tipo", "Quimico"), ("UnidadMedida", "ml") }, "El nombre del insumo es obligatorio."),
                (new[] { ("crear", "true"), ("Nombre", "shampoo neutro"), ("Tipo", "Quimico"), ("UnidadMedida", "ml") }, "Ya existe un insumo con ese nombre."),
                (new[] { ("crear", "true"), ("Nombre", "Cera"), ("Tipo", "Quimico"), ("UnidadMedida", "ml"), ("StockMinimo", "-1") }, "Los niveles de stock no pueden ser negativos."),
                (new[] { ("crear", "true"), ("Nombre", "Cera"), ("Tipo", "Quimico"), ("UnidadMedida", "ml"), ("ReabastecimientoAutomatico", "true") }, "Seleccione al menos un proveedor para el reabastecimiento automático.")
            })
            {
                Assert.Equal(HttpStatusCode.Redirect, (await PostGuardar(fields)).StatusCode);
                Assert.Contains(expectedError, await Page());
            }
            Assert.Single(await insumoService.ObtenerTodos());
            await AssertAuditEvents(
                ("Creacion de insumo", shampoo.Id, "Shampoo Neutro", new(), esperadoShampooInicial));

            // 3. Edición exitosa con solo Insumos.Modificar: captura before/after exactos (incluye stock real previo e ignora intentos de mutar StockActual/Estado)
            var stockService = new StockService(db);
            Assert.True((await stockService.Ajustar(shampoo.Id, 12.5m, true, "Stock inicial", "actor")).EsExitoso);
            var antesEdicion = new Dictionary<string, string>(esperadoShampooInicial) { ["StockActual"] = "12.5" };

            SetPermissions("Insumos.Modificar");
            var edicionResp = await PostGuardar(
                ("crear", "false"),
                ("Id", shampoo.Id),
                ("Nombre", " Shampoo Premium "),
                ("Tipo", "Quimico"),
                ("UnidadMedida", "ml"),
                ("StockActual", "999"),
                ("StockMinimo", "8"),
                ("CantidadReabastecimiento", "20"),
                ("ReabastecimientoAutomatico", "false"),
                ("Estado", "Inactivo"));
            Assert.Equal(HttpStatusCode.Redirect, edicionResp.StatusCode);

            var shampooActualizado = (await insumoService.Obtener(shampoo.Id))!;
            Assert.Equal("Shampoo Premium", shampooActualizado.Nombre);
            Assert.Equal(12.5m, shampooActualizado.StockActual);
            Assert.Equal("Activo", shampooActualizado.Estado);
            Assert.Empty(shampooActualizado.ProveedoresIds);

            var despuesEdicion = new Dictionary<string, string>
            {
                ["Nombre"] = "Shampoo Premium",
                ["Tipo"] = "Quimico",
                ["UnidadMedida"] = "ml",
                ["StockActual"] = "12.5",
                ["StockMinimo"] = "8",
                ["CantidadReabastecimiento"] = "20",
                ["ReabastecimientoAutomatico"] = "False",
                ["ProveedoresIds"] = "[]",
                ["Estado"] = "Activo"
            };
            await AssertAuditEvents(
                ("Creacion de insumo", shampoo.Id, "Shampoo Neutro", new(), esperadoShampooInicial),
                ("Actualizacion de insumo", shampoo.Id, "Shampoo Premium", antesEdicion, despuesEdicion));

            // Segundo insumo para probar rechazo por nombre duplicado en edición
            SetPermissions("Insumos.Consultar,Insumos.Crear,Insumos.Modificar");
            Assert.Equal(HttpStatusCode.Redirect, (await PostGuardar(
                ("crear", "true"),
                ("Nombre", "Cera Liquida"),
                ("Tipo", "Quimico"),
                ("UnidadMedida", "l"),
                ("StockMinimo", "2"),
                ("CantidadReabastecimiento", "5"))).StatusCode);
            var cera = (await insumoService.ObtenerTodos()).Single(x => x.Nombre == "Cera Liquida");
            var esperadoCera = new Dictionary<string, string>
            {
                ["Nombre"] = "Cera Liquida",
                ["Tipo"] = "Quimico",
                ["UnidadMedida"] = "l",
                ["StockActual"] = "0",
                ["StockMinimo"] = "2",
                ["CantidadReabastecimiento"] = "5",
                ["ReabastecimientoAutomatico"] = "False",
                ["ProveedoresIds"] = "[]",
                ["Estado"] = "Activo"
            };

            // 4. Rechazos en edición no modifican insumo ni emiten auditoría
            foreach (var (fields, expectedError) in new[]
            {
                (new[] { ("crear", "false"), ("Id", shampoo.Id), ("Nombre", "cera liquida"), ("Tipo", "Quimico"), ("UnidadMedida", "ml") }, "Ya existe un insumo con ese nombre."),
                (new[] { ("crear", "false"), ("Id", shampoo.Id), ("Nombre", "Shampoo Premium"), ("Tipo", "Fisico"), ("UnidadMedida", "ml") }, "El tipo y la unidad de medida no se pueden modificar."),
                (new[] { ("crear", "false"), ("Id", shampoo.Id), ("Nombre", "Shampoo Premium"), ("Tipo", "Quimico"), ("UnidadMedida", "l") }, "El tipo y la unidad de medida no se pueden modificar."),
                (new[] { ("crear", "false"), ("Id", shampoo.Id), ("Nombre", "Shampoo Premium"), ("Tipo", "Quimico"), ("UnidadMedida", "ml"), ("ReabastecimientoAutomatico", "true") }, "Seleccione al menos un proveedor para el reabastecimiento automático."),
                (new[] { ("crear", "false"), ("Id", ""), ("Nombre", "Fantasma"), ("Tipo", "Quimico"), ("UnidadMedida", "ml") }, "El insumo no existe."),
                (new[] { ("crear", "false"), ("Id", "no-existe"), ("Nombre", "Fantasma"), ("Tipo", "Quimico"), ("UnidadMedida", "ml") }, "El insumo no existe.")
            })
            {
                Assert.Equal(HttpStatusCode.Redirect, (await PostGuardar(fields)).StatusCode);
                Assert.Contains(expectedError, await Page());
            }

            // Rechazo por uso en lavado activo no modifica insumo ni emite auditoría
            await db.Collection("lavados").Document("lav-activo").SetAsync(new Lavado
            {
                Id = "lav-activo",
                ClienteId = "cli-1",
                VehiculoId = "veh-1",
                Estado = "EnProceso",
                FechaCreacion = DateTime.UtcNow,
                Servicios = new()
                {
                    new ServicioEnLavado
                    {
                        ServicioId = "srv-1",
                        Estado = "EnProceso",
                        Receta = new() { new RecetaInsumo { InsumoId = shampoo.Id, DosisTeorica = 1m } }
                    }
                }
            });
            Assert.Equal(HttpStatusCode.Redirect, (await PostGuardar(
                ("crear", "false"),
                ("Id", shampoo.Id),
                ("Nombre", "Shampoo Bloqueado"),
                ("Tipo", "Quimico"),
                ("UnidadMedida", "ml"))).StatusCode);
            Assert.Contains("No se puede modificar el insumo porque está siendo utilizado por un lavado en ejecución.", await Page());

            shampooActualizado = (await insumoService.Obtener(shampoo.Id))!;
            Assert.Equal("Shampoo Premium", shampooActualizado.Nombre);
            Assert.Equal(12.5m, shampooActualizado.StockActual);
            await AssertAuditEvents(
                ("Creacion de insumo", shampoo.Id, "Shampoo Neutro", new(), esperadoShampooInicial),
                ("Actualizacion de insumo", shampoo.Id, "Shampoo Premium", antesEdicion, despuesEdicion),
                ("Creacion de insumo", cera.Id, "Cera Liquida", new(), esperadoCera));

            // 5. Permisos S14: roles sin Insumos.Crear / Insumos.Modificar reciben 403 y no emiten auditoría ni mutan datos
            foreach (var permiso in new[]
            {
                "",
                "Insumos.Consultar",
                "Insumos.Recepcionar",
                "Insumos.Consultar,Insumos.Recepcionar",
                "Insumos.AjustarStock",
                "Insumos.Kardex",
                "Insumos.Eliminar"
            })
            {
                SetPermissions(permiso);
                Assert.Equal(HttpStatusCode.Forbidden, (await PostGuardar(
                    ("crear", "true"),
                    ("Nombre", "No autorizado"),
                    ("Tipo", "Quimico"),
                    ("UnidadMedida", "ml"))).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, (await PostGuardar(
                    ("crear", "false"),
                    ("Id", shampoo.Id),
                    ("Nombre", "Alterado"),
                    ("Tipo", "Quimico"),
                    ("UnidadMedida", "ml"))).StatusCode);
            }

            // Index permite Insumos.Consultar (Empleado/Trabajador) y rechaza permisos ajenos
            SetPermissions("Insumos.Consultar,Insumos.Recepcionar");
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Insumos/Index")).StatusCode);
            SetPermissions("Insumos.Recepcionar");
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/Insumos/Index")).StatusCode);

            Assert.Equal(2, (await insumoService.ObtenerTodos()).Count);
            Assert.Equal("Shampoo Premium", (await insumoService.Obtener(shampoo.Id))!.Nombre);
            await AssertAuditEvents(
                ("Creacion de insumo", shampoo.Id, "Shampoo Neutro", new(), esperadoShampooInicial),
                ("Actualizacion de insumo", shampoo.Id, "Shampoo Premium", antesEdicion, despuesEdicion),
                ("Creacion de insumo", cera.Id, "Cera Liquida", new(), esperadoCera));

            // 6. Roles personalizados persistidos en Firestore permanecen intactos
            var rolSnap = await db.Collection("roles").Document(rolPersonalizado.Id).GetSnapshotAsync();
            Assert.True(rolSnap.Exists);
            var rolPersistido = rolSnap.ConvertTo<Rol>();
            Assert.Equal("Supervisor Insumos", rolPersistido.Nombre);
            Assert.Equal(new[] { "Insumos.Consultar", "Insumos.Crear" }, rolPersistido.Permisos);
            Assert.Equal("Activo", rolPersistido.Estado);
        }
        finally { await app.StopAsync(); }
    }

    [LocalFirestoreFact]
    public async Task AjusteManualHttpUsaClaimsYRegistraAuditoriaTransaccionalSinDuplicar()
    {
        var db = Db();
        var insumo = new Insumo { Id = "ins-http", Nombre = "Jabon Http", Tipo = "Quimico", UnidadMedida = "ml", StockActual = 10m, Estado = "Activo" };
        await db.Collection("insumos").Document(insumo.Id).SetAsync(insumo);

        var auditMock = new Mock<IAuditWriter>();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(InsumosController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, ProveedorCatalogIntegrationTests.HeaderAuth>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db);
        builder.Services.AddMemoryCache();
        builder.Services.AddScoped<ConfiguracionService>();
        builder.Services.AddScoped<InsumoService>();
        builder.Services.AddScoped<ProveedorService>();
        builder.Services.AddScoped<StockService>();
        builder.Services.AddScoped<ComprasService>();
        builder.Services.AddSingleton(auditMock.Object);
        builder.Services.AddSingleton(Mock.Of<IEmailSender>());

        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();

        try
        {
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() })
            {
                BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single())
            };
            client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Consultar,Insumos.AjustarStock");

            var page = await client.GetStringAsync("/Insumos/Index");
            var token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.NotEmpty(token);

            // POST Ingreso
            var postIngreso = await client.PostAsync("/Insumos/Ajustar", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["insumoId"] = "ins-http",
                ["cantidad"] = "5",
                ["ingreso"] = "true",
                ["justificacion"] = "Ingreso HTTP"
            }));
            Assert.Equal(HttpStatusCode.Redirect, postIngreso.StatusCode);

            // IAuditWriter mock never invoked (moved to transaction)
            auditMock.Verify(a => a.LogEvent(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);

            var insumoDb = (await db.Collection("insumos").Document("ins-http").GetSnapshotAsync()).ConvertTo<Insumo>();
            Assert.Equal(15m, insumoDb.StockActual);

            var auditorias = (await db.Collection("registros_auditoria").WhereEqualTo("TargetId", "ins-http").GetSnapshotAsync()).Documents
                .Select(d => d.ConvertTo<AuditLog>()).ToList();
            var log = Assert.Single(auditorias);
            Assert.Equal("test", log.UserId);
            Assert.Equal("test@example.invalid", log.UserEmail);
            Assert.Equal("Ingreso manual de stock", log.Action);
            Assert.Equal("Insumo", log.TargetType);
            Assert.Equal("10", log.Antes["StockActual"]);
            Assert.Equal(15m.ToString(System.Globalization.CultureInfo.InvariantCulture), log.Despues["StockActual"]);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [LocalFirestoreFact]
    public async Task CambiarEstado_InsumoUsadoEnTurnoPendiente_RechazaDesactivacionYPermiteAlCancelar()
    {
        var db = Db();
        var servicio = new InsumoService(db);

        await db.Collection("insumos").Document("ins-turno").SetAsync(new Insumo
        {
            Id = "ins-turno",
            Nombre = "Shampoo Turno",
            Tipo = "Quimico",
            UnidadMedida = "ml",
            Estado = "Activo",
            StockActual = 100m
        });

        await db.Collection("turnos").Document("tur-pendiente").SetAsync(new Turno
        {
            Id = "tur-pendiente",
            ClienteId = "cli-1",
            Estado = "Pendiente",
            InicioUtc = DateTime.UtcNow.AddHours(2),
            FinUtc = DateTime.UtcNow.AddHours(3),
            RecetaOperativa = new RecetaOperativaTurno
            {
                Version = 1,
                Servicios =
                {
                    new ServicioOperativoTurno
                    {
                        ServicioId = "srv-1",
                        Nombre = "Lavado",
                        Receta =
                        {
                            new RecetaInsumo { InsumoId = "ins-turno", DosisTeorica = 10m, UnidadMedida = "ml" }
                        }
                    }
                }
            }
        });

        var bloqueo = await servicio.CambiarEstado("ins-turno", "Inactivo", "u1");
        Assert.False(bloqueo.EsExitoso);
        Assert.Equal("No se puede desactivar el insumo porque está siendo utilizado por un turno pendiente.", bloqueo.MensajeError);
        Assert.Equal("Activo", (await servicio.Obtener("ins-turno"))!.Estado);

        await db.Collection("turnos").Document("tur-pendiente").UpdateAsync("Estado", "Cancelado");

        var permitido = await servicio.CambiarEstado("ins-turno", "Inactivo", "u1");
        Assert.True(permitido.EsExitoso);
        Assert.Equal("Inactivo", (await servicio.Obtener("ins-turno"))!.Estado);
    }
}

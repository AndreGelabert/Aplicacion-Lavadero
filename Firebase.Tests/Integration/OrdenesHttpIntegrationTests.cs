using System.Net;
using System.Text.RegularExpressions;
using Firebase.Controllers;
using Firebase.Models;
using Firebase.Security;
using Firebase.Services;
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

public sealed class OrdenesHttpIntegrationTests
{
    [LocalFirestoreFact]
    public async Task AprobarOrdenAceptadaAdvierteQueNoEsEntregaYReintentoNoEnvia()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-orden-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("proveedores").Document("p").SetAsync(new Proveedor { Id = "p", RazonSocial = "Proveedor", EmailOrdenes = "p@example.invalid" });
        await db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "Jabon" });
        await db.Collection("ordenesCompra").Document("o").SetAsync(new OrdenCompra { Id = "o", ProveedorId = "p", Estado = "PendienteAprobacion", Items = new() { new OrdenCompraItem { InsumoId = "i", NombreInsumo = "Jabon", Cantidad = 2, PrecioUnitario = 5 } } });
        var email = new Mock<IEmailSender>();
        email.Setup(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ResultadoEnvioEmail(true, "Aceptado"));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(InsumosController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, ProveedorCatalogIntegrationTests.HeaderAuth>("Test", _ => { });
        builder.Services.AddAuthorization(); builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db); builder.Services.AddMemoryCache(); builder.Services.AddScoped<ConfiguracionService>();
        builder.Services.AddScoped<InsumoService>(); builder.Services.AddScoped<ProveedorService>(); builder.Services.AddScoped<StockService>(); builder.Services.AddScoped<ComprasService>();
        builder.Services.AddSingleton(Mock.Of<IAuditWriter>()); builder.Services.AddSingleton(email.Object);
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"); await app.StartAsync();
        try
        {
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Modificar");
            var page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Ordenes"));
            var token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.NotEmpty(token);
            Assert.Contains("Aprobar y enviar", page);
            email.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
            async Task<HttpResponseMessage> Aprobar() => await client.PostAsync("/Insumos/AprobarOrden", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["id"] = "o" }));
            var response = await Aprobar();
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Insumos/Ordenes", response.Headers.Location?.OriginalString);
            page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Ordenes"));
            Assert.Contains("El transporte aceptó la solicitud; esto no confirma entrega ni lectura.", page);
            Assert.Contains("Enviada", page);
            Assert.Equal("Enviada", (await db.Collection("ordenesCompra").Document("o").GetSnapshotAsync()).ConvertTo<OrdenCompra>().Estado);
            email.Verify(x => x.EnviarAsync(It.Is<EmailMessage>(m => m.To == "p@example.invalid" && m.Subject == "Orden de compra de insumos"), It.IsAny<CancellationToken>()), Times.Once);
            response = await Aprobar();
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Ordenes"));
            Assert.Contains("La orden no está pendiente de aprobación; revise manualmente el resultado del envío.", page);
            Assert.Equal("Enviada", (await db.Collection("ordenesCompra").Document("o").GetSnapshotAsync()).ConvertTo<OrdenCompra>().Estado);
            Assert.Single((await db.Collection("ordenesCompra").GetSnapshotAsync()).Documents);
            Assert.Empty((await db.Collection("movimientosStock").GetSnapshotAsync()).Documents);
            email.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        }
        finally { await app.StopAsync(); }
    }

    [LocalFirestoreFact]
    public async Task InterrupcionDeTransporteTrasEstadoDurableImpideReenvio()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-orden-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("proveedores").Document("p").SetAsync(new Proveedor { Id = "p", RazonSocial = "Proveedor", EmailOrdenes = "p@example.invalid" });
        await db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "Jabon", StockActual = 3 });
        await db.Collection("ordenesCompra").Document("o").SetAsync(new OrdenCompra { Id = "o", ProveedorId = "p", Estado = "PendienteAprobacion", Items = new() { new OrdenCompraItem { InsumoId = "i", NombreInsumo = "Jabon", Cantidad = 2, PrecioUnitario = 5 } } });
        var iniciado = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var interrumpir = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var email = new Mock<IEmailSender>();
        email.Setup(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>())).Returns(async () =>
        {
            iniciado.TrySetResult();
            await interrumpir.Task;
            throw new OperationCanceledException("Interrupción simulada del transporte");
        });
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(InsumosController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, ProveedorCatalogIntegrationTests.HeaderAuth>("Test", _ => { });
        builder.Services.AddAuthorization(); builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db); builder.Services.AddMemoryCache(); builder.Services.AddScoped<ConfiguracionService>();
        builder.Services.AddScoped<InsumoService>(); builder.Services.AddScoped<ProveedorService>(); builder.Services.AddScoped<StockService>(); builder.Services.AddScoped<ComprasService>();
        builder.Services.AddSingleton(Mock.Of<IAuditWriter>()); builder.Services.AddSingleton(email.Object);
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"); await app.StartAsync();
        try
        {
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Modificar");
            var page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Ordenes"));
            var token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.NotEmpty(token);
            async Task<HttpResponseMessage> Post(string action, Dictionary<string, string> fields) => await client.PostAsync("/Insumos/" + action, new FormUrlEncodedContent(fields));
            async Task<HttpResponseMessage> Aprobar() => await Post("AprobarOrden", new() { ["__RequestVerificationToken"] = token, ["id"] = "o" });
            Assert.Equal(HttpStatusCode.BadRequest, (await Post("AprobarOrden", new() { ["id"] = "o" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Post("EditarOrdenPendiente", new() { ["id"] = "o", ["insumoId"] = "i", ["cantidad"] = "4", ["precio"] = "8" })).StatusCode);
            client.DefaultRequestHeaders.Remove("X-Test-Permission"); client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Recepcionar");
            Assert.Equal(HttpStatusCode.Forbidden, (await Aprobar()).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Post("EditarOrdenPendiente", new() { ["__RequestVerificationToken"] = token, ["id"] = "o", ["insumoId"] = "i", ["cantidad"] = "4", ["precio"] = "8" })).StatusCode);
            client.DefaultRequestHeaders.Remove("X-Test-Permission"); client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Modificar,Insumos.Recepcionar,Insumos.Consultar");
            foreach (var (cantidad, precio) in new[] { ("0", "5"), ("2", "-1"), ("invalido", "5"), ("2", "invalido") })
            {
                Assert.Equal(HttpStatusCode.Redirect, (await Post("EditarOrdenPendiente", new() { ["__RequestVerificationToken"] = token, ["id"] = "o", ["insumoId"] = "i", ["cantidad"] = cantidad, ["precio"] = precio })).StatusCode);
                page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Ordenes"));
                Assert.Contains("cantidad", page, StringComparison.OrdinalIgnoreCase);
                var item = Assert.Single((await db.Collection("ordenesCompra").Document("o").GetSnapshotAsync()).ConvertTo<OrdenCompra>().Items);
                Assert.Equal(2, item.Cantidad); Assert.Equal(5, item.PrecioUnitario);
            }
            email.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
            var primera = Aprobar();
            await iniciado.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal("RevisionManual", (await db.Collection("ordenesCompra").Document("o").GetSnapshotAsync()).ConvertTo<OrdenCompra>().Estado);
            Assert.Equal(HttpStatusCode.Redirect, (await Post("EstadoProveedor", new() { ["__RequestVerificationToken"] = token, ["id"] = "p", ["estado"] = "Inactivo" })).StatusCode);
            page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Proveedores"));
            Assert.Contains("No se puede desactivar un proveedor con órdenes pendientes.", page);
            Assert.Equal("Activo", (await db.Collection("proveedores").Document("p").GetSnapshotAsync()).ConvertTo<Proveedor>().Estado);
            Assert.Equal(HttpStatusCode.Redirect, (await Post("CambiarEstado", new() { ["__RequestVerificationToken"] = token, ["id"] = "i", ["estado"] = "Inactivo" })).StatusCode);
            page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Index"));
            Assert.Contains("No se puede desactivar un insumo con órdenes pendientes.", page);
            Assert.Equal("Activo", (await db.Collection("insumos").Document("i").GetSnapshotAsync()).ConvertTo<Insumo>().Estado);
            Assert.Equal("RevisionManual", (await db.Collection("ordenesCompra").Document("o").GetSnapshotAsync()).ConvertTo<OrdenCompra>().Estado);
            Assert.Equal(HttpStatusCode.Redirect, (await Post("CambiarEstadoOrden", new() { ["__RequestVerificationToken"] = token, ["id"] = "o", ["estado"] = "Cancelada", ["motivo"] = "prueba" })).StatusCode);
            page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Ordenes"));
            Assert.Contains("Se requiere revisión manual del resultado del envío.", page);
            Assert.Equal(HttpStatusCode.Redirect, (await Post("RecibirOrden", new() { ["__RequestVerificationToken"] = token, ["id"] = "o", ["insumoIds"] = "i", ["cantidades"] = "2" })).StatusCode);
            page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Ordenes"));
            Assert.Contains("La orden no está disponible para recepción.", page);
            Assert.Equal("RevisionManual", (await db.Collection("ordenesCompra").Document("o").GetSnapshotAsync()).ConvertTo<OrdenCompra>().Estado);
            var reintento = await Aprobar();
            Assert.Equal(HttpStatusCode.Redirect, reintento.StatusCode);
            page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Ordenes"));
            Assert.Contains("La orden no está pendiente de aprobación; revise manualmente el resultado del envío.", page);
            Assert.DoesNotContain("El transporte aceptó la solicitud", page);
            email.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
            interrumpir.TrySetResult();
            Assert.Equal(HttpStatusCode.Redirect, (await primera.WaitAsync(TimeSpan.FromSeconds(20))).StatusCode);
            page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Ordenes"));
            Assert.Contains("Resultado incierto. Se requiere revisión manual.", page);
            Assert.DoesNotContain("El transporte aceptó la solicitud", page);
            Assert.Equal(HttpStatusCode.Redirect, (await Aprobar()).StatusCode);
            page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Ordenes"));
            Assert.Contains("La orden no está pendiente de aprobación; revise manualmente el resultado del envío.", page);
            Assert.Equal("RevisionManual", (await db.Collection("ordenesCompra").Document("o").GetSnapshotAsync()).ConvertTo<OrdenCompra>().Estado);
            Assert.Equal(3, (await db.Collection("insumos").Document("i").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
            Assert.Single((await db.Collection("ordenesCompra").GetSnapshotAsync()).Documents);
            Assert.Empty((await db.Collection("movimientosStock").GetSnapshotAsync()).Documents);
            email.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        }
        finally { interrumpir.TrySetResult(); await app.StopAsync(); }
    }

    [LocalFirestoreFact]
    public async Task OrdenManualValidaYExponeErrorDeEnvio()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-orden-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("proveedores").Document("p").SetAsync(new Proveedor { Id = "p", RazonSocial = "Proveedor", EmailOrdenes = "p@example.invalid" });
        await db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "Jabon" });
        var email = new Mock<IEmailSender>();
        email.Setup(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ResultadoEnvioEmail(false, "Fallo de prueba"));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(InsumosController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, ProveedorCatalogIntegrationTests.HeaderAuth>("Test", _ => { });
        builder.Services.AddAuthorization(); builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db); builder.Services.AddMemoryCache(); builder.Services.AddScoped<ConfiguracionService>();
        builder.Services.AddScoped<InsumoService>(); builder.Services.AddScoped<ProveedorService>(); builder.Services.AddScoped<StockService>(); builder.Services.AddScoped<ComprasService>();
        builder.Services.AddSingleton(Mock.Of<IAuditWriter>()); builder.Services.AddSingleton(email.Object);
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"); await app.StartAsync();
        try
        {
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Crear,Insumos.Modificar,Insumos.Recepcionar");
            var page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Ordenes"));
            var token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.NotEmpty(token); Assert.Contains("Crear orden", page);
            await db.Collection("ordenesCompra").Document("repetida").SetAsync(new OrdenCompra { Id = "repetida", ProveedorId = "p", Estado = "PendienteAprobacion", Notas = "El precio y la disponibilidad requieren confirmación del proveedor.", Items = new() { new OrdenCompraItem { InsumoId = "i", NombreInsumo = "Jabon", Cantidad = 2, PrecioUnitario = 5 } } });
            page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Ordenes"));
            Assert.Contains("Editar orden", page);
            Assert.Contains("requieren confirmación", page);
            async Task<HttpResponseMessage> Editar(string cantidad, string precio) => await client.PostAsync("/Insumos/EditarOrdenPendiente", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["id"] = "repetida", ["insumoId"] = "i", ["cantidad"] = cantidad, ["precio"] = precio }));
            client.DefaultRequestHeaders.Remove("X-Test-Permission"); client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Recepcionar");
            Assert.Equal(HttpStatusCode.Forbidden, (await Editar("4", "8")).StatusCode);
            client.DefaultRequestHeaders.Remove("X-Test-Permission"); client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Crear,Insumos.Modificar,Insumos.Recepcionar");
            Assert.Equal(HttpStatusCode.Redirect, (await Editar("0", "8")).StatusCode);
            var pendiente = (await db.Collection("ordenesCompra").Document("repetida").GetSnapshotAsync()).ConvertTo<OrdenCompra>();
            Assert.Equal(2, pendiente.Items[0].Cantidad); Assert.Equal(5, pendiente.Items[0].PrecioUnitario);
            Assert.Equal(HttpStatusCode.Redirect, (await Editar("4", "8")).StatusCode);
            pendiente = (await db.Collection("ordenesCompra").Document("repetida").GetSnapshotAsync()).ConvertTo<OrdenCompra>();
            Assert.Equal(4, pendiente.Items[0].Cantidad); Assert.Equal(8, pendiente.Items[0].PrecioUnitario); Assert.Equal("PendienteAprobacion", pendiente.Estado);
            email.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.Contains("Aprobar y enviar", page);
            async Task<HttpResponseMessage> Aprobar() => await client.PostAsync("/Insumos/AprobarOrden", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["id"] = "repetida" }));
            var aprobaciones = await Task.WhenAll(Aprobar(), Aprobar());
            Assert.All(aprobaciones, r => Assert.Equal(HttpStatusCode.Redirect, r.StatusCode));
            pendiente = (await db.Collection("ordenesCompra").Document("repetida").GetSnapshotAsync()).ConvertTo<OrdenCompra>();
            Assert.Equal("RevisionManual", pendiente.Estado);
            email.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
            await Aprobar();
            email.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
            await db.Collection("ordenesCompra").Document("repetida").UpdateAsync("Estado", "Cancelada");
            Assert.Equal(HttpStatusCode.Redirect, (await Editar("7", "9")).StatusCode);
            pendiente = (await db.Collection("ordenesCompra").Document("repetida").GetSnapshotAsync()).ConvertTo<OrdenCompra>();
            Assert.Equal(4, pendiente.Items[0].Cantidad); Assert.Equal(8, pendiente.Items[0].PrecioUnitario);
            async Task Post(string cantidad) => Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Insumos/CrearOrdenManual", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["ProveedorId"] = "p", ["Items[0].InsumoId"] = "i", ["Items[0].Cantidad"] = cantidad }))).StatusCode);
            await Post("0");
            page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Ordenes"));
            Assert.Contains("cantidad", page, StringComparison.OrdinalIgnoreCase);
            Assert.Single((await db.Collection("ordenesCompra").GetSnapshotAsync()).Documents);
            email.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
            await Post("2");
            page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Ordenes"));
            Assert.Contains("Fallo de prueba", page); Assert.Contains("Jabon", page); Assert.Contains("RevisionManual", page);
            Assert.Equal(2, (await db.Collection("ordenesCompra").GetSnapshotAsync()).Count);
            email.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            var orden = (await db.Collection("ordenesCompra").GetSnapshotAsync()).Documents.Select(d => d.ConvertTo<OrdenCompra>()).Single(o => o.Id != "repetida");
            Assert.Equal("RevisionManual", orden.Estado);
            await db.Collection("ordenesCompra").Document(orden.Id).UpdateAsync("Estado", "Enviada");
            client.DefaultRequestHeaders.Remove("X-Test-Permission"); client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Recepcionar");
            page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Ordenes"));
            Assert.Contains("Registrar recepción", page);
            async Task<HttpResponseMessage> Recibir(string cantidad) => await client.PostAsync("/Insumos/RecibirOrden", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["id"] = orden.Id, ["insumoIds"] = "i", ["cantidades"] = cantidad }));
            Assert.Equal(HttpStatusCode.Redirect, (await Recibir("0")).StatusCode);
            page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Ordenes")); Assert.Contains("cantidad", page, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, (await db.Collection("insumos").Document("i").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
            Assert.Empty((await db.Collection("movimientosStock").GetSnapshotAsync()).Documents);
            client.DefaultRequestHeaders.Remove("X-Test-Permission"); client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Modificar");
            Assert.Equal(HttpStatusCode.Forbidden, (await Recibir("2")).StatusCode);
            client.DefaultRequestHeaders.Remove("X-Test-Permission"); client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Recepcionar");
            await db.Collection("ordenesCompra").Document(orden.Id).UpdateAsync(new Dictionary<string, object> { ["Estado"] = "Confirmada", ["GrupoCotizacionId"] = "g" });
            await db.Collection("ordenesCompra").Document("rival").SetAsync(new OrdenCompra { Id = "rival", GrupoCotizacionId = "g", Estado = "Enviada" });
            await db.Collection("ordenesCompra").Document("otra").SetAsync(new OrdenCompra { Id = "otra", GrupoCotizacionId = "otro", Estado = "Enviada" });
            await db.Collection("ordenesCompra").Document("incierta").SetAsync(new OrdenCompra { Id = "incierta", GrupoCotizacionId = "g", Estado = "RevisionManual" });
            page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Ordenes")); Assert.Contains("Cancelar órdenes concurrentes", page);
            Assert.Equal(HttpStatusCode.Redirect, (await Recibir("1")).StatusCode);
            Assert.Equal("Confirmada", (await db.Collection("ordenesCompra").Document(orden.Id).GetSnapshotAsync()).ConvertTo<OrdenCompra>().Estado);
            Assert.Equal("Enviada", (await db.Collection("ordenesCompra").Document("rival").GetSnapshotAsync()).ConvertTo<OrdenCompra>().Estado);
            Assert.Equal(HttpStatusCode.Redirect, (await Recibir("3")).StatusCode);
            page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Ordenes")); Assert.Contains("supera la cantidad pendiente", page);
            Assert.Equal(1, (await db.Collection("insumos").Document("i").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
            var cierre = new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["id"] = orden.Id, ["insumoIds"] = "i", ["cantidades"] = "1", ["cancelarConcurrentes"] = "true" };
            Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Insumos/RecibirOrden", new FormUrlEncodedContent(cierre))).StatusCode);
            var rival = (await db.Collection("ordenesCompra").Document("rival").GetSnapshotAsync()).ConvertTo<OrdenCompra>();
            Assert.Equal("Cancelada", rival.Estado); Assert.Equal("Adjudicacion orden " + orden.Id, rival.MotivoCancelacion);
            Assert.Equal("Enviada", (await db.Collection("ordenesCompra").Document("otra").GetSnapshotAsync()).ConvertTo<OrdenCompra>().Estado);
            Assert.Equal("RevisionManual", (await db.Collection("ordenesCompra").Document("incierta").GetSnapshotAsync()).ConvertTo<OrdenCompra>().Estado);
            Assert.Equal(HttpStatusCode.Redirect, (await Recibir("2")).StatusCode);
            Assert.Equal(HttpStatusCode.Redirect, (await Recibir("2")).StatusCode);
            Assert.Equal(2, (await db.Collection("insumos").Document("i").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
            Assert.Equal(2, (await db.Collection("movimientosStock").GetSnapshotAsync()).Count);
            Assert.Equal("Recibida", (await db.Collection("ordenesCompra").Document(orden.Id).GetSnapshotAsync()).ConvertTo<OrdenCompra>().Estado);
        }
        finally { await app.StopAsync(); }
    }

    [LocalFirestoreFact]
    public async Task EdicionYAprobacionDeOrdenRepetidaRegistranAuditoriaBeforeAfterYSinDuplicadosEnRechazosYReintentos()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-orden-audit-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("proveedores").Document("p").SetAsync(new Proveedor { Id = "p", RazonSocial = "Proveedor", EmailOrdenes = "p@example.invalid" });
        await db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "Jabon" });
        var rolPersonalizado = new Rol
        {
            Id = "rol-custom-ordenes",
            Nombre = "Supervisor Ordenes",
            Descripcion = "Rol personalizado preservado",
            Permisos = new() { "Insumos.Consultar", "Insumos.Recepcionar" },
            Estado = "Activo",
            FechaCreacion = new DateTime(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc)
        };
        await db.Collection("roles").Document(rolPersonalizado.Id).SetAsync(rolPersonalizado);

        var fechaAprobada = new DateTime(2025, 2, 1, 8, 0, 0, DateTimeKind.Utc);
        var ordenAprobada = new OrdenCompra
        {
            Id = "ord-aprobada",
            ProveedorId = "p",
            FechaEmision = fechaAprobada,
            Estado = "PendienteAprobacion",
            Notas = "El precio y la disponibilidad requieren confirmación del proveedor.",
            Items = new() { new OrdenCompraItem { InsumoId = "i", NombreInsumo = "Jabon", Cantidad = 2m, PrecioUnitario = 5m } }
        };
        await db.Collection("ordenesCompra").Document(ordenAprobada.Id).SetAsync(ordenAprobada);

        var fechaIncierta = new DateTime(2025, 2, 2, 9, 0, 0, DateTimeKind.Utc);
        var ordenIncierta = new OrdenCompra
        {
            Id = "ord-incierta",
            ProveedorId = "p",
            FechaEmision = fechaIncierta,
            Estado = "PendienteAprobacion",
            Notas = "El precio y la disponibilidad requieren confirmación del proveedor.",
            Items = new() { new OrdenCompraItem { InsumoId = "i", NombreInsumo = "Jabon", Cantidad = 3m, PrecioUnitario = 6m } }
        };
        await db.Collection("ordenesCompra").Document(ordenIncierta.Id).SetAsync(ordenIncierta);

        var fechaCancelada = new DateTime(2025, 2, 3, 10, 0, 0, DateTimeKind.Utc);
        await db.Collection("ordenesCompra").Document("ord-cancelada").SetAsync(new OrdenCompra
        {
            Id = "ord-cancelada",
            ProveedorId = "p",
            FechaEmision = fechaCancelada,
            Estado = "Cancelada",
            MotivoCancelacion = "Ya cancelada",
            Items = new() { new OrdenCompraItem { InsumoId = "i", NombreInsumo = "Jabon", Cantidad = 1m, PrecioUnitario = 4m } }
        });

        var responderExito = true;
        TaskCompletionSource? bloqueoTransporte = null;
        TaskCompletionSource? transporteIniciado = null;
        var email = new Mock<IEmailSender>();
        email.Setup(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                if (transporteIniciado != null) transporteIniciado.TrySetResult();
                if (bloqueoTransporte != null) await bloqueoTransporte.Task;
                return responderExito
                    ? new ResultadoEnvioEmail(true, "Aceptado")
                    : throw new OperationCanceledException("Interrupción simulada");
            });

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
        builder.Services.AddSingleton(email.Object);

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

            void SetPermissions(string permissions)
            {
                client.DefaultRequestHeaders.Remove("X-Test-Permission");
                if (!string.IsNullOrEmpty(permissions))
                    client.DefaultRequestHeaders.Add("X-Test-Permission", permissions);
            }

            SetPermissions("Insumos.Modificar");
            var page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Ordenes"));
            var token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.NotEmpty(token);

            async Task<HttpResponseMessage> Post(string action, Dictionary<string, string> fields) =>
                await client.PostAsync("/Insumos/" + action, new FormUrlEncodedContent(fields));

            async Task AssertAuditEvents(params (string Action, string TargetId, string NombreHistorico, Dictionary<string, string> Antes, Dictionary<string, string> Despues)[] expected)
            {
                var docs = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents;
                Assert.Equal(expected.Length, docs.Count);
                foreach (var e in expected)
                {
                    var doc = Assert.Single(docs, d => d.GetValue<string>("Action") == e.Action && d.GetValue<string>("TargetId") == e.TargetId);
                    Assert.Equal("test", doc.GetValue<string>("UserId"));
                    Assert.Equal("test@example.invalid", doc.GetValue<string>("UserEmail"));
                    Assert.Equal("OrdenCompra", doc.GetValue<string>("TargetType"));
                    Assert.Equal(e.NombreHistorico, doc.GetValue<string>("NombreHistorico"));
                    var antes = doc.GetValue<Dictionary<string, string>>("Antes");
                    var despues = doc.GetValue<Dictionary<string, string>>("Despues");
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

            // 1. Rechazos de permisos (403), antiforgery (400) e inputs inválidos en EditarOrdenPendiente / AprobarOrden no emiten auditoría
            Assert.Equal(HttpStatusCode.BadRequest, (await Post("EditarOrdenPendiente", new() { ["id"] = ordenAprobada.Id, ["insumoId"] = "i", ["cantidad"] = "4", ["precio"] = "8" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Post("AprobarOrden", new() { ["id"] = ordenAprobada.Id })).StatusCode);

            foreach (var permiso in new[] { "", "Insumos.Consultar", "Insumos.Crear", "Insumos.Recepcionar", "Insumos.AjustarStock" })
            {
                SetPermissions(permiso);
                Assert.Equal(HttpStatusCode.Forbidden, (await Post("EditarOrdenPendiente", new() { ["__RequestVerificationToken"] = token, ["id"] = ordenAprobada.Id, ["insumoId"] = "i", ["cantidad"] = "4", ["precio"] = "8" })).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, (await Post("AprobarOrden", new() { ["__RequestVerificationToken"] = token, ["id"] = ordenAprobada.Id })).StatusCode);
            }

            SetPermissions("Insumos.Modificar");
            foreach (var invalid in new[]
            {
                new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["id"] = ordenAprobada.Id, ["insumoId"] = "i", ["cantidad"] = "0", ["precio"] = "8" },
                new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["id"] = ordenAprobada.Id, ["insumoId"] = "i", ["cantidad"] = "4", ["precio"] = "-1" },
                new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["id"] = ordenAprobada.Id, ["insumoId"] = "i", ["cantidad"] = "abc", ["precio"] = "8" },
                new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["id"] = "no-existe", ["insumoId"] = "i", ["cantidad"] = "4", ["precio"] = "8" },
                new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["id"] = "ord-cancelada", ["insumoId"] = "i", ["cantidad"] = "4", ["precio"] = "8" }
            })
            {
                Assert.Equal(HttpStatusCode.Redirect, (await Post("EditarOrdenPendiente", invalid)).StatusCode);
            }
            Assert.Equal(HttpStatusCode.Redirect, (await Post("AprobarOrden", new() { ["__RequestVerificationToken"] = token, ["id"] = "no-existe" })).StatusCode);
            Assert.Equal(HttpStatusCode.Redirect, (await Post("AprobarOrden", new() { ["__RequestVerificationToken"] = token, ["id"] = "ord-cancelada" })).StatusCode);
            await AssertAuditEvents();
            email.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);

            // 2. Edición sin cambios reales (mismos cantidad=2, precio=5) no emite evento de auditoría
            var antesOrdenAprobada = AuditCampos.CapturarOrdenCompra((await db.Collection("ordenesCompra").Document(ordenAprobada.Id).GetSnapshotAsync()).ConvertTo<OrdenCompra>());
            Assert.Equal(HttpStatusCode.Redirect, (await Post("EditarOrdenPendiente", new() { ["__RequestVerificationToken"] = token, ["id"] = ordenAprobada.Id, ["insumoId"] = "i", ["cantidad"] = "2", ["precio"] = "5" })).StatusCode);
            await AssertAuditEvents();

            // 3. Edición exitosa con cambio real emite 1 evento before/after
            Assert.Equal(HttpStatusCode.Redirect, (await Post("EditarOrdenPendiente", new() { ["__RequestVerificationToken"] = token, ["id"] = ordenAprobada.Id, ["insumoId"] = "i", ["cantidad"] = "4", ["precio"] = "8" })).StatusCode);
            var despuesEdicionAprobada = AuditCampos.CapturarOrdenCompra((await db.Collection("ordenesCompra").Document(ordenAprobada.Id).GetSnapshotAsync()).ConvertTo<OrdenCompra>());
            Assert.NotEqual(antesOrdenAprobada["Items"], despuesEdicionAprobada["Items"]);
            Assert.Contains("\"Cantidad\":4", despuesEdicionAprobada["Items"]);
            Assert.Contains("\"PrecioUnitario\":8", despuesEdicionAprobada["Items"]);
            var evEdicion = ("Edicion de orden de compra pendiente", ordenAprobada.Id, $"Orden {ordenAprobada.Id}", antesOrdenAprobada, despuesEdicionAprobada);
            await AssertAuditEvents(evEdicion);

            // Reintento idéntico tras edición no emite evento duplicado
            Assert.Equal(HttpStatusCode.Redirect, (await Post("EditarOrdenPendiente", new() { ["__RequestVerificationToken"] = token, ["id"] = ordenAprobada.Id, ["insumoId"] = "i", ["cantidad"] = "4", ["precio"] = "8" })).StatusCode);
            await AssertAuditEvents(evEdicion);

            // 4. Aprobación exitosa (transporte falso acepta) y reintento/concurrencia sin duplicado
            var aprobacionesExitosas = await Task.WhenAll(
                Post("AprobarOrden", new() { ["__RequestVerificationToken"] = token, ["id"] = ordenAprobada.Id }),
                Post("AprobarOrden", new() { ["__RequestVerificationToken"] = token, ["id"] = ordenAprobada.Id }));
            Assert.All(aprobacionesExitosas, r => Assert.Equal(HttpStatusCode.Redirect, r.StatusCode));
            var despuesAprobada = AuditCampos.CapturarOrdenCompra((await db.Collection("ordenesCompra").Document(ordenAprobada.Id).GetSnapshotAsync()).ConvertTo<OrdenCompra>());
            Assert.Equal("Enviada", despuesAprobada["Estado"]);
            var intermedioRevision = new Dictionary<string, string>(despuesEdicionAprobada) { ["Estado"] = "RevisionManual" };
            var evRevisionAprobada = ("Paso de orden de compra a revision manual", ordenAprobada.Id, $"Orden {ordenAprobada.Id}", despuesEdicionAprobada, intermedioRevision);
            var evAprobacion = ("Aprobacion de orden de compra", ordenAprobada.Id, $"Orden {ordenAprobada.Id}", intermedioRevision, despuesAprobada);
            await AssertAuditEvents(evEdicion, evRevisionAprobada, evAprobacion);
            email.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);

            // Reintento secuencial tras aprobación no envía ni duplica auditoría
            Assert.Equal(HttpStatusCode.Redirect, (await Post("AprobarOrden", new() { ["__RequestVerificationToken"] = token, ["id"] = ordenAprobada.Id })).StatusCode);
            await AssertAuditEvents(evEdicion, evRevisionAprobada, evAprobacion);
            email.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);

            // 5. Envío incierto: registra cambio a RevisionManual sin afirmar aceptación ni entrega; reintento en vuelo y posterior no duplican evento
            responderExito = false;
            bloqueoTransporte = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            transporteIniciado = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var antesIncierta = AuditCampos.CapturarOrdenCompra((await db.Collection("ordenesCompra").Document(ordenIncierta.Id).GetSnapshotAsync()).ConvertTo<OrdenCompra>());

            var aprobacionIncierta = Post("AprobarOrden", new() { ["__RequestVerificationToken"] = token, ["id"] = ordenIncierta.Id });
            await transporteIniciado.Task.WaitAsync(TimeSpan.FromSeconds(20));

            // Reintento mientras está en RevisionManual en vuelo: no envía ni duplica evento
            Assert.Equal(HttpStatusCode.Redirect, (await Post("AprobarOrden", new() { ["__RequestVerificationToken"] = token, ["id"] = ordenIncierta.Id })).StatusCode);
            bloqueoTransporte.TrySetResult();
            Assert.Equal(HttpStatusCode.Redirect, (await aprobacionIncierta.WaitAsync(TimeSpan.FromSeconds(20))).StatusCode);

            var despuesIncierta = AuditCampos.CapturarOrdenCompra((await db.Collection("ordenesCompra").Document(ordenIncierta.Id).GetSnapshotAsync()).ConvertTo<OrdenCompra>());
            Assert.Equal("RevisionManual", despuesIncierta["Estado"]);
            var evRevision = ("Paso de orden de compra a revision manual", ordenIncierta.Id, $"Orden {ordenIncierta.Id}", antesIncierta, despuesIncierta);
            Assert.DoesNotContain("acept", evRevision.Item1, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("entreg", evRevision.Item1, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("enviad", evRevision.Item1, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("recibid", evRevision.Item1, StringComparison.OrdinalIgnoreCase);
            await AssertAuditEvents(evEdicion, evRevisionAprobada, evAprobacion, evRevision);
            email.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

            // Reintentos posteriores sobre orden en RevisionManual tampoco duplican evento ni reenvían
            Assert.Equal(HttpStatusCode.Redirect, (await Post("AprobarOrden", new() { ["__RequestVerificationToken"] = token, ["id"] = ordenIncierta.Id })).StatusCode);
            Assert.Equal(HttpStatusCode.Redirect, (await Post("EditarOrdenPendiente", new() { ["__RequestVerificationToken"] = token, ["id"] = ordenIncierta.Id, ["insumoId"] = "i", ["cantidad"] = "9", ["precio"] = "9" })).StatusCode);
            await AssertAuditEvents(evEdicion, evRevisionAprobada, evAprobacion, evRevision);
            email.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

            // 6. Roles personalizados persistidos en Firestore permanecen intactos
            var rolSnap = await db.Collection("roles").Document(rolPersonalizado.Id).GetSnapshotAsync();
            Assert.True(rolSnap.Exists);
            var rolPersistido = rolSnap.ConvertTo<Rol>();
            Assert.Equal("Supervisor Ordenes", rolPersistido.Nombre);
            Assert.Equal(new[] { "Insumos.Consultar", "Insumos.Recepcionar" }, rolPersistido.Permisos);
            Assert.Equal("Activo", rolPersistido.Estado);
        }
        finally
        {
            bloqueoTransporte?.TrySetResult();
            await app.StopAsync();
        }
    }

    [LocalFirestoreFact]
    public async Task EdicionesYAprobacionesConcurrentesCapturanSnapshotsDeTransicionEfectiva()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-orden-race-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("proveedores").Document("p").SetAsync(new Proveedor { Id = "p", RazonSocial = "Proveedor", EmailOrdenes = "p@example.invalid" });
        await db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "Jabon" });

        Func<string, Task>? onAntesDeResponderExito = null;
        var email = new Mock<IEmailSender>();
        email.Setup(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Returns<EmailMessage, CancellationToken>(async (msg, _) =>
            {
                if (onAntesDeResponderExito != null)
                    await onAntesDeResponderExito(msg.Html);
                return new ResultadoEnvioEmail(true, "Aceptado");
            });

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
        builder.Services.AddSingleton(email.Object);

        await using var app = builder.Build();
        app.UseRequestLocalization(new RequestLocalizationOptions
        {
            DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("es-AR"),
            SupportedCultures = new[] { new System.Globalization.CultureInfo("es-AR") },
            SupportedUICultures = new[] { new System.Globalization.CultureInfo("es-AR") }
        });
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();
        try
        {
            var baseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = baseAddress };
            using var client2 = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = baseAddress };
            client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Modificar");
            client2.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Modificar");

            var page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Ordenes"));
            var token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.NotEmpty(token);
            var page2 = WebUtility.HtmlDecode(await client2.GetStringAsync("/Insumos/Ordenes"));
            var token2 = Regex.Match(page2, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.NotEmpty(token2);

            Task<HttpResponseMessage> Post(HttpClient c, string t, string action, Dictionary<string, string> fields)
            {
                fields["__RequestVerificationToken"] = t;
                return c.PostAsync("/Insumos/" + action, new FormUrlEncodedContent(fields));
            }

            // 1. Ediciones concurrentes sobre la misma orden: la cadena Antes -> Despues debe reflejar cada transición efectiva
            for (var n = 0; n < 6; n++)
            {
                var id = $"ord-ed-{n}";
                await db.Collection("ordenesCompra").Document(id).SetAsync(new OrdenCompra
                {
                    Id = id,
                    ProveedorId = "p",
                    FechaEmision = new DateTime(2025, 2, 10, 8, 0, n, DateTimeKind.Utc),
                    Estado = "PendienteAprobacion",
                    Notas = "El precio y la disponibilidad requieren confirmación del proveedor.",
                    Items = new() { new OrdenCompraItem { InsumoId = "i", NombreInsumo = "Jabon", Cantidad = 2m, PrecioUnitario = 5m } }
                });
                var estadoInicial = AuditCampos.CapturarOrdenCompra((await db.Collection("ordenesCompra").Document(id).GetSnapshotAsync()).ConvertTo<OrdenCompra>());

                var res = await Task.WhenAll(
                    Post(client, token, "EditarOrdenPendiente", new() { ["id"] = id, ["insumoId"] = "i", ["cantidad"] = "4", ["precio"] = "8" }),
                    Post(client2, token2, "EditarOrdenPendiente", new() { ["id"] = id, ["insumoId"] = "i", ["cantidad"] = "7", ["precio"] = "9" }));
                Assert.All(res, r => Assert.Equal(HttpStatusCode.Redirect, r.StatusCode));

                var estadoFinal = AuditCampos.CapturarOrdenCompra((await db.Collection("ordenesCompra").Document(id).GetSnapshotAsync()).ConvertTo<OrdenCompra>());
                var docs = (await db.Collection("registros_auditoria").WhereEqualTo("TargetId", id).GetSnapshotAsync()).Documents;
                Assert.Equal(2, docs.Count);

                var eventos = docs.Select(d => new
                {
                    Action = d.GetValue<string>("Action"),
                    Antes = d.GetValue<Dictionary<string, string>>("Antes"),
                    Despues = d.GetValue<Dictionary<string, string>>("Despues")
                }).ToList();
                Assert.All(eventos, e => Assert.Equal("Edicion de orden de compra pendiente", e.Action));

                var primero = Assert.Single(eventos, e => e.Antes.OrderBy(x => x.Key).SequenceEqual(estadoInicial.OrderBy(x => x.Key)));
                var segundo = Assert.Single(eventos, e => e != primero);
                Assert.Equal(primero.Despues.OrderBy(x => x.Key), segundo.Antes.OrderBy(x => x.Key));
                Assert.Equal(estadoFinal.OrderBy(x => x.Key), segundo.Despues.OrderBy(x => x.Key));
                Assert.NotEqual(primero.Antes["Items"], primero.Despues["Items"]);
                Assert.NotEqual(segundo.Antes["Items"], segundo.Despues["Items"]);
                Assert.Equal("PendienteAprobacion", primero.Despues["Estado"]);
                Assert.Equal("PendienteAprobacion", segundo.Despues["Estado"]);
            }

            // 2. Edición concurrente con aprobación: si ambas aplican, Edicion.Despues == Aprobacion.Antes y no se mezclan estados
            for (var n = 0; n < 6; n++)
            {
                var id = $"ord-mix-{n}";
                await db.Collection("ordenesCompra").Document(id).SetAsync(new OrdenCompra
                {
                    Id = id,
                    ProveedorId = "p",
                    FechaEmision = new DateTime(2025, 2, 11, 8, 0, n, DateTimeKind.Utc),
                    Estado = "PendienteAprobacion",
                    Notas = "El precio y la disponibilidad requieren confirmación del proveedor.",
                    Items = new() { new OrdenCompraItem { InsumoId = "i", NombreInsumo = "Jabon", Cantidad = 2m, PrecioUnitario = 5m } }
                });
                var estadoInicial = AuditCampos.CapturarOrdenCompra((await db.Collection("ordenesCompra").Document(id).GetSnapshotAsync()).ConvertTo<OrdenCompra>());

                var res = await Task.WhenAll(
                    Post(client, token, "EditarOrdenPendiente", new() { ["id"] = id, ["insumoId"] = "i", ["cantidad"] = "4", ["precio"] = "8" }),
                    Post(client2, token2, "AprobarOrden", new() { ["id"] = id }));
                Assert.All(res, r => Assert.Equal(HttpStatusCode.Redirect, r.StatusCode));

                var ordenFinal = (await db.Collection("ordenesCompra").Document(id).GetSnapshotAsync()).ConvertTo<OrdenCompra>();
                Assert.Equal("Enviada", ordenFinal.Estado);
                var estadoFinal = AuditCampos.CapturarOrdenCompra(ordenFinal);
                var docs = (await db.Collection("registros_auditoria").WhereEqualTo("TargetId", id).GetSnapshotAsync()).Documents;
                var evAprob = Assert.Single(docs, d => d.GetValue<string>("Action") == "Aprobacion de orden de compra");
                var antesAprob = evAprob.GetValue<Dictionary<string, string>>("Antes");
                var despuesAprob = evAprob.GetValue<Dictionary<string, string>>("Despues");
                Assert.Equal(estadoFinal.OrderBy(x => x.Key), despuesAprob.OrderBy(x => x.Key));
                var evFase1 = Assert.Single(docs, d => d.GetValue<string>("Action") == "Paso de orden de compra a revision manual");
                Assert.Equal("RevisionManual", antesAprob["Estado"]);
                Assert.Equal(evFase1.GetValue<Dictionary<string, string>>("Despues")["Estado"], antesAprob["Estado"]);

                if (ordenFinal.Items[0].Cantidad == 4m)
                {
                    var evEd = Assert.Single(docs, d => d.GetValue<string>("Action") == "Edicion de orden de compra pendiente");
                    Assert.Equal(3, docs.Count);
                    Assert.Equal(estadoInicial.OrderBy(x => x.Key), evEd.GetValue<Dictionary<string, string>>("Antes").OrderBy(x => x.Key));
                    var despuesEd = evEd.GetValue<Dictionary<string, string>>("Despues");
                    Assert.Equal("PendienteAprobacion", despuesEd["Estado"]);
                    Assert.Equal(despuesEd.OrderBy(x => x.Key), evFase1.GetValue<Dictionary<string, string>>("Antes").OrderBy(x => x.Key));
                }
                else
                {
                    Assert.Equal(2, docs.Count);
                    Assert.Equal(estadoInicial.OrderBy(x => x.Key), evFase1.GetValue<Dictionary<string, string>>("Antes").OrderBy(x => x.Key));
                }
            }

            // 3. AprobarOrden concurrente con CambiarEstadoOrden("Confirmada"): AprobarOrden debe registrar Despues.Estado == "Enviada", nunca "Confirmada"
            for (var n = 0; n < 4; n++)
            {
                var id = $"ord-conf-{n}";
                await db.Collection("ordenesCompra").Document(id).SetAsync(new OrdenCompra
                {
                    Id = id,
                    ProveedorId = "p",
                    FechaEmision = new DateTime(2025, 2, 12, 8, 0, n, DateTimeKind.Utc),
                    Estado = "PendienteAprobacion",
                    Notas = "El precio y la disponibilidad requieren confirmación del proveedor.",
                    Items = new() { new OrdenCompraItem { InsumoId = "i", NombreInsumo = "Jabon", Cantidad = 3m, PrecioUnitario = 7m } }
                });
                var estadoInicial = AuditCampos.CapturarOrdenCompra((await db.Collection("ordenesCompra").Document(id).GetSnapshotAsync()).ConvertTo<OrdenCompra>());

                var cancelarConfirmacion = new CancellationTokenSource();
                Task? confirmador = null;
                onAntesDeResponderExito = _ =>
                {
                    confirmador = Task.Run(async () =>
                    {
                        while (!cancelarConfirmacion.IsCancellationRequested)
                        {
                            await Post(client2, token2, "CambiarEstadoOrden", new() { ["id"] = id, ["estado"] = "Confirmada" });
                            var snap = await db.Collection("ordenesCompra").Document(id).GetSnapshotAsync();
                            if (snap.ConvertTo<OrdenCompra>().Estado == "Confirmada")
                                break;
                        }
                    });
                    return Task.CompletedTask;
                };

                var aprobRes = await Post(client, token, "AprobarOrden", new() { ["id"] = id });
                Assert.Equal(HttpStatusCode.Redirect, aprobRes.StatusCode);
                if (confirmador != null) await confirmador.WaitAsync(TimeSpan.FromSeconds(10));
                cancelarConfirmacion.Cancel();
                onAntesDeResponderExito = null;

                var docs = (await db.Collection("registros_auditoria").WhereEqualTo("TargetId", id).GetSnapshotAsync()).Documents;
                var evAprob = Assert.Single(docs, d => d.GetValue<string>("Action") == "Aprobacion de orden de compra");
                var evFase1 = Assert.Single(docs, d => d.GetValue<string>("Action") == "Paso de orden de compra a revision manual");
                Assert.Equal(estadoInicial.OrderBy(x => x.Key), evFase1.GetValue<Dictionary<string, string>>("Antes").OrderBy(x => x.Key));
                var despuesAprob = evAprob.GetValue<Dictionary<string, string>>("Despues");
                Assert.Equal("Enviada", despuesAprob["Estado"]);
            }

            // 4. Edición con formato decimal equivalente ("4,00" en es-AR vs 4m) no emite evento fantasma
            var idNorm = "ord-norm";
            await db.Collection("ordenesCompra").Document(idNorm).SetAsync(new OrdenCompra
            {
                Id = idNorm,
                ProveedorId = "p",
                FechaEmision = new DateTime(2025, 2, 13, 8, 0, 0, DateTimeKind.Utc),
                Estado = "PendienteAprobacion",
                Items = new() { new OrdenCompraItem { InsumoId = "i", NombreInsumo = "Jabon", Cantidad = 4m, PrecioUnitario = 8m } }
            });
            Assert.Equal(HttpStatusCode.Redirect, (await Post(client, token, "EditarOrdenPendiente", new() { ["id"] = idNorm, ["insumoId"] = "i", ["cantidad"] = "4,00", ["precio"] = "8,00" })).StatusCode);
            var itemNorm = Assert.Single((await db.Collection("ordenesCompra").Document(idNorm).GetSnapshotAsync()).ConvertTo<OrdenCompra>().Items);
            Assert.Equal(4m, itemNorm.Cantidad);
            Assert.Equal(8m, itemNorm.PrecioUnitario);
            Assert.Empty((await db.Collection("registros_auditoria").WhereEqualTo("TargetId", idNorm).GetSnapshotAsync()).Documents);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [LocalFirestoreFact]
    public async Task RecepcionDeOrdenHttpRegistraAuditoriaBeforeAfterEnRecepcionParcialYTotalYSinDuplicadosEnReintentosYRechazos()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-orden-recibir-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("proveedores").Document("p").SetAsync(new Proveedor { Id = "p", RazonSocial = "Proveedor", EmailOrdenes = "p@example.invalid" });
        await db.Collection("insumos").Document("i1").SetAsync(new Insumo { Id = "i1", Nombre = "Jabon", StockActual = 0m });
        await db.Collection("insumos").Document("i2").SetAsync(new Insumo { Id = "i2", Nombre = "Cera", StockActual = 0m });
        var rolPersonalizado = new Rol
        {
            Id = "rol-custom-recepcion",
            Nombre = "Operador Recepcion",
            Descripcion = "Rol personalizado preservado",
            Permisos = new() { "Insumos.Consultar", "Insumos.Recepcionar" },
            Estado = "Activo",
            FechaCreacion = new DateTime(2025, 1, 20, 9, 0, 0, DateTimeKind.Utc)
        };
        await db.Collection("roles").Document(rolPersonalizado.Id).SetAsync(rolPersonalizado);

        var ordenRec = new OrdenCompra
        {
            Id = "ord-rec",
            ProveedorId = "p",
            FechaEmision = new DateTime(2025, 3, 1, 10, 0, 0, DateTimeKind.Utc),
            Estado = "Confirmada",
            GrupoCotizacionId = "g-rec",
            Items = new()
            {
                new OrdenCompraItem { InsumoId = "i1", NombreInsumo = "Jabon", Cantidad = 2m, CantidadRecibida = 0m, PrecioUnitario = 10m },
                new OrdenCompraItem { InsumoId = "i2", NombreInsumo = "Cera", Cantidad = 4m, CantidadRecibida = 0m, PrecioUnitario = 20m }
            }
        };
        await db.Collection("ordenesCompra").Document(ordenRec.Id).SetAsync(ordenRec);
        await db.Collection("ordenesCompra").Document("ord-rival").SetAsync(new OrdenCompra
        {
            Id = "ord-rival",
            ProveedorId = "p",
            FechaEmision = new DateTime(2025, 3, 1, 9, 0, 0, DateTimeKind.Utc),
            Estado = "Enviada",
            GrupoCotizacionId = "g-rec"
        });
        await db.Collection("ordenesCompra").Document("ord-cancelada").SetAsync(new OrdenCompra
        {
            Id = "ord-cancelada",
            ProveedorId = "p",
            FechaEmision = new DateTime(2025, 3, 1, 8, 0, 0, DateTimeKind.Utc),
            Estado = "Cancelada",
            MotivoCancelacion = "Cancelada previamente",
            Items = new() { new OrdenCompraItem { InsumoId = "i1", NombreInsumo = "Jabon", Cantidad = 1m } }
        });
        await db.Collection("ordenesCompra").Document("ord-pendiente").SetAsync(new OrdenCompra
        {
            Id = "ord-pendiente",
            ProveedorId = "p",
            FechaEmision = new DateTime(2025, 3, 1, 7, 0, 0, DateTimeKind.Utc),
            Estado = "PendienteAprobacion",
            Items = new() { new OrdenCompraItem { InsumoId = "i1", NombreInsumo = "Jabon", Cantidad = 1m } }
        });
        await db.Collection("ordenesCompra").Document("ord-revision").SetAsync(new OrdenCompra
        {
            Id = "ord-revision",
            ProveedorId = "p",
            FechaEmision = new DateTime(2025, 3, 1, 6, 0, 0, DateTimeKind.Utc),
            Estado = "RevisionManual",
            Items = new() { new OrdenCompraItem { InsumoId = "i1", NombreInsumo = "Jabon", Cantidad = 1m } }
        });

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
        builder.Services.AddSingleton(Mock.Of<IEmailSender>());

        await using var app = builder.Build();
        app.UseRequestLocalization(new RequestLocalizationOptions
        {
            DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("es-AR"),
            SupportedCultures = new[] { new System.Globalization.CultureInfo("es-AR") },
            SupportedUICultures = new[] { new System.Globalization.CultureInfo("es-AR") }
        });
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();
        try
        {
            var baseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = baseAddress };
            using var client2 = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = baseAddress };

            void SetPermissions(HttpClient c, string permissions)
            {
                c.DefaultRequestHeaders.Remove("X-Test-Permission");
                if (!string.IsNullOrEmpty(permissions))
                    c.DefaultRequestHeaders.Add("X-Test-Permission", permissions);
            }

            SetPermissions(client, "Insumos.Recepcionar");
            SetPermissions(client2, "Insumos.Recepcionar");
            var page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Ordenes"));
            var token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.NotEmpty(token);
            var page2 = WebUtility.HtmlDecode(await client2.GetStringAsync("/Insumos/Ordenes"));
            var token2 = Regex.Match(page2, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.NotEmpty(token2);

            Task<HttpResponseMessage> PostRecibir(HttpClient c, string t, string id, (string insumoId, string cantidad)[] items, bool cancelarConcurrentes = false, bool includeToken = true)
            {
                var form = new List<KeyValuePair<string, string>> { new("id", id) };
                if (includeToken) form.Add(new("__RequestVerificationToken", t));
                foreach (var (insumoId, cantidad) in items)
                {
                    form.Add(new("insumoIds", insumoId));
                    form.Add(new("cantidades", cantidad));
                }
                if (cancelarConcurrentes) form.Add(new("cancelarConcurrentes", "true"));
                return c.PostAsync("/Insumos/RecibirOrden", new FormUrlEncodedContent(form));
            }

            // 1. Rechazos (CSRF, permisos, estados inválidos, cantidades inválidas) no cambian stock/Kardex ni emiten auditoría
            Assert.Equal(HttpStatusCode.BadRequest, (await PostRecibir(client, token, ordenRec.Id, new[] { ("i1", "1"), ("i2", "2") }, includeToken: false)).StatusCode);

            foreach (var permiso in new[] { "", "Insumos.Consultar", "Insumos.Crear", "Insumos.Modificar", "Insumos.AjustarStock" })
            {
                SetPermissions(client, permiso);
                Assert.Equal(HttpStatusCode.Forbidden, (await PostRecibir(client, token, ordenRec.Id, new[] { ("i1", "1"), ("i2", "2") })).StatusCode);
            }

            SetPermissions(client, "Insumos.Recepcionar");
            foreach (var (id, items) in new (string, (string, string)[])[]
            {
                ("", new[] { ("i1", "1") }),
                ("no-existe", new[] { ("i1", "1") }),
                ("ord-cancelada", new[] { ("i1", "1") }),
                ("ord-pendiente", new[] { ("i1", "1") }),
                ("ord-revision", new[] { ("i1", "1") }),
                (ordenRec.Id, new[] { ("i1", "0") }),
                (ordenRec.Id, new[] { ("i1", "-1") }),
                (ordenRec.Id, new[] { ("i1", "abc") }),
                (ordenRec.Id, new[] { ("i1", "3") })
            })
            {
                Assert.Equal(HttpStatusCode.Redirect, (await PostRecibir(client, token, id, items)).StatusCode);
            }

            Assert.Equal(0m, (await db.Collection("insumos").Document("i1").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
            Assert.Equal(0m, (await db.Collection("insumos").Document("i2").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
            Assert.Empty((await db.Collection("movimientosStock").GetSnapshotAsync()).Documents);
            Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);

            // 2. Recepción parcial emite exactamente 1 evento before/after con el cambio de CantidadRecibida
            var antesParcial = AuditCampos.CapturarOrdenCompra((await db.Collection("ordenesCompra").Document(ordenRec.Id).GetSnapshotAsync()).ConvertTo<OrdenCompra>());
            var resParcial = await PostRecibir(client, token, ordenRec.Id, new[] { ("i1", "1,00"), ("i2", "2,00") }, cancelarConcurrentes: true);
            Assert.Equal(HttpStatusCode.Redirect, resParcial.StatusCode);

            var ordenTrasParcial = (await db.Collection("ordenesCompra").Document(ordenRec.Id).GetSnapshotAsync()).ConvertTo<OrdenCompra>();
            Assert.Equal("Confirmada", ordenTrasParcial.Estado);
            Assert.Equal(1m, ordenTrasParcial.Items[0].CantidadRecibida);
            Assert.Equal(2m, ordenTrasParcial.Items[1].CantidadRecibida);
            Assert.Equal("Enviada", (await db.Collection("ordenesCompra").Document("ord-rival").GetSnapshotAsync()).ConvertTo<OrdenCompra>().Estado);
            Assert.Equal(1m, (await db.Collection("insumos").Document("i1").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
            Assert.Equal(2m, (await db.Collection("insumos").Document("i2").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
            Assert.Equal(2, (await db.Collection("movimientosStock").GetSnapshotAsync()).Count);

            var despuesParcial = AuditCampos.CapturarOrdenCompra(ordenTrasParcial);
            var docsParcial = (await db.Collection("registros_auditoria").WhereEqualTo("TargetId", ordenRec.Id).GetSnapshotAsync()).Documents;
            var evParcial = Assert.Single(docsParcial);
            Assert.Equal("Recepción de orden de compra", evParcial.GetValue<string>("Action"));
            Assert.Equal("OrdenCompra", evParcial.GetValue<string>("TargetType"));
            Assert.Equal($"Orden {ordenRec.Id}", evParcial.GetValue<string>("NombreHistorico"));
            Assert.Equal("test", evParcial.GetValue<string>("UserId"));
            Assert.Equal("test@example.invalid", evParcial.GetValue<string>("UserEmail"));
            Assert.Equal(antesParcial.OrderBy(x => x.Key), evParcial.GetValue<Dictionary<string, string>>("Antes").OrderBy(x => x.Key));
            Assert.Equal(despuesParcial.OrderBy(x => x.Key), evParcial.GetValue<Dictionary<string, string>>("Despues").OrderBy(x => x.Key));
            Assert.Equal("ConCambios", AuditCampos.ObtenerEstadoHistorial(evParcial.GetValue<Dictionary<string, string>>("Antes"), evParcial.GetValue<Dictionary<string, string>>("Despues")));

            // 3. Recepción total emite segundo evento before/after (parcial -> Recibida) y cancela rival
            var resTotal = await PostRecibir(client, token, ordenRec.Id, new[] { ("i1", "1"), ("i2", "2") }, cancelarConcurrentes: true);
            Assert.Equal(HttpStatusCode.Redirect, resTotal.StatusCode);

            var ordenTrasTotal = (await db.Collection("ordenesCompra").Document(ordenRec.Id).GetSnapshotAsync()).ConvertTo<OrdenCompra>();
            Assert.Equal("Recibida", ordenTrasTotal.Estado);
            Assert.Equal(2m, ordenTrasTotal.Items[0].CantidadRecibida);
            Assert.Equal(4m, ordenTrasTotal.Items[1].CantidadRecibida);
            var rival = (await db.Collection("ordenesCompra").Document("ord-rival").GetSnapshotAsync()).ConvertTo<OrdenCompra>();
            Assert.Equal("Cancelada", rival.Estado);
            Assert.Equal($"Adjudicacion orden {ordenRec.Id}", rival.MotivoCancelacion);
            Assert.Equal(2m, (await db.Collection("insumos").Document("i1").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
            Assert.Equal(4m, (await db.Collection("insumos").Document("i2").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
            Assert.Equal(4, (await db.Collection("movimientosStock").GetSnapshotAsync()).Count);

            var despuesTotal = AuditCampos.CapturarOrdenCompra(ordenTrasTotal);
            var docsTrasTotal = (await db.Collection("registros_auditoria").WhereEqualTo("TargetId", ordenRec.Id).GetSnapshotAsync()).Documents;
            Assert.Equal(2, docsTrasTotal.Count);
            var evTotal = Assert.Single(docsTrasTotal, d => d.Id != evParcial.Id);
            Assert.Equal("Recepción de orden de compra", evTotal.GetValue<string>("Action"));
            Assert.Equal($"Orden {ordenRec.Id}", evTotal.GetValue<string>("NombreHistorico"));
            Assert.Equal(despuesParcial.OrderBy(x => x.Key), evTotal.GetValue<Dictionary<string, string>>("Antes").OrderBy(x => x.Key));
            Assert.Equal(despuesTotal.OrderBy(x => x.Key), evTotal.GetValue<Dictionary<string, string>>("Despues").OrderBy(x => x.Key));

            var rivalAudits = (await db.Collection("registros_auditoria").WhereEqualTo("TargetId", "ord-rival").GetSnapshotAsync()).Documents;
            var evRivalCancel = Assert.Single(rivalAudits);
            Assert.Equal("Cambio de estado de orden a Cancelada", evRivalCancel.GetValue<string>("Action"));
            Assert.Equal("OrdenCompra", evRivalCancel.GetValue<string>("TargetType"));
            Assert.Equal("test", evRivalCancel.GetValue<string>("UserId"));
            Assert.Equal("test@example.invalid", evRivalCancel.GetValue<string>("UserEmail"));
            Assert.Equal("Cancelada", evRivalCancel.GetValue<Dictionary<string, string>>("Despues")["Estado"]);
            Assert.Equal($"Adjudicacion orden {ordenRec.Id}", evRivalCancel.GetValue<Dictionary<string, string>>("Despues")["MotivoCancelacion"]);

            // 4. Reintentos idempotentes (secuenciales y concurrentes) sobre orden Recibida NO duplican stock, Kardex ni auditoría
            Assert.Equal(HttpStatusCode.Redirect, (await PostRecibir(client, token, ordenRec.Id, new[] { ("i1", "1"), ("i2", "2") }, cancelarConcurrentes: true)).StatusCode);
            Assert.Equal(HttpStatusCode.Redirect, (await PostRecibir(client, token, ordenRec.Id, new[] { ("i1", "2"), ("i2", "4") })).StatusCode);
            var reintentosConcurrentes = await Task.WhenAll(
                PostRecibir(client, token, ordenRec.Id, new[] { ("i1", "1"), ("i2", "2") }),
                PostRecibir(client2, token2, ordenRec.Id, new[] { ("i1", "1"), ("i2", "2") }));
            Assert.All(reintentosConcurrentes, r => Assert.Equal(HttpStatusCode.Redirect, r.StatusCode));

            Assert.Equal(2m, (await db.Collection("insumos").Document("i1").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
            Assert.Equal(4m, (await db.Collection("insumos").Document("i2").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
            Assert.Equal(4, (await db.Collection("movimientosStock").GetSnapshotAsync()).Count);
            Assert.Equal(2, (await db.Collection("registros_auditoria").WhereEqualTo("TargetId", ordenRec.Id).GetSnapshotAsync()).Count);
            Assert.Single((await db.Collection("registros_auditoria").WhereEqualTo("TargetId", "ord-rival").GetSnapshotAsync()).Documents);

            // 5. Recepciones concurrentes simultáneas sobre orden abierta emiten exactamente 1 evento de auditoría
            var ordenConc = new OrdenCompra
            {
                Id = "ord-conc",
                ProveedorId = "p",
                FechaEmision = new DateTime(2025, 3, 2, 10, 0, 0, DateTimeKind.Utc),
                Estado = "Enviada",
                Items = new() { new OrdenCompraItem { InsumoId = "i1", NombreInsumo = "Jabon", Cantidad = 2m, CantidadRecibida = 0m, PrecioUnitario = 10m } }
            };
            await db.Collection("ordenesCompra").Document(ordenConc.Id).SetAsync(ordenConc);
            var antesConc = AuditCampos.CapturarOrdenCompra((await db.Collection("ordenesCompra").Document(ordenConc.Id).GetSnapshotAsync()).ConvertTo<OrdenCompra>());

            var concRes = await Task.WhenAll(
                PostRecibir(client, token, ordenConc.Id, new[] { ("i1", "2") }),
                PostRecibir(client2, token2, ordenConc.Id, new[] { ("i1", "2") }));
            Assert.All(concRes, r => Assert.Equal(HttpStatusCode.Redirect, r.StatusCode));

            var finalConc = (await db.Collection("ordenesCompra").Document(ordenConc.Id).GetSnapshotAsync()).ConvertTo<OrdenCompra>();
            Assert.Equal("Recibida", finalConc.Estado);
            Assert.Equal(4m, (await db.Collection("insumos").Document("i1").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
            Assert.Equal(5, (await db.Collection("movimientosStock").GetSnapshotAsync()).Count);
            var docsConc = (await db.Collection("registros_auditoria").WhereEqualTo("TargetId", ordenConc.Id).GetSnapshotAsync()).Documents;
            var evConc = Assert.Single(docsConc);
            Assert.Equal("Recepción de orden de compra", evConc.GetValue<string>("Action"));
            Assert.Equal(antesConc.OrderBy(x => x.Key), evConc.GetValue<Dictionary<string, string>>("Antes").OrderBy(x => x.Key));
            Assert.Equal(AuditCampos.CapturarOrdenCompra(finalConc).OrderBy(x => x.Key), evConc.GetValue<Dictionary<string, string>>("Despues").OrderBy(x => x.Key));

            // 6. Roles personalizados persistidos en Firestore permanecen intactos
            var rolSnap = await db.Collection("roles").Document(rolPersonalizado.Id).GetSnapshotAsync();
            Assert.True(rolSnap.Exists);
            var rolPersistido = rolSnap.ConvertTo<Rol>();
            Assert.Equal("Operador Recepcion", rolPersistido.Nombre);
            Assert.Equal(new[] { "Insumos.Consultar", "Insumos.Recepcionar" }, rolPersistido.Permisos);
            Assert.Equal("Activo", rolPersistido.Estado);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [LocalFirestoreFact]
    public async Task CreacionYCambiosDeEstadoDeOrdenRegistranAuditoriaBeforeAfterYSinDuplicadosEnRechazosYReintentos()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-orden-crud-audit-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("proveedores").Document("p").SetAsync(new Proveedor { Id = "p", RazonSocial = "Proveedor Quimico", EmailOrdenes = "proveedor@example.invalid", Estado = "Activo" });
        await db.Collection("proveedores").Document("p-inactivo").SetAsync(new Proveedor { Id = "p-inactivo", RazonSocial = "Proveedor Inactivo", EmailOrdenes = "inactivo@example.invalid", Estado = "Inactivo" });
        await db.Collection("insumos").Document("i1").SetAsync(new Insumo { Id = "i1", Nombre = "Shampoo Neutro", Estado = "Activo" });
        await db.Collection("insumos").Document("i2").SetAsync(new Insumo { Id = "i2", Nombre = "Cera Liquida", Estado = "Activo" });
        await db.Collection("insumos").Document("i-inactivo").SetAsync(new Insumo { Id = "i-inactivo", Nombre = "Insumo Inactivo", Estado = "Inactivo" });

        var rolPersonalizado = new Rol
        {
            Id = "rol-custom-operador-ordenes",
            Nombre = "Operador Control Ordenes",
            Descripcion = "Rol personalizado preservado",
            Permisos = new() { "Insumos.Consultar", "Insumos.Recepcionar" },
            Estado = "Activo",
            FechaCreacion = new DateTime(2025, 1, 10, 12, 0, 0, DateTimeKind.Utc)
        };
        await db.Collection("roles").Document(rolPersonalizado.Id).SetAsync(rolPersonalizado);

        Func<Task<ResultadoEnvioEmail>> enviarHandler = () => Task.FromResult(new ResultadoEnvioEmail(true, "Aceptado"));
        var email = new Mock<IEmailSender>();
        email.Setup(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Returns(() => enviarHandler());

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
        builder.Services.AddSingleton(email.Object);

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

            void SetPermissions(string permissions)
            {
                client.DefaultRequestHeaders.Remove("X-Test-Permission");
                if (!string.IsNullOrEmpty(permissions))
                    client.DefaultRequestHeaders.Add("X-Test-Permission", permissions);
            }

            SetPermissions("Insumos.Crear,Insumos.Modificar,Insumos.Consultar");
            var page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Ordenes"));
            var token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.NotEmpty(token);

            async Task<HttpResponseMessage> Post(string action, Dictionary<string, string> fields) =>
                await client.PostAsync("/Insumos/" + action, new FormUrlEncodedContent(fields));

            var solicitudesEsperadas = 0;
            async Task AssertAuditEvents(params (string Action, string TargetId, string NombreHistorico, Dictionary<string, string> Antes, Dictionary<string, string> Despues)[] expected)
            {
                var todos = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents;
                var docs = todos.Where(d => d.GetValue<string>("TargetType") == "OrdenCompra").ToList();
                Assert.Equal(expected.Length, docs.Count);
                Assert.Equal(expected.Length + solicitudesEsperadas * 3, todos.Count);
                foreach (var e in expected)
                {
                    var doc = Assert.Single(docs, d => d.GetValue<string>("Action") == e.Action && d.GetValue<string>("TargetId") == e.TargetId);
                    Assert.Equal("test", doc.GetValue<string>("UserId"));
                    Assert.Equal("test@example.invalid", doc.GetValue<string>("UserEmail"));
                    Assert.Equal("OrdenCompra", doc.GetValue<string>("TargetType"));
                    Assert.Equal(e.NombreHistorico, doc.GetValue<string>("NombreHistorico"));
                    var antes = doc.GetValue<Dictionary<string, string>>("Antes");
                    var despues = doc.GetValue<Dictionary<string, string>>("Despues");
                    if (e.Action == "Creacion de orden de compra")
                    {
                        Assert.Empty(e.Antes);
                        Assert.Null(antes);
                    }
                    else
                        Assert.Equal(e.Antes.OrderBy(x => x.Key), antes.OrderBy(x => x.Key));
                    Assert.Equal(e.Despues.OrderBy(x => x.Key), despues.OrderBy(x => x.Key));
                    Assert.Equal("ConCambios", AuditCampos.ObtenerEstadoHistorial(antes, despues));
                    var json = System.Text.Json.JsonSerializer.Serialize(doc.ToDictionary());
                    Assert.DoesNotContain("Password", json, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("Token", json, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("Secret", json, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("ApiKey", json, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("Clave", json, StringComparison.OrdinalIgnoreCase);
                }
            }

            // 1. Guards de permisos y token CSRF
            SetPermissions("Insumos.Recepcionar");
            Assert.Equal(HttpStatusCode.Forbidden, (await Post("CrearOrdenManual", new() { ["__RequestVerificationToken"] = token, ["ProveedorId"] = "p", ["Items[0].InsumoId"] = "i1", ["Items[0].Cantidad"] = "2" })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Post("CambiarEstadoOrden", new() { ["__RequestVerificationToken"] = token, ["id"] = "ord-x", ["estado"] = "Confirmada" })).StatusCode);

            SetPermissions("Insumos.Crear,Insumos.Modificar,Insumos.Consultar");
            Assert.Equal(HttpStatusCode.BadRequest, (await Post("CrearOrdenManual", new() { ["ProveedorId"] = "p", ["Items[0].InsumoId"] = "i1", ["Items[0].Cantidad"] = "2" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Post("CambiarEstadoOrden", new() { ["id"] = "ord-x", ["estado"] = "Confirmada" })).StatusCode);

            // 2. Rechazos en creación: no emiten eventos de auditoría ni crean órdenes
            foreach (var invalido in new[]
            {
                new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["ProveedorId"] = "p", ["Items[0].InsumoId"] = "i1", ["Items[0].Cantidad"] = "0" },
                new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["ProveedorId"] = "p-inactivo", ["Items[0].InsumoId"] = "i1", ["Items[0].Cantidad"] = "2" },
                new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["ProveedorId"] = "p", ["Items[0].InsumoId"] = "i-inactivo", ["Items[0].Cantidad"] = "2" },
                new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["ProveedorId"] = "p", ["Items[0].InsumoId"] = "i1", ["Items[0].Cantidad"] = "2", ["Items[1].InsumoId"] = "i1", ["Items[1].Cantidad"] = "3" }
            })
            {
                Assert.Equal(HttpStatusCode.Redirect, (await Post("CrearOrdenManual", invalido)).StatusCode);
            }
            Assert.Empty((await db.Collection("ordenesCompra").GetSnapshotAsync()).Documents);
            await AssertAuditEvents();

            // S11: rechazo de orden libre válida, sin efectos persistidos ni envío.
            Assert.Equal(HttpStatusCode.Redirect, (await Post("CrearOrdenManual", new() { ["__RequestVerificationToken"] = token, ["ProveedorId"] = "p", ["Items[0].InsumoId"] = "i1", ["Items[0].Cantidad"] = "2" })).StatusCode);
            Assert.Contains("Debe adjudicar una cotización o preparar una repetición reciente para aprobación.", WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Ordenes")));
            Assert.Empty((await db.Collection("ordenesCompra").GetSnapshotAsync()).Documents);
            Assert.Empty((await db.Collection("movimientosStock").GetSnapshotAsync()).Documents);
            Assert.Equal(0m, (await new InsumoService(db).Obtener("i1"))!.StockActual);
            await AssertAuditEvents();
            email.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);

            async Task<OrdenCompra> CotizarYAdjudicar(string insumoId, string cantidad, Func<Task<ResultadoEnvioEmail>> despacho)
            {
                var antes = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Count;
                enviarHandler = () => Task.FromResult(new ResultadoEnvioEmail(true, "Aceptado"));
                Assert.Equal(HttpStatusCode.Redirect, (await Post("CrearSolicitudes", new() { ["__RequestVerificationToken"] = token, ["proveedorIds"] = "p", ["items[0].InsumoId"] = insumoId, ["items[0].Cantidad"] = cantidad })).StatusCode);
                var solicitud = Assert.Single((await db.Collection("solicitudesCotizacion").GetSnapshotAsync()).Documents.Select(d => d.ConvertTo<SolicitudCotizacion>()), s => s.Estado == "Pendiente");
                Assert.Equal(antes + 1, (await db.Collection("registros_auditoria").GetSnapshotAsync()).Count);
                Assert.Equal(HttpStatusCode.Redirect, (await Post("RegistrarRespuesta", new() { ["__RequestVerificationToken"] = token, ["SolicitudId"] = solicitud.Id, ["PlazoEntrega"] = "1", ["Items[0].InsumoId"] = insumoId, ["Items[0].Cantidad"] = cantidad, ["Items[0].PrecioUnitario"] = "5", ["Items[0].Disponible"] = "true" })).StatusCode);
                Assert.Equal(antes + 2, (await db.Collection("registros_auditoria").GetSnapshotAsync()).Count);
                var respuesta = Assert.Single((await db.Collection("cotizacionesRespuestas").GetSnapshotAsync()).Documents.Select(d => d.ConvertTo<CotizacionRespuesta>()), r => r.SolicitudId == solicitud.Id);
                enviarHandler = despacho;
                Assert.Equal(HttpStatusCode.Redirect, (await Post("Adjudicar", new() { ["__RequestVerificationToken"] = token, ["id"] = solicitud.Id, ["respuestaId"] = respuesta.Id })).StatusCode);
                var orden = Assert.Single((await db.Collection("ordenesCompra").GetSnapshotAsync()).Documents.Select(d => d.ConvertTo<OrdenCompra>()), o => o.GrupoCotizacionId == solicitud.GrupoId);
                Assert.Equal(antes + (orden.Estado == "Enviada" ? 6 : 5), (await db.Collection("registros_auditoria").GetSnapshotAsync()).Count);
                solicitudesEsperadas++;
                return orden;
            }
            var ordenA = await CotizarYAdjudicar("i1", "2", () => Task.FromResult(new ResultadoEnvioEmail(true, "Aceptado")));
            Assert.Equal("Enviada", ordenA.Estado);
            var despuesA = AuditCampos.CapturarOrdenCompra(ordenA);
            var revisionA = new Dictionary<string, string>(despuesA) { ["Estado"] = "RevisionManual" };
            var pendienteA = new Dictionary<string, string>(despuesA) { ["Estado"] = "PendienteAprobacion" };
            var evCrearA1 = ("Creacion de orden de compra", ordenA.Id, $"Orden {ordenA.Id}", new Dictionary<string, string>(), pendienteA);
            var evRevisionA = ("Paso de orden de compra a revision manual", ordenA.Id, $"Orden {ordenA.Id}", pendienteA, revisionA);
            var evCrearA2 = ("Aprobacion de orden de compra", ordenA.Id, $"Orden {ordenA.Id}", revisionA, despuesA);
            await AssertAuditEvents(evCrearA1, evRevisionA, evCrearA2);
            var ordenB = await CotizarYAdjudicar("i2", "5", () => Task.FromResult(new ResultadoEnvioEmail(false, "Rechazo controlado")));
            Assert.Equal("RevisionManual", ordenB.Estado);
            var despuesB = AuditCampos.CapturarOrdenCompra(ordenB);
            var pendienteB = new Dictionary<string, string>(despuesB) { ["Estado"] = "PendienteAprobacion" };
            var evCrearB = ("Creacion de orden de compra", ordenB.Id, $"Orden {ordenB.Id}", new Dictionary<string, string>(), pendienteB);
            var evRevisionB = ("Paso de orden de compra a revision manual", ordenB.Id, $"Orden {ordenB.Id}", pendienteB, despuesB);
            await AssertAuditEvents(evCrearA1, evRevisionA, evCrearA2, evCrearB, evRevisionB);
            var ordenC = await CotizarYAdjudicar("i1", "1", () => throw new InvalidOperationException("Falla de red simulada"));
            Assert.Equal("RevisionManual", ordenC.Estado);
            var despuesC = AuditCampos.CapturarOrdenCompra(ordenC);
            var pendienteC = new Dictionary<string, string>(despuesC) { ["Estado"] = "PendienteAprobacion" };
            var evCrearC = ("Creacion de orden de compra", ordenC.Id, $"Orden {ordenC.Id}", new Dictionary<string, string>(), pendienteC);
            var evRevisionC = ("Paso de orden de compra a revision manual", ordenC.Id, $"Orden {ordenC.Id}", pendienteC, despuesC);
            await AssertAuditEvents(evCrearA1, evRevisionA, evCrearA2, evCrearB, evRevisionB, evCrearC, evRevisionC);

            // 6. Cambios de estado en ordenA: Enviada -> Confirmada
            var antesConfirmar = AuditCampos.CapturarOrdenCompra((await db.Collection("ordenesCompra").Document(ordenA.Id).GetSnapshotAsync()).ConvertTo<OrdenCompra>());
            Assert.Equal("Enviada", antesConfirmar["Estado"]);
            var resConf = await Post("CambiarEstadoOrden", new() { ["__RequestVerificationToken"] = token, ["id"] = ordenA.Id, ["estado"] = "Confirmada" });
            Assert.Equal(HttpStatusCode.Redirect, resConf.StatusCode);
            var ordenAConfirmada = (await db.Collection("ordenesCompra").Document(ordenA.Id).GetSnapshotAsync()).ConvertTo<OrdenCompra>();
            Assert.Equal("Confirmada", ordenAConfirmada.Estado);
            var despuesConfirmar = AuditCampos.CapturarOrdenCompra(ordenAConfirmada);
            var evConfirmar = ("Cambio de estado de orden a Confirmada", ordenA.Id, $"Orden {ordenA.Id}", antesConfirmar, despuesConfirmar);
            await AssertAuditEvents(evCrearA1, evRevisionA, evCrearA2, evCrearB, evRevisionB, evCrearC, evRevisionC, evConfirmar);

            // 7. No-op / reintento en Confirmada no emite evento duplicado
            var resConfRep = await Post("CambiarEstadoOrden", new() { ["__RequestVerificationToken"] = token, ["id"] = ordenA.Id, ["estado"] = "Confirmada" });
            Assert.Equal(HttpStatusCode.Redirect, resConfRep.StatusCode);
            await AssertAuditEvents(evCrearA1, evRevisionA, evCrearA2, evCrearB, evRevisionB, evCrearC, evRevisionC, evConfirmar);

            // 8. Transición Confirmada -> Despachada
            var antesDespachar = AuditCampos.CapturarOrdenCompra((await db.Collection("ordenesCompra").Document(ordenA.Id).GetSnapshotAsync()).ConvertTo<OrdenCompra>());
            Assert.Equal("Confirmada", antesDespachar["Estado"]);
            var resDesp = await Post("CambiarEstadoOrden", new() { ["__RequestVerificationToken"] = token, ["id"] = ordenA.Id, ["estado"] = "Despachada" });
            Assert.Equal(HttpStatusCode.Redirect, resDesp.StatusCode);
            var ordenADespachada = (await db.Collection("ordenesCompra").Document(ordenA.Id).GetSnapshotAsync()).ConvertTo<OrdenCompra>();
            Assert.Equal("Despachada", ordenADespachada.Estado);
            var despuesDespachar = AuditCampos.CapturarOrdenCompra(ordenADespachada);
            var evDespachar = ("Cambio de estado de orden a Despachada", ordenA.Id, $"Orden {ordenA.Id}", antesDespachar, despuesDespachar);
            await AssertAuditEvents(evCrearA1, evRevisionA, evCrearA2, evCrearB, evRevisionB, evCrearC, evRevisionC, evConfirmar, evDespachar);

            // 9. Rechazos en cambios de estado:
            // a) Transición inválida (Despachada a Confirmada)
            Assert.Equal(HttpStatusCode.Redirect, (await Post("CambiarEstadoOrden", new() { ["__RequestVerificationToken"] = token, ["id"] = ordenA.Id, ["estado"] = "Confirmada" })).StatusCode);
            // b) Cancelación sin motivo obligatorio
            Assert.Equal(HttpStatusCode.Redirect, (await Post("CambiarEstadoOrden", new() { ["__RequestVerificationToken"] = token, ["id"] = ordenA.Id, ["estado"] = "Cancelada", ["motivo"] = "   " })).StatusCode);
            // c) Orden inexistente
            Assert.Equal(HttpStatusCode.Redirect, (await Post("CambiarEstadoOrden", new() { ["__RequestVerificationToken"] = token, ["id"] = "no-existe", ["estado"] = "Confirmada" })).StatusCode);
            await AssertAuditEvents(evCrearA1, evRevisionA, evCrearA2, evCrearB, evRevisionB, evCrearC, evRevisionC, evConfirmar, evDespachar);

            // 10. Cancelación válida con motivo
            var antesCancelar = AuditCampos.CapturarOrdenCompra((await db.Collection("ordenesCompra").Document(ordenA.Id).GetSnapshotAsync()).ConvertTo<OrdenCompra>());
            Assert.Equal("Despachada", antesCancelar["Estado"]);
            var resCanc = await Post("CambiarEstadoOrden", new()
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = ordenA.Id,
                ["estado"] = "Cancelada",
                ["motivo"] = "Proveedor canceló el despacho por falta de stock"
            });
            Assert.Equal(HttpStatusCode.Redirect, resCanc.StatusCode);
            var ordenACancelada = (await db.Collection("ordenesCompra").Document(ordenA.Id).GetSnapshotAsync()).ConvertTo<OrdenCompra>();
            Assert.Equal("Cancelada", ordenACancelada.Estado);
            Assert.Equal("Proveedor canceló el despacho por falta de stock", ordenACancelada.MotivoCancelacion);
            var despuesCancelar = AuditCampos.CapturarOrdenCompra(ordenACancelada);
            var evCancelar = ("Cambio de estado de orden a Cancelada", ordenA.Id, $"Orden {ordenA.Id}", antesCancelar, despuesCancelar);
            await AssertAuditEvents(evCrearA1, evRevisionA, evCrearA2, evCrearB, evRevisionB, evCrearC, evRevisionC, evConfirmar, evDespachar, evCancelar);

            // 11. Reintento sobre orden cancelada (no admite cambios) no emite evento
            Assert.Equal(HttpStatusCode.Redirect, (await Post("CambiarEstadoOrden", new() { ["__RequestVerificationToken"] = token, ["id"] = ordenA.Id, ["estado"] = "Cancelada", ["motivo"] = "otro" })).StatusCode);
            await AssertAuditEvents(evCrearA1, evRevisionA, evCrearA2, evCrearB, evRevisionB, evCrearC, evRevisionC, evConfirmar, evDespachar, evCancelar);

            // 12. Rol personalizado en Firestore permanece intacto
            var rolSnap = await db.Collection("roles").Document(rolPersonalizado.Id).GetSnapshotAsync();
            Assert.True(rolSnap.Exists);
            var rolPersistido = rolSnap.ConvertTo<Rol>();
            Assert.Equal("Operador Control Ordenes", rolPersistido.Nombre);
            Assert.Equal(new[] { "Insumos.Consultar", "Insumos.Recepcionar" }, rolPersistido.Permisos);
            Assert.Equal("Activo", rolPersistido.Estado);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [LocalFirestoreFact]
    public async Task AdjudicacionHttpRegistraAuditoriaTransaccionalDeOrdenYSolicitudConActorClaimsYSinDuplicados()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-adjudicar-http-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("proveedores").Document("p").SetAsync(new Proveedor { Id = "p", RazonSocial = "Proveedor Oficial", EmailOrdenes = "p@example.invalid", Estado = "Activo" });
        await db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "Shampoo Neutro", Estado = "Activo" });

        var solicitud = new SolicitudCotizacion
        {
            Id = "sol-http",
            GrupoId = "grupo-http",
            ProveedorId = "p",
            Estado = "Respondida",
            Items = new() { new OrdenCompraItem { InsumoId = "i", NombreInsumo = "Shampoo Neutro", Cantidad = 5m } }
        };
        await db.Collection("solicitudesCotizacion").Document(solicitud.Id).SetAsync(solicitud);

        var respuesta = new CotizacionRespuesta
        {
            Id = "resp-http",
            SolicitudId = solicitud.Id,
            ProveedorId = "p",
            PlazoEntrega = "3",
            Items = new() { new OrdenCompraItem { InsumoId = "i", NombreInsumo = "Shampoo Neutro", Cantidad = 5m, PrecioUnitario = 12m } }
        };
        await db.Collection("cotizacionesRespuestas").Document(respuesta.Id).SetAsync(respuesta);

        var email = new Mock<IEmailSender>();
        var auditWriter = new Mock<IAuditWriter>();

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
        builder.Services.AddSingleton(auditWriter.Object);
        builder.Services.AddSingleton(email.Object);

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
            client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Modificar");

            var page = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Ordenes"));
            var token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.NotEmpty(token);

            // POST Adjudicar
            var response = await client.PostAsync("/Insumos/Adjudicar", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = solicitud.Id,
                ["respuestaId"] = respuesta.Id
            }));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            // Orden creada
            var ordenes = (await db.Collection("ordenesCompra").GetSnapshotAsync()).Documents.Select(d => d.ConvertTo<OrdenCompra>()).ToList();
            var orden = Assert.Single(ordenes);
            Assert.Equal("Enviada", orden.Estado);
            Assert.Equal("p", orden.ProveedorId);

            // Auditoría de la orden generada registrada en la transacción con los claims del actor autenticado ("test", "test@example.invalid")
            var auditsOrden = (await db.Collection("registros_auditoria").WhereEqualTo("TargetId", orden.Id).GetSnapshotAsync()).Documents;
            var evOrden = Assert.Single(auditsOrden);
            Assert.Equal("Creacion de orden de compra", evOrden.GetValue<string>("Action"));
            Assert.Equal("OrdenCompra", evOrden.GetValue<string>("TargetType"));
            Assert.Equal("test", evOrden.GetValue<string>("UserId"));
            Assert.Equal("test@example.invalid", evOrden.GetValue<string>("UserEmail"));

            // Auditoría de la solicitud también registrada en la transacción
            var auditsSol = (await db.Collection("registros_auditoria").WhereEqualTo("TargetId", solicitud.Id).GetSnapshotAsync()).Documents;
            var evSol = Assert.Single(auditsSol);
            Assert.Equal("Adjudicación de cotización", evSol.GetValue<string>("Action"));
            Assert.Equal("SolicitudCotizacion", evSol.GetValue<string>("TargetType"));
            Assert.Equal("test", evSol.GetValue<string>("UserId"));
            Assert.Equal("test@example.invalid", evSol.GetValue<string>("UserEmail"));

            // Reintento: no duplica orden ni auditoría
            var retryResponse = await client.PostAsync("/Insumos/Adjudicar", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = solicitud.Id,
                ["respuestaId"] = respuesta.Id
            }));
            Assert.Equal(HttpStatusCode.Redirect, retryResponse.StatusCode);
            Assert.Single((await db.Collection("ordenesCompra").GetSnapshotAsync()).Documents);
            Assert.Single((await db.Collection("registros_auditoria").WhereEqualTo("TargetId", orden.Id).GetSnapshotAsync()).Documents);
            Assert.Single((await db.Collection("registros_auditoria").WhereEqualTo("TargetId", solicitud.Id).GetSnapshotAsync()).Documents);
        }
        finally
        {
            await app.StopAsync();
        }
    }
}

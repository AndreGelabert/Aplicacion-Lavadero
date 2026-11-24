using System.Net;
using System.Net.Http.Json;
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
using Moq;
using Xunit;

namespace Firebase.Tests.Integration;

public sealed class TiposInsumoModalHttpIntegrationTests
{
    [LocalFirestoreFact]
    public async Task ModalValidaContratoYConservaEdicionPorHttp()
    {
        await using var f = await Fixture.Start();
        using var client = f.Client("Insumos.Crear,Insumos.Modificar");
        var token = await client.GetStringAsync("/test-token");
        var original = await f.Tipos.ObtenerTodos();

        async Task Reject(Dictionary<string, string> fields, string message)
        {
            using var response = await client.PostAsync("/TiposInsumo/ModalGuardar", new FormUrlEncodedContent(fields));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(message, (await response.Content.ReadFromJsonAsync<Reply>())!.mensaje);
            Assert.Equal(original.Count, (await f.Tipos.ObtenerTodos()).Count);
        }
        Dictionary<string, string> Fields(string id = "", string nombre = "Prueba modal", string categoria = "Volumen", string unidad = "l", string crear = "true") => new()
        {
            ["__RequestVerificationToken"] = token, ["Id"] = id, ["Nombre"] = nombre,
            ["Categoria"] = categoria, ["UnidadResumen"] = unidad, ["crear"] = crear
        };
        const string invalid = "Datos del tipo de insumo inválidos.";
        await Reject(Fields(nombre: ""), invalid);
        await Reject(Fields(nombre: new string('x', 121)), invalid);
        await Reject(Fields(categoria: ""), invalid);
        await Reject(Fields(unidad: ""), invalid);
        await Reject(Fields(crear: "false"), "El tipo de insumo no existe.");
        using (var noToken = await client.PostAsync("/TiposInsumo/ModalGuardar", new FormUrlEncodedContent(new Dictionary<string, string>())))
            Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        Assert.Equal(original.Count, (await f.Tipos.ObtenerTodos()).Count);

        using (var emptyId = await client.PostAsync("/TiposInsumo/ModalGuardar", new FormUrlEncodedContent(Fields(nombre: "Creación sin id"))))
        {
            Assert.Equal(HttpStatusCode.OK, emptyId.StatusCode);
            var emptyResult = (await emptyId.Content.ReadFromJsonAsync<Reply>())!;
            Assert.Equal("Tipo de insumo creado correctamente.", emptyResult.mensaje);
            Assert.False(string.IsNullOrWhiteSpace(emptyResult.id));
            Assert.NotNull(await f.Tipos.ObtenerPorId(emptyResult.id!));
        }

        var create = Fields(id: "client-supplied");
        create["Estado"] = "Inactivo"; create["EsProtegido"] = "true";
        using var created = await client.PostAsync("/TiposInsumo/ModalGuardar", new FormUrlEncodedContent(create));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var result = (await created.Content.ReadFromJsonAsync<Reply>())!;
        Assert.Equal("Tipo de insumo creado correctamente.", result.mensaje);
        Assert.False(string.IsNullOrWhiteSpace(result.id));
        Assert.NotEqual("client-supplied", result.id);
        var saved = await f.Tipos.ObtenerPorId(result.id!);
        Assert.NotNull(saved); Assert.Equal("Activo", saved!.Estado); Assert.False(saved.EsProtegido);
        Assert.Equal("Prueba modal", saved.Nombre); Assert.Equal("Volumen", saved.Categoria); Assert.Equal("l", saved.UnidadResumen);

        using var edited = await client.PostAsync("/TiposInsumo/ModalGuardar", new FormUrlEncodedContent(Fields(result.id!, "Editado", crear: "false")));
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.Equal("Tipo de insumo actualizado correctamente.", (await edited.Content.ReadFromJsonAsync<Reply>())!.mensaje);
        Assert.Equal("Editado", (await f.Tipos.ObtenerPorId(result.id!))!.Nombre);
    }

    private sealed record Reply(string mensaje, string? id);

    private sealed class Fixture(WebApplication app, FirestoreDb db) : IAsyncDisposable
    {
        public TipoInsumoService Tipos { get; } = new(db);
        public static async Task<Fixture> Start()
        {
            if (Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST") != "127.0.0.1:8190") throw new InvalidOperationException("Only the disposable local emulator is permitted.");
            var db = new FirestoreDbBuilder { ProjectId = "demo-tipos-http-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
            builder.Services.AddControllersWithViews().AddApplicationPart(typeof(TiposInsumoController).Assembly);
            builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, InsumosConfiguracionGeneralHttpIntegrationTests.HeaderAuth>("Test", _ => { });
            builder.Services.AddAuthorization(); builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
            builder.Services.AddSingleton(db); builder.Services.AddSingleton(Mock.Of<IAuditWriter>()); builder.Services.AddScoped<TipoInsumoService>();
            var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization();
            app.MapGet("/test-token", (HttpContext context, IAntiforgery antiforgery) => antiforgery.GetAndStoreTokens(context).RequestToken!).RequireAuthorization();
            app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
            await app.StartAsync(); return new(app, db);
        }
        public HttpClient Client(string permissions)
        {
            var client = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Test-Permissions", permissions); return client;
        }
        public async ValueTask DisposeAsync() { await app.StopAsync(); await app.DisposeAsync(); }
    }
}

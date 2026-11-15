using System.Security.Claims;
using System.Text.Json;
using Firebase.Models;
using Firebase.Security;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Firebase.Tests.Integration;

public sealed class ServicioRecetaRenderIntegrationTests
{
    [Fact]
    public void UnidadesAusentesSeSerializanComoObjetoVacio()
    {
        object? unidades = null;
        var json = JsonSerializer.Serialize((unidades as Dictionary<string, string[]>) ?? new Dictionary<string, string[]>());
        Assert.Equal("{}", json);
    }

    [LocalFirestoreFact]
    public async Task IndexYFormPartialExponenUnidadesYOpcionesSinStock()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-receta-render-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var servicio = new ServicioService(db);
        var tipos = new TipoServicioService(db);
        var vehiculos = new TipoVehiculoService(db);
        var controller = new ServicioController(servicio, Mock.Of<IAuditWriter>(), tipos, vehiculos, new VehiculoService(db),
            new ServicioQueryService(servicio), new ServicioLifecycleService(servicio, null!), new ServicioFormService(servicio), null!,
            new InsumoService(db), new TipoInsumoService(db))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
                new Claim(PermisoClaim.Tipo, "Servicios.Consultar"), new Claim(PermisoClaim.Tipo, "Servicios.Crear") }, "test")) } }
        };

        await VerificarIndexYParcial(false);
        await db.Collection("tiposInsumo").Document("tipo1").SetAsync(new TipoInsumo { Id = "tipo1", Nombre = "Jabon", Categoria = "Volumen", UnidadResumen = "l", Estado = "Activo" });
        await db.Collection("insumos").Document("producto1").SetAsync(new Insumo { Id = "producto1", Nombre = "Jabon liquido", TipoId = "tipo1", Tipo = "Jabon", UnidadMedida = "ml", Estado = "Activo", StockActual = 42 });
        await VerificarIndexYParcial(true);

        async Task VerificarIndexYParcial(bool populated)
        {
            Assert.IsType<ViewResult>(await controller.Index(new(), new(), new()));
            Verificar();
            Assert.IsType<PartialViewResult>(await controller.FormPartial(null!));
            Verificar();
            void Verificar()
            {
                var unidades = Assert.IsType<Dictionary<string, string[]>>((object)controller.ViewBag.UnidadesReceta);
                var json = JsonSerializer.Serialize(unidades);
                using var documento = JsonDocument.Parse(json);
                Assert.NotEmpty(documento.RootElement.EnumerateObject().ToArray());
                Assert.All(documento.RootElement.EnumerateObject(), propiedad => Assert.NotEmpty(propiedad.Value.EnumerateArray().ToArray()));
                var productos = Assert.IsType<List<ServicioController.InsumoRecetaOpcion>>((object)controller.ViewBag.InsumosDisponibles);
                var opcionesTipo = Assert.IsType<List<TipoInsumo>>((object)controller.ViewBag.TiposInsumoDisponibles);
                Assert.Equal(populated ? 1 : 0, productos.Count);
                Assert.Equal(populated ? 1 : 0, opcionesTipo.Count);
                if (populated)
                {
                    var producto = Assert.Single(productos);
                    Assert.Equal("producto1", producto.Id);
                    Assert.Equal("ml", producto.UnidadMedida);
                    Assert.DoesNotContain("StockActual", JsonSerializer.Serialize(producto));
                    Assert.Equal("tipo1", Assert.Single(opcionesTipo).Id);
                }
            }
        }
    }
}

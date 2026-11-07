using Firebase.Models;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Firebase.Tests.Integration;

public class ServicioQueryRefactorTests
{
    [LocalFirestoreFact]
    public async Task IndexPreservesPageClampFormAndCreateOnlyBranch()
    {
        var db = new FirestoreDbBuilder
        {
            ProjectId = "demo-serviceindex-" + Guid.NewGuid().ToString("N")[..16],
            EmulatorDetection = EmulatorDetection.EmulatorOnly
        }.Build();
        var services = new ServicioService(db);
        var service = new Servicio
        {
            Id = "generated", Nombre = "Lavado Basico", Precio = 100m, Estado = "Activo",
            Tipo = "Exterior", TipoVehiculo = "Auto", Descripcion = "Description", TiempoEstimado = 10
        };
        await services.CrearServicio(service);
        ServicioController Controller(bool consult) => new(services, null!, new TipoServicioService(db),
            new TipoVehiculoService(db), null!, new Firebase.Services.ServicioQueryService(services), new Firebase.Services.ServicioLifecycleService(services, null!), new Firebase.Services.ServicioFormService(services))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                        new[] { new System.Security.Claims.Claim("permiso", consult ? "Servicios.Consultar" : "Servicios.Crear") }, "test"))
                }
            }
        };
        foreach (var page in new[] { 1, 2 })
        {
            var controller = Controller(true);
            var view = Assert.IsType<ViewResult>(await controller.Index(null!, null!, null!, pageNumber: page, pageSize: 1));
            Assert.Equal(page == 1 ? new[] { service.Id } : Array.Empty<string>(),
                Assert.IsType<List<Servicio>>(view.Model).Select(row => row.Id));
            Assert.Equal(1, controller.ViewData["CurrentPage"]);
            Assert.Equal(1, controller.ViewData["TotalPages"]);
            Assert.Equal(1, controller.ViewData["PageSize"]);
            Assert.Equal(new[] { 1 }, Assert.IsType<List<int>>(controller.ViewData["VisiblePages"]));
            Assert.Equal(new[] { "Activo" }, Assert.IsType<List<string>>(controller.ViewData["Estados"]));
            Assert.Equal("Nombre", controller.ViewData["SortBy"]);
            Assert.Equal("asc", controller.ViewData["SortOrder"]);
            Assert.Equal(100m, controller.ViewData["PrecioMinimo"]);
            Assert.Equal(100m, controller.ViewData["PrecioMaximo"]);
            Assert.Equal("CrearServicioAjax", controller.ViewData["FormAction"]);
            Assert.Equal("Registrando un Servicio", controller.ViewData["FormTitle"]);
            Assert.False(controller.ViewData.ContainsKey("SearchTerm"));
        }
        var createOnly = Controller(false);
        // Invalid paging and edit ID must not reach listing or edit reads in this branch.
        var creation = Assert.IsType<ViewResult>(await createOnly.Index(null!, null!, null!, pageNumber: 0, pageSize: 0, editId: "missing"));
        Assert.Empty(Assert.IsType<List<Servicio>>(creation.Model));
        Assert.Equal("CrearServicioAjax", createOnly.ViewData["FormAction"]);
        Assert.Equal("Registrar", createOnly.ViewData["SubmitButtonText"]);
        Assert.Equal("Limpiar Campos", createOnly.ViewData["ClearButtonText"]);
        Assert.IsType<List<string>>(createOnly.ViewData["TiposServicio"]);
        Assert.IsType<List<string>>(createOnly.ViewData["TodosLosTiposVehiculo"]);
        Assert.False(createOnly.ViewData.ContainsKey("CurrentPage"));
        Assert.False(createOnly.ViewData.ContainsKey("EditServicio"));
    }

    [LocalFirestoreFact]
    public async Task PartialActionsPreserveRowsFiltersAndDistinctMetadata()
    {
        var db = new FirestoreDbBuilder
        {
            ProjectId = "demo-servicequery-" + Guid.NewGuid().ToString("N")[..16],
            EmulatorDetection = EmulatorDetection.EmulatorOnly
        }.Build();
        var services = new ServicioService(db);
        var ids = new Dictionary<string, string>();
        foreach (var (id, name, price, state) in new[]
        {
            ("a", "Lavado Alfa", 100m, "Activo"),
            ("b", "Lavado Beta", 200m, "Activo"),
            ("c", "Lavado Gamma", 300m, "Activo"),
            ("d", "Lavado Inactivo", 150m, "Inactivo")
        })
        {
            var service = new Servicio
            {
                Id = id, Nombre = name, Precio = price, Estado = state,
                Tipo = "Exterior", TipoVehiculo = "Auto", Descripcion = "Description", TiempoEstimado = 10
            };
            await services.CrearServicio(service);
            ids[id] = service.Id;
        }

        foreach (var search in new[] { false, true })
        foreach (var emptyStates in new[] { false, true })
        {
            var controller = new ServicioController(services, null!, null!, null!, null!, new Firebase.Services.ServicioQueryService(services), new Firebase.Services.ServicioLifecycleService(services, null!), new Firebase.Services.ServicioFormService(services));
            List<string>? states = emptyStates ? new() : null;
            var result = search
                ? await controller.SearchPartial("Lavado", states!, null!, null!, pageNumber: 2, pageSize: 1)
                : await controller.TablePartial(states!, null!, null!, pageNumber: 2, pageSize: 1);
            var partial = Assert.IsType<PartialViewResult>(result);
            Assert.Equal("_ServicioTable", partial.ViewName);
            Assert.Equal(ids["b"], Assert.Single(Assert.IsType<List<Servicio>>(partial.Model)).Id);
            var data = controller.ViewData;
            Assert.Equal(2, data["CurrentPage"]);
            Assert.Equal(3, data["TotalPages"]);
            Assert.Equal(new[] { 1, 2, 3 }, Assert.IsType<List<int>>(data["VisiblePages"]));
            Assert.Equal(new[] { "Activo" }, Assert.IsType<List<string>>(data["Estados"]));
            if (states is not null) Assert.Equal(new[] { "Activo" }, states);
            Assert.True(data.ContainsKey("Tipos")); Assert.Null(data["Tipos"]);
            Assert.True(data.ContainsKey("TiposVehiculo")); Assert.Null(data["TiposVehiculo"]);
            Assert.Equal("Nombre", data["SortBy"]);
            Assert.Equal("asc", data["SortOrder"]);
            Assert.Equal(search, data.ContainsKey("SearchTerm"));
            if (search) Assert.Equal("Lavado", data["SearchTerm"]);
            foreach (var key in new[] { "PageSize", "PrecioDesde", "PrecioHasta", "PrecioMinimo", "PrecioMaximo" })
                Assert.False(data.ContainsKey(key));
        }

        foreach (var search in new[] { false, true })
        foreach (var page in new[] { 1, 2 })
        {
            var controller = new ServicioController(services, null!, null!, null!, null!, new Firebase.Services.ServicioQueryService(services), new Firebase.Services.ServicioLifecycleService(services, null!), new Firebase.Services.ServicioFormService(services));
            var states = new List<string> { "Activo" };
            var types = new List<string> { "Exterior" };
            var vehicles = new List<string> { "Auto" };
            var result = search
                ? await controller.SearchPartial("Beta", states, types, vehicles, 190m, 210m, page, 1, "Precio", "desc")
                : await controller.TablePartial(states, types, vehicles, 190m, 210m, page, 1, "Precio", "desc");
            var partial = Assert.IsType<PartialViewResult>(result);
            Assert.Equal("_ServicioTable", partial.ViewName);
            Assert.Equal(page == 1 ? new[] { ids["b"] } : Array.Empty<string>(),
                Assert.IsType<List<Servicio>>(partial.Model).Select(row => row.Id));
            Assert.Equal(page, controller.ViewData["CurrentPage"]);
            Assert.Equal(1, controller.ViewData["TotalPages"]);
            Assert.Equal(new[] { 1 }, Assert.IsType<List<int>>(controller.ViewData["VisiblePages"]));
            Assert.Same(states, controller.ViewData["Estados"]);
            Assert.Same(types, controller.ViewData["Tipos"]);
            Assert.Same(vehicles, controller.ViewData["TiposVehiculo"]);
            Assert.Equal("Precio", controller.ViewData["SortBy"]);
            Assert.Equal("desc", controller.ViewData["SortOrder"]);
            Assert.Equal(search, controller.ViewData.ContainsKey("SearchTerm"));
            if (search) Assert.Equal("Beta", controller.ViewData["SearchTerm"]);
        }

        foreach (var search in new[] { false, true })
        {
            var controller = new ServicioController(services, null!, null!, null!, null!, new Firebase.Services.ServicioQueryService(services), new Firebase.Services.ServicioLifecycleService(services, null!), new Firebase.Services.ServicioFormService(services));
            var result = search
                ? await controller.SearchPartial("missing", null!, null!, null!)
                : await controller.TablePartial(null!, new List<string> { "missing" }, null!);
            Assert.Empty(Assert.IsType<List<Servicio>>(Assert.IsType<PartialViewResult>(result).Model));
            Assert.Equal(1, controller.ViewData["CurrentPage"]);
            Assert.Equal(1, controller.ViewData["TotalPages"]);
            Assert.Equal(new[] { 1 }, Assert.IsType<List<int>>(controller.ViewData["VisiblePages"]));
        }
    }
}

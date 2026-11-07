using System.Security.Claims;
using Firebase.Models;
using Firebase.Services;
using FirebaseLoginCustom.Controllers;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Firebase.Tests.Integration;

public class LavadoQueryRefactorTests
{
    [LocalFirestoreFact]
    public async Task PartialActionsPreserveRowsAndMetadata()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-washquery-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var services = new ServicioService(db);
        var packages = new PaqueteServicioService(db, services, cache);
        var staff = new PersonalService(db);
        var config = new ConfiguracionService(db, cache);
        var washes = new LavadoService(db, services, packages, staff, config);
        var date = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        foreach (var (id, day) in new[] { ("a", 1), ("b", 2), ("c", 3) })
            await washes.ActualizarLavado(new Lavado
            {
                Id = id, Estado = "EnProceso", ClienteId = "client", ClienteNombre = "Ana Perez",
                VehiculoId = "vehicle", VehiculoPatente = "ABC123", Precio = 100,
                Servicios = new() { new ServicioEnLavado { ServicioId = "service", Estado = "Pendiente" } },
                FechaCreacion = date.AddDays(day), EstadoRetiro = "Pendiente", Pago = new PagoLavado { Estado = "Pendiente" }
            });

        LavadosController Controller() => new(NullLogger<LavadosController>.Instance, new AuditService(db), washes,
            services, packages, new ClienteService(db), new VehiculoService(db), staff, config,
            new TipoVehiculoService(db), new TipoDocumentoService(db), null!)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "actor") }, "test"))
            } }
        };

        foreach (var search in new[] { true, false })
        foreach (var emptyStates in new[] { false, true })
        {
            var controller = Controller();
            List<string>? states = emptyStates ? new() : null;
            var result = search
                ? await controller.SearchPartial("ABC", states, null, null, null, null, null, null, null, null, 2, 1)
                : await controller.TablePartial(states, null, null, null, null, null, null, null, null, 2, 1);
            var partial = Assert.IsType<PartialViewResult>(result);
            Assert.Equal("_LavadoTable", partial.ViewName);
            var row = Assert.Single(Assert.IsType<List<Lavado>>(partial.Model));
            Assert.Equal("b", row.Id);
            Assert.Equal("ABC123", row.VehiculoPatente);
            var data = controller.ViewData;
            Assert.Equal(2, data["CurrentPage"]);
            Assert.Equal(3, data["TotalPages"]);
            Assert.Equal(new[] { 1, 2, 3 }, Assert.IsType<List<int>>(data["VisiblePages"]));
            Assert.Equal(new[] { "Pendiente", "EnProceso", "Realizado", "RealizadoParcialmente", "Cancelado" }, Assert.IsType<List<string>>(data["Estados"]));
            Assert.Equal("FechaCreacion", data["SortBy"]);
            Assert.Equal("desc", data["SortOrder"]);
            foreach (var key in new[] { "ClienteId", "VehiculoId", "FechaDesde", "FechaHasta", "PrecioDesde", "PrecioHasta", "EstadosPago", "EstadosRetiro" })
            {
                Assert.True(data.ContainsKey(key));
                Assert.Null(data[key]);
            }
            Assert.False(data.ContainsKey("PageSize"));
            Assert.Equal(search, data.ContainsKey("SearchTerm"));
            if (search) Assert.Equal("ABC", data["SearchTerm"]);
        }

        foreach (var search in new[] { true, false })
        foreach (var page in new[] { 1, 2 })
        {
            var controller = Controller();
            var states = new List<string> { "EnProceso" };
            var payments = new List<string> { "Pendiente" };
            var pickups = new List<string> { "Pendiente" };
            var from = date.AddDays(2); var to = date.AddDays(2);
            var result = search
                ? await controller.SearchPartial("ABC", states, "client", "vehicle", from, to, 90, 110, payments, pickups, page, 1, "Precio", "asc")
                : await controller.TablePartial(states, "client", "vehicle", from, to, 90, 110, payments, pickups, page, 1, "Precio", "asc");
            var partial = Assert.IsType<PartialViewResult>(result);
            Assert.Equal("_LavadoTable", partial.ViewName);
            var rows = Assert.IsType<List<Lavado>>(partial.Model);
            Assert.Equal(page == 1 ? new[] { "b" } : Array.Empty<string>(), rows.Select(x => x.Id));
            var data = controller.ViewData;
            Assert.Equal(page, data["CurrentPage"]);
            Assert.Equal(1, data["TotalPages"]);
            Assert.Equal(new[] { 1 }, Assert.IsType<List<int>>(data["VisiblePages"]));
            Assert.Equal(states, Assert.IsType<List<string>>(data["Estados"]));
            Assert.Equal("client", data["ClienteId"]); Assert.Equal("vehicle", data["VehiculoId"]);
            Assert.Equal(from, data["FechaDesde"]); Assert.Equal(to, data["FechaHasta"]);
            Assert.Equal(90m, data["PrecioDesde"]); Assert.Equal(110m, data["PrecioHasta"]);
            Assert.Equal(payments, Assert.IsType<List<string>>(data["EstadosPago"]));
            Assert.Equal(pickups, Assert.IsType<List<string>>(data["EstadosRetiro"]));
            Assert.Equal("Precio", data["SortBy"]); Assert.Equal("asc", data["SortOrder"]);
            Assert.False(data.ContainsKey("PageSize"));
            Assert.Equal(search, data.ContainsKey("SearchTerm"));
            if (search) Assert.Equal("ABC", data["SearchTerm"]);
        }

        foreach (var search in new[] { true, false })
        {
            var controller = Controller();
            var result = search
                ? await controller.SearchPartial("missing", null, null, null, null, null, null, null, null, null)
                : await controller.TablePartial(null, "missing", null, null, null, null, null, null, null);
            Assert.Empty(Assert.IsType<List<Lavado>>(Assert.IsType<PartialViewResult>(result).Model));
            Assert.Equal(1, controller.ViewData["CurrentPage"]);
            Assert.Equal(1, controller.ViewData["TotalPages"]);
            Assert.Equal(new[] { 1 }, Assert.IsType<List<int>>(controller.ViewData["VisiblePages"]));
        }
    }
}

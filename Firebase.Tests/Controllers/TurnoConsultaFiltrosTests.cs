using Firebase.Models;
using Firebase.Services;
using FirebaseLoginCustom.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace Firebase.Tests.Controllers;

public class TurnoConsultaFiltrosTests
{
    private sealed class Consulta : ITurnoConsultaService
    {
        public int Llamadas { get; private set; }
        public Task<List<TurnoConsultaViewModel>> Listar()
        {
            Llamadas++;
            return Task.FromResult(new List<TurnoConsultaViewModel> {
                Item("a", "Zoe", 10, 1), Item("b", "Ana", 20, 2), Item("c", "Luis", 30, 3)
            });
        }
        public Task<TurnoConsultaViewModel?> Obtener(string id) => Task.FromResult<TurnoConsultaViewModel?>(null);
        public Task<List<Turno>> ProximosDeCliente(string clienteId, DateTime desdeUtc) => Task.FromResult(new List<Turno>());
        private static TurnoConsultaViewModel Item(string id, string nombre, decimal precio, int dia) =>
            new(id, nombre, "Pendiente", new DateTime(2030, 1, dia, 12, 0, 0, DateTimeKind.Utc), new DateTime(2030, 1, dia, 13, 0, 0, DateTimeKind.Utc), false, precio, Array.Empty<VehiculoConsultaViewModel>());
    }

    [Fact]
    public async Task Index_FiltersInclusiveRangesAndSortsByClient()
    {
        var controller = new TurnosController(new Consulta());
        var view = Assert.IsType<ViewResult>(await controller.Index(new DateTime(2030, 1, 2), new DateTime(2030, 1, 3), 20, 30, "Cliente", "asc"));
        var items = Assert.IsType<List<TurnoConsultaViewModel>>(view.Model);
        Assert.Equal(new[] { "b", "c" }, items.Select(x => x.Id));
    }

    [Fact]
    public async Task Index_RejectsInvalidAndInvertedRangesBeforeQuery()
    {
        var consulta = new Consulta();
        var controller = new TurnosController(consulta);
        foreach (var result in new[] {
            await controller.Index(new DateTime(2030, 1, 3), new DateTime(2030, 1, 2)),
            await controller.Index(precioDesde: 21, precioHasta: 20),
            await controller.Index(precioDesde: -1),
            await controller.Index(precioHasta: -1)
        }) Assert.IsType<BadRequestObjectResult>(result);
        controller.ModelState.AddModelError("fechaDesde", "invalid");
        Assert.IsType<BadRequestObjectResult>(await controller.Index());
        Assert.Equal(0, consulta.Llamadas);
    }

    [Fact]
    public async Task Index_WithoutFiltersPreservesExistingOrder()
    {
        var controller = new TurnosController(new Consulta());
        var view = Assert.IsType<ViewResult>(await controller.Index());
        Assert.Equal(new[] { "a", "b", "c" }, Assert.IsType<List<TurnoConsultaViewModel>>(view.Model).Select(x => x.Id));
    }
}

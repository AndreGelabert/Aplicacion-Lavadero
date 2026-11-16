using Firebase.Models;
using Xunit;

namespace Firebase.Tests.Services;

public class InventarioAgrupadoTests
{
    private static TipoInsumo Tipo(string id, string nombre, string categoria, string unidad) =>
        new() { Id = id, Nombre = nombre, Categoria = categoria, UnidadResumen = unidad };
    private static Insumo Item(string id, string tipoId, string tipo, string unidad, decimal stock) =>
        new() { Id = id, Nombre = id, TipoId = tipoId, Tipo = tipo, UnidadMedida = unidad, StockActual = stock, StockMinimo = 1 };

    [Fact]
    public void AgrupaYConvierteTodosLosProductosYConservaDetalleYReservas()
    {
        var items = new[] { Item("a", "v", "Limpieza", "ml", 1500), Item("b", "v", "Limpieza", "l", 2), Item("c", "m", "Polvo", "g", 500) };
        var tipos = new[] { Tipo("v", "Limpieza", "Volumen", "l"), Tipo("m", "Polvo", "Masa", "kg") };
        var reservas = new Dictionary<string, decimal?> { ["a"] = 500, ["b"] = 0, ["c"] = 100 };
        var grupos = InventarioAgrupadoViewModel.Crear(items, tipos, reservas, items);
        var volumen = Assert.Single(grupos.Where(x => x.Nombre == "Limpieza"));
        Assert.Equal(3.5m, volumen.TotalFisico);
        Assert.Equal("l", volumen.UnidadResumen);
        Assert.Equal(2, volumen.Productos.Count);
        Assert.Equal(500m, volumen.Productos[0].Reservado);
        Assert.Equal(1000m, volumen.Productos[0].Disponible);
        Assert.Equal(0.5m, Assert.Single(grupos.Where(x => x.Nombre == "Polvo")).TotalFisico);
    }

    [Fact]
    public void CabeceraUsaTodosLosProductosAunqueDetalleEstePaginado()
    {
        var todos = new[] { Item("a", "v", "Limpieza", "ml", 1000), Item("b", "v", "Limpieza", "l", 2) };
        var grupo = Assert.Single(InventarioAgrupadoViewModel.Crear(todos, new[] { Tipo("v", "Limpieza", "Volumen", "l") }, new Dictionary<string, decimal?> { ["a"] = 0, ["b"] = 0 }, todos.Take(1)));
        Assert.Equal(3m, grupo.TotalFisico);
        Assert.Single(grupo.Productos);
    }

    [Fact]
    public void ControladorFiltraDetallePeroTotalIncluyeTodosInclusoInactivosYPaginas()
    {
        var todos = Enumerable.Range(0, 23).Select(i => Item($"producto{i:00}", "v", "Limpieza", "ml", 1000)).ToList();
        todos[22].Estado = "Inactivo";
        todos[0].StockMinimo = 1000;
        var tipo = new[] { Tipo("v", "Limpieza", "Volumen", "l") };
        var reservas = todos.ToDictionary(x => x.Id, _ => (decimal?)0);
        var consulta = Firebase.Controllers.InsumosController.ProyectarInventario(todos, tipo, reservas, "producto0", "v", false, 1);
        Assert.Equal(1, consulta.TotalPages);
        Assert.Equal(10, consulta.Pagina.Count);
        Assert.Equal(23m, Assert.Single(consulta.Grupos).TotalFisico);
        var criticos = Firebase.Controllers.InsumosController.ProyectarInventario(todos, tipo, reservas, null, "Limpieza", true, 1);
        Assert.Single(criticos.Pagina);
        Assert.Equal(23m, Assert.Single(criticos.Grupos).TotalFisico);
        var pagina = Firebase.Controllers.InsumosController.ProyectarInventario(todos, tipo, reservas, null, null, false, 2);
        Assert.Equal(1, pagina.TotalPages);
        Assert.Equal(23, pagina.Pagina.Count);
        Assert.Equal(23m, Assert.Single(pagina.Grupos).TotalFisico);
        Assert.Equal(23, Assert.Single(pagina.Grupos).Productos.Count);
        Assert.Equal("producto20", pagina.Pagina[20].Nombre);
        Assert.Empty(Firebase.Controllers.InsumosController.ProyectarInventario(todos, tipo, reservas, "inexistente", null, false, 1).Grupos);
    }

    [Fact]
    public void LegacySoloUsaMetadataPorNombreUnivocaYCompatible()
    {
        var tipos = new[] { Tipo("v", "Limpieza", "Volumen", "l") };
        var a = Item("a", "", "Limpieza", "ml", 1000);
        var b = Item("b", "", "Limpieza", "l", 2);
        var invalido = Item("c", "faltante", "Limpieza", "l", 1);
        var grupos = InventarioAgrupadoViewModel.Crear(new[] { a, b, invalido }, tipos, new Dictionary<string, decimal?>(), new[] { a, b, invalido });
        Assert.Equal(3m, grupos[0].TotalFisico);
        Assert.Equal("l", grupos[0].UnidadResumen);
        Assert.Null(grupos[1].TotalFisico);
        var ambiguo = InventarioAgrupadoViewModel.Crear(new[] { a }, new[] { tipos[0], Tipo("otro", "Limpieza", "Masa", "kg") }, new Dictionary<string, decimal?>(), new[] { a });
        Assert.Null(Assert.Single(ambiguo).TotalFisico);
        b.UnidadMedida = "kg";
        Assert.Null(Assert.Single(InventarioAgrupadoViewModel.Crear(new[] { a, b }, tipos, new Dictionary<string, decimal?>(), new[] { a, b })).TotalFisico);
    }

    [Fact]
    public void LegacyYModernoConTipoInequivocoCompartenGrupoYTotal()
    {
        var items = new[] { Item("antiguo", "", "Limpieza", "ml", 1500), Item("nuevo", "v", "Limpieza", "l", 2) };
        var grupos = InventarioAgrupadoViewModel.Crear(items, new[] { Tipo("v", "Limpieza", "Volumen", "l") },
            new Dictionary<string, decimal?> { ["antiguo"] = 0, ["nuevo"] = 0 }, items);
        var grupo = Assert.Single(grupos);
        Assert.Equal(3.5m, grupo.TotalFisico);
        Assert.Equal("l", grupo.UnidadResumen);
        Assert.Equal(new[] { "antiguo", "nuevo" }, grupo.Productos.Select(x => x.Insumo.Id).OrderBy(x => x).ToArray());
        Assert.Equal("", items[0].TipoId);
    }

    [Fact]
    public void FiltroPorIdIncluyeLegacyConTipoInequivoco()
    {
        var items = new List<Insumo> { Item("antiguo", "", "Limpieza", "ml", 1500), Item("nuevo", "v", "Limpieza", "l", 2) };
        var consulta = Firebase.Controllers.InsumosController.ProyectarInventario(items,
            new[] { Tipo("v", "Limpieza", "Volumen", "l") }, new Dictionary<string, decimal?>(), null, "v", false, 1);
        Assert.Equal(new[] { "antiguo", "nuevo" }, consulta.Pagina.Select(x => x.Id).OrderBy(x => x).ToArray());
        var grupo = Assert.Single(consulta.Grupos);
        Assert.Equal(3.5m, grupo.TotalFisico);
        Assert.Equal(2, grupo.Productos.Count);
        Assert.Equal("", items[0].TipoId);
    }

    [Fact]
    public void FiltroNombreVigenteIncluyeProductoConIdYNombreHistorico()
    {
        var items = new List<Insumo> { Item("moderno", "v", "Nombre anterior", "l", 2), Item("legacy", "", "Limpieza", "ml", 1000) };
        var consulta = Firebase.Controllers.InsumosController.ProyectarInventario(items,
            new[] { Tipo("v", "Limpieza", "Volumen", "l") }, new Dictionary<string, decimal?>(), null, "Limpieza", false, 1);
        Assert.Equal(new[] { "legacy", "moderno" }, consulta.Pagina.Select(x => x.Id).OrderBy(x => x).ToArray());
        var grupo = Assert.Single(consulta.Grupos);
        Assert.Equal(3m, grupo.TotalFisico);
        Assert.Equal(2, grupo.Productos.Count);
        Assert.Equal("Nombre anterior", items[0].Tipo);
    }

    [Fact]
    public void FiltroNombreVigenteNoIncluyeIdDesconocidoConNombreCoincidente()
    {
        var items = new List<Insumo> { Item("valido", "v", "Limpieza", "l", 2), Item("desconocido", "faltante", "Limpieza", "l", 7) };
        var consulta = Firebase.Controllers.InsumosController.ProyectarInventario(items,
            new[] { Tipo("v", "Limpieza", "Volumen", "l") }, new Dictionary<string, decimal?>(), null, "Limpieza", false, 1);
        Assert.Equal("valido", Assert.Single(consulta.Pagina).Id);
        var grupo = Assert.Single(consulta.Grupos);
        Assert.Equal(2m, grupo.TotalFisico);
        Assert.Equal("valido", Assert.Single(grupo.Productos).Insumo.Id);
        Assert.Equal("faltante", items[1].TipoId);
    }

    [Fact]
    public void NombreAmbiguoNoVinculaLegacyConNingunoDeLosTipos()
    {
        var items = new[] { Item("antiguo", "", "Igual", "unidad", 1), Item("a", "1", "Igual", "unidad", 2), Item("b", "2", "Igual", "unidad", 3) };
        var tipos = new[] { Tipo("1", "Igual", "Unidades", "unidad"), Tipo("2", "Igual", "Unidades", "unidad") };
        var grupos = InventarioAgrupadoViewModel.Crear(items, tipos, new Dictionary<string, decimal?>(), items);
        Assert.Equal(3, grupos.Count);
        var legacy = Assert.Single(grupos, g => g.Productos.Any(p => p.Insumo.Id == "antiguo"));
        Assert.Single(legacy.Productos);
        Assert.Null(legacy.TotalFisico);
        Assert.Equal("", items[0].TipoId);
        var filtrados = Firebase.Controllers.InsumosController.ProyectarInventario(items.ToList(), tipos,
            new Dictionary<string, decimal?>(), null, "1", false, 1);
        Assert.Equal("a", Assert.Single(filtrados.Pagina).Id);
    }

    [Fact]
    public void FiltroNombreAmbiguoConservaGruposSeparadosSinReclasificarIdsExplicitos()
    {
        var tipos = new[] { Tipo("1", "Igual", "Unidades", "unidad"), Tipo("2", "Igual", "Unidades", "unidad"), Tipo("3", "Otro", "Unidades", "unidad") };
        var items = new List<Insumo> {
            Item("a", "1", "Igual", "unidad", 2), Item("b", "2", "Igual", "unidad", 3),
            Item("legacy", "", "Igual", "unidad", 1), Item("desconocido", "faltante", "Igual", "unidad", 7),
            Item("conflicto", "3", "Igual", "unidad", 9)
        };
        var consulta = Firebase.Controllers.InsumosController.ProyectarInventario(items, tipos,
            new Dictionary<string, decimal?>(), null, "Igual", false, 1);
        Assert.Equal(new[] { "a", "b", "legacy" }, consulta.Pagina.Select(x => x.Id).OrderBy(x => x).ToArray());
        Assert.Equal(3, consulta.Grupos.Count);
        Assert.Equal(2m, Assert.Single(consulta.Grupos, g => g.Productos.Any(p => p.Insumo.Id == "a")).TotalFisico);
        Assert.Equal(3m, Assert.Single(consulta.Grupos, g => g.Productos.Any(p => p.Insumo.Id == "b")).TotalFisico);
        Assert.Null(Assert.Single(consulta.Grupos, g => g.Productos.Any(p => p.Insumo.Id == "legacy")).TotalFisico);
        Assert.Equal("faltante", items[3].TipoId);
        Assert.Equal("3", items[4].TipoId);
    }

    [Fact]
    public void DatosIncompatiblesOReservaInconsistenteNoInventanTotalesNiDisponibilidad()
    {
        var items = new[] { Item("a", "v", "Limpieza", "kg", 2), Item("b", "v", "Limpieza", "l", 1), Item("c", "", "Antiguo", "unidad", 2) };
        var grupos = InventarioAgrupadoViewModel.Crear(items, new[] { Tipo("v", "Limpieza", "Volumen", "l") }, new Dictionary<string, decimal?> { ["a"] = null, ["b"] = 2, ["c"] = 0 }, items);
        var grupo = Assert.Single(grupos.Where(x => x.Nombre == "Limpieza"));
        Assert.Null(grupo.TotalFisico);
        Assert.Null(grupo.Productos[0].Reservado);
        Assert.Null(grupo.Productos[0].Disponible);
        Assert.Equal(2m, grupo.Productos[1].Reservado);
        Assert.Null(grupo.Productos[1].Disponible);
        Assert.Null(Assert.Single(grupos.Where(x => x.Nombre == "Antiguo")).TotalFisico);
    }

    [Fact]
    public void TiposDistintosConIgualNombreNoSeMezclanYConversorPrevioSePreserva()
    {
        var items = new[] { Item("a", "1", "Igual", "unidad", 2), Item("b", "2", "Igual", "unidad", 3) };
        var tipos = new[] { Tipo("1", "Igual", "Unidades", "unidad"), Tipo("2", "Igual", "Unidades", "unidad") };
        var grupos = InventarioAgrupadoViewModel.Crear(items, tipos, new Dictionary<string, decimal?> { ["a"] = 0, ["b"] = 0 }, items);
        Assert.Equal(2, grupos.Count);
        Assert.Equal(new decimal?[] { 2, 3 }, grupos.Select(x => x.TotalFisico).ToArray());
        Assert.Equal(1m, new Firebase.Services.ConversionUnidadesService().Convertir(1000, "ml", "l"));
    }
}

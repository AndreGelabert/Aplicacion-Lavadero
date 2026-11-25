using Firebase.Models;
using Firebase.Services;

namespace Firebase.Tests.Services;

public class TurnoInsumosForecastTests
{
    private static Servicio Servicio(string id) => new() { Id = id, Nombre = id, Tipo = "x", TipoVehiculo = "x", Descripcion = "x", Estado = "Activo" };
    private static RecetaInsumo Producto(string id, decimal dosis, string unidad) => new() { InsumoId = id, DosisTeorica = dosis, UnidadMedida = unidad };

    [Fact]
    public void UsaEtapasCuandoExistenYRecetaDeServicioEnOtroCaso()
    {
        var a = Servicio("a"); a.Receta.Add(Producto("ignorado", 9, "l"));
        a.Etapas.Add(new Etapa { Id = "e1", Receta = [Producto("i1", 1, "l")] });
        a.Etapas.Add(new Etapa { Id = "e2", Receta = [Producto("i2", 2, "ml")] });
        var b = Servicio("b"); b.Receta.Add(Producto("i3", 3, "kg"));
        var result = TurnoInsumosForecast.Proyectar([a, b]);
        Assert.Equal(new[] { "i1", "i2", "i3" }, result.Select(x => x.InsumoId));
        Assert.Equal(new[] { "a", "a", "b" }, result.Select(x => x.ServicioId));
        Assert.Equal(new string?[] { "e1", "e2", null }, result.Select(x => x.EtapaId));
    }

    [Fact]
    public void ConservaRepeticionesUnidadesYTipoSinSeleccionNiMutacion()
    {
        var a = Servicio("a"); var b = Servicio("b");
        var tipo = new RecetaInsumo { Modalidad = "Tipo", TipoId = "t", NombreTipo = "Jabon", Categoria = "Volumen", UnidadMedida = "ml", DosisTeorica = 250m };
        a.Receta.Add(Producto("i", 1.25m, "l")); a.Receta.Add(tipo);
        b.Receta.Add(Producto("i", 500m, "ml")); b.Receta.Add(Producto("i", 2m, "l"));
        var result = TurnoInsumosForecast.Proyectar([a, b]);
        Assert.Equal(4, result.Count);
        Assert.Equal(new[] { "l", "ml", "ml", "l" }, result.Select(x => x.UnidadMedida));
        Assert.Equal(new[] { 1.25m, 250m, 500m, 2m }, result.Select(x => x.DosisTeorica));
        Assert.Equal(new[] { "i", "", "i", "i" }, result.Select(x => x.InsumoId));
        Assert.Equal("Tipo", result[1].Modalidad); Assert.Equal("t", result[1].TipoId);
        Assert.Equal("Jabon", result[1].NombreTipo); Assert.Equal("Volumen", result[1].Categoria);
        Assert.Equal("", tipo.InsumoId); Assert.Equal(1.25m, a.Receta[0].DosisTeorica);
        tipo.NombreTipo = "cambiado"; a.Receta[0].InsumoId = "otro";
        Assert.Equal("Jabon", result[1].NombreTipo); Assert.Equal("i", result[0].InsumoId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RechazaDosisNoPositiva(int dosis)
    {
        var servicio = Servicio("a"); servicio.Receta.Add(Producto("i", dosis, "l"));
        Assert.Throws<ArgumentException>(() => TurnoInsumosForecast.Proyectar([servicio]));
        Assert.Equal(dosis, servicio.Receta[0].DosisTeorica);
    }
}

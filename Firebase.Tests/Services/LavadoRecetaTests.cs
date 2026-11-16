using System.Runtime.CompilerServices;
using Firebase.Models;
using Firebase.Services;
using Xunit;

namespace Firebase.Tests.Services;

public class LavadoRecetaTests
{
    [Fact]
    public void CopiaRecetaTipoEnServicioYEtapaSinPerderSnapshot()
    {
        var lavado = (LavadoService)RuntimeHelpers.GetUninitializedObject(typeof(LavadoService));
        var tipo = new RecetaInsumo { Modalidad = "Tipo", TipoId = "tipo", NombreTipo = "Jabon", Categoria = "Volumen", UnidadMedida = "ml", DosisTeorica = 250m };
        var servicio = new Servicio { Id = "s", Nombre = "Lavado", Tipo = "Lavado", TipoVehiculo = "Auto", Descripcion = "Lavado", Estado = "Activo", Receta = new() { tipo } };
        var sinEtapas = lavado.CrearServicioEnLavado(servicio, 0, null, null);
        Verificar(Assert.Single(sinEtapas.Receta));
        servicio.Etapas.Add(new Etapa { Id = "e", Nombre = "Etapa", Receta = new() { tipo } });
        var conEtapas = lavado.CrearServicioEnLavado(servicio, 0, null, null);
        var copia = Assert.Single(Assert.Single(conEtapas.Etapas).Receta);
        Verificar(copia);
        tipo.NombreTipo = "Otro";
        Assert.Equal("Jabon", copia.NombreTipo);
    }

    [Fact]
    public void CopiaRecetaProductoLegacy()
    {
        var lavado = (LavadoService)RuntimeHelpers.GetUninitializedObject(typeof(LavadoService));
        var servicio = new Servicio { Id = "s", Nombre = "Lavado", Tipo = "Lavado", TipoVehiculo = "Auto", Descripcion = "Lavado", Estado = "Activo", Receta = new() { new RecetaInsumo { InsumoId = "i", NombreInsumo = "Producto", UnidadMedida = "l", DosisTeorica = 2m } } };
        var item = Assert.Single(lavado.CrearServicioEnLavado(servicio, 0, null, null).Receta);
        Assert.Equal("Producto", item.Modalidad);
        Assert.Equal("i", item.InsumoId);
        Assert.Equal("Producto", item.NombreInsumo);
        Assert.Equal("l", item.UnidadMedida);
        Assert.Equal(2m, item.DosisTeorica);
    }

    private static void Verificar(RecetaInsumo item)
    {
        Assert.Equal("Tipo", item.Modalidad);
        Assert.Equal("tipo", item.TipoId);
        Assert.Equal("Jabon", item.NombreTipo);
        Assert.Equal("Volumen", item.Categoria);
        Assert.Equal("ml", item.UnidadMedida);
        Assert.Equal(250m, item.DosisTeorica);
    }
}

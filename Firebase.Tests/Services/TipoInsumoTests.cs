using Firebase.Models;
using Xunit;

namespace Firebase.Tests.Services;

public class TipoInsumoTests
{
    [Fact]
    public void TipoInsumo_CategoriasValidas_AceptaSoloVolumenMasaUnidades()
    {
        Assert.Contains(TipoInsumo.Categorias.Volumen, TipoInsumo.Categorias.Todas);
        Assert.Contains(TipoInsumo.Categorias.Masa, TipoInsumo.Categorias.Todas);
        Assert.Contains(TipoInsumo.Categorias.Unidades, TipoInsumo.Categorias.Todas);
        Assert.Equal(3, TipoInsumo.Categorias.Todas.Count);

        Assert.True(TipoInsumo.EsCategoriaValida("Volumen"));
        Assert.True(TipoInsumo.EsCategoriaValida("volumen"));
        Assert.True(TipoInsumo.EsCategoriaValida("Masa"));
        Assert.True(TipoInsumo.EsCategoriaValida("MASA"));
        Assert.True(TipoInsumo.EsCategoriaValida("Unidades"));
        Assert.True(TipoInsumo.EsCategoriaValida("unidades"));

        Assert.False(TipoInsumo.EsCategoriaValida("Longitud"));
        Assert.False(TipoInsumo.EsCategoriaValida("Tiempo"));
        Assert.False(TipoInsumo.EsCategoriaValida(""));
        Assert.False(TipoInsumo.EsCategoriaValida("   "));
        Assert.False(TipoInsumo.EsCategoriaValida(null));
    }

    [Fact]
    public void TipoInsumo_EsUnidadValidaParaCategoria_ValidaMagnitudesCorrectamente()
    {
        // Volumen
        Assert.True(TipoInsumo.EsUnidadValidaParaCategoria("Volumen", "ml"));
        Assert.True(TipoInsumo.EsUnidadValidaParaCategoria("Volumen", "l"));
        Assert.True(TipoInsumo.EsUnidadValidaParaCategoria("Volumen", "litro"));
        Assert.True(TipoInsumo.EsUnidadValidaParaCategoria("Volumen", "litros"));
        Assert.True(TipoInsumo.EsUnidadValidaParaCategoria("Volumen", "mililitros"));
        Assert.True(TipoInsumo.EsUnidadValidaParaCategoria("Volumen", "cm3"));
        Assert.False(TipoInsumo.EsUnidadValidaParaCategoria("Volumen", "kg"));
        Assert.False(TipoInsumo.EsUnidadValidaParaCategoria("Volumen", "g"));
        Assert.False(TipoInsumo.EsUnidadValidaParaCategoria("Volumen", "unidad"));

        // Masa
        Assert.True(TipoInsumo.EsUnidadValidaParaCategoria("Masa", "g"));
        Assert.True(TipoInsumo.EsUnidadValidaParaCategoria("Masa", "kg"));
        Assert.True(TipoInsumo.EsUnidadValidaParaCategoria("Masa", "mg"));
        Assert.True(TipoInsumo.EsUnidadValidaParaCategoria("Masa", "gramos"));
        Assert.True(TipoInsumo.EsUnidadValidaParaCategoria("Masa", "kilogramo"));
        Assert.False(TipoInsumo.EsUnidadValidaParaCategoria("Masa", "ml"));
        Assert.False(TipoInsumo.EsUnidadValidaParaCategoria("Masa", "l"));
        Assert.False(TipoInsumo.EsUnidadValidaParaCategoria("Masa", "unidad"));

        // Unidades (conteo)
        Assert.True(TipoInsumo.EsUnidadValidaParaCategoria("Unidades", "unidad"));
        Assert.True(TipoInsumo.EsUnidadValidaParaCategoria("Unidades", "unidades"));
        Assert.True(TipoInsumo.EsUnidadValidaParaCategoria("Unidades", "u"));
        Assert.False(TipoInsumo.EsUnidadValidaParaCategoria("Unidades", "ml"));
        Assert.False(TipoInsumo.EsUnidadValidaParaCategoria("Unidades", "kg"));

        // Inválidos
        Assert.False(TipoInsumo.EsUnidadValidaParaCategoria(null, "l"));
        Assert.False(TipoInsumo.EsUnidadValidaParaCategoria("Invalida", "l"));
        Assert.False(TipoInsumo.EsUnidadValidaParaCategoria("Volumen", null));
        Assert.False(TipoInsumo.EsUnidadValidaParaCategoria("Volumen", ""));
    }

    [Fact]
    public void TipoInsumo_TipoHerramienta_ConstantesYDefinicionFija()
    {
        Assert.Equal("Herramienta", TipoInsumo.TipoHerramienta);
        Assert.Equal("unidad", TipoInsumo.UnidadHerramienta);
        Assert.Equal("Unidades", TipoInsumo.Categorias.Unidades);
    }

    [Fact]
    public void AuditCampos_TipoInsumo_CapturaYAllowlistConcordantes()
    {
        var allowlist = AuditCampos.TipoInsumo;
        Assert.Contains("Nombre", allowlist);
        Assert.Contains("Categoria", allowlist);
        Assert.Contains("UnidadResumen", allowlist);
        Assert.Contains("EsProtegido", allowlist);
        Assert.Contains("Estado", allowlist);
        Assert.Equal(5, allowlist.Count);

        // Sin datos sensibles
        Assert.DoesNotContain("Password", allowlist);
        Assert.DoesNotContain("Token", allowlist);
        Assert.DoesNotContain("Secret", allowlist);

        var tipo = new TipoInsumo
        {
            Id = "tipo-1",
            Nombre = "Químico Líquido",
            Categoria = "Volumen",
            UnidadResumen = "l",
            EsProtegido = false,
            Estado = "Activo"
        };

        var snapshot = AuditCampos.CapturarTipoInsumo(tipo);
        Assert.Equal("Químico Líquido", snapshot["Nombre"]);
        Assert.Equal("Volumen", snapshot["Categoria"]);
        Assert.Equal("l", snapshot["UnidadResumen"]);
        Assert.Equal("False", snapshot["EsProtegido"]);
        Assert.Equal("Activo", snapshot["Estado"]);
        Assert.Equal(5, snapshot.Count);

        var vacio = AuditCampos.CapturarTipoInsumo(null);
        Assert.Empty(vacio);

        var modificado = new TipoInsumo
        {
            Id = "tipo-1",
            Nombre = "Químico Líquido Modificado",
            Categoria = "Volumen",
            UnidadResumen = "l",
            EsProtegido = false,
            Estado = "Activo"
        };
        var snapshotMod = AuditCampos.CapturarTipoInsumo(modificado);
        Assert.True(AuditCampos.HayCambios(snapshot, snapshotMod));
        Assert.False(AuditCampos.HayCambios(snapshot, snapshot));
    }
}

using System.ComponentModel.DataAnnotations;
using Google.Cloud.Firestore;

namespace Firebase.Models;

/// <summary>
/// Modelo de dominio que representa un tipo configurable de insumo (S24).
/// Define la clasificación del insumo, su categoría de magnitud física
/// y la unidad resumen utilizada para consolidar reportes.
/// </summary>
[FirestoreData]
public sealed class TipoInsumo
{
    public const string TipoHerramienta = "Herramienta";
    public const string UnidadHerramienta = "unidad";

    public static class Categorias
    {
        public const string Volumen = "Volumen";
        public const string Masa = "Masa";
        public const string Unidades = "Unidades";

        public static readonly IReadOnlyList<string> Todas = new[] { Volumen, Masa, Unidades };
    }

    private static readonly HashSet<string> UnidadesVolumen = new(StringComparer.OrdinalIgnoreCase)
    {
        "ml", "mililitro", "mililitros", "l", "lt", "litro", "litros", "cm3", "cc"
    };

    private static readonly HashSet<string> UnidadesMasa = new(StringComparer.OrdinalIgnoreCase)
    {
        "mg", "miligramo", "miligramos", "g", "gr", "gramo", "gramos", "kg", "kilo", "kilogramo", "kilogramos"
    };

    private static readonly HashSet<string> UnidadesConteo = new(StringComparer.OrdinalIgnoreCase)
    {
        "unidad", "unidades", "u"
    };

    [FirestoreProperty]
    public string Id { get; set; } = string.Empty;

    [FirestoreProperty, Required(ErrorMessage = "El nombre del tipo de insumo es obligatorio"), StringLength(120)]
    public string Nombre { get; set; } = string.Empty;

    [FirestoreProperty, Required(ErrorMessage = "La categoría de magnitud es obligatoria")]
    public string Categoria { get; set; } = Categorias.Unidades;

    [FirestoreProperty, Required(ErrorMessage = "La unidad resumen es obligatoria")]
    public string UnidadResumen { get; set; } = UnidadHerramienta;

    [FirestoreProperty]
    public bool EsProtegido { get; set; }

    [FirestoreProperty]
    public string Estado { get; set; } = "Activo";

    public static bool EsCategoriaValida(string? categoria)
    {
        if (string.IsNullOrWhiteSpace(categoria)) return false;
        var cat = categoria.Trim();
        return Categorias.Todas.Any(c => string.Equals(c, cat, StringComparison.OrdinalIgnoreCase));
    }

    public static string NormalizarCategoria(string? categoria)
    {
        if (string.IsNullOrWhiteSpace(categoria)) return string.Empty;
        var cat = categoria.Trim();
        if (string.Equals(cat, Categorias.Volumen, StringComparison.OrdinalIgnoreCase)) return Categorias.Volumen;
        if (string.Equals(cat, Categorias.Masa, StringComparison.OrdinalIgnoreCase)) return Categorias.Masa;
        if (string.Equals(cat, Categorias.Unidades, StringComparison.OrdinalIgnoreCase)) return Categorias.Unidades;
        return cat;
    }

    public static bool EsUnidadValidaParaCategoria(string? categoria, string? unidad)
    {
        if (string.IsNullOrWhiteSpace(categoria) || string.IsNullOrWhiteSpace(unidad)) return false;
        var catNorm = NormalizarCategoria(categoria);
        var u = unidad.Trim();

        return catNorm switch
        {
            Categorias.Volumen => UnidadesVolumen.Contains(u),
            Categorias.Masa => UnidadesMasa.Contains(u),
            Categorias.Unidades => UnidadesConteo.Contains(u),
            _ => false
        };
    }

    /// <summary>
    /// Identifica la categoría de magnitud física a la que pertenece una unidad compatible de insumo.
    /// Retorna null si la unidad no es reconocida o es nula/vacía.
    /// </summary>
    public static string? ObtenerCategoriaDeUnidad(string? unidad)
    {
        if (string.IsNullOrWhiteSpace(unidad)) return null;
        var u = unidad.Trim();

        if (UnidadesVolumen.Contains(u)) return Categorias.Volumen;
        if (UnidadesMasa.Contains(u)) return Categorias.Masa;
        if (UnidadesConteo.Contains(u)) return Categorias.Unidades;
        return null;
    }

    /// <summary>
    /// Determina si una unidad de medida representa cantidades discretas (conteo entero),
    /// como "unidad", "unidades", "u".
    /// </summary>
    public static bool EsUnidadEntera(string? unidad)
    {
        if (string.IsNullOrWhiteSpace(unidad)) return false;
        var u = unidad.Trim();
        return UnidadesConteo.Contains(u);
    }

    /// <summary>
    /// Determina si un valor decimal posee parte fraccionaria (no es entero).
    /// </summary>
    public static bool TieneDecimales(decimal valor) => decimal.Remainder(valor, 1m) != 0m;

    /// <summary>
    /// Determina si dos unidades de medida pertenecen a la misma magnitud física (compatibles)
    /// según las unidades definidas en TipoInsumo (S24, S25).
    /// </summary>
    public static bool SonUnidadesCompatibles(string? unidadOrigen, string? unidadDestino)
    {
        var catOrigen = ObtenerCategoriaDeUnidad(unidadOrigen);
        if (catOrigen == null) return false;
        var catDestino = ObtenerCategoriaDeUnidad(unidadDestino);
        if (catDestino == null) return false;

        return string.Equals(catOrigen, catDestino, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Retorna el conjunto de unidades permitidas para una categoría de magnitud.
    /// </summary>
    public static IReadOnlySet<string> ObtenerUnidadesPermitidas(string? categoria)
    {
        var catNorm = NormalizarCategoria(categoria);
        return catNorm switch
        {
            Categorias.Volumen => UnidadesVolumen,
            Categorias.Masa => UnidadesMasa,
            Categorias.Unidades => UnidadesConteo,
            _ => new HashSet<string>()
        };
    }
}

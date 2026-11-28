using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Firebase.Utils;

/// <summary>
/// Normalizes catalog type names for display and diacritic-insensitive uniqueness checks.
/// </summary>
public static partial class NombreCatalogoNormalizer
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiWhitespaceRegex();

    /// <summary>
    /// Trims leading/trailing whitespace and collapses internal whitespace runs to a single space.
    /// </summary>
    public static string LimpiarNombre(string? nombre)
    {
        var trimmed = (nombre ?? string.Empty).Trim();
        return string.IsNullOrEmpty(trimmed) ? string.Empty : MultiWhitespaceRegex().Replace(trimmed, " ");
    }

    /// <summary>
    /// Produces a canonical key without accents/diacritics, whitespace, or case differences
    /// to prevent duplicate catalog entries.
    /// </summary>
    public static string ClaveUnicidad(string? nombre)
    {
        var decomposed = LimpiarNombre(nombre).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsWhiteSpace(ch))
                continue;

            builder.Append(char.ToLowerInvariant(ch));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Returns true when two catalog names collide under diacritic-, whitespace-, and case-insensitive normalization.
    /// </summary>
    public static bool SonEquivalentes(string? a, string? b)
    {
        var claveA = ClaveUnicidad(a);
        return !string.IsNullOrEmpty(claveA) && string.Equals(claveA, ClaveUnicidad(b), StringComparison.Ordinal);
    }

    /// <summary>
    /// Normalizes a catalog status, defaulting legacy missing/empty values to "Activo".
    /// </summary>
    public static string NormalizarEstado(string? estado) =>
        string.Equals(estado, "Inactivo", StringComparison.OrdinalIgnoreCase) ? "Inactivo" : "Activo";
}

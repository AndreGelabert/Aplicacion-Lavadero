namespace Firebase.Services;

internal static class AuditTextMatcher
{
    internal static bool Matches(string texto, string termino)
    {
        if (string.IsNullOrWhiteSpace(texto) || string.IsNullOrWhiteSpace(termino))
            return false;

        if (texto.Equals(termino, StringComparison.OrdinalIgnoreCase))
            return true;

        var textoNormalizado = NormalizarTexto(texto);
        var terminoNormalizado = NormalizarTexto(termino);

        if (textoNormalizado.Equals(terminoNormalizado, StringComparison.OrdinalIgnoreCase))
            return true;

        if (textoNormalizado.Contains(terminoNormalizado, StringComparison.OrdinalIgnoreCase))
            return true;

        if (termino.Contains(' '))
        {
            var palabrasBusqueda = terminoNormalizado.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var palabrasTexto = textoNormalizado.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            return palabrasBusqueda.All(palabraBusqueda =>
                palabrasTexto.Any(palabraTexto =>
                    palabraTexto.Contains(palabraBusqueda, StringComparison.OrdinalIgnoreCase) ||
                    palabraBusqueda.Contains(palabraTexto, StringComparison.OrdinalIgnoreCase)));
        }

        return false;
    }

    private static string NormalizarTexto(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        var textoNormalizado = texto.Normalize(System.Text.NormalizationForm.FormD);
        var resultado = new System.Text.StringBuilder(textoNormalizado.Length);
        foreach (var c in textoNormalizado)
        {
            var categoriaUnicode = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (categoriaUnicode != System.Globalization.UnicodeCategory.NonSpacingMark)
                resultado.Append(c);
        }

        return resultado.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }
}

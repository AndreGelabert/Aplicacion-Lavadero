using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Firebase.Utils;

/// <summary>
/// Provee validación, normalización y formateo para CUIT / CUIL de la República Argentina
/// siguiendo el algoritmo de Módulo 11 oficial de ARCA (ex-AFIP).
/// </summary>
public static class CuitHelper
{
    private static readonly int[] Multiplicadores = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];
    private static readonly HashSet<string> PrefijosValidos = ["20", "23", "24", "27", "30", "33", "34", "50", "51", "55"];

    /// <summary>
    /// Remueve guiones, espacios y puntos, retornando solo caracteres limpios.
    /// </summary>
    public static string Limpiar(string? cuit)
    {
        if (string.IsNullOrWhiteSpace(cuit)) return string.Empty;
        return Regex.Replace(cuit, @"[-\s\.]", "").Trim();
    }

    /// <summary>
    /// Formatea un CUIT limpio o con formato a la convención canónica argentina XX-XXXXXXXX-X.
    /// </summary>
    public static string Formatear(string? cuit)
    {
        var limpio = Limpiar(cuit);
        if (limpio.Length != 11) return cuit?.Trim() ?? string.Empty;
        return $"{limpio[..2]}-{limpio.Substring(2, 8)}-{limpio[10]}";
    }

    /// <summary>
    /// Valida longitud, prefijo oficial argentino y dígito verificador calculado por Módulo 11.
    /// </summary>
    public static bool EsValido(string? cuit, out string mensajeError)
    {
        mensajeError = string.Empty;

        if (string.IsNullOrWhiteSpace(cuit))
        {
            mensajeError = "La identificación fiscal (CUIT) es obligatoria.";
            return false;
        }

        var trimmed = cuit.Trim();

        if (trimmed.Contains('-') && !Regex.IsMatch(trimmed, @"^\d{2}-\d{8}-\d{1}$"))
        {
            mensajeError = "El CUIT con guiones debe respetar el formato XX-XXXXXXXX-X.";
            return false;
        }

        var limpio = Limpiar(trimmed);

        if (limpio.Length != 11 || !limpio.All(char.IsDigit))
        {
            mensajeError = "El CUIT debe contener exactamente 11 dígitos numéricos.";
            return false;
        }

        var prefijo = limpio[..2];
        if (!PrefijosValidos.Contains(prefijo))
        {
            mensajeError = $"El prefijo '{prefijo}' no corresponde a un CUIT/CUIL argentino válido.";
            return false;
        }

        var suma = 0;
        for (var i = 0; i < 10; i++)
        {
            suma += (limpio[i] - '0') * Multiplicadores[i];
        }

        var resto = suma % 11;
        var esperado = 11 - resto;
        if (esperado == 11) esperado = 0;
        else if (esperado == 10) esperado = 9;

        var digitoVerificador = limpio[10] - '0';
        if (digitoVerificador != esperado)
        {
            mensajeError = "El CUIT ingresado no es válido (dígito verificador incorrecto).";
            return false;
        }

        return true;
    }
}

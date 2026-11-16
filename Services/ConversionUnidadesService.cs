using Firebase.Models;

namespace Firebase.Services;

/// <summary>
/// Servicio de conversión matemática básica entre unidades compatibles de la misma magnitud física (T12, S25, D6).
/// Exclusivamente conversiones entre unidades compatibles ya definidas por TipoInsumo (Volumen, Masa, Unidades).
/// Quedan explícitamente excluidos envases, recetas de dilución, conversiones inter-magnitud y selección automática.
///
/// Diseño técnico de precisión y serialización (D6, S25):
/// - Dado que todos los factores de conversión entre unidades compatibles de la misma categoría en TipoInsumo
///   son potencias enteras de 10 (10^3, 10^6, 1), las operaciones en decimal en memoria preservan exactitud matemática
///   sin introducir errores de redondeo binario ni requerir políticas de truncamiento forzadas.
/// - Cuando un valor se almacena en Firestore mediante DecimalConverter, la persistencia se realiza como double (IEEE 754),
///   cuya precisión está acotada a ~15-17 dígitos significativos (53 bits de mantisa). No se afirma exactitud persistida
///   más allá de dicho límite sin prueba específica.
/// </summary>
public class ConversionUnidadesService
{
    public sealed record ResultadoConversion(
        bool EsExitoso,
        decimal Valor,
        string? Error = null,
        string? Categoria = null,
        string? UnidadOrigenNormalizada = null,
        string? UnidadDestinoNormalizada = null)
    {
        public static ResultadoConversion Exito(decimal valor, string categoria, string origenNorm, string destinoNorm) =>
            new(true, valor, null, categoria, origenNorm, destinoNorm);

        public static ResultadoConversion Fallo(string error) =>
            new(false, 0m, error, null, null, null);
    }

    // Factores a unidad base dentro de cada categoría:
    // Base Volumen: "ml"
    // Base Masa: "mg"
    // Base Unidades: "unidad"
    private static readonly Dictionary<string, (string Categoria, string Normalizada, decimal FactorBase)> TablaUnidades =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Volumen (Base: ml)
            ["ml"] = (TipoInsumo.Categorias.Volumen, "ml", 1m),
            ["mililitro"] = (TipoInsumo.Categorias.Volumen, "ml", 1m),
            ["mililitros"] = (TipoInsumo.Categorias.Volumen, "ml", 1m),
            ["cm3"] = (TipoInsumo.Categorias.Volumen, "ml", 1m),
            ["cc"] = (TipoInsumo.Categorias.Volumen, "ml", 1m),
            ["l"] = (TipoInsumo.Categorias.Volumen, "l", 1000m),
            ["lt"] = (TipoInsumo.Categorias.Volumen, "l", 1000m),
            ["litro"] = (TipoInsumo.Categorias.Volumen, "l", 1000m),
            ["litros"] = (TipoInsumo.Categorias.Volumen, "l", 1000m),

            // Masa (Base: mg)
            ["mg"] = (TipoInsumo.Categorias.Masa, "mg", 1m),
            ["miligramo"] = (TipoInsumo.Categorias.Masa, "mg", 1m),
            ["miligramos"] = (TipoInsumo.Categorias.Masa, "mg", 1m),
            ["g"] = (TipoInsumo.Categorias.Masa, "g", 1000m),
            ["gr"] = (TipoInsumo.Categorias.Masa, "g", 1000m),
            ["gramo"] = (TipoInsumo.Categorias.Masa, "g", 1000m),
            ["gramos"] = (TipoInsumo.Categorias.Masa, "g", 1000m),
            ["kg"] = (TipoInsumo.Categorias.Masa, "kg", 1000000m),
            ["kilo"] = (TipoInsumo.Categorias.Masa, "kg", 1000000m),
            ["kilogramo"] = (TipoInsumo.Categorias.Masa, "kg", 1000000m),
            ["kilogramos"] = (TipoInsumo.Categorias.Masa, "kg", 1000000m),

            // Conteo / Unidades (Base: unidad)
            ["unidad"] = (TipoInsumo.Categorias.Unidades, "unidad", 1m),
            ["unidades"] = (TipoInsumo.Categorias.Unidades, "unidad", 1m),
            ["u"] = (TipoInsumo.Categorias.Unidades, "unidad", 1m)
        };

    /// <summary>
    /// Verifica si dos unidades de medida pertenecen a la misma magnitud física y son compatibles para conversión.
    /// </summary>
    public bool SonCompatibles(string? unidadOrigen, string? unidadDestino)
    {
        return TipoInsumo.SonUnidadesCompatibles(unidadOrigen, unidadDestino);
    }

    /// <summary>
    /// Retorna la categoría de magnitud física a la que pertenece la unidad especificada (Volumen, Masa, Unidades), o null si es inválida.
    /// </summary>
    public string? ObtenerCategoria(string? unidad)
    {
        return TipoInsumo.ObtenerCategoriaDeUnidad(unidad);
    }

    /// <summary>
    /// Normaliza una unidad retornando su símbolo canónico estándar (por ejemplo: "mililitros" -> "ml", "kilogramos" -> "kg").
    /// </summary>
    public string NormalizarUnidad(string? unidad)
    {
        if (string.IsNullOrWhiteSpace(unidad)) return string.Empty;
        var u = unidad.Trim();
        return TablaUnidades.TryGetValue(u, out var info) ? info.Normalizada : u;
    }

    /// <summary>
    /// Obtiene el factor multiplicativo directo de conversión entre dos unidades compatibles de la misma magnitud física.
    /// Lanza ArgumentException si las unidades son incompatibles o inválidas.
    /// </summary>
    public decimal ObtenerFactorConversion(string? unidadOrigen, string? unidadDestino)
    {
        ValidarUnidades(unidadOrigen, unidadDestino, out var infoOrigen, out var infoDestino);

        if (infoOrigen.FactorBase >= infoDestino.FactorBase)
        {
            return infoOrigen.FactorBase / infoDestino.FactorBase;
        }

        return 1m / (infoDestino.FactorBase / infoOrigen.FactorBase);
    }

    /// <summary>
    /// Realiza la conversión matemática directa entre dos unidades compatibles de la misma categoría de magnitud física.
    /// </summary>
    /// <param name="cantidad">Cantidad a convertir en unidadOrigen.</param>
    /// <param name="unidadOrigen">Unidad de medida origen.</param>
    /// <param name="unidadDestino">Unidad de medida destino.</param>
    /// <param name="decimalesRedondeo">Opcional: cantidad de decimales a redondear. Si es null, se preserva exactitud decimal.</param>
    /// <param name="modoRedondeo">Modo de redondeo cuando decimalesRedondeo no es null.</param>
    /// <returns>Valor convertido a unidadDestino.</returns>
    /// <exception cref="ArgumentException">Si las unidades son incompatibles, nulas, vacías o no reconocidas.</exception>
    /// <exception cref="OverflowException">Si la operación excede los límites representables de decimal.</exception>
    public decimal Convertir(
        decimal cantidad,
        string? unidadOrigen,
        string? unidadDestino,
        int? decimalesRedondeo = null,
        MidpointRounding modoRedondeo = MidpointRounding.ToEven)
    {
        ValidarUnidades(unidadOrigen, unidadDestino, out var infoOrigen, out var infoDestino);

        decimal resultado;

        if (infoOrigen.FactorBase == infoDestino.FactorBase)
        {
            resultado = cantidad;
        }
        else if (infoOrigen.FactorBase > infoDestino.FactorBase)
        {
            var ratio = infoOrigen.FactorBase / infoDestino.FactorBase;
            resultado = checked(cantidad * ratio);
        }
        else
        {
            var ratio = infoDestino.FactorBase / infoOrigen.FactorBase;
            resultado = checked(cantidad / ratio);
        }

        if (decimalesRedondeo.HasValue)
        {
            resultado = Math.Round(resultado, decimalesRedondeo.Value, modoRedondeo);
        }

        return resultado;
    }

    /// <summary>
    /// Intenta realizar la conversión matemática entre dos unidades compatibles. No lanza excepciones ante errores de validación u overflow.
    /// </summary>
    public bool TryConvertir(
        decimal cantidad,
        string? unidadOrigen,
        string? unidadDestino,
        out decimal resultado,
        out string? error,
        int? decimalesRedondeo = null,
        MidpointRounding modoRedondeo = MidpointRounding.ToEven)
    {
        try
        {
            resultado = Convertir(cantidad, unidadOrigen, unidadDestino, decimalesRedondeo, modoRedondeo);
            error = null;
            return true;
        }
        catch (OverflowException)
        {
            resultado = 0m;
            error = $"El valor numérico '{cantidad}' excede el límite representable al convertir de '{unidadOrigen}' a '{unidadDestino}'.";
            return false;
        }
        catch (ArgumentException ex)
        {
            resultado = 0m;
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Realiza la conversión matemática retornando un objeto ResultadoConversion estructurado.
    /// </summary>
    public ResultadoConversion ConvertirConResultado(
        decimal cantidad,
        string? unidadOrigen,
        string? unidadDestino,
        int? decimalesRedondeo = null,
        MidpointRounding modoRedondeo = MidpointRounding.ToEven)
    {
        if (TryConvertir(cantidad, unidadOrigen, unidadDestino, out var resultado, out var error, decimalesRedondeo, modoRedondeo))
        {
            var cat = ObtenerCategoria(unidadOrigen) ?? string.Empty;
            var origNorm = NormalizarUnidad(unidadOrigen);
            var destNorm = NormalizarUnidad(unidadDestino);
            return ResultadoConversion.Exito(resultado, cat, origNorm, destNorm);
        }

        return ResultadoConversion.Fallo(error ?? "Error desconocido en la conversión de unidades.");
    }

    /// <summary>
    /// Simula la serialización de Firestore (DecimalConverter) que almacena decimales como double (IEEE 754).
    /// </summary>
    public decimal SimularSerializacionFirestore(decimal valor)
    {
        return (decimal)(double)valor;
    }

    /// <summary>
    /// Determina si un valor decimal se preserva con exactitud al roundtripear por la serialización double de Firestore.
    /// Retorna false si la representación IEEE 754 pierde precisión (más de ~15-17 dígitos significativos o enteros mayores a 2^53).
    /// </summary>
    public bool EsPreservadoExactamenteEnFirestore(decimal valor)
    {
        return (decimal)(double)valor == valor;
    }

    private static void ValidarUnidades(
        string? unidadOrigen,
        string? unidadDestino,
        out (string Categoria, string Normalizada, decimal FactorBase) infoOrigen,
        out (string Categoria, string Normalizada, decimal FactorBase) infoDestino)
    {
        if (string.IsNullOrWhiteSpace(unidadOrigen))
        {
            throw new ArgumentException("La unidad de origen es obligatoria y no puede estar vacía.", nameof(unidadOrigen));
        }

        if (string.IsNullOrWhiteSpace(unidadDestino))
        {
            throw new ArgumentException("La unidad de destino es obligatoria y no puede estar vacía.", nameof(unidadDestino));
        }

        var uOrig = unidadOrigen.Trim();
        var uDest = unidadDestino.Trim();

        if (!TablaUnidades.TryGetValue(uOrig, out infoOrigen))
        {
            throw new ArgumentException($"La unidad de origen '{unidadOrigen}' no es una unidad válida ni reconocida por el catálogo de TipoInsumo.", nameof(unidadOrigen));
        }

        if (!TablaUnidades.TryGetValue(uDest, out infoDestino))
        {
            throw new ArgumentException($"La unidad de destino '{unidadDestino}' no es una unidad válida ni reconocida por el catálogo de TipoInsumo.", nameof(unidadDestino));
        }

        if (!string.Equals(infoOrigen.Categoria, infoDestino.Categoria, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Incompatibilidad de magnitud física: no se puede convertir entre unidades de '{infoOrigen.Categoria}' ('{unidadOrigen}') y '{infoDestino.Categoria}' ('{unidadDestino}'). (S25)");
        }
    }
}

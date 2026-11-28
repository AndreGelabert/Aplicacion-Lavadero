using Firebase.Converters;
using Google.Cloud.Firestore;

namespace Firebase.Models;

/// <summary>Instantánea de un turno; su creación y reserva corresponden al servicio transaccional.</summary>
[FirestoreData]
public class Turno
{
    [FirestoreProperty] public string Id { get; set; } = string.Empty;
    [FirestoreProperty] public string ClienteId { get; set; } = string.Empty;
    [FirestoreProperty] public string? ClienteNombre { get; set; }
    [FirestoreProperty] public string? ClienteTrajoNombre { get; set; }
    [FirestoreProperty] public string? ClienteRetiraNombre { get; set; }
    [FirestoreProperty] public string? VehiculoPatente { get; set; }
    [FirestoreProperty] public string? TipoVehiculo { get; set; }
    [FirestoreProperty] public string Estado { get; set; } = string.Empty;
    [FirestoreProperty] public string? MotivoCancelacion { get; set; }
    [FirestoreProperty] public string Origen { get; set; } = string.Empty;
    [FirestoreProperty] public string ClaveIdempotencia { get; set; } = string.Empty;
    [FirestoreProperty, System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? CuponId { get; set; }
    [FirestoreProperty, System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? FechaCreacionUtc { get; set; }
    [FirestoreProperty] public DateTime InicioUtc { get; set; }
    [FirestoreProperty] public DateTime FinUtc { get; set; }
    [FirestoreProperty] public bool AceptaAdelanto { get; set; }
    [FirestoreProperty] public string ClienteTrajoId { get; set; } = string.Empty;
    [FirestoreProperty] public string ClienteRetiraId { get; set; } = string.Empty;
    [FirestoreProperty] public string Notas { get; set; } = string.Empty;
    [FirestoreProperty(ConverterType = typeof(DecimalConverter))] public decimal Descuento { get; set; }
    [FirestoreProperty(ConverterType = typeof(DecimalConverter))] public decimal PrecioAcordado { get; set; }
    [FirestoreProperty] public DesglosePrecioTurno? DesglosePrecio { get; set; }
    [FirestoreProperty] public List<VehiculoEnTurno> Vehiculos { get; set; } = new();
    // Null identifica turnos anteriores a la captura; una lista vacía es un pronóstico válido.
    [FirestoreProperty] public List<DemandaInsumoTurno>? InsumosForecast { get; set; }
    // Un documento sin captura tiene receta desconocida, no necesariamente vacía.
    [FirestoreProperty] public RecetaOperativaTurno? RecetaOperativa { get; set; }

    /// <summary>Devuelve nombres de campos inválidos, sin modificar ni persistir el turno.</summary>
    public IReadOnlyList<string> Validar()
    {
        var errores = new HashSet<string>();
        if (PrecioAcordado < 0) errores.Add(nameof(PrecioAcordado));
        if (Descuento < 0 || Descuento > 100) errores.Add(nameof(Descuento));
        if (string.IsNullOrWhiteSpace(Id)) errores.Add(nameof(Id));
        if (string.IsNullOrWhiteSpace(ClienteId)) errores.Add(nameof(ClienteId));
        if (string.IsNullOrWhiteSpace(Estado)) errores.Add(nameof(Estado));
        if (string.IsNullOrWhiteSpace(Origen)) errores.Add(nameof(Origen));
        if (string.IsNullOrWhiteSpace(ClaveIdempotencia)) errores.Add(nameof(ClaveIdempotencia));
        if (FechaCreacionUtc.HasValue && (FechaCreacionUtc.Value == default || FechaCreacionUtc.Value.Kind != DateTimeKind.Utc)) errores.Add(nameof(FechaCreacionUtc));
        if (InicioUtc == default || InicioUtc.Kind != DateTimeKind.Utc) errores.Add(nameof(InicioUtc));
        if (FinUtc == default || FinUtc.Kind != DateTimeKind.Utc || FinUtc <= InicioUtc) errores.Add(nameof(FinUtc));
        if (Vehiculos == null || Vehiculos.Count != 1) errores.Add(nameof(Vehiculos));
        long duracionTicks = 0;
        bool duracionValida = true;
        if (Vehiculos == null || Vehiculos.Count == 0) duracionValida = false;
        else foreach (var vehiculo in Vehiculos)
        {
            if (vehiculo == null) { errores.Add(nameof(Vehiculos)); duracionValida = false; continue; }
            if (string.IsNullOrWhiteSpace(vehiculo.VehiculoId)) errores.Add(nameof(vehiculo.VehiculoId));
            if (vehiculo.CantidadEmpleadosRequeridos <= 0) errores.Add(nameof(vehiculo.CantidadEmpleadosRequeridos));
            if (vehiculo.Servicios == null || vehiculo.Servicios.Count == 0) { errores.Add(nameof(vehiculo.Servicios)); duracionValida = false; }
            else foreach (var servicio in vehiculo.Servicios)
            {
                if (servicio == null) { errores.Add(nameof(vehiculo.Servicios)); duracionValida = false; continue; }
                if (string.IsNullOrWhiteSpace(servicio.ServicioId)) errores.Add(nameof(servicio.ServicioId));
                if (servicio.TiempoEstimado <= 0) { errores.Add(nameof(servicio.TiempoEstimado)); duracionValida = false; }
                else if (Vehiculos.Count == 1)
                {
                    var ticks = (long)servicio.TiempoEstimado * TimeSpan.TicksPerMinute;
                    if (duracionTicks > TimeSpan.MaxValue.Ticks - ticks) duracionValida = false;
                    else duracionTicks += ticks;
                }
                if (servicio.Precio < 0) errores.Add(nameof(servicio.Precio));
            }
        }
        if (Vehiculos?.Count == 1 && duracionValida && FinUtc.Ticks - InicioUtc.Ticks != duracionTicks)
            errores.Add(nameof(FinUtc));
        if (Vehiculos?.Count == 1 && !duracionValida && !errores.Contains("TiempoEstimado") && !errores.Contains("Servicios"))
            errores.Add(nameof(FinUtc));
        return errores.ToArray();
    }
}

// Decimal text preserves every digit across Firestore's double-based numeric representation.
public sealed class DecimalPrecioNullableConverter : IFirestoreConverter<decimal>
{
    public object ToFirestore(decimal value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public decimal FromFirestore(object value) => decimal.Parse((string)value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture);
}

[FirestoreData]
public class DesglosePrecioTurno
{
    [FirestoreProperty] public int Version { get; set; }
    [FirestoreProperty(ConverterType = typeof(DecimalPrecioNullableConverter))] public decimal? Subtotal { get; set; }
    [FirestoreProperty(ConverterType = typeof(DecimalPrecioNullableConverter))] public decimal? Total { get; set; }
}

[FirestoreData]
public class RecetaOperativaTurno
{
    [FirestoreProperty] public int Version { get; set; }
    [FirestoreProperty] public List<ServicioOperativoTurno> Servicios { get; set; } = new();
}

[FirestoreData]
public class ServicioOperativoTurno
{
    [FirestoreProperty] public string ServicioId { get; set; } = string.Empty;
    [FirestoreProperty] public string Nombre { get; set; } = string.Empty;
    [FirestoreProperty] public string Tipo { get; set; } = string.Empty;
    [FirestoreProperty] public List<RecetaInsumo> Receta { get; set; } = new();
    [FirestoreProperty] public List<Etapa> Etapas { get; set; } = new();
}

[FirestoreData]
public class DemandaInsumoTurno
{
    [FirestoreProperty] public string ServicioId { get; set; } = string.Empty;
    [FirestoreProperty] public string? EtapaId { get; set; }
    [FirestoreProperty] public string Modalidad { get; set; } = string.Empty;
    [FirestoreProperty] public string TipoId { get; set; } = string.Empty;
    [FirestoreProperty] public string NombreTipo { get; set; } = string.Empty;
    [FirestoreProperty] public string Categoria { get; set; } = string.Empty;
    [FirestoreProperty] public string InsumoId { get; set; } = string.Empty;
    [FirestoreProperty] public string NombreInsumo { get; set; } = string.Empty;
    [FirestoreProperty] public string UnidadMedida { get; set; } = string.Empty;
    [FirestoreProperty(ConverterType = typeof(DecimalConverter))] public decimal DosisTeorica { get; set; }
}

[FirestoreData]
public class VehiculoEnTurno
{
    [FirestoreProperty] public string VehiculoId { get; set; } = string.Empty;
    [FirestoreProperty] public string? Patente { get; set; }
    [FirestoreProperty] public string? TipoVehiculo { get; set; }
    [FirestoreProperty] public int CantidadEmpleadosRequeridos { get; set; }
    [FirestoreProperty] public List<ServicioEnTurno> Servicios { get; set; } = new();
}

[FirestoreData]
public class ServicioEnTurno
{
    [FirestoreProperty] public string ServicioId { get; set; } = string.Empty;
    [FirestoreProperty] public string? PaqueteId { get; set; }
    [FirestoreProperty(ConverterType = typeof(DecimalConverter))] public decimal Precio { get; set; }
    [FirestoreProperty] public int TiempoEstimado { get; set; }
}

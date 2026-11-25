using System.Text;

namespace Firebase.Models;

// Input preparation only; transactional booking remains the authority.
public sealed class TurnoWebForm
{
    public string FormIdentity { get; set; } = "";
    public string ClienteId { get; set; } = "";
    public string VehiculoId { get; set; } = "";
    public string ClienteTrajoId { get; set; } = "";
    public string ClienteRetiraId { get; set; } = "";
    public DateOnly? Fecha { get; set; }
    public TimeOnly? Hora { get; set; }
    public bool AceptaAdelanto { get; set; }
    public decimal Descuento { get; set; }
    public string? CuponId { get; set; }
    public string? Notas { get; set; }
    public List<TurnoWebFormServicio> Servicios { get; set; } = new();

    public IReadOnlyList<string> Validar()
    {
        var errors = new List<string>();
        if (!ValidId(ClienteId)) errors.Add(nameof(ClienteId));
        if (!ValidId(VehiculoId)) errors.Add(nameof(VehiculoId));
        if (!ValidId(ClienteTrajoId)) errors.Add(nameof(ClienteTrajoId));
        if (!ValidId(ClienteRetiraId)) errors.Add(nameof(ClienteRetiraId));
        if (Fecha == null || Fecha == DateOnly.MinValue) errors.Add(nameof(Fecha));
        if (Hora == null) errors.Add(nameof(Hora));
        if (Descuento is < 0 or > 100) errors.Add(nameof(Descuento));
        if (CuponId != null && (!ValidId(CuponId) || Descuento != 0)) errors.Add(nameof(CuponId));
        if (Notas?.Length > 2000) errors.Add(nameof(Notas));
        if (Servicios == null || Servicios.Count is < 1 or > 100 || Servicios.Any(s => s == null || !ValidId(s.ServicioId) || (s.PaqueteId != null && !ValidId(s.PaqueteId)))) errors.Add(nameof(Servicios));
        return errors;
    }

    private static bool ValidId(string? id) => !string.IsNullOrWhiteSpace(id) && !id.Contains('/') && id is not "." and not ".." &&
        !(id.StartsWith("__", StringComparison.Ordinal) && id.EndsWith("__", StringComparison.Ordinal)) && Encoding.UTF8.GetByteCount(id) <= 1500;
}

public sealed class TurnoWebFormServicio
{
    public string ServicioId { get; set; } = "";
    public string? PaqueteId { get; set; }
}

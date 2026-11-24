namespace Firebase.Models;

public sealed class TurnoEditWebForm
{
    public string Id { get; set; } = "";
    public string FormIdentity { get; set; } = "";
    public string VersionEsperada { get; set; } = "";
    public DateOnly? Fecha { get; set; }
    public TimeOnly? Hora { get; set; }
    public bool AceptaAdelanto { get; set; }
    public decimal? Descuento { get; set; }
    public string? ClienteTrajoId { get; set; }
    public string? ClienteRetiraId { get; set; }
    private string? notas;
    public string? Notas { get => notas; set { notas = value; NotasEnviadas = true; } }
    public bool NotasEnviadas { get; private set; }
    private string? cuponId;
    public string? CuponId { get => cuponId; set { cuponId = value; CuponIdEnviado = true; } }
    public bool CuponIdEnviado { get; private set; }
    // Null preserves the existing selection for schedule-only clients.
    public string? VehiculoId { get; set; }
    public List<TurnoWebFormServicio>? Servicios { get; set; }
}

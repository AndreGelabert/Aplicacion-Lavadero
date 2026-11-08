namespace Firebase.Models;

public class ClienteVehiculoInput
{
    public string? Id { get; set; }
    public string Patente { get; set; } = "";
    public string Marca { get; set; } = "";
    public string Modelo { get; set; } = "";
    public string MarcaId { get; set; } = "";
    public string Color { get; set; } = "";
    public string TipoVehiculo { get; set; } = "";
    public bool EsNuevo { get; set; }
    public bool MarcaManual { get; set; }
    public bool ModeloManual { get; set; }
    public bool EsReasignacion { get; set; }
    public bool EsAsociacion { get; set; }
    public string? ClaveAsociacion { get; set; }
}

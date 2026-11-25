namespace Firebase.Models;

public sealed record TurnoConsultaViewModel(string Id, string Cliente, string Estado, DateTime InicioUtc, DateTime FinUtc,
    bool AceptaAdelanto, decimal PrecioAcordado, IReadOnlyList<VehiculoConsultaViewModel> Vehiculos, string? VersionEsperada = null,
    decimal Descuento = 0, IReadOnlyList<string>? EmpleadosAsignados = null);
public sealed record TurnoDatosEdicion(string Id, string VersionEsperada, DateTime InicioUtc, bool AceptaAdelanto,
    string ClienteId, string ClienteTrajoId, string ClienteRetiraId, string Notas, decimal Descuento, string? CuponId,
    decimal PrecioAcordado, IReadOnlyList<VehiculoEdicionViewModel> Vehiculos);
public sealed record VehiculoEdicionViewModel(string Patente, string VehiculoId, string TipoVehiculo,
    int CantidadEmpleadosRequeridos, IReadOnlyList<ServicioConsultaViewModel> Servicios);

public sealed record VehiculoConsultaViewModel(string Patente, int CantidadEmpleadosRequeridos, IReadOnlyList<ServicioConsultaViewModel> Servicios,
    string VehiculoId = "");
public sealed record ServicioConsultaViewModel(string Nombre, int TiempoEstimado, decimal Precio, string ServicioId = "", string? PaqueteId = null);

using Firebase.Models;

namespace Firebase.Services;

/// <summary>
/// Coordinates sequential listing and search reads without HTTP or view state.
/// Callers retain filter defaults, presentation and permission decisions.
/// </summary>
public sealed class ServicioQueryService(ServicioService servicios)
{
    public async Task<ResultadoListado> ObtenerListado(
        List<string> estados, List<string> tipos, List<string> tiposVehiculo,
        decimal? precioDesde, decimal? precioHasta, int pageNumber, int pageSize,
        string sortBy, string sortOrder)
    {
        var filas = await servicios.ObtenerServicios(estados, tipos, tiposVehiculo,
            precioDesde, precioHasta, pageNumber, pageSize, sortBy, sortOrder);
        var totalPages = await servicios.ObtenerTotalPaginas(estados, tipos, tiposVehiculo,
            precioDesde, precioHasta, pageSize);
        return new(filas, Math.Max(totalPages, 1));
    }

    public async Task<ResultadoListado> Buscar(
        string searchTerm, List<string> estados, List<string> tipos, List<string> tiposVehiculo,
        decimal? precioDesde, decimal? precioHasta, int pageNumber, int pageSize,
        string sortBy, string sortOrder)
    {
        var filas = await servicios.BuscarServicios(searchTerm, estados, tipos, tiposVehiculo,
            precioDesde, precioHasta, pageNumber, pageSize, sortBy, sortOrder);
        var totalServicios = await servicios.ObtenerTotalServiciosBusqueda(
            searchTerm, estados, tipos, tiposVehiculo, precioDesde, precioHasta);
        var totalPages = Math.Max((int)Math.Ceiling(totalServicios / (double)pageSize), 1);
        return new(filas, totalPages);
    }

    public sealed record ResultadoListado(List<Servicio> Servicios, int TotalPages);
}

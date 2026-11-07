using Firebase.Models;

namespace Firebase.Services;

/// <summary>
/// Coordina las consultas secuenciales de personal sin estado de presentación.
/// </summary>
public sealed class PersonalQueryService(PersonalService personal)
{
    public async Task<ResultadoListado> ObtenerListado(
        List<string> estados, List<string> roles, int pageNumber, int pageSize,
        string sortBy, string sortOrder)
    {
        var empleados = await personal.ObtenerEmpleados(estados, roles, pageNumber, pageSize, sortBy, sortOrder);
        var totalPages = await personal.ObtenerTotalPaginas(estados, roles, pageSize);
        return new(empleados, Math.Max(totalPages, 1));
    }

    public async Task<ResultadoListado> Buscar(
        string searchTerm, List<string> estados, List<string> roles, int pageNumber, int pageSize,
        string sortBy, string sortOrder)
    {
        var empleados = await personal.BuscarEmpleados(searchTerm, estados, roles, pageNumber, pageSize, sortBy, sortOrder);
        var totalEmpleados = await personal.ObtenerTotalEmpleadosBusqueda(searchTerm, estados, roles);
        var totalPages = Math.Max((int)Math.Ceiling(totalEmpleados / (double)pageSize), 1);
        return new(empleados, totalPages);
    }

    public sealed record ResultadoListado(List<Empleado> Empleados, int TotalPages);
}

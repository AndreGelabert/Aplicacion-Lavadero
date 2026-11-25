using Firebase.Models;

namespace Firebase.Services;

// Read-only projections for the appointment form; never return complete customer or vehicle records.
public interface ITurnoSelectorService
{
    Task<object> BuscarVehiculos(string? search, bool consultar);
    Task<object> ClientesVehiculo(string? id, string? patente, bool consultar);
    Task<object> Servicios(string? tipo);
    Task<object> Paquetes(string? tipo);
}

public class TurnoSelectorService(ClienteService clientes, VehiculoService vehiculos, ServicioService servicios, PaqueteServicioService paquetes) : ITurnoSelectorService
{
    public async Task<object> BuscarVehiculos(string? search, bool consultar)
    {
        if (string.IsNullOrWhiteSpace(search) || search.Length < 2) return Array.Empty<object>();
        if (!consultar)
        {
            var v = await vehiculos.ObtenerVehiculoPorPatente(search.Trim().ToUpperInvariant());
            return v == null || v.Estado != "Activo" ? Array.Empty<object>() : new[] { Vehiculo(v) };
        }
        var encontrados = await vehiculos.ObtenerVehiculos(search, null!, null!, null!, 1, 20, "Patente", "asc", new() { "Activo" });
        return encontrados.Select(Vehiculo).ToArray();
    }

    public async Task<object> ClientesVehiculo(string? id, string? patente, bool consultar)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Contains('/') || (!consultar && string.IsNullOrWhiteSpace(patente))) return Array.Empty<object>();
        var v = consultar ? await vehiculos.ObtenerVehiculo(id) : await vehiculos.ObtenerVehiculoPorPatente(patente!.Trim().ToUpperInvariant());
        if (v == null || v.Estado != "Activo" || v.Id != id) return Array.Empty<object>();
        var asociados = await clientes.ObtenerAsociadosVehiculo(v);
        return asociados.Where(c => c.Estado == "Activo").Select(c => new { id = c.Id, nombre = c.NombreCompleto, documento = $"{c.TipoDocumento}: {c.NumeroDocumento}" }).ToArray();
    }

    public async Task<object> Servicios(string? tipo)
    {
        if (string.IsNullOrWhiteSpace(tipo)) return Array.Empty<object>();
        var lista = await servicios.ObtenerServicios(estados: new() { "Activo" }, tiposVehiculo: new() { tipo }, pageNumber: 1, pageSize: 100);
        return lista.Select(s => new { id = s.Id, nombre = s.Nombre, tipo = s.Tipo, tipoVehiculo = s.TipoVehiculo, precio = s.Precio, tiempoEstimado = s.TiempoEstimado, tieneEtapas = s.Etapas?.Any() ?? false, cantidadEtapas = s.Etapas?.Count ?? 0, descripcion = s.Descripcion }).ToArray();
    }

    public async Task<object> Paquetes(string? tipo)
    {
        if (string.IsNullOrWhiteSpace(tipo)) return Array.Empty<object>();
        var lista = await paquetes.ObtenerPaquetes(estados: new() { "Activo" }, tiposVehiculo: new() { tipo }, pageNumber: 1, pageSize: 100);
        var resultado = new List<object>();
        foreach (var p in lista)
        {
            var incluidos = await ServiciosValidosPaquete(p, tipo, paquetes);
            if (incluidos == null) continue;
            resultado.Add(new { id = p.Id, nombre = p.Nombre, tipoVehiculo = p.TipoVehiculo, precio = incluidos.Sum(s => s.Precio) * (1m - p.PorcentajeDescuento / 100m), precioOriginal = incluidos.Sum(s => s.Precio), descuento = p.PorcentajeDescuento, tiempoEstimado = incluidos.Sum(s => s.TiempoEstimado), servicios = incluidos.Select(s => new { id = s.Id, nombre = s.Nombre, tipo = s.Tipo, precio = s.Precio, tiempoEstimado = s.TiempoEstimado }) });
        }
        return resultado;
    }

    internal static async Task<List<Servicio>?> ServiciosValidosPaquete(PaqueteServicio p, string tipo, PaqueteServicioService paquetes)
    {
        var ids = p.ServiciosIds;
        if (ids == null || ids.Count == 0 || ids.Any(id => string.IsNullOrWhiteSpace(id) || id.Contains('/')) ||
            ids.Distinct(StringComparer.Ordinal).Count() != ids.Count || p.PorcentajeDescuento < 0 || p.PorcentajeDescuento > 100) return null;
        var incluidos = await paquetes.ObtenerServiciosDePaquete(ids);
        return incluidos.Count != ids.Count || incluidos.Any(s => s.Estado != "Activo" ||
            !string.Equals(s.TipoVehiculo, tipo, StringComparison.OrdinalIgnoreCase) || s.TiempoEstimado <= 0 || s.Precio < 0) ? null : incluidos;
    }

    internal Task<List<Servicio>?> ServiciosValidosPaquete(PaqueteServicio p, string tipo) => ServiciosValidosPaquete(p, tipo, paquetes);

    private static object Vehiculo(Vehiculo v) => new { id = v.Id, patente = v.Patente, tipoVehiculo = v.TipoVehiculo, marca = v.Marca, modelo = v.Modelo, color = v.Color, descripcion = $"{v.Patente} - {v.Marca} {v.Modelo} ({v.Color})" };
}

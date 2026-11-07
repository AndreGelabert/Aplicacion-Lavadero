using Firebase.Models;

namespace Firebase.Services;

/// <summary>Coordinates type administration without MVC or audit emission.</summary>
public sealed class ServicioTypeService(ServicioService servicios, TipoServicioService tiposServicio,
    TipoVehiculoService tiposVehiculo, VehiculoService vehiculos)
{
    public sealed record Creacion(string? Id, string? Error);
    public sealed record Eliminacion(List<string>? Ids, Dictionary<string, TipoVehiculo>? Capturas, string? Error);

    public async Task<Creacion> CrearServicioAjax(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return new(null, "El nombre del tipo de servicio es obligatorio.");
        if (nombre.Length < 3) return new(null, "El nombre debe tener al menos 3 caracteres.");
        if (await tiposServicio.ExisteTipoServicio(nombre)) return new(null, "Ya existe un tipo de servicio con el mismo nombre.");
        return new(await tiposServicio.CrearTipoServicio(nombre), null);
    }

    public async Task<Creacion> CrearVehiculoAjax(string nombre, string? formato, int empleados)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return new(null, "El nombre del tipo de vehículo es obligatorio.");
        if (nombre.Length < 3) return new(null, "El nombre debe tener al menos 3 caracteres.");
        if (string.IsNullOrWhiteSpace(formato)) return new(null, "El formato de patente es obligatorio.");
        if (formato.Length < 3) return new(null, "El formato debe tener al menos 3 caracteres.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(formato, @"^[nl.\-|]{3,}$"))
            return new(null, "El formato solo puede contener 'n' (números), 'l' (letras), '.' '-' y '|'. Mínimo 3 caracteres.");
        if (empleados < 1 || empleados > 10) return new(null, "La cantidad de empleados debe estar entre 1 y 10.");
        if (await tiposVehiculo.ExisteTipoVehiculo(nombre)) return new(null, "Ya existe un tipo de vehículo con el mismo nombre.");
        return new(await tiposVehiculo.CrearTipoVehiculo(nombre, formato, empleados), null);
    }

    public async Task<Creacion> CrearServicioFallback(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return new(null, null);
        if (await tiposServicio.ExisteTipoServicio(nombre)) return new(null, "Ya existe un TipoServicio con el mismo nombre.");
        return new(await tiposServicio.CrearTipoServicio(nombre), null);
    }

    public async Task<Creacion> CrearVehiculoFallback(string nombre, string? formato, int empleados)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return new(null, null);
        if (await tiposVehiculo.ExisteTipoVehiculo(nombre)) return new(null, "Ya existe un TipoVehiculo con el mismo nombre.");
        return new(await tiposVehiculo.CrearTipoVehiculo(nombre, formato, empleados), null);
    }

    public async Task<Eliminacion> EliminarServicioAjax(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return new(null, null, "Debe seleccionar un tipo de servicio.");
        if ((await servicios.ObtenerServiciosPorTipo(nombre)).Any())
            return new(null, null, "No se puede eliminar el tipo de servicio porque hay servicios que lo utilizan.");
        return new(await tiposServicio.EliminarTipoServicio(nombre), null, null);
    }

    public async Task<Eliminacion> EliminarServicioFallback(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return new(null, null, null);
        if ((await servicios.ObtenerServiciosPorTipo(nombre)).Any())
            return new(null, null, "No se puede eliminar TipoServicio porque hay servicios que lo utilizan.");
        return new(await tiposServicio.EliminarTipoServicio(nombre), null, null);
    }

    public async Task<Eliminacion> EliminarVehiculoAjax(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return new(null, null, "Debe seleccionar un tipo de vehículo.");
        if ((await servicios.ObtenerServiciosPorTipoVehiculo(nombre)).Any())
            return new(null, null, $"No se puede eliminar el tipo '{nombre}' porque hay servicios que lo utilizan.");
        if (await vehiculos.ExisteTipoVehiculoEnUso(nombre))
            return new(null, null, $"No se puede eliminar el tipo '{nombre}' porque está en uso por uno o más vehículos.");
        return await EliminarVehiculoCapturado(nombre);
    }

    public async Task<Eliminacion> EliminarVehiculoFallback(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return new(null, null, null);
        if ((await servicios.ObtenerServiciosPorTipoVehiculo(nombre)).Any())
            return new(null, null, "No se puede eliminar TipoVehiculo porque hay servicios que lo utilizan.");
        return await EliminarVehiculoCapturado(nombre);
    }

    private async Task<Eliminacion> EliminarVehiculoCapturado(string nombre)
    {
        var capturas = (await tiposVehiculo.ObtenerParaAuditoriaPorNombre(nombre)).ToDictionary(t => t.Id);
        var ids = await tiposVehiculo.EliminarTipoVehiculo(nombre, capturas.Keys.ToHashSet());
        return new(ids, capturas, null);
    }
}

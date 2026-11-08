using Firebase.Models;

namespace Firebase.Services;

/// <summary>Validates and persists service form business data without MVC state.</summary>
public sealed class ServicioFormService(ServicioService servicios)
{
    public sealed record ErrorCampo(string Campo, string Mensaje);
    public sealed record ResultadoFormulario(ResultadoOperacion Operacion, ErrorCampo? Error);

    public List<ErrorCampo> Validar(Servicio servicio)
    {
        var errores = new List<ErrorCampo>();
        if (!string.IsNullOrEmpty(servicio.Nombre) && !System.Text.RegularExpressions.Regex.IsMatch(servicio.Nombre, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$"))
            errores.Add(new("Nombre", "El nombre solo puede contener letras y espacios."));
        if (servicio.Precio < 0)
            errores.Add(new("Precio", "El precio debe ser igual o mayor a 0."));
        if (servicio.TiempoEstimado <= 0)
            errores.Add(new("TiempoEstimado", "El tiempo estimado debe ser mayor a 0."));
        return errores;
    }

    public async Task<ResultadoFormulario> Crear(Servicio servicio)
    {
        if (await servicios.ExisteServicioConNombreTipoVehiculo(servicio.Nombre, servicio.TipoVehiculo))
            return Duplicado(servicio);

        servicio.Estado = "Activo";
        await servicios.CrearServicio(servicio);
        return new(ResultadoOperacion.CrearExito("Servicio creado correctamente."), null);
    }

    public async Task<ResultadoFormulario> Actualizar(Servicio servicio)
    {
        var servicioActual = await servicios.ObtenerServicio(servicio.Id);
        if (servicioActual == null)
            return Error("", "No se pudo encontrar el servicio a actualizar.", "No se pudo encontrar el servicio a actualizar.");

        // Validación: Prevenir cambio de tipo de vehículo
        if (!string.Equals(servicioActual.TipoVehiculo, servicio.TipoVehiculo, StringComparison.OrdinalIgnoreCase))
            return Error("TipoVehiculo", "No se puede cambiar el tipo de vehículo de un servicio existente.", "No se puede cambiar el tipo de vehículo de un servicio existente. Si necesita cambiar el tipo de vehículo, debe crear un nuevo servicio.");

        // NUEVA VALIDACIÓN: Prevenir cambio de tipo de servicio
        if (!string.Equals(servicioActual.Tipo, servicio.Tipo, StringComparison.OrdinalIgnoreCase))
            return Error("Tipo", "No se puede cambiar el tipo de servicio de un servicio existente.", "No se puede cambiar el tipo de servicio de un servicio existente. Si necesita cambiar el tipo, debe crear un nuevo servicio.");

        if (await servicios.ExisteServicioConNombreTipoVehiculo(servicio.Nombre, servicio.TipoVehiculo, servicio.Id))
            return Duplicado(servicio);

        servicio.Estado = servicioActual.Estado;
        await servicios.ActualizarServicio(servicio);
        return new(ResultadoOperacion.CrearExito("Servicio actualizado correctamente."), null);
    }

    private static ResultadoFormulario Duplicado(Servicio servicio) => Error("Nombre",
        $"Ya existe un servicio con este nombre para vehículos tipo '{servicio.TipoVehiculo}'.",
        $"Ya existe un servicio con el nombre '{servicio.Nombre}' para vehículos tipo '{servicio.TipoVehiculo}'.");

    private static ResultadoFormulario Error(string campo, string mensajeCampo, string mensaje) =>
        new(ResultadoOperacion.CrearError(mensaje), new(campo, mensajeCampo));
}

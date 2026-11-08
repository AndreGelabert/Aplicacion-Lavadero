using Firebase.Models;

namespace Firebase.Services;

/// <summary>Prepares package form business validation without MVC or audit state.</summary>
public sealed class PaqueteServicioFormService(PaqueteServicioService paquetes, ConfiguracionService configuracion)
{
    public sealed record ErrorCampo(string Campo, string Mensaje);
    public sealed record Preparacion(PaqueteServicio? Original, ResultadoOperacion? Error, ErrorCampo? ErrorCampo);

    public List<ErrorCampo> ValidarNombre(PaqueteServicio paquete)
    {
        var errores = new List<ErrorCampo>();
        if (!string.IsNullOrEmpty(paquete.Nombre) && !System.Text.RegularExpressions.Regex.IsMatch(paquete.Nombre, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$"))
            errores.Add(new("Nombre", "El nombre solo puede contener letras y espacios."));
        return errores;
    }

    public async Task<List<ErrorCampo>> ValidarDescuentoYServicios(PaqueteServicio paquete)
    {
        var descuentoMinimo = await configuracion.ObtenerPaquetesDescuentoStep();
        descuentoMinimo = Math.Max(descuentoMinimo, 5);
        var errores = new List<ErrorCampo>();
        if (paquete.PorcentajeDescuento < descuentoMinimo || paquete.PorcentajeDescuento > 95)
            errores.Add(new("PorcentajeDescuento", $"El porcentaje de descuento debe estar entre {descuentoMinimo} y 95."));
        if (paquete.ServiciosIds == null || paquete.ServiciosIds.Count < 2)
            errores.Add(new("ServiciosIds", "Debe seleccionar al menos 2 servicios."));
        return errores;
    }

    public async Task<Preparacion> PrepararCreacion(PaqueteServicio paquete)
    {
        if (await paquetes.ExistePaqueteConNombre(paquete.Nombre))
            return Duplicado(paquete);
        paquete.Estado = "Activo";
        return new(null, null, null);
    }

    public async Task<Preparacion> PrepararActualizacion(PaqueteServicio paquete)
    {
        var actual = await paquetes.ObtenerPaquete(paquete.Id);
        if (actual == null)
            return Error("", "No se pudo encontrar el paquete a actualizar.", "No se pudo encontrar el paquete a actualizar.");
        if (!string.Equals(actual.TipoVehiculo, paquete.TipoVehiculo, StringComparison.OrdinalIgnoreCase))
            return Error("TipoVehiculo", "No se puede cambiar el tipo de vehículo de un paquete existente.", "No se puede cambiar el tipo de vehículo de un paquete existente. Si necesita cambiar el tipo de vehículo, debe crear un nuevo paquete.");
        if (await paquetes.ExistePaqueteConNombre(paquete.Nombre, paquete.Id))
            return Duplicado(paquete);
        return new(actual, null, null);
    }

    private static Preparacion Duplicado(PaqueteServicio paquete) => Error("Nombre",
        "Ya existe un paquete con este nombre.", $"Ya existe un paquete con el nombre '{paquete.Nombre}'.");

    private static Preparacion Error(string campo, string mensajeCampo, string mensaje) =>
        new(null, ResultadoOperacion.CrearError(mensaje), new(campo, mensajeCampo));
}

using Firebase.Models;

namespace Firebase.Services;

/// <summary>Coordinates eligibility and the first state write without HTTP or audit emission.</summary>
public sealed class ServicioLifecycleService(ServicioService servicios, PaqueteServicioService paquetes)
{
    public async Task<ResultadoCambioEstado> CambiarEstado(string id, string nuevoEstado)
    {
        if (nuevoEstado == "Inactivo")
        {
            var paquetesActivos = await paquetes.ObtenerPaquetes(new List<string> { "Activo" }, null, 1, int.MaxValue, "Nombre", "asc");
            var paquetesConServicio = paquetesActivos
                .Where(p => p.ServiciosIds != null && p.ServiciosIds.Contains(id))
                   .ToList();

            if (paquetesConServicio.Any())
            {
                var nombresPaquetes = string.Join(", ", paquetesConServicio.Select(p => p.Nombre));
                return new($"No se puede desactivar el servicio porque está siendo usado en los siguientes paquetes activos: {nombresPaquetes}", null, null);
            }
        }

        var servicioAntesCambio = await servicios.ObtenerServicio(id) ?? throw new InvalidOperationException("No se encontró el servicio para auditoría.");
        var antesCambio = AuditCampos.CapturarServicio(servicioAntesCambio);
        await servicios.CambiarEstadoServicio(id, nuevoEstado);
        return new(null, servicioAntesCambio, antesCambio);
    }

    public sealed record ResultadoCambioEstado(string? Error, Servicio? ServicioAntes, Dictionary<string, string>? Antes);
}

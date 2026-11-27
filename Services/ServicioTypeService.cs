using Firebase.Models;

namespace Firebase.Services;

/// <summary>Coordinates inline type creation and logical deactivation.</summary>
public sealed class ServicioTypeService(
    TipoServicioService tiposServicio,
    TipoVehiculoService tiposVehiculo)
{
    public sealed record Creacion(string? Id, string? Error);
    public sealed record Desactivacion(string? Id, string? Error);

    public async Task<Creacion> CrearServicioAjax(string nombre, ActorAuditoria? actor = null)
    {
        var res = await tiposServicio.GuardarConAuditoria(
            new TipoServicio { Id = string.Empty, Nombre = nombre ?? string.Empty },
            crear: true,
            actor ?? new ActorAuditoria(string.Empty, string.Empty));
        return res.Resultado.EsExitoso
            ? new(res.TipoServicioId, null)
            : new(null, res.Resultado.MensajeError);
    }

    public async Task<Creacion> CrearVehiculoAjax(string nombre, string? formato, int empleados, ActorAuditoria? actor = null)
    {
        var res = await tiposVehiculo.GuardarConAuditoria(
            new TipoVehiculo
            {
                Id = string.Empty,
                Nombre = nombre ?? string.Empty,
                FormatoPatente = formato ?? string.Empty,
                CantidadEmpleadosRequeridos = empleados
            },
            crear: true,
            actor ?? new ActorAuditoria(string.Empty, string.Empty));
        return res.Resultado.EsExitoso
            ? new(res.TipoVehiculoId, null)
            : new(null, res.Resultado.MensajeError);
    }

    public Task<Creacion> CrearServicioFallback(string nombre, ActorAuditoria? actor = null) =>
        string.IsNullOrWhiteSpace(nombre)
            ? Task.FromResult(new Creacion(null, null))
            : CrearServicioAjax(nombre, actor);

    public Task<Creacion> CrearVehiculoFallback(string nombre, string? formato, int empleados, ActorAuditoria? actor = null) =>
        string.IsNullOrWhiteSpace(nombre)
            ? Task.FromResult(new Creacion(null, null))
            : CrearVehiculoAjax(nombre, formato, empleados, actor);

    public async Task<Desactivacion> DesactivarServicioAjax(string nombre, ActorAuditoria? actor = null)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return new(null, "Debe seleccionar un tipo de servicio.");

        var tipo = await tiposServicio.ObtenerPorNombre(nombre);
        if (tipo == null)
            return new(null, "El tipo de servicio no existe.");

        var res = await tiposServicio.CambiarEstadoConAuditoria(
            tipo.Id,
            "Inactivo",
            actor ?? new ActorAuditoria(string.Empty, string.Empty));

        return res.Resultado.EsExitoso
            ? new(tipo.Id, null)
            : new(null, res.Resultado.MensajeError);
    }

    public Task<Desactivacion> DesactivarServicioFallback(string nombre, ActorAuditoria? actor = null) =>
        string.IsNullOrWhiteSpace(nombre)
            ? Task.FromResult(new Desactivacion(null, null))
            : DesactivarServicioAjax(nombre, actor);

    public async Task<Desactivacion> DesactivarVehiculoAjax(string nombre, ActorAuditoria? actor = null)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return new(null, "Debe seleccionar un tipo de vehículo.");

        var tipo = await tiposVehiculo.ObtenerTipoVehiculoPorNombre(nombre);
        if (tipo == null)
            return new(null, "El tipo de vehículo no existe.");

        var res = await tiposVehiculo.CambiarEstadoConAuditoria(
            tipo.Id,
            "Inactivo",
            actor ?? new ActorAuditoria(string.Empty, string.Empty));

        return res.Resultado.EsExitoso
            ? new(tipo.Id, null)
            : new(null, res.Resultado.MensajeError);
    }

    public Task<Desactivacion> DesactivarVehiculoFallback(string nombre, ActorAuditoria? actor = null) =>
        string.IsNullOrWhiteSpace(nombre)
            ? Task.FromResult(new Desactivacion(null, null))
            : DesactivarVehiculoAjax(nombre, actor);
}

using Firebase.Models;

namespace Firebase.Services;

/// <summary>Pure snapshot construction from caller-provided data; does not resolve, authorize, or reserve.</summary>
internal static class TurnoSnapshotBuilder
{
    internal record Seleccion(Servicio Servicio, string? PaqueteId);
    internal record Entrada(string Id, string ClienteId, string Origen, string ClaveIdempotencia,
        DateTime InicioUtc, bool AceptaAdelanto, Vehiculo Vehiculo, TipoVehiculo TipoVehiculo,
        IReadOnlyList<Seleccion> Servicios, IReadOnlyList<PaqueteServicio> Paquetes, decimal Descuento,
        string? ClienteTrajoId = null, string? ClienteRetiraId = null, string? Notas = null,
        string? ClienteNombre = null, string? ClienteTrajoNombre = null, string? ClienteRetiraNombre = null);

    internal static RecetaOperativaTurno CapturarRecetaOperativa(IEnumerable<Servicio> servicios) => new()
    {
        Version = 1, Servicios = servicios.Select(s => new ServicioOperativoTurno
        {
            ServicioId = s.Id, Nombre = s.Nombre, Tipo = s.Tipo,
            Receta = (s.Receta ?? []).Select(CopiarReceta).ToList(),
            Etapas = (s.Etapas ?? []).Select(e => new Etapa
            {
                Id = e.Id, Nombre = e.Nombre, Receta = (e.Receta ?? []).Select(CopiarReceta).ToList()
            }).ToList()
        }).ToList()
    };

    private static RecetaInsumo CopiarReceta(RecetaInsumo r) => new()
    {
        Modalidad = r.Modalidad, TipoId = r.TipoId, NombreTipo = r.NombreTipo, Categoria = r.Categoria,
        InsumoId = r.InsumoId, NombreInsumo = r.NombreInsumo, UnidadMedida = r.UnidadMedida, DosisTeorica = r.DosisTeorica, EsHerramienta = r.EsHerramienta
    };

    internal static Turno Build(Entrada input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Vehiculo == null || input.TipoVehiculo == null || input.Servicios == null || input.Paquetes == null ||
            string.IsNullOrWhiteSpace(input.Vehiculo.Id) || string.IsNullOrWhiteSpace(input.Vehiculo.TipoVehiculo) ||
            !string.Equals(input.Vehiculo.TipoVehiculo, input.TipoVehiculo.Nombre, StringComparison.OrdinalIgnoreCase) ||
            input.TipoVehiculo.CantidadEmpleadosRequeridos <= 0 || input.Servicios.Count == 0 ||
            input.Descuento < 0 || input.Descuento > 100 || input.InicioUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Datos de turno inválidos.", nameof(input));

        var washServices = new List<ServicioEnLavado>();
        long ticks = 0;
        foreach (var selection in input.Servicios)
        {
            var service = selection?.Servicio;
            if (service == null || string.IsNullOrWhiteSpace(service.Id) || service.Estado != "Activo" ||
                !string.Equals(service.TipoVehiculo, input.Vehiculo.TipoVehiculo, StringComparison.OrdinalIgnoreCase) ||
                service.TiempoEstimado <= 0 || service.Precio < 0 ||
                (selection!.PaqueteId != null && string.IsNullOrWhiteSpace(selection.PaqueteId)))
                throw new ArgumentException("Servicio inválido.", nameof(input));
            ticks = checked(ticks + (long)service.TiempoEstimado * TimeSpan.TicksPerMinute);
            washServices.Add(new ServicioEnLavado { ServicioId = service.Id, Precio = service.Precio,
                TiempoEstimado = service.TiempoEstimado, PaqueteId = selection.PaqueteId });
        }

        decimal subtotal = 0;
        foreach (var group in washServices.Where(s => s.PaqueteId != null).GroupBy(s => s.PaqueteId, StringComparer.Ordinal))
        {
            var matches = input.Paquetes.Where(p => p != null && p.Id == group.Key).ToArray();
            if (matches.Length != 1 || matches[0].Estado != "Activo" || matches[0].PorcentajeDescuento < 0 ||
                matches[0].PorcentajeDescuento > 100 ||
                !string.Equals(matches[0].TipoVehiculo, input.Vehiculo.TipoVehiculo, StringComparison.OrdinalIgnoreCase) ||
                !PaqueteServicioService.TryCalcularPrecioSeleccionado(matches[0], group, out var packagePrice))
                throw new ArgumentException("Paquete inválido.", nameof(input));
            subtotal += packagePrice;
        }
        subtotal += washServices.Where(s => s.PaqueteId == null).Sum(s => s.Precio);
        var turno = new Turno { Id = input.Id, ClienteId = input.ClienteId,
            ClienteNombre = input.ClienteNombre,
            ClienteTrajoNombre = input.ClienteTrajoNombre ?? input.ClienteNombre,
            ClienteRetiraNombre = input.ClienteRetiraNombre ?? input.ClienteNombre,
            VehiculoPatente = input.Vehiculo.Patente,
            TipoVehiculo = input.Vehiculo.TipoVehiculo,
            Estado = "Pendiente", Origen = input.Origen,
            ClaveIdempotencia = input.ClaveIdempotencia, InicioUtc = input.InicioUtc, FinUtc = input.InicioUtc.AddTicks(ticks),
            AceptaAdelanto = input.AceptaAdelanto, PrecioAcordado = subtotal - subtotal * input.Descuento / 100m,
            DesglosePrecio = new DesglosePrecioTurno { Version = 1, Subtotal = subtotal, Total = subtotal - subtotal * input.Descuento / 100m },
            ClienteTrajoId = input.ClienteTrajoId ?? input.ClienteId, ClienteRetiraId = input.ClienteRetiraId ?? input.ClienteId,
            Notas = input.Notas ?? string.Empty, Descuento = input.Descuento,
            Vehiculos = [new VehiculoEnTurno { VehiculoId = input.Vehiculo.Id,
                Patente = input.Vehiculo.Patente,
                TipoVehiculo = input.Vehiculo.TipoVehiculo,
                CantidadEmpleadosRequeridos = input.TipoVehiculo.CantidadEmpleadosRequeridos,
                Servicios = washServices.Select(s => new ServicioEnTurno { ServicioId = s.ServicioId, PaqueteId = s.PaqueteId, Precio = s.Precio, TiempoEstimado = s.TiempoEstimado }).ToList() }] };
        if (turno.Validar().Count != 0) throw new ArgumentException("Datos de turno inválidos.", nameof(input));
        return turno;
    }
}

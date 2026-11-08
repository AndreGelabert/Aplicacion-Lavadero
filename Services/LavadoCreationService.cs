using Firebase.Models;
using FirebaseLoginCustom.Controllers;

namespace Firebase.Services;

public class LavadoCreationService
{
    private readonly ClienteService _clienteService;
    private readonly VehiculoService _vehiculoService;
    private readonly ServicioService _servicioService;
    private readonly PaqueteServicioService _paqueteServicioService;
    private readonly TipoVehiculoService _tipoVehiculoService;
    private readonly LavadoService _lavadoService;

    public LavadoCreationService(ClienteService clienteService, VehiculoService vehiculoService,
        ServicioService servicioService, PaqueteServicioService paqueteServicioService,
        TipoVehiculoService tipoVehiculoService, LavadoService lavadoService)
    {
        _clienteService = clienteService;
        _vehiculoService = vehiculoService;
        _servicioService = servicioService;
        _paqueteServicioService = paqueteServicioService;
        _tipoVehiculoService = tipoVehiculoService;
        _lavadoService = lavadoService;
    }

    public record Contexto(Cliente Cliente, List<VehiculoServiciosRequest> Vehiculos, Dictionary<string, PaqueteServicio> Paquetes);
    public record ResultadoPreparacion(Contexto? Contexto = null, string? Error = null);
    public record ResultadoVehiculo(Lavado? Lavado = null, string? Error = null);

    public async Task<ResultadoPreparacion> Preparar(CrearLavadoRequest request)
    {
        var cliente = await _clienteService.ObtenerCliente(request.ClienteId);
        if (cliente == null)
            return new(Error: "Cliente no encontrado.");

        var vehiculosConServicios = request.VehiculosServicios;
        if (vehiculosConServicios == null || !vehiculosConServicios.Any())
            return new(Error: "Debe seleccionar al menos un vehículo con servicios.");

        var paquetesPorId = new Dictionary<string, PaqueteServicio>(StringComparer.Ordinal);
        var serviciosConPaquete = vehiculosConServicios
            .SelectMany(vehiculo => vehiculo.Servicios.Select(servicio => new { Servicio = servicio, servicio.PaqueteId }))
            .Where(item => !string.IsNullOrWhiteSpace(item.PaqueteId))
            .GroupBy(item => item.PaqueteId!, StringComparer.Ordinal);

        foreach (var grupo in serviciosConPaquete)
        {
            var paquete = await _paqueteServicioService.ObtenerPaquete(grupo.Key);
            if (paquete == null)
                return new(Error: "Uno de los paquetes seleccionados ya no está disponible.");

            var idsPaquete = new HashSet<string>(paquete.ServiciosIds ?? new List<string>(), StringComparer.Ordinal);
            if (grupo.Any(item => string.IsNullOrWhiteSpace(item.Servicio.ServicioId) || !idsPaquete.Contains(item.Servicio.ServicioId)))
                return new(Error: "Los servicios seleccionados no corresponden al paquete indicado.");

            paquetesPorId.Add(grupo.Key, paquete);
        }

        return new(new Contexto(cliente, vehiculosConServicios, paquetesPorId));
    }

    public async Task<ResultadoVehiculo> CrearVehiculo(CrearLavadoRequest request, Contexto contexto, VehiculoServiciosRequest vehiculoServicios)
    {
        var cliente = contexto.Cliente;
        var vehiculo = await _vehiculoService.ObtenerVehiculo(vehiculoServicios.VehiculoId);
        if (vehiculo == null)
            return new(Error: $"Vehículo con ID {vehiculoServicios.VehiculoId} no encontrado.");

        var serviciosEnLavado = new List<ServicioEnLavado>();
        var tiempoEstimadoTotal = 0;
        var orden = 0;
        foreach (var servicioItem in vehiculoServicios.Servicios)
        {
            var servicio = await _servicioService.ObtenerServicio(servicioItem.ServicioId);
            if (servicio == null)
                return new(Error: $"Servicio con ID {servicioItem.ServicioId} no encontrado.");

            if (!string.Equals(servicio.TipoVehiculo, vehiculo.TipoVehiculo, StringComparison.OrdinalIgnoreCase))
                return new(Error: $"El servicio '{servicio.Nombre}' no es compatible con el tipo de vehículo '{vehiculo.TipoVehiculo}'.");

            var servicioEnLavado = _lavadoService.CrearServicioEnLavado(
                servicio, orden++, servicioItem.PaqueteId, servicioItem.PaqueteNombre);
            serviciosEnLavado.Add(servicioEnLavado);
            tiempoEstimadoTotal += servicio.TiempoEstimado;
        }

        var descuento = request.Descuento;
        var resultadoPrecio = _lavadoService.CalcularPrecioCreacion(serviciosEnLavado, contexto.Paquetes, descuento);
        if (resultadoPrecio.Error != null)
            return new(Error: resultadoPrecio.Error);
        var precioTotal = resultadoPrecio.PrecioOriginal;
        var precioConDescuento = resultadoPrecio.Precio;

        var tipoVehiculo = await _tipoVehiculoService.ObtenerTipoVehiculoPorNombre(vehiculo.TipoVehiculo);
        var asignacion = await _lavadoService.PrepararAsignacionCreacion(tipoVehiculo?.CantidadEmpleadosRequeridos);
        var cantidadEmpleadosPorTipo = asignacion.CantidadEmpleadosRequeridos;
        var empleadosAsignados = asignacion.EmpleadosAsignados;

        var clienteTrajoId = request.ClienteTrajoId ?? cliente.Id;
        var clienteRetiraId = request.ClienteRetiraId ?? cliente.Id;
        var clienteTrajo = await _clienteService.ObtenerCliente(clienteTrajoId);
        var clienteRetira = await _clienteService.ObtenerCliente(clienteRetiraId);

        var lavado = new Lavado
        {
            Id = string.Empty,
            Estado = "EnProceso",
            ClienteId = cliente.Id,
            ClienteNombre = cliente.NombreCompleto,
            VehiculoId = vehiculo.Id,
            VehiculoPatente = vehiculo.Patente,
            TipoVehiculo = vehiculo.TipoVehiculo,
            Servicios = serviciosEnLavado,
            PrecioOriginal = precioTotal,
            Precio = precioConDescuento,
            Descuento = descuento,
            CantidadEmpleadosRequeridos = cantidadEmpleadosPorTipo,
            EmpleadosAsignadosIds = empleadosAsignados.Select(e => e.Id).ToList(),
            EmpleadosAsignadosNombres = empleadosAsignados.Select(e => e.NombreCompleto).ToList(),
            TiempoEstimado = tiempoEstimadoTotal,
            Notas = request.Notas,
            EstadoRetiro = "Pendiente",
            ClienteTrajoId = clienteTrajoId,
            ClienteTrajoNombre = clienteTrajo?.NombreCompleto ?? cliente.NombreCompleto,
            ClienteRetiraId = clienteRetiraId,
            ClienteRetiraNombre = clienteRetira?.NombreCompleto ?? cliente.NombreCompleto
        };

        var lavadoCreado = await _lavadoService.CrearLavado(lavado);
        await _lavadoService.IniciarPrimerServicioCreacion(lavadoCreado);
        return new(lavadoCreado);
    }
}

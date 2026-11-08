using Firebase.Models;

namespace Firebase.Services;

/// <summary>Coordinates client lifecycle cascades and their ordered audit snapshots.</summary>
public sealed class ClienteLifecycleService
{
    private readonly ClienteService _clienteService;
    private readonly VehiculoService _vehiculoService;
    private readonly IAuditWriter _auditService;

    public ClienteLifecycleService(ClienteService clients, VehiculoService vehicles, IAuditWriter audit)
    {
        _clienteService = clients;
        _vehiculoService = vehicles;
        _auditService = audit;
    }

    public async Task<string> Desactivar(string id, string userId, string userEmail)
    {
            // Obtener vehículos del cliente
            var vehiculos = await _vehiculoService.ObtenerVehiculosPorCliente(id);

            int vehiculosDesactivados = 0;
            int vehiculosCompartidosNoDesactivados = 0;

            // Procesar cada vehículo activo
            foreach (var vehiculo in vehiculos.Where(v => v.Estado == "Activo"))
            {
                // Verificar si el vehículo es compartido (tiene otros clientes activos)
                bool tieneOtrosClientesActivos = await TieneOtrosClientesActivos(vehiculo, id);

                if (tieneOtrosClientesActivos)
                {
                    // No desactivar el vehículo porque otros clientes activos lo usan
                    vehiculosCompartidosNoDesactivados++;
                }
                else
                {
                    // Desactivar el vehículo porque es exclusivo de este cliente (o compartido solo con clientes inactivos)
                    var antesVehiculo = AuditCampos.CapturarVehiculo(vehiculo);
                    await _vehiculoService.CambiarEstadoVehiculo(vehiculo.Id, "Inactivo");
                    await RegistrarVehiculo("Desactivacion de vehiculo (por desactivacion de cliente)", vehiculo.Id, antesVehiculo, userId, userEmail);
                    vehiculosDesactivados++;
                }
            }

            // Desactivar el cliente
            var antesCliente = AuditCampos.CapturarCliente(await _clienteService.ObtenerCliente(id));
            await _clienteService.CambiarEstadoCliente(id, "Inactivo");
            await RegistrarCliente("Desactivacion de cliente", id, antesCliente, userId, userEmail);

            // Construir mensaje informativo
            var mensajeParts = new List<string> { "Cliente desactivado correctamente." };

            if (vehiculosDesactivados > 0)
            {
                mensajeParts.Add($"Se desactivaron {vehiculosDesactivados} vehículo(s) exclusivo(s).");
            }

            if (vehiculosCompartidosNoDesactivados > 0)
            {
                mensajeParts.Add($"{vehiculosCompartidosNoDesactivados} vehículo(s) compartido(s) permanecen activos (usados por otros clientes).");
            }

            return string.Join(" ", mensajeParts);
    }

    public async Task<string> Reactivar(string id, string userId, string userEmail)
    {
            // Obtener vehículos del cliente
            var vehiculos = await _vehiculoService.ObtenerVehiculosPorCliente(id);

            int vehiculosReactivados = 0;
            int vehiculosYaActivos = 0;

            // Procesar cada vehículo inactivo
            foreach (var vehiculo in vehiculos.Where(v => v.Estado == "Inactivo"))
            {
                // Solo reactivar si el vehículo tenía algún dueño (no quedó huérfano)
                bool tieneAlgunCliente = !string.IsNullOrEmpty(vehiculo.ClienteId) ||
                                         (vehiculo.ClientesIds != null && vehiculo.ClientesIds.Any());

                if (tieneAlgunCliente)
                {
                    // Mantener las consultas de otros clientes antes de reactivar.
                    _ = await TieneOtrosClientesActivos(vehiculo, id);

                    // Reactivar el vehículo (ya sea porque es exclusivo de este cliente o compartido)
                    var antesVehiculo = AuditCampos.CapturarVehiculo(vehiculo);
                    await _vehiculoService.CambiarEstadoVehiculo(vehiculo.Id, "Activo");
                    await RegistrarVehiculo("Reactivacion de vehiculo (por reactivacion de cliente)", vehiculo.Id, antesVehiculo, userId, userEmail);
                    vehiculosReactivados++;
                }
            }

            // Contar vehículos que ya estaban activos (compartidos con otros clientes activos)
            vehiculosYaActivos = vehiculos.Count(v => v.Estado == "Activo");

            // Reactivar el cliente
            var antesCliente = AuditCampos.CapturarCliente(await _clienteService.ObtenerCliente(id));
            await _clienteService.CambiarEstadoCliente(id, "Activo");
            await RegistrarCliente("Reactivacion de cliente", id, antesCliente, userId, userEmail);

            // Construir mensaje informativo
            var mensajeParts = new List<string> { "Cliente reactivado correctamente." };

            if (vehiculosReactivados > 0)
            {
                mensajeParts.Add($"Se reactivaron {vehiculosReactivados} vehículo(s).");
            }

            if (vehiculosYaActivos > 0)
            {
                mensajeParts.Add($"{vehiculosYaActivos} vehículo(s) ya estaban activos (compartidos con otros clientes).");
            }

            return string.Join(" ", mensajeParts);
    }

    private async Task<bool> TieneOtrosClientesActivos(Vehiculo vehiculo, string id)
    {
        if (vehiculo.ClientesIds != null)
        {
            foreach (var clienteId in vehiculo.ClientesIds.Where(c => c != id))
            {
                var otroCliente = await _clienteService.ObtenerCliente(clienteId);
                if (otroCliente != null && otroCliente.Estado == "Activo")
                {
                    return true;
                }
            }
        }

        if (!string.IsNullOrEmpty(vehiculo.ClienteId) && vehiculo.ClienteId != id)
        {
            var clientePrincipal = await _clienteService.ObtenerCliente(vehiculo.ClienteId);
            return clientePrincipal != null && clientePrincipal.Estado == "Activo";
        }

        return false;
    }

    private async Task RegistrarCliente(string accion, string id, Dictionary<string, string> antes, string userId, string userEmail)
    {
        var cliente = await _clienteService.ObtenerCliente(id) ?? throw new InvalidOperationException("Cliente no encontrado después de guardar.");
        await _auditService.LogEvent(userId, userEmail, accion, id, "Cliente", antes, AuditCampos.CapturarCliente(cliente), cliente.NombreCompleto);
    }

    private async Task RegistrarVehiculo(string accion, string id, Dictionary<string, string> antes, string userId, string userEmail)
    {
        var vehiculo = await _vehiculoService.ObtenerVehiculo(id) ?? throw new InvalidOperationException("Vehículo no encontrado después de guardar.");
        await _auditService.LogEvent(userId, userEmail, accion, id, "Vehiculo", antes, AuditCampos.CapturarVehiculo(vehiculo), vehiculo.Patente);
    }
}

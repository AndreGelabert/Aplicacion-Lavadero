using Firebase.Models;

namespace Firebase.Services;

public sealed class ClienteVehicleService
{
    private readonly ClienteService _clients;
    private readonly VehiculoService _vehicles;
    private readonly IAuditWriter _audit;

    public ClienteVehicleService(ClienteService clients, VehiculoService vehicles, IAuditWriter audit)
    {
        _clients = clients;
        _vehicles = vehicles;
        _audit = audit;
    }

    public sealed record Resultado(string? ErrorKey = null, string? ErrorMessage = null, List<string>? Claves = null);

    public async Task<Resultado> Validar(Cliente cliente, List<ClienteVehiculoInput>? datos, bool crear)
    {
        if (datos == null || !datos.Any())
            return new("", crear ? "Debe agregar al menos un vehículo." : "Debe tener al menos un vehículo.");

        foreach (var data in datos.Where(item => item.EsNuevo))
            _ = VehicleCatalogueProposal.FromClientInput(data.TipoVehiculo, data.Marca, data.MarcaId,
                data.Modelo, data.MarcaManual, data.ModeloManual);

        foreach (var data in datos.Where(item => item.EsAsociacion))
            if (await _vehicles.ValidarClaveYObtenerVehiculo(data.Patente.Trim().ToUpperInvariant(), data.ClaveAsociacion ?? "") is null)
                return new("", "La patente o la clave de asociación no son válidas.");

        foreach (var data in datos.Where(item => !item.EsNuevo && !item.EsAsociacion))
        {
            var vehiculo = string.IsNullOrWhiteSpace(data.Id) ? null : await _vehicles.ObtenerVehiculo(data.Id);
            if (vehiculo is null || !PuedeVincularVehiculo(vehiculo, cliente.Id))
                return new("", "Uno o más vehículos ya están asociados a otro cliente.");
        }
        return new();
    }

    public async Task<Resultado> Guardar(Cliente cliente, List<ClienteVehiculoInput> datos, bool crear, string userId, string userEmail)
    {
        var vinculos = new List<ClienteService.VinculoWeb>();
        var claves = new List<string>();
        foreach (var data in datos)
        {
            var vinculo = new ClienteService.VinculoWeb { Id = data.Id ?? "", Patente = data.Patente, Clave = data.ClaveAsociacion ?? "", Asociacion = data.EsAsociacion, Color = data.Color, Tipo = data.TipoVehiculo };
            if (data.EsAsociacion && string.IsNullOrWhiteSpace(data.ClaveAsociacion))
            {
                var clave = VehiculoService.GenerarClaveAsociacion();
                vinculo.NuevaClaveHash = VehiculoService.HashClaveAsociacion(clave);
                claves.Add($"{data.Patente.ToUpperInvariant()}: {clave}");
            }
            if (data.EsNuevo)
            {
                var clave = VehiculoService.GenerarClaveAsociacion();
                vinculo.Nuevo = new Vehiculo { Id = "", Patente = data.Patente.ToUpperInvariant(), TipoVehiculo = data.TipoVehiculo, Marca = data.Marca, Modelo = data.Modelo, Color = data.Color, Estado = "Activo", ClaveAsociacionHash = VehiculoService.HashClaveAsociacion(clave) };
                vinculo.Propuesta = VehicleCatalogueProposal.FromClientInput(data.TipoVehiculo, data.Marca, data.MarcaId, data.Modelo, data.MarcaManual, data.ModeloManual);
                claves.Add($"{data.Patente.ToUpperInvariant()}: {clave}");
            }
            vinculos.Add(vinculo);
        }
        var auditorias = new List<ClienteService.AuditoriaVehiculo>();
        var auditoriasCliente = new List<ClienteService.AuditoriaCliente>();
        if (!await _clients.GuardarClienteConVehiculos(cliente, crear, vinculos, auditorias, auditoriasCliente))
            return new("", "No se pudo guardar. Verifique la patente y la clave; para quitar un vehículo compartido del titular, primero los demás deben desvincularse.");

        foreach (var auditoria in auditorias)
            await _audit.LogEvent(userId, userEmail, auditoria.Accion, auditoria.Id, "Vehiculo", auditoria.Antes, auditoria.Despues, auditoria.Nombre);
        var auditoriaCliente = auditoriasCliente.Single();
        await _audit.LogEvent(userId, userEmail, crear ? "Creacion de cliente" : "Actualizacion de cliente", cliente.Id, "Cliente", auditoriaCliente.Antes, auditoriaCliente.Despues, auditoriaCliente.Nombre);
        return new(Claves: claves);
    }

    public async Task<List<object>?> ObtenerVehiculos(string clienteId)
    {
        var cliente = await _clients.ObtenerCliente(clienteId);
        if (cliente == null) return null;
        var vehiculos = await _vehicles.ObtenerVehiculosPorCliente(clienteId);
        var resultado = new List<object>();
        foreach (var v in vehiculos)
        {
            bool esCompartido = false;
            int cantidadOtrosClientesActivos = 0;
            if (v.ClientesIds != null && v.ClientesIds.Any())
                foreach (var otroClienteId in v.ClientesIds.Where(c => c != clienteId))
                {
                    var otroCliente = await _clients.ObtenerCliente(otroClienteId);
                    if (otroCliente != null && otroCliente.Estado == "Activo")
                    {
                        esCompartido = true;
                        cantidadOtrosClientesActivos++;
                    }
                }
            if (!esCompartido && !string.IsNullOrEmpty(v.ClienteId) && v.ClienteId != clienteId)
            {
                var clientePrincipal = await _clients.ObtenerCliente(v.ClienteId);
                if (clientePrincipal != null && clientePrincipal.Estado == "Activo")
                {
                    esCompartido = true;
                    cantidadOtrosClientesActivos++;
                }
            }
            resultado.Add(new
            {
                id = v.Id, patente = v.Patente, marca = v.Marca, modelo = v.Modelo,
                color = v.Color, tipoVehiculo = v.TipoVehiculo, estado = v.Estado,
                esCompartido, esTitular = v.ClienteId == clienteId,
                tieneOtrosAsociados = v.ClientesIds?.Any(id => !string.IsNullOrWhiteSpace(id) && id != clienteId) == true,
                cantidadOtrosClientesActivos
            });
        }
        return resultado;
    }

    private static bool PuedeVincularVehiculo(Vehiculo vehiculo, string clienteId)
    {
        var asociadoACliente = vehiculo.ClienteId == clienteId || vehiculo.ClientesIds?.Contains(clienteId) == true;
        var sinPropietario = string.IsNullOrEmpty(vehiculo.ClienteId) &&
            (vehiculo.ClientesIds == null || vehiculo.ClientesIds.Count == 0);
        return asociadoACliente || sinPropietario;
    }
}

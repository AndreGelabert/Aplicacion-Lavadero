using Firebase.Models;
using Google.Cloud.Firestore;

/// <summary>
/// Servicio para la gestión de clientes en Firestore.
/// </summary>
public class ClienteService
{
    private readonly FirestoreDb _firestore;

    public ClienteService(FirestoreDb firestore)
    {
        _firestore = firestore;
    }

    /// <summary>
    /// Obtiene una lista paginada y filtrada de clientes.
    /// </summary>
    public async Task<List<Cliente>> ObtenerClientes(
        string searchTerm,
        int pageNumber,
        int pageSize,
        string sortBy,
        string sortOrder,
        List<string> estados = null)
    {
        var clientesRef = _firestore.Collection("clientes");
        var snapshot = await clientesRef.GetSnapshotAsync();
        var clientes = snapshot.Documents.Select(d => d.ConvertTo<Cliente>()).ToList();

        clientes = AplicarFiltros(clientes, searchTerm, estados);

        // Ordenamiento
        clientes = sortOrder?.ToLower() == "desc"
            ? clientes.OrderByDescending(c => GetPropValue(c, sortBy)).ToList()
            : clientes.OrderBy(c => GetPropValue(c, sortBy)).ToList();

        // Paginación
        return clientes
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();
    }

    /// <summary>
    /// Obtiene el total de clientes que coinciden con el filtro para paginación.
    /// </summary>
    public async Task<int> ObtenerTotalClientes(string searchTerm, List<string> estados = null)
    {
        var clientesRef = _firestore.Collection("clientes");
        var snapshot = await clientesRef.GetSnapshotAsync();
        var clientes = snapshot.Documents.Select(d => d.ConvertTo<Cliente>()).ToList();

        return AplicarFiltros(clientes, searchTerm, estados).Count;
    }

    private static List<Cliente> AplicarFiltros(List<Cliente> clientes, string searchTerm, List<string> estados)
    {
        // Filtrado compartido del listado y su contador
        if (estados != null && estados.Any())
        {
            clientes = clientes.Where(c => estados.Contains(c.Estado)).ToList();
        }
        else
        {
            clientes = clientes.Where(c => c.Estado == "Activo").ToList();
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            string term = searchTerm.ToLowerInvariant();
            clientes = clientes.Where(c =>
                c.Nombre.ToLowerInvariant().Contains(term) ||
                c.Apellido.ToLowerInvariant().Contains(term) ||
                c.NumeroDocumento.Contains(term) ||
                c.Email.ToLowerInvariant().Contains(term) ||
                (!string.IsNullOrEmpty(c.Telefono) && c.Telefono.Contains(term))
            ).ToList();
        }

        return clientes;
    }

    /// <summary>
    /// Cambia el estado de un cliente (Activar/Desactivar)
    /// </summary>
    public async Task CambiarEstadoCliente(string id, string nuevoEstado)
    {
        var cliente = await ObtenerCliente(id);
        if (cliente == null) throw new Exception("Cliente no encontrado");

        cliente.Estado = nuevoEstado;
        await ActualizarCliente(cliente);
    }

    public async Task<Cliente?> ObtenerCliente(string id)
    {
        var doc = await _firestore.Collection("clientes").Document(id).GetSnapshotAsync();
        return doc.Exists ? doc.ConvertTo<Cliente>() : null;
    }

    public async Task<Cliente?> ObtenerClientePorDocumento(string tipo, string numero)
    {
        var query = _firestore.Collection("clientes")
            .WhereEqualTo("TipoDocumento", tipo)
            .WhereEqualTo("NumeroDocumento", numero);

        var snapshot = await query.GetSnapshotAsync();
        return snapshot.Documents.Count > 0 ? snapshot.Documents[0].ConvertTo<Cliente>() : null;
    }

    public async Task CrearCliente(Cliente cliente)
    {
        var docRef = _firestore.Collection("clientes").Document();
        cliente.Id = docRef.Id;
        await docRef.SetAsync(cliente);
    }

    public async Task<bool> CrearClienteAsociado(Cliente cliente, string vehiculoId, string claveAsociacion)
    {
        var clienteRef = _firestore.Collection("clientes").Document();
        var vehiculoRef = _firestore.Collection("vehiculos").Document(vehiculoId);
        cliente.Id = clienteRef.Id;

        return await _firestore.RunTransactionAsync(async transaction =>
        {
            var snapshot = await transaction.GetSnapshotAsync(vehiculoRef);
            if (!snapshot.Exists) return false;

            var vehiculo = snapshot.ConvertTo<Vehiculo>();
            if (vehiculo.Estado != "Activo" ||
                !VehiculoService.ValidarClaveAsociacion(claveAsociacion, vehiculo.ClaveAsociacionHash))
                return false;

            transaction.Set(clienteRef, cliente);
            transaction.Update(vehiculoRef, new Dictionary<string, object>
            {
                ["ClientesIds"] = FieldValue.ArrayUnion(cliente.Id)
            });
            return true;
        });
    }

    public async Task<bool> CrearClienteYVincular(Cliente cliente, string vehiculoId, string hash, bool sinDueno)
    {
        if (string.IsNullOrWhiteSpace(vehiculoId)) return false;
        var clienteRef = _firestore.Collection("clientes").Document();
        var vehiculoRef = _firestore.Collection("vehiculos").Document(vehiculoId);
        cliente.Id = clienteRef.Id;
        return await _firestore.RunTransactionAsync(async tx =>
        {
            var snapshot = await tx.GetSnapshotAsync(vehiculoRef);
            if (!snapshot.Exists) return false;
            var vehiculo = snapshot.ConvertTo<Vehiculo>();
            if (sinDueno)
            {
                if (!VehiculoService.EsHuerfanoReclamable(vehiculo) || string.IsNullOrWhiteSpace(hash)) return false;
                vehiculo.ClienteId = cliente.Id;
                vehiculo.ClienteNombreCompleto = cliente.NombreCompleto;
                vehiculo.ClientesIds = new List<string> { cliente.Id };
                vehiculo.Estado = "Activo";
                vehiculo.ClaveAsociacionHash = hash;
            }
            else
            {
                if (vehiculo.Estado != "Activo" || string.IsNullOrEmpty(hash) || !string.Equals(hash, vehiculo.ClaveAsociacionHash, StringComparison.OrdinalIgnoreCase)) return false;
                vehiculo.ClientesIds ??= new List<string>();
                if (!vehiculo.ClientesIds.Contains(cliente.Id)) vehiculo.ClientesIds.Add(cliente.Id);
            }
            cliente.VehiculosIds = new List<string> { vehiculoId };
            tx.Create(clienteRef, cliente);
            tx.Set(vehiculoRef, vehiculo);
            return true;
        });
    }

    public sealed class VinculoWeb
    {
        public string Id { get; set; } = "";
        public string Patente { get; set; } = "";
        public string Clave { get; set; } = "";
        public bool Asociacion { get; set; }
        public string NuevaClaveHash { get; set; } = "";
        public string HuerfanoEsperadoId { get; set; } = "";
        public Vehiculo? Nuevo { get; set; }
        public Firebase.Services.VehicleCatalogueProposal? Propuesta { get; set; }
        public string Color { get; set; } = "";
        public string Tipo { get; set; } = "";
    }

    public sealed record AuditoriaVehiculo(string Accion, string Id, Dictionary<string, string> Antes, Dictionary<string, string> Despues, string Nombre);
    public sealed record AuditoriaCliente(Dictionary<string, string> Antes, Dictionary<string, string> Despues, string Nombre);

    public async Task<bool> GuardarClienteConVehiculos(Cliente cliente, bool crear, List<VinculoWeb> solicitados, List<AuditoriaVehiculo> auditorias, List<AuditoriaCliente> auditoriasCliente, bool conservarExistentes = false, bool permitirReclamarHuerfano = false)
    {
        if (solicitados.Count == 0) return false;
        var patentesNuevas = solicitados.Where(x => x.Nuevo != null).Select(x => x.Nuevo!.Patente.Trim().ToUpperInvariant()).ToList();
        if (patentesNuevas.Distinct().Count() != patentesNuevas.Count) return false;
        var clienteRef = crear ? _firestore.Collection("clientes").Document() : _firestore.Collection("clientes").Document(cliente.Id);
        if (crear) cliente.Id = clienteRef.Id;
        foreach (var item in solicitados.Where(x => x.Nuevo != null))
        {
            item.Nuevo!.Id = _firestore.Collection("vehiculos").Document().Id;
            item.Nuevo.ClienteId = cliente.Id;
            item.Nuevo.ClientesIds = new List<string> { cliente.Id };
        }
        var idsGuardados = new Dictionary<VinculoWeb, string>();
        var guardado = await _firestore.RunTransactionAsync(async tx =>
        {
            idsGuardados.Clear();
            auditorias.Clear();
            auditoriasCliente.Clear();
            var clienteSnapshot = await tx.GetSnapshotAsync(clienteRef);
            if (!crear && !clienteSnapshot.Exists) return false;
            var collection = _firestore.Collection("vehiculos");
            var vehiculos = new Dictionary<string, Vehiculo>();
            void Include(QuerySnapshot snapshot)
            {
                foreach (var doc in snapshot.Documents) vehiculos[doc.Id] = doc.ConvertTo<Vehiculo>();
            }
            // Discover inverse relationships even when the client's cached list is stale.
            Include(await tx.GetSnapshotAsync(collection.WhereEqualTo("ClienteId", cliente.Id)));
            Include(await tx.GetSnapshotAsync(collection.WhereArrayContains("ClientesIds", cliente.Id)));
            foreach (var id in solicitados.Where(x => x.Nuevo == null && !x.Asociacion)
                         .Select(x => x.Id).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct())
            {
                var doc = await tx.GetSnapshotAsync(collection.Document(id));
                if (doc.Exists) vehiculos[doc.Id] = doc.ConvertTo<Vehiculo>();
            }
            var guards = patentesNuevas.OrderBy(x => x, StringComparer.Ordinal)
                .Select(plate => VehiclePlateGuard.Reference(_firestore, plate)).ToList();
            foreach (var guard in guards) await tx.GetSnapshotAsync(guard);
            foreach (var plate in solicitados.Where(x => x.Nuevo != null || x.Asociacion)
                         .Select(x => VehiclePlateGuard.Normalize(x.Nuevo?.Patente ?? x.Patente)).Distinct())
                Include(await tx.GetSnapshotAsync(collection.WhereEqualTo("Patente", plate)));
            // Re-read each candidate document through the transaction before authorizing reuse.
            // Keep eligibility tied to this attempt's document version, not discovery state.
            foreach (var id in vehiculos.Values.Where(v => solicitados.Any(item => item.Nuevo != null &&
                         VehiclePlateGuard.Normalize(item.Nuevo.Patente) == v.Patente)).Select(v => v.Id).ToList())
            {
                var actual = await tx.GetSnapshotAsync(collection.Document(id));
                if (!actual.Exists) return false;
                vehiculos[id] = actual.ConvertTo<Vehiculo>();
            }
            if (conservarExistentes && clienteSnapshot.Exists)
                cliente = clienteSnapshot.ConvertTo<Cliente>();
            var seleccion = new Dictionary<string, VinculoWeb>();
            var reutilizados = new HashSet<string>();
            if (conservarExistentes)
                foreach (var v in vehiculos.Values.Where(v => v.ClienteId == cliente.Id || (v.ClientesIds?.Contains(cliente.Id) ?? false)))
                    seleccion[v.Id] = new VinculoWeb { Id = v.Id, Color = v.Color, Tipo = v.TipoVehiculo };
            foreach (var item in solicitados)
            {
                if (item.Nuevo != null)
                {
                    item.Nuevo.Patente = VehiclePlateGuard.Normalize(item.Nuevo.Patente);
                    var coincidencias = vehiculos.Values.Where(v => v.Patente == item.Nuevo.Patente).ToList();
                    if (!string.IsNullOrEmpty(item.HuerfanoEsperadoId) &&
                        (coincidencias.Count != 1 || coincidencias[0].Id != item.HuerfanoEsperadoId)) return false;
                    if (coincidencias.Count > 0)
                    {
                        if (!permitirReclamarHuerfano || coincidencias.Count != 1 || !VehiculoService.EsHuerfanoReclamable(coincidencias[0]) || string.IsNullOrEmpty(item.Nuevo.ClaveAsociacionHash)) return false;
                        var existente = coincidencias[0];
                        if (!seleccion.TryAdd(existente.Id, item)) return false;
                        reutilizados.Add(existente.Id);
                    }
                    else if (!seleccion.TryAdd(item.Nuevo.Id, item)) return false;
                    continue;
                }
                var v = item.Asociacion ? vehiculos.Values.FirstOrDefault(v => v.Patente == item.Patente.Trim().ToUpperInvariant()) : vehiculos.GetValueOrDefault(item.Id);
                if (v == null || !seleccion.TryAdd(v.Id, item)) return false;
                if (item.Asociacion)
                {
                    if (string.IsNullOrWhiteSpace(item.Clave))
                    {
                        if (!VehiculoService.EsHuerfanoReclamable(v) || string.IsNullOrEmpty(item.NuevaClaveHash)) return false;
                    }
                    else if (v.Estado != "Activo" || !VehiculoService.ValidarClaveAsociacion(item.Clave, v.ClaveAsociacionHash ?? "")) return false;
                }
                else if (v.ClienteId != cliente.Id && !(v.ClientesIds?.Contains(cliente.Id) ?? false)) return false;
            }
            var removidos = vehiculos.Values.Where(v => (v.ClienteId == cliente.Id || (v.ClientesIds?.Contains(cliente.Id) ?? false)) && !seleccion.ContainsKey(v.Id)).ToList();
            if (removidos.Any(v => v.ClienteId == cliente.Id && (v.ClientesIds?.Any(id => !string.IsNullOrWhiteSpace(id) && id != cliente.Id) ?? false))) return false;
            foreach (var guard in guards) VehiclePlateGuard.Touch(tx, guard);
            foreach (var v in removidos)
            {
                var antesVehiculo = AuditCampos.CapturarVehiculo(v);
                v.ClientesIds ??= new List<string>();
                v.ClientesIds.RemoveAll(id => id == cliente.Id);
                if (v.ClienteId == cliente.Id)
                {
                    v.ClienteId = "";
                    v.ClienteNombreCompleto = null;
                }
                if (string.IsNullOrEmpty(v.ClienteId) && !v.ClientesIds.Any()) { v.Estado = "Inactivo"; v.ClaveAsociacionHash = null; }
                auditorias.Add(new AuditoriaVehiculo("Vehiculo desvinculado de cliente", v.Id, antesVehiculo, AuditCampos.CapturarVehiculo(v), v.Patente));
                tx.Set(_firestore.Collection("vehiculos").Document(v.Id), v);
            }
            foreach (var (id, item) in seleccion)
            {
                if (item.Nuevo != null) idsGuardados[item] = id;
                var reutilizado = reutilizados.Contains(id);
                var v = reutilizado ? vehiculos[id] : item.Nuevo ?? vehiculos[id];
                var antesVehiculo = item.Nuevo != null && !reutilizado ? new Dictionary<string, string>() : AuditCampos.CapturarVehiculo(v);
                if (reutilizado)
                {
                    v.Marca = item.Nuevo!.Marca; v.Modelo = item.Nuevo.Modelo;
                    v.Color = item.Nuevo.Color; v.TipoVehiculo = item.Nuevo.TipoVehiculo;
                    v.ClaveAsociacionHash = item.Nuevo.ClaveAsociacionHash;
                }
                else if (item.Asociacion && string.IsNullOrWhiteSpace(item.Clave)) v.ClaveAsociacionHash = item.NuevaClaveHash;
                v.ClientesIds ??= new List<string>();
                if (!v.ClientesIds.Contains(cliente.Id)) v.ClientesIds.Add(cliente.Id);
                if (string.IsNullOrEmpty(v.ClienteId)) { v.ClienteId = cliente.Id; v.Estado = "Activo"; }
                if (v.ClienteId == cliente.Id) v.ClienteNombreCompleto = cliente.NombreCompleto;
                if (!item.Asociacion && item.Nuevo == null) { v.Color = item.Color; v.TipoVehiculo = item.Tipo; }
                if (item.Nuevo != null || item.Asociacion)
                    auditorias.Add(new AuditoriaVehiculo(item.Nuevo != null ? "Creacion de vehiculo" : "Asociacion de cliente a vehiculo compartido", id, antesVehiculo, AuditCampos.CapturarVehiculo(v), v.Patente));
                var vref = _firestore.Collection("vehiculos").Document(id);
                if (item.Nuevo != null && !reutilizado) tx.Create(vref, v); else tx.Set(vref, v);
                if (item.Propuesta != null)
                {
                    item.Propuesta.VehiculoId = id;
                    if (reutilizado) tx.Set(_firestore.Collection("vehicleCatalogueProposals").Document(id), item.Propuesta);
                    else tx.Create(_firestore.Collection("vehicleCatalogueProposals").Document(id), item.Propuesta);
                }
            }
            cliente.VehiculosIds = seleccion.Keys.ToList();
            if (!crear) cliente.Estado = clienteSnapshot.ConvertTo<Cliente>().Estado;
            auditoriasCliente.Add(new AuditoriaCliente(crear ? new() : AuditCampos.CapturarCliente(clienteSnapshot.ConvertTo<Cliente>()), AuditCampos.CapturarCliente(cliente), cliente.NombreCompleto));
            if (crear) tx.Create(clienteRef, cliente); else tx.Set(clienteRef, cliente);
            return true;
        });
        // Publish resolved IDs only after a successful commit, never during a retried attempt.
        if (guardado)
            foreach (var (item, id) in idsGuardados) item.Nuevo!.Id = id;
        return guardado;
    }

    public async Task ActualizarCliente(Cliente cliente)
    {
        var docRef = _firestore.Collection("clientes").Document(cliente.Id);
        await docRef.SetAsync(cliente, SetOptions.Overwrite);
    }

    public async Task EliminarCliente(string id)
    {
        await _firestore.Collection("clientes").Document(id).DeleteAsync();
    }

    /// <summary>
    /// Verifica si un tipo de documento está siendo usado por algún cliente.
    /// Útil para prevenir la eliminación de tipos en uso.
    /// </summary>
    /// <param name="tipoDocumento">Nombre del tipo de documento a verificar</param>
    /// <returns>True si hay al menos un cliente usando este tipo de documento</returns>
    public async Task<bool> ExisteTipoDocumentoEnUso(string tipoDocumento)
    {
        if (string.IsNullOrWhiteSpace(tipoDocumento))
            return false;

        var clientesRef = _firestore.Collection("clientes");
        var query = clientesRef.WhereEqualTo("TipoDocumento", tipoDocumento).Limit(1);
        var snapshot = await query.GetSnapshotAsync();
        return snapshot.Count > 0;
    }

    private object GetPropValue(object src, string propName)
    {
        if (src == null) return null;
        if (string.IsNullOrEmpty(propName)) return null;

        if (propName == "NombreCompleto") return ((Cliente)src).NombreCompleto;

        return src.GetType().GetProperty(propName)?.GetValue(src, null);
    }
}

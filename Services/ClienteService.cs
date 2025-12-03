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

        // Filtrado por estado (por defecto solo Activos)
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
                c.Email.ToLowerInvariant().Contains(term)
            ).ToList();
        }

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

        // Filtrado por estado
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
                c.Email.ToLowerInvariant().Contains(term)
            ).ToList();
        }

        return clientes.Count;
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

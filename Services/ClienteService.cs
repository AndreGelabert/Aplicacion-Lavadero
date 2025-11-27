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
        string sortOrder)
    {
        var clientesRef = _firestore.Collection("clientes");
        Query query = clientesRef;

        // Nota: Firestore no soporta búsqueda parcial nativa eficiente (LIKE %term%).
        // Se traerán todos y se filtrará en memoria si hay término de búsqueda,
        // o se usará un índice específico si el volumen crece.
        // Para este MVP, filtro en memoria post-fetch si hay búsqueda,
        // pero para paginación sin búsqueda usamos cursores.

        // Sin embargo, para mantener consistencia con ServicioService y dado que
        // la colección no será masiva inmediatamente, traeremos todo y filtraremos/paginaremos en memoria
        // para simplificar la lógica de búsqueda compleja (nombre, apellido, dni).

        var snapshot = await clientesRef.GetSnapshotAsync();
        var clientes = snapshot.Documents.Select(d => d.ConvertTo<Cliente>()).ToList();

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
    public async Task<int> ObtenerTotalClientes(string searchTerm)
    {
        var clientesRef = _firestore.Collection("clientes");
        var snapshot = await clientesRef.GetSnapshotAsync();
        var clientes = snapshot.Documents.Select(d => d.ConvertTo<Cliente>()).ToList();

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

    private object GetPropValue(object src, string propName)
    {
        if (src == null) return null;
        if (string.IsNullOrEmpty(propName)) return null;

        if (propName == "NombreCompleto") return ((Cliente)src).NombreCompleto;

        return src.GetType().GetProperty(propName)?.GetValue(src, null);
    }
}

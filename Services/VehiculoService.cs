using Firebase.Models;
using Google.Cloud.Firestore;

/// <summary>
/// Servicio para la gestión de vehículos en Firestore.
/// </summary>
public class VehiculoService
{
    private readonly FirestoreDb _firestore;

    public VehiculoService(FirestoreDb firestore)
    {
        _firestore = firestore;
    }

    public async Task<List<Vehiculo>> ObtenerVehiculos(
        string searchTerm,
        string tipoVehiculo,
        int pageNumber,
        int pageSize,
        string sortBy,
        string sortOrder,
        List<string> estados = null)
    {
        var vehiculosRef = _firestore.Collection("vehiculos");
        var snapshot = await vehiculosRef.GetSnapshotAsync();
        var vehiculos = snapshot.Documents.Select(d => d.ConvertTo<Vehiculo>()).ToList();

        // Filtrado por estado (por defecto solo Activos)
        if (estados != null && estados.Any())
        {
            vehiculos = vehiculos.Where(v => estados.Contains(v.Estado)).ToList();
        }
        else
        {
            vehiculos = vehiculos.Where(v => v.Estado == "Activo").ToList();
        }

        // Filtrado en memoria
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            string term = searchTerm.ToLowerInvariant();
            vehiculos = vehiculos.Where(v =>
                v.Patente.ToLowerInvariant().Contains(term) ||
                v.Marca.ToLowerInvariant().Contains(term) ||
                v.Modelo.ToLowerInvariant().Contains(term) ||
                (v.ClienteNombreCompleto != null && v.ClienteNombreCompleto.ToLowerInvariant().Contains(term))
            ).ToList();
        }

        if (!string.IsNullOrWhiteSpace(tipoVehiculo))
        {
            vehiculos = vehiculos.Where(v => v.TipoVehiculo == tipoVehiculo).ToList();
        }

        // Ordenamiento
        vehiculos = sortOrder?.ToLower() == "desc"
            ? vehiculos.OrderByDescending(v => GetPropValue(v, sortBy)).ToList()
            : vehiculos.OrderBy(v => GetPropValue(v, sortBy)).ToList();

        // Paginación
        return vehiculos
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();
    }

    public async Task<int> ObtenerTotalVehiculos(string searchTerm, string tipoVehiculo, List<string> estados = null)
    {
        var vehiculosRef = _firestore.Collection("vehiculos");
        var snapshot = await vehiculosRef.GetSnapshotAsync();
        var vehiculos = snapshot.Documents.Select(d => d.ConvertTo<Vehiculo>()).ToList();

        // Filtrado por estado
        if (estados != null && estados.Any())
        {
            vehiculos = vehiculos.Where(v => estados.Contains(v.Estado)).ToList();
        }
        else
        {
            vehiculos = vehiculos.Where(v => v.Estado == "Activo").ToList();
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            string term = searchTerm.ToLowerInvariant();
            vehiculos = vehiculos.Where(v =>
                v.Patente.ToLowerInvariant().Contains(term) ||
                v.Marca.ToLowerInvariant().Contains(term) ||
                v.Modelo.ToLowerInvariant().Contains(term) ||
                (v.ClienteNombreCompleto != null && v.ClienteNombreCompleto.ToLowerInvariant().Contains(term))
            ).ToList();
        }

        if (!string.IsNullOrWhiteSpace(tipoVehiculo))
        {
            vehiculos = vehiculos.Where(v => v.TipoVehiculo == tipoVehiculo).ToList();
        }

        return vehiculos.Count;
    }

    public async Task<Vehiculo?> ObtenerVehiculo(string id)
    {
        var doc = await _firestore.Collection("vehiculos").Document(id).GetSnapshotAsync();
        return doc.Exists ? doc.ConvertTo<Vehiculo>() : null;
    }

    public async Task<Vehiculo?> ObtenerVehiculoPorPatente(string patente)
    {
        var query = _firestore.Collection("vehiculos").WhereEqualTo("Patente", patente);
        var snapshot = await query.GetSnapshotAsync();
        return snapshot.Documents.Count > 0 ? snapshot.Documents[0].ConvertTo<Vehiculo>() : null;
    }

    public async Task<List<Vehiculo>> ObtenerVehiculosPorCliente(string clienteId)
    {
        var query = _firestore.Collection("vehiculos").WhereEqualTo("ClienteId", clienteId);
        var snapshot = await query.GetSnapshotAsync();
        return snapshot.Documents.Select(d => d.ConvertTo<Vehiculo>()).ToList();
    }

    // Nuevo método para obtener vehículos disponibles (sin dueño o todos, según necesidad)
    // Para el dropdown de clientes, queremos ver todos, o filtrar.
    public async Task<List<Vehiculo>> ObtenerTodosVehiculos()
    {
        var snapshot = await _firestore.Collection("vehiculos").GetSnapshotAsync();
        return snapshot.Documents.Select(d => d.ConvertTo<Vehiculo>()).ToList();
    }

    /// <summary>
    /// Obtiene vehículos disponibles (Activos y sin dueño asignado)
    /// </summary>
    public async Task<List<Vehiculo>> ObtenerVehiculosDisponibles()
    {
        var snapshot = await _firestore.Collection("vehiculos").GetSnapshotAsync();
        return snapshot.Documents
            .Select(d => d.ConvertTo<Vehiculo>())
            .Where(v => v.Estado == "Activo" && string.IsNullOrEmpty(v.ClienteId))
            .ToList();
    }

    /// <summary>
    /// Cambia el estado de un vehículo (Activar/Desactivar)
    /// </summary>
    public async Task CambiarEstadoVehiculo(string id, string nuevoEstado)
    {
        var vehiculo = await ObtenerVehiculo(id);
        if (vehiculo == null) throw new Exception("Vehículo no encontrado");

        vehiculo.Estado = nuevoEstado;
        await ActualizarVehiculo(vehiculo);
    }

    public async Task CrearVehiculo(Vehiculo vehiculo)
    {
        var docRef = _firestore.Collection("vehiculos").Document();
        vehiculo.Id = docRef.Id;
        await docRef.SetAsync(vehiculo);
    }

    public async Task ActualizarVehiculo(Vehiculo vehiculo)
    {
        var docRef = _firestore.Collection("vehiculos").Document(vehiculo.Id);
        await docRef.SetAsync(vehiculo, SetOptions.Overwrite);
    }

    public async Task EliminarVehiculo(string id)
    {
        await _firestore.Collection("vehiculos").Document(id).DeleteAsync();
    }

    private object GetPropValue(object src, string propName)
    {
        if (src == null) return null;
        if (string.IsNullOrEmpty(propName)) return null;

        return src.GetType().GetProperty(propName)?.GetValue(src, null);
    }
}

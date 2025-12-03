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
        List<string> tiposVehiculo,
        List<string> marcas,
        List<string> colores,
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

        // Filtrado en memoria por búsqueda
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

        // Filtrado por tipos de vehículo
        if (tiposVehiculo != null && tiposVehiculo.Any())
        {
            vehiculos = vehiculos.Where(v => tiposVehiculo.Contains(v.TipoVehiculo)).ToList();
        }

        // Filtrado por marcas
        if (marcas != null && marcas.Any())
        {
            vehiculos = vehiculos.Where(v => marcas.Contains(v.Marca)).ToList();
        }

        // Filtrado por colores
        if (colores != null && colores.Any())
        {
            vehiculos = vehiculos.Where(v => colores.Contains(v.Color)).ToList();
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

    public async Task<int> ObtenerTotalVehiculos(string searchTerm, List<string> tiposVehiculo, List<string> marcas, List<string> colores, List<string> estados = null)
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

        if (tiposVehiculo != null && tiposVehiculo.Any())
        {
            vehiculos = vehiculos.Where(v => tiposVehiculo.Contains(v.TipoVehiculo)).ToList();
        }

        if (marcas != null && marcas.Any())
        {
            vehiculos = vehiculos.Where(v => marcas.Contains(v.Marca)).ToList();
        }

        if (colores != null && colores.Any())
        {
            vehiculos = vehiculos.Where(v => colores.Contains(v.Color)).ToList();
        }

        return vehiculos.Count;
    }

    public async Task<List<string>> ObtenerMarcasUnicas()
    {
        var snapshot = await _firestore.Collection("vehiculos").GetSnapshotAsync();
        var marcas = snapshot.Documents
            .Select(d => d.ConvertTo<Vehiculo>().Marca)
            .Distinct()
            .OrderBy(m => m)
            .ToList();
        return marcas;
    }

    public async Task<List<string>> ObtenerColoresUnicos()
    {
        var snapshot = await _firestore.Collection("vehiculos").GetSnapshotAsync();
        var colores = snapshot.Documents
            .Select(d => d.ConvertTo<Vehiculo>().Color)
            .Distinct()
            .OrderBy(c => c)
            .ToList();
        return colores;
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

    /// <summary>
    /// Verifica si un tipo de vehículo está siendo usado por algún vehículo.
    /// Útil para prevenir la eliminación de tipos en uso.
    /// </summary>
    /// <param name="tipoVehiculo">Nombre del tipo de vehículo a verificar</param>
    /// <returns>True si hay al menos un vehículo usando este tipo</returns>
    public async Task<bool> ExisteTipoVehiculoEnUso(string tipoVehiculo)
    {
        if (string.IsNullOrWhiteSpace(tipoVehiculo))
            return false;

        var vehiculosRef = _firestore.Collection("vehiculos");
        var query = vehiculosRef.WhereEqualTo("TipoVehiculo", tipoVehiculo).Limit(1);
        var snapshot = await query.GetSnapshotAsync();
        return snapshot.Count > 0;
    }

    private object GetPropValue(object src, string propName)
    {
        if (src == null) return null;
        if (string.IsNullOrEmpty(propName)) return null;

        return src.GetType().GetProperty(propName)?.GetValue(src, null);
    }
}

using Firebase.Models;
using Google.Cloud.Firestore;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Servicio para la gestión de vehículos en Firestore.
/// Soporta vehículos con múltiples clientes asociados mediante clave de asociación.
/// </summary>
public class VehiculoService
{
    private readonly FirestoreDb _firestore;

    public VehiculoService(FirestoreDb firestore)
    {
        _firestore = firestore;
    }

    /// <summary>
    /// Genera una clave de asociación aleatoria de 8 caracteres alfanuméricos.
    /// Formato: XXXX-XXXX (letras mayúsculas y números, sin caracteres confusos como O, 0, I, 1, L).
    /// </summary>
    /// <returns>La clave en texto plano (debe mostrarse solo una vez al usuario).</returns>
    public static string GenerarClaveAsociacion()
    {
        // Caracteres sin ambigüedades (sin O, 0, I, 1, L)
        const string chars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[8];
        rng.GetBytes(bytes);
        
        var result = new StringBuilder(9); // 8 chars + 1 hyphen
        for (int i = 0; i < 8; i++)
        {
            if (i == 4) result.Append('-');
            result.Append(chars[bytes[i] % chars.Length]);
        }
        return result.ToString();
    }

    /// <summary>
    /// Genera el hash SHA256 de una clave de asociación.
    /// </summary>
    /// <param name="claveTextoPlano">La clave en texto plano.</param>
    /// <returns>El hash SHA256 en formato hexadecimal.</returns>
    public static string HashClaveAsociacion(string claveTextoPlano)
    {
        if (string.IsNullOrEmpty(claveTextoPlano)) return string.Empty;
        
        // Normalizar: quitar guiones y convertir a mayúsculas
        var claveNormalizada = claveTextoPlano.Replace("-", "").ToUpperInvariant();
        
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(claveNormalizada));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Verifica si una clave de asociación coincide con el hash almacenado.
    /// </summary>
    /// <param name="claveTextoPlano">La clave ingresada por el usuario.</param>
    /// <param name="hashAlmacenado">El hash almacenado en el vehículo.</param>
    /// <returns>True si la clave es válida.</returns>
    public static bool ValidarClaveAsociacion(string claveTextoPlano, string hashAlmacenado)
    {
        if (string.IsNullOrEmpty(claveTextoPlano) || string.IsNullOrEmpty(hashAlmacenado))
            return false;
        
        var hashCalculado = HashClaveAsociacion(claveTextoPlano);
        return string.Equals(hashCalculado, hashAlmacenado, StringComparison.OrdinalIgnoreCase);
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
        // Buscar vehículos donde el cliente es el dueño principal o está en la lista de clientes asociados
        var snapshot = await _firestore.Collection("vehiculos").GetSnapshotAsync();
        var vehiculos = snapshot.Documents
            .Select(d => d.ConvertTo<Vehiculo>())
            .Where(v => v.ClienteId == clienteId || 
                       (v.ClientesIds != null && v.ClientesIds.Contains(clienteId)))
            .ToList();
        return vehiculos;
    }

    /// <summary>
    /// Obtiene todos los vehículos activos disponibles para asociación (para dropdown de asociación).
    /// </summary>
    public async Task<List<Vehiculo>> ObtenerVehiculosParaAsociacion()
    {
        var snapshot = await _firestore.Collection("vehiculos").GetSnapshotAsync();
        return snapshot.Documents
            .Select(d => d.ConvertTo<Vehiculo>())
            .Where(v => v.Estado == "Activo" && !string.IsNullOrEmpty(v.ClaveAsociacionHash))
            .ToList();
    }

    /// <summary>
    /// Valida la clave de asociación para un vehículo específico.
    /// </summary>
    /// <param name="patente">La patente del vehículo.</param>
    /// <param name="claveTextoPlano">La clave de asociación ingresada.</param>
    /// <returns>El vehículo si la clave es válida, null en caso contrario.</returns>
    public async Task<Vehiculo?> ValidarClaveYObtenerVehiculo(string patente, string claveTextoPlano)
    {
        var vehiculo = await ObtenerVehiculoPorPatente(patente);
        if (vehiculo == null) return null;
        
        if (string.IsNullOrEmpty(vehiculo.ClaveAsociacionHash)) return null;
        
        if (!ValidarClaveAsociacion(claveTextoPlano, vehiculo.ClaveAsociacionHash)) return null;
        
        return vehiculo;
    }

    /// <summary>
    /// Asocia un cliente a un vehículo existente.
    /// </summary>
    /// <param name="vehiculoId">ID del vehículo.</param>
    /// <param name="clienteId">ID del cliente a asociar.</param>
    /// <returns>True si la asociación fue exitosa.</returns>
    public async Task<bool> AsociarClienteAVehiculo(string vehiculoId, string clienteId)
    {
        var vehiculo = await ObtenerVehiculo(vehiculoId);
        if (vehiculo == null) return false;
        
        // Verificar que el cliente no esté ya asociado
        if (vehiculo.ClientesIds == null)
        {
            vehiculo.ClientesIds = new List<string>();
        }
        
        if (!vehiculo.ClientesIds.Contains(clienteId))
        {
            vehiculo.ClientesIds.Add(clienteId);
            await ActualizarVehiculo(vehiculo);
        }
        
        return true;
    }

    /// <summary>
    /// Desvincula un cliente de un vehículo.
    /// </summary>
    /// <param name="vehiculoId">ID del vehículo.</param>
    /// <param name="clienteId">ID del cliente a desvincular.</param>
    /// <returns>True si la desvinculación fue exitosa.</returns>
    public async Task<bool> DesvincularClienteDeVehiculo(string vehiculoId, string clienteId)
    {
        var vehiculo = await ObtenerVehiculo(vehiculoId);
        if (vehiculo == null) return false;
        
        // Si es el cliente principal, limpiar ClienteId y ClienteNombreCompleto
        if (vehiculo.ClienteId == clienteId)
        {
            vehiculo.ClienteId = "";
            vehiculo.ClienteNombreCompleto = null;
        }
        
        // Remover de la lista de clientes asociados
        if (vehiculo.ClientesIds != null && vehiculo.ClientesIds.Contains(clienteId))
        {
            vehiculo.ClientesIds.Remove(clienteId);
        }
        
        // Si no quedan clientes asociados, desactivar el vehículo
        bool tieneClientes = !string.IsNullOrEmpty(vehiculo.ClienteId) || 
                            (vehiculo.ClientesIds != null && vehiculo.ClientesIds.Any());
        
        if (!tieneClientes)
        {
            vehiculo.Estado = "Inactivo";
        }
        
        await ActualizarVehiculo(vehiculo);
        return true;
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

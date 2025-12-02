using Firebase.Models;
using Google.Cloud.Firestore;

/// <summary>
/// Servicio para la gestión de tipos de vehículo en Firestore.
/// Proporciona operaciones CRUD para los tipos de vehículo disponibles en el lavadero.
/// </summary>
/// <remarks>
/// Los tipos de vehículo se almacenan en la colección "tiposVehiculos" de Firestore.
/// Ejemplos de tipos: "Automóvil", "SUV", "Camioneta", "Motocicleta", etc.
/// Se utilizan para categorizar los servicios según el tipo de vehículo al que aplican.
/// </remarks>
public class TipoVehiculoService
{
    #region Dependencias

    private readonly FirestoreDb _firestore;

    /// <summary>
    /// Inicializa una nueva instancia del servicio de tipos de vehículo.
    /// </summary>
    /// <param name="firestore">Instancia de la base de datos Firestore.</param>
    public TipoVehiculoService(FirestoreDb firestore)
    {
        _firestore = firestore;
    }

    #endregion

    #region Operaciones de Consulta

    /// <summary>
    /// Obtiene todos los tipos de vehículo registrados (solo nombres).
    /// </summary>
    /// <returns>Lista de nombres de tipos de vehículo. Retorna lista vacía si hay error.</returns>
    public async Task<List<string>> ObtenerTiposVehiculos()
    {
        try
        {
            var snapshot = await _firestore.Collection("tiposVehiculos").GetSnapshotAsync();
            var tipos = snapshot.Documents.Select(doc => doc.GetValue<string>("Nombre")).ToList();

            return tipos ?? new List<string>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al obtener tipos de servicio: {ex.Message}");
            return new List<string>();
        }
    }

    /// <summary>
    /// Obtiene todos los tipos de vehículo con sus formatos de patente.
    /// </summary>
    /// <returns>Lista completa de tipos de vehículo con todos sus campos.</returns>
    public async Task<List<TipoVehiculo>> ObtenerTiposVehiculosCompletos()
    {
        try
        {
            var snapshot = await _firestore.Collection("tiposVehiculos").GetSnapshotAsync();
            var tipos = new List<TipoVehiculo>();
            
            foreach (var doc in snapshot.Documents)
            {
                tipos.Add(new TipoVehiculo
                {
                    Id = doc.Id,
                    Nombre = doc.GetValue<string>("Nombre") ?? "",
                    FormatoPatente = doc.ContainsField("FormatoPatente") ? doc.GetValue<string>("FormatoPatente") : null
                });
            }
            
            return tipos;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al obtener tipos de vehículo: {ex.Message}");
            return new List<TipoVehiculo>();
        }
    }

    /// <summary>
    /// Obtiene un tipo de vehículo por su nombre.
    /// </summary>
    /// <param name="nombre">Nombre del tipo de vehículo.</param>
    /// <returns>El tipo de vehículo o null si no existe.</returns>
    public async Task<TipoVehiculo?> ObtenerTipoVehiculoPorNombre(string nombre)
    {
        try
        {
            var query = _firestore.Collection("tiposVehiculos").WhereEqualTo("Nombre", nombre);
            var snapshot = await query.GetSnapshotAsync();
            
            if (snapshot.Documents.Count == 0)
                return null;

            var doc = snapshot.Documents.First();
            return new TipoVehiculo
            {
                Id = doc.Id,
                Nombre = doc.GetValue<string>("Nombre") ?? "",
                FormatoPatente = doc.ContainsField("FormatoPatente") ? doc.GetValue<string>("FormatoPatente") : null
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al obtener tipo de vehículo: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Obtiene el nombre de un tipo de vehículo por su ID.
    /// </summary>
    /// <param name="id">ID del documento en Firestore.</param>
    /// <returns>Nombre del tipo de vehículo o null si no existe.</returns>
    public async Task<string?> ObtenerNombrePorId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var doc = await _firestore.Collection("tiposVehiculos").Document(id).GetSnapshotAsync();
        return doc.Exists && doc.ContainsField("Nombre") ? doc.GetValue<string>("Nombre") : null;
    }

    /// <summary>
    /// Verifica si existe un tipo de vehículo con el nombre especificado.
    /// La comparación ignora espacios y mayúsculas/minúsculas.
    /// </summary>
    /// <param name="nombre">Nombre del tipo de vehículo a verificar.</param>
    /// <returns>True si existe, false en caso contrario.</returns>
    public async Task<bool> ExisteTipoVehiculo(string nombre)
    {
        var tiposExistentes = await ObtenerTiposVehiculos();
        string nombreNormalizado = nombre.Replace(" ", "").ToLowerInvariant();

        return tiposExistentes.Any(t =>
            t.Replace(" ", "").ToLowerInvariant() == nombreNormalizado);
    }

    #endregion

    #region Operaciones CRUD

    /// <summary>
    /// Crea un nuevo tipo de vehículo en la base de datos.
    /// </summary>
    /// <param name="nombre">Nombre del tipo de vehículo a crear.</param>
    /// <param name="formatoPatente">Formato de patente opcional (ej: "llnnnll|lllnnn").</param>
    /// <returns>ID del documento creado en Firestore.</returns>
    public async Task<string> CrearTipoVehiculo(string nombre, string? formatoPatente = null)
    {
        var tipoRef = _firestore.Collection("tiposVehiculos").Document();
        var tipo = new Dictionary<string, object>
        {
            { "Nombre", nombre }
        };
        
        if (!string.IsNullOrWhiteSpace(formatoPatente))
        {
            tipo["FormatoPatente"] = formatoPatente;
        }
        
        await tipoRef.SetAsync(tipo);
        return tipoRef.Id;
    }

    /// <summary>
    /// Elimina todos los tipos de vehículo con el nombre especificado.
    /// Puede haber múltiples documentos con el mismo nombre.
    /// </summary>
    /// <param name="nombre">Nombre del tipo de vehículo a eliminar.</param>
    /// <returns>Lista de IDs de los documentos eliminados.</returns>
    public async Task<List<string>> EliminarTipoVehiculo(string nombre)
    {
        var tiposRef = _firestore.Collection("tiposVehiculos");
        var query = tiposRef.WhereEqualTo("Nombre", nombre);
        var snapshot = await query.GetSnapshotAsync();

        var eliminados = new List<string>();
        foreach (var documento in snapshot.Documents)
        {
            eliminados.Add(documento.Id);
            await documento.Reference.DeleteAsync();
        }
        return eliminados;
    }

    #endregion
}
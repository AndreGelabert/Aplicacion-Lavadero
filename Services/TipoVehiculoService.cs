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
    /// Obtiene todos los tipos de vehículo registrados.
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
    /// <returns>ID del documento creado en Firestore.</returns>
    public async Task<string> CrearTipoVehiculo(string nombre)
    {
        var tipoRef = _firestore.Collection("tiposVehiculos").Document();
        var tipo = new Dictionary<string, object>
        {
            { "Nombre", nombre }
        };
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
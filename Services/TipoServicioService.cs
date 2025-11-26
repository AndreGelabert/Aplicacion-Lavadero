using Google.Cloud.Firestore;

/// <summary>
/// Servicio para la gestión de tipos de servicio en Firestore.
/// Proporciona operaciones CRUD para los tipos de servicio disponibles en el lavadero.
/// </summary>
/// <remarks>
/// Los tipos de servicio se almacenan en la colección "tiposServicio" de Firestore.
/// Ejemplos de tipos: "Lavado Básico", "Lavado Premium", "Encerado", etc.
/// Se utilizan para categorizar los servicios y facilitar el filtrado.
/// </remarks>
public class TipoServicioService
{
    #region Dependencias

    private readonly FirestoreDb _firestore;

    /// <summary>
    /// Inicializa una nueva instancia del servicio de tipos de servicio.
    /// </summary>
    /// <param name="firestore">Instancia de la base de datos Firestore.</param>
    public TipoServicioService(FirestoreDb firestore)
    {
        _firestore = firestore;
    }

    #endregion

    #region Operaciones de Consulta

    /// <summary>
    /// Obtiene todos los tipos de servicio registrados.
    /// </summary>
    /// <returns>Lista de nombres de tipos de servicio. Retorna lista vacía si hay error.</returns>
    public async Task<List<string>> ObtenerTiposServicio()
    {
        try
        {
            var snapshot = await _firestore.Collection("tiposServicio").GetSnapshotAsync();
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
    /// Obtiene el nombre de un tipo de servicio por su ID.
    /// </summary>
    /// <param name="id">ID del documento en Firestore.</param>
    /// <returns>Nombre del tipo de servicio o null si no existe.</returns>
    public async Task<string?> ObtenerNombrePorId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var doc = await _firestore.Collection("tiposServicio").Document(id).GetSnapshotAsync();
        return doc.Exists && doc.ContainsField("Nombre") ? doc.GetValue<string>("Nombre") : null;
    }

    /// <summary>
    /// Verifica si existe un tipo de servicio con el nombre especificado.
    /// La comparación ignora espacios y mayúsculas/minúsculas.
    /// </summary>
    /// <param name="nombre">Nombre del tipo de servicio a verificar.</param>
    /// <returns>True si existe, false en caso contrario.</returns>
    public async Task<bool> ExisteTipoServicio(string nombre)
    {
        var tiposExistentes = await ObtenerTiposServicio();
        string nombreNormalizado = nombre.Replace(" ", "").ToLowerInvariant();

        return tiposExistentes.Any(t =>
            t.Replace(" ", "").ToLowerInvariant() == nombreNormalizado);
    }

    #endregion

    #region Operaciones CRUD

    /// <summary>
    /// Crea un nuevo tipo de servicio en la base de datos.
    /// </summary>
    /// <param name="nombre">Nombre del tipo de servicio a crear.</param>
    /// <returns>ID del documento creado en Firestore.</returns>
    public async Task<string> CrearTipoServicio(string nombre)
    {
        var tipoRef = _firestore.Collection("tiposServicio").Document();
        var tipo = new Dictionary<string, object>
        {
            { "Nombre", nombre }
        };
        await tipoRef.SetAsync(tipo);
        return tipoRef.Id;
    }

    /// <summary>
    /// Elimina todos los tipos de servicio con el nombre especificado.
    /// Puede haber múltiples documentos con el mismo nombre.
    /// </summary>
    /// <param name="nombre">Nombre del tipo de servicio a eliminar.</param>
    /// <returns>Lista de IDs de los documentos eliminados.</returns>
    public async Task<List<string>> EliminarTipoServicio(string nombre)
    {
        var tiposRef = _firestore.Collection("tiposServicio");
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
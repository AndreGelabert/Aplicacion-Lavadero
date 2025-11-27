using Google.Cloud.Firestore;

/// <summary>
/// Servicio para la gestión de tipos de documento en Firestore.
/// Proporciona operaciones CRUD para los tipos de documento disponibles.
/// </summary>
public class TipoDocumentoService
{
    private readonly FirestoreDb _firestore;

    public TipoDocumentoService(FirestoreDb firestore)
    {
        _firestore = firestore;
    }

    /// <summary>
    /// Obtiene todos los tipos de documento registrados.
    /// </summary>
    public async Task<List<string>> ObtenerTiposDocumento()
    {
        try
        {
            var snapshot = await _firestore.Collection("tiposDocumento").GetSnapshotAsync();
            var tipos = snapshot.Documents.Select(doc => doc.GetValue<string>("Nombre")).ToList();
            return tipos ?? new List<string>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al obtener tipos de documento: {ex.Message}");
            return new List<string>();
        }
    }

    /// <summary>
    /// Verifica si existe un tipo de documento con el nombre especificado.
    /// </summary>
    public async Task<bool> ExisteTipoDocumento(string nombre)
    {
        var tiposExistentes = await ObtenerTiposDocumento();
        string nombreNormalizado = nombre.Replace(" ", "").ToLowerInvariant();

        return tiposExistentes.Any(t =>
            t.Replace(" ", "").ToLowerInvariant() == nombreNormalizado);
    }

    /// <summary>
    /// Crea un nuevo tipo de documento.
    /// </summary>
    public async Task<string> CrearTipoDocumento(string nombre)
    {
        var tipoRef = _firestore.Collection("tiposDocumento").Document();
        var tipo = new Dictionary<string, object>
        {
            { "Nombre", nombre }
        };
        await tipoRef.SetAsync(tipo);
        return tipoRef.Id;
    }

    /// <summary>
    /// Elimina un tipo de documento por nombre.
    /// </summary>
    public async Task<List<string>> EliminarTipoDocumento(string nombre)
    {
        var tiposRef = _firestore.Collection("tiposDocumento");
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
}

using Google.Cloud.Firestore;

public class TipoServicioService
{
    private readonly FirestoreDb _firestore;

    public TipoServicioService(FirestoreDb firestore)
    {
        _firestore = firestore;
    }

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

    // Ahora devuelve el ID del documento creado
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

    // Ahora devuelve los IDs de los documentos eliminados (puede haber más de uno con el mismo nombre)
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

    public async Task<string?> ObtenerNombrePorId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var doc = await _firestore.Collection("tiposServicio").Document(id).GetSnapshotAsync();
        return doc.Exists && doc.ContainsField("Nombre") ? doc.GetValue<string>("Nombre") : null;
    }

    public async Task<bool> ExisteTipoServicio(string nombre)
    {
        var tiposExistentes = await ObtenerTiposServicio();
        string nombreNormalizado = nombre.Replace(" ", "").ToLowerInvariant();

        return tiposExistentes.Any(t =>
            t.Replace(" ", "").ToLowerInvariant() == nombreNormalizado);
    }
}
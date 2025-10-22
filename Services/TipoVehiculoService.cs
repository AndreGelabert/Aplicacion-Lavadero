using Google.Cloud.Firestore;

public class TipoVehiculoService
{
    private readonly FirestoreDb _firestore;

    public TipoVehiculoService(FirestoreDb firestore)
    {
        _firestore = firestore;
    }

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

    // Ahora devuelve el ID del documento creado
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

    // Ahora devuelve los IDs de los documentos eliminados (puede haber más de uno con el mismo nombre)
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

    public async Task<bool> ExisteTipoVehiculo(string nombre)
    {
        var tiposExistentes = await ObtenerTiposVehiculos();
        string nombreNormalizado = nombre.Replace(" ", "").ToLowerInvariant();

        return tiposExistentes.Any(t =>
            t.Replace(" ", "").ToLowerInvariant() == nombreNormalizado);
    }
}
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

            // Si no hay tipos, devuelve una lista vacía en lugar de null
            return tipos ?? new List<string>();
        }
        catch (Exception ex)
        {
            // Si ocurre un error, registra la excepción y devuelve una lista vacía
            Console.WriteLine($"Error al obtener tipos de servicio: {ex.Message}");
            return new List<string>();
        }
    }


    public async Task CrearTipoServicio(string nombre)
    {
        var tipoRef = _firestore.Collection("tiposServicio").Document();
        var tipo = new Dictionary<string, object>
        {
            { "Nombre", nombre }
        };
        await tipoRef.SetAsync(tipo);
    }

    // Nuevo método para eliminar un tipo de servicio
    public async Task EliminarTipoServicio(string nombre)
    {
        var tiposRef = _firestore.Collection("tiposServicio");
        var query = tiposRef.WhereEqualTo("Nombre", nombre);
        var snapshot = await query.GetSnapshotAsync();

        foreach (var documento in snapshot.Documents)
        {
            await documento.Reference.DeleteAsync();
        }
    }

    // Nuevo método para verificar si un tipo de servicio ya existe
    public async Task<bool> ExisteTipoServicio(string nombre)
    {
        var tiposExistentes = await ObtenerTiposServicio();
        string nombreNormalizado = nombre.Replace(" ", "").ToLowerInvariant();

        return tiposExistentes.Any(t =>
            t.Replace(" ", "").ToLowerInvariant() == nombreNormalizado);
    }
}

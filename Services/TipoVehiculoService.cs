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


    public async Task CrearTipoVehiculo(string nombre)
    {
        var tipoRef = _firestore.Collection("tiposVehiculos").Document();
        var tipo = new Dictionary<string, object>
        {
            { "Nombre", nombre }
        };
        await tipoRef.SetAsync(tipo);
    }

    public async Task EliminarTipoVehiculo(string nombre)
    {
        var tiposRef = _firestore.Collection("tiposVehiculos");
        var query = tiposRef.WhereEqualTo("Nombre", nombre);
        var snapshot = await query.GetSnapshotAsync();

        foreach (var documento in snapshot.Documents)
        {
            await documento.Reference.DeleteAsync();
        }
    }

    // Método para verificar si un tipo de servicio ya existe
    public async Task<bool> ExisteTipoVehiculo(string nombre)
    {
        var tiposExistentes = await ObtenerTiposVehiculos();
        string nombreNormalizado = nombre.Replace(" ", "").ToLowerInvariant();

        return tiposExistentes.Any(t =>
            t.Replace(" ", "").ToLowerInvariant() == nombreNormalizado);
    }
}

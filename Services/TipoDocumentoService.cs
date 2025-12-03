using Firebase.Models;
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
    /// Obtiene todos los tipos de documento registrados (solo nombres).
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
    /// Obtiene todos los tipos de documento con sus formatos.
    /// </summary>
    public async Task<List<TipoDocumento>> ObtenerTiposDocumentoCompletos()
    {
        try
        {
            var snapshot = await _firestore.Collection("tiposDocumento").GetSnapshotAsync();
            var tipos = new List<TipoDocumento>();
            
            foreach (var doc in snapshot.Documents)
            {
                tipos.Add(new TipoDocumento
                {
                    Id = doc.Id,
                    Nombre = doc.GetValue<string>("Nombre") ?? "",
                    Formato = doc.ContainsField("Formato") ? doc.GetValue<string>("Formato") : null
                });
            }
            
            return tipos;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al obtener tipos de documento: {ex.Message}");
            return new List<TipoDocumento>();
        }
    }

    /// <summary>
    /// Obtiene un tipo de documento por su nombre.
    /// </summary>
    public async Task<TipoDocumento?> ObtenerTipoDocumentoPorNombre(string nombre)
    {
        try
        {
            var query = _firestore.Collection("tiposDocumento").WhereEqualTo("Nombre", nombre);
            var snapshot = await query.GetSnapshotAsync();
            
            if (snapshot.Documents.Count == 0)
                return null;

            var doc = snapshot.Documents.First();
            return new TipoDocumento
            {
                Id = doc.Id,
                Nombre = doc.GetValue<string>("Nombre") ?? "",
                Formato = doc.ContainsField("Formato") ? doc.GetValue<string>("Formato") : null
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al obtener tipo de documento: {ex.Message}");
            return null;
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
    /// Crea un nuevo tipo de documento con formato opcional.
    /// </summary>
    public async Task<string> CrearTipoDocumento(string nombre, string? formato = null)
    {
        var tipoRef = _firestore.Collection("tiposDocumento").Document();
        var tipo = new Dictionary<string, object>
        {
            { "Nombre", nombre }
        };
        
        if (!string.IsNullOrWhiteSpace(formato))
        {
            tipo["Formato"] = formato;
        }
        
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

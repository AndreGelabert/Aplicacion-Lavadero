using Firebase.Models;
using Google.Cloud.Firestore;

/// <summary>
/// Servicio para la gestión de tipos de documento en Firestore.
/// </summary>
public class TipoDocumentoService
{
    private const string COLLECTION_NAME = "tipos_documento";
    private readonly FirestoreDb _firestore;

    public TipoDocumentoService(FirestoreDb firestore)
    {
        _firestore = firestore ?? throw new ArgumentNullException(nameof(firestore));
    }

    /// <summary>
    /// Obtiene todos los tipos de documento (solo nombres).
    /// </summary>
    public async Task<List<string>> ObtenerTiposDocumento()
    {
        var snapshot = await _firestore.Collection(COLLECTION_NAME)
            .OrderBy("Nombre")
            .GetSnapshotAsync();

        return snapshot.Documents
            .Select(doc => doc.GetValue<string>("Nombre"))
            .ToList();
    }

    /// <summary>
    /// Verifica si existe un tipo de documento con el nombre especificado.
    /// </summary>
    public async Task<bool> ExisteTipoDocumento(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return false;

        var snapshot = await _firestore.Collection(COLLECTION_NAME)
            .WhereEqualTo("Nombre", nombre)
            .Limit(1)
            .GetSnapshotAsync();

        return snapshot.Documents.Count > 0;
    }

    /// <summary>
    /// Crea un nuevo tipo de documento.
    /// </summary>
    public async Task<string> CrearTipoDocumento(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre del tipo de documento no puede estar vacío");

        var docRef = _firestore.Collection(COLLECTION_NAME).Document();
        await docRef.SetAsync(new TipoDocumento
        {
            Id = docRef.Id,
            Nombre = nombre.Trim()
        });

        return docRef.Id;
    }

    /// <summary>
    /// Elimina todos los tipos de documento con el nombre especificado.
    /// </summary>
    public async Task<List<string>> EliminarTipoDocumento(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre del tipo de documento no puede estar vacío");

        var snapshot = await _firestore.Collection(COLLECTION_NAME)
            .WhereEqualTo("Nombre", nombre)
            .GetSnapshotAsync();

        var idsEliminados = new List<string>();
        foreach (var doc in snapshot.Documents)
        {
            await doc.Reference.DeleteAsync();
            idsEliminados.Add(doc.Id);
        }

        return idsEliminados;
    }
}

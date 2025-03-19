using Firebase.Models;
using Google.Cloud.Firestore;
public class PersonalService
{
    private readonly FirestoreDb _firestore;

    public PersonalService(FirestoreDb firestore)
    {
        _firestore = firestore;
    }

    public async Task<List<Empleado>> ObtenerEmpleados(List<string> estados, string firstDocId, string lastDocId, int pageNumber, int pageSize)
    {
        if (estados == null || !estados.Any())
        {
            estados = new List<string> { "Activo" };
        }

        Query query = _firestore.Collection("empleados");

        if (estados.Any()) query = query.WhereIn("Estado", estados);
        else query = query.WhereEqualTo("Estado", "Activo");

        query = query.OrderBy("Estado").OrderBy("Nombre").Limit(pageSize);

        if (!string.IsNullOrEmpty(lastDocId) && pageNumber > 1)
        {
            var lastDoc = await _firestore.Collection("empleados").Document(lastDocId).GetSnapshotAsync();
            query = query.StartAfter(lastDoc);
        }
        else if (!string.IsNullOrEmpty(firstDocId) && pageNumber > 1)
        {
            var firstDoc = await _firestore.Collection("empleados").Document(firstDocId).GetSnapshotAsync();
            query = query.StartAt(firstDoc);
        }

        var snapshot = await query.GetSnapshotAsync();
        return snapshot.Documents.Select(doc => new Empleado
        {
            Id = doc.Id,
            NombreCompleto = doc.GetValue<string>("Nombre"),
            Email = doc.GetValue<string>("Email"),
            Rol = doc.GetValue<string>("Rol"),
            Estado = doc.GetValue<string>("Estado")
        }).ToList();
    }

    public async Task<int> ObtenerTotalPaginas(List<string> estados, int pageSize)
    {
        Query query = _firestore.Collection("empleados");
        if (estados.Any()) query = query.WhereIn("Estado", estados);

        var countQuery = query.Select("__name__");
        var snapshot = await countQuery.GetSnapshotAsync();
        return (int)Math.Ceiling(snapshot.Count / (double)pageSize);
    }

    public async Task ActualizarRol(string id, string nuevoRol)
    {
        var employeeRef = _firestore.Collection("empleados").Document(id);
        await employeeRef.UpdateAsync("Rol", nuevoRol);
    }

    public async Task CambiarEstadoEmpleado(string id, string nuevoEstado)
    {
        var employeeRef = _firestore.Collection("empleados").Document(id);
        await employeeRef.UpdateAsync("Estado", nuevoEstado);
    }
}

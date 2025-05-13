using Firebase.Models;
using Google.Cloud.Firestore;

public class ServicioService
{
    private readonly FirestoreDb _firestore;

    public ServicioService(FirestoreDb firestore)
    {
        _firestore = firestore;
    }

    public async Task<List<Servicio>> ObtenerServicios(List<string> estados, List<string> tipos, string firstDocId, string lastDocId, int pageNumber, int pageSize)
    {
        if (estados == null || !estados.Any())
        {
            estados = new List<string> { "Activo" };
        }

        Query query = _firestore.Collection("servicios");

        if (estados.Any()) query = query.WhereIn("Estado", estados);
        else query = query.WhereEqualTo("Estado", "Activo");

        if (tipos != null && tipos.Any()) query = query.WhereIn("Tipo", tipos);

        query = query.OrderBy("Estado").OrderBy("Nombre").Limit(pageSize);

        if (!string.IsNullOrEmpty(lastDocId) && pageNumber > 1)
        {
            var lastDoc = await _firestore.Collection("servicios").Document(lastDocId).GetSnapshotAsync();
            query = query.StartAfter(lastDoc);
        }
        else if (!string.IsNullOrEmpty(firstDocId) && pageNumber > 1)
        {
            var firstDoc = await _firestore.Collection("servicios").Document(firstDocId).GetSnapshotAsync();
            query = query.StartAt(firstDoc);
        }

        var snapshot = await query.GetSnapshotAsync();
        return snapshot.Documents.Select(doc => new Servicio
        {
            Id = doc.Id,
            Nombre = doc.GetValue<string>("Nombre"),
            Precio = doc.GetValue<decimal>("Precio"),
            Tipo = doc.GetValue<string>("Tipo"),
            TipoVehiculo = doc.GetValue<string>("TipoVehiculo"),
            TiempoEstimado = doc.GetValue<int>("TiempoEstimado"),
            Descripcion = doc.GetValue<string>("Descripcion"),
            Estado = doc.GetValue<string>("Estado")
        }).ToList();
    }

    public async Task<Servicio> ObtenerServicio(string id)
    {
        var docRef = _firestore.Collection("servicios").Document(id);
        var snapshot = await docRef.GetSnapshotAsync();

        if (!snapshot.Exists)
            return null;

        return new Servicio
        {
            Id = snapshot.Id,
            Nombre = snapshot.GetValue<string>("Nombre"),
            Precio = snapshot.GetValue<decimal>("Precio"),
            Tipo = snapshot.GetValue<string>("Tipo"),
            TipoVehiculo = snapshot.GetValue<string>("TipoVehiculo"),
            TiempoEstimado = snapshot.GetValue<int>("TiempoEstimado"),
            Descripcion = snapshot.GetValue<string>("Descripcion"),
            Estado = snapshot.GetValue<string>("Estado")
        };
    }

    // Añadir este método a la clase ServicioService
    public async Task<List<Servicio>> ObtenerServiciosPorTipo(string tipo)
    {
        var servicios = new List<Servicio>();
        var coleccion = _firestore.Collection("servicios");
        var querySnapshot = await coleccion.WhereEqualTo("Tipo", tipo).GetSnapshotAsync();

        foreach (var documento in querySnapshot.Documents)
        {
            var servicio = new Servicio
            {
                Id = documento.Id,
                Nombre = documento.GetValue<string>("Nombre"),
                Precio = documento.GetValue<decimal>("Precio"),
                Tipo = documento.GetValue<string>("Tipo"),
                TipoVehiculo = documento.GetValue<string>("TipoVehiculo"),
                TiempoEstimado = documento.GetValue<int>("TiempoEstimado"),
                Descripcion = documento.GetValue<string>("Descripcion"),
                Estado = documento.GetValue<string>("Estado")
            };
            servicios.Add(servicio);
        }

        return servicios;
    }

    // Nuevo método para obtener servicios por tipo de vehículo
    public async Task<List<Servicio>> ObtenerServiciosPorTipoVehiculo(string tipoVehiculo)
    {
        var servicios = new List<Servicio>();
        var coleccion = _firestore.Collection("servicios");
        var querySnapshot = await coleccion.WhereEqualTo("TipoVehiculo", tipoVehiculo).GetSnapshotAsync();

        foreach (var documento in querySnapshot.Documents)
        {
            var servicio = new Servicio
            {
                Id = documento.Id,
                Nombre = documento.GetValue<string>("Nombre"),
                Precio = documento.GetValue<decimal>("Precio"),
                Tipo = documento.GetValue<string>("Tipo"),
                TipoVehiculo = documento.GetValue<string>("TipoVehiculo"),
                TiempoEstimado = documento.GetValue<int>("TiempoEstimado"),
                Descripcion = documento.GetValue<string>("Descripcion"),
                Estado = documento.GetValue<string>("Estado")
            };
            servicios.Add(servicio);
        }

        return servicios;
    }

    public async Task<int> ObtenerTotalPaginas(List<string> estados, List<string> tipos, int pageSize)
    {
        Query query = _firestore.Collection("servicios");
        if (estados.Any()) query = query.WhereIn("Estado", estados);
        if (tipos != null && tipos.Any()) query = query.WhereIn("Tipo", tipos);

        var countQuery = query.Select("__name__");
        var snapshot = await countQuery.GetSnapshotAsync();
        return (int)Math.Ceiling(snapshot.Count / (double)pageSize);
    }

    public async Task CrearServicio(Servicio servicio)
    {
        try
        {
            var servicioRef = _firestore.Collection("servicios").Document();
            servicio.Id = servicioRef.Id;

            // Crear un diccionario para asegurar que todos los campos se escriban correctamente
            var servicioData = new Dictionary<string, object>
            {
                { "Nombre", servicio.Nombre },
                { "Precio", servicio.Precio },
                { "Tipo", servicio.Tipo },
                { "TipoVehiculo", servicio.TipoVehiculo },
                { "TiempoEstimado", servicio.TiempoEstimado },
                { "Descripcion", servicio.Descripcion },
                { "Estado", servicio.Estado }
            };

            await servicioRef.SetAsync(servicioData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al crear servicio: {ex.Message}");
            throw; // Relanzar la excepción para que el controlador pueda manejarla
        }
    }

    public async Task ActualizarServicio(Servicio servicio)
    {
        try
        {
            var servicioRef = _firestore.Collection("servicios").Document(servicio.Id);

            // Crear un diccionario para asegurar que todos los campos se actualicen correctamente
            var servicioData = new Dictionary<string, object>
            {
                { "Nombre", servicio.Nombre },
                { "Precio", servicio.Precio },
                { "Tipo", servicio.Tipo },
                { "TipoVehiculo", servicio.TipoVehiculo },
                { "TiempoEstimado", servicio.TiempoEstimado },
                { "Descripcion", servicio.Descripcion },
                { "Estado", servicio.Estado }
            };

            await servicioRef.SetAsync(servicioData, SetOptions.Overwrite);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al actualizar servicio: {ex.Message}");
            throw; // Relanzar la excepción para que el controlador pueda manejarla
        }
    }

    public async Task CambiarEstadoServicio(string id, string nuevoEstado)
    {
        var servicioRef = _firestore.Collection("servicios").Document(id);
        await servicioRef.UpdateAsync("Estado", nuevoEstado);
    }
}

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
            Precio = doc.ContainsField("Precio") ?
                (decimal)Convert.ToDouble(doc.GetValue<object>("Precio")) : 0m, // Convertir a decimal
            Tipo = doc.GetValue<string>("Tipo"),
            TipoVehiculo = doc.ContainsField("TipoVehiculo") ?
                doc.GetValue<string>("TipoVehiculo") : "General", // Valor predeterminado
            TiempoEstimado = doc.ContainsField("TiempoEstimado") ?
                doc.GetValue<int>("TiempoEstimado") : 0,
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
            Precio = snapshot.ContainsField("Precio") ?
                (decimal)Convert.ToDouble(snapshot.GetValue<object>("Precio")) : 0m,
            Tipo = snapshot.GetValue<string>("Tipo"),
            TipoVehiculo = snapshot.ContainsField("TipoVehiculo") ?
                snapshot.GetValue<string>("TipoVehiculo") : "General",
            TiempoEstimado = snapshot.ContainsField("TiempoEstimado") ?
                snapshot.GetValue<int>("TiempoEstimado") : 0,
            Descripcion = snapshot.GetValue<string>("Descripcion"),
            Estado = snapshot.GetValue<string>("Estado")
        };
    }

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
                Precio = documento.ContainsField("Precio") ?
                    (decimal)Convert.ToDouble(documento.GetValue<object>("Precio")) : 0m,
                Tipo = documento.GetValue<string>("Tipo"),
                TipoVehiculo = documento.ContainsField("TipoVehiculo") ?
                    documento.GetValue<string>("TipoVehiculo") : "General",
                TiempoEstimado = documento.ContainsField("TiempoEstimado") ?
                    documento.GetValue<int>("TiempoEstimado") : 0,
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
                Precio = documento.ContainsField("Precio") ?
                    (decimal)Convert.ToDouble(documento.GetValue<object>("Precio")) : 0m,
                Tipo = documento.GetValue<string>("Tipo"),
                TipoVehiculo = documento.GetValue<string>("TipoVehiculo"),
                TiempoEstimado = documento.ContainsField("TiempoEstimado") ?
                    documento.GetValue<int>("TiempoEstimado") : 0,
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
            // Validar si ya existe un servicio con el mismo nombre para el mismo tipo de vehículo
            bool existeServicio = await ExisteServicioConNombreTipoVehiculo(servicio.Nombre, servicio.TipoVehiculo);
            if (existeServicio)
            {
                throw new ArgumentException($"Ya existe un servicio con el nombre '{servicio.Nombre}' para vehículos tipo '{servicio.TipoVehiculo}'");
            }

            var servicioRef = _firestore.Collection("servicios").Document();
            servicio.Id = servicioRef.Id; // Asignamos el ID generado por Firestore

            // Verificar valores nulos o vacíos
            if (string.IsNullOrEmpty(servicio.Nombre))
                throw new ArgumentException("El nombre del servicio no puede estar vacío");

            if (string.IsNullOrEmpty(servicio.Tipo))
                throw new ArgumentException("El tipo de servicio no puede estar vacío");

            if (string.IsNullOrEmpty(servicio.TipoVehiculo))
                throw new ArgumentException("El tipo de vehículo no puede estar vacío");

            if (string.IsNullOrEmpty(servicio.Descripcion))
                throw new ArgumentException("La descripción no puede estar vacía");

            // Validaciones adicionales
            if (!System.Text.RegularExpressions.Regex.IsMatch(servicio.Nombre, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$"))
                throw new ArgumentException("El nombre solo puede contener letras y espacios");

            if (servicio.Precio < 0)
                throw new ArgumentException("El precio debe ser igual o mayor a 0");

            if (servicio.TiempoEstimado <= 0)
                throw new ArgumentException("El tiempo estimado debe ser mayor a 0");

            // Convertir el decimal a double manualmente para Firestore
            var servicioData = new Dictionary<string, object>
        {
            { "Nombre", servicio.Nombre },
            { "Precio", (double)servicio.Precio }, // Conversión explícita a double
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
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw; // Relanzar la excepción para que el controlador pueda manejarla
        }
    }

    public async Task ActualizarServicio(Servicio servicio)
    {
        try
        {
            // Validar si ya existe un servicio con el mismo nombre para el mismo tipo de vehículo (excluyendo el actual)
            bool existeServicio = await ExisteServicioConNombreTipoVehiculo(servicio.Nombre, servicio.TipoVehiculo, servicio.Id);
            if (existeServicio)
            {
                throw new ArgumentException($"Ya existe un servicio con el nombre '{servicio.Nombre}' para vehículos tipo '{servicio.TipoVehiculo}'");
            }

            // Validaciones adicionales
            if (!System.Text.RegularExpressions.Regex.IsMatch(servicio.Nombre, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$"))
                throw new ArgumentException("El nombre solo puede contener letras y espacios");

            if (servicio.Precio < 0)
                throw new ArgumentException("El precio debe ser igual o mayor a 0");

            if (servicio.TiempoEstimado <= 0)
                throw new ArgumentException("El tiempo estimado debe ser mayor a 0");

            var servicioRef = _firestore.Collection("servicios").Document(servicio.Id);

            // Convertir manualmente decimal a double
            var servicioData = new Dictionary<string, object>
        {
            { "Nombre", servicio.Nombre },
            { "Precio", (double)servicio.Precio }, // Conversión explícita
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

    public async Task<bool> ExisteServicioConNombreTipoVehiculo(string nombre, string tipoVehiculo, string idActual = null)
    {
        var coleccion = _firestore.Collection("servicios");
        // Primero buscamos por nombre exacto (ignorando mayúsculas/minúsculas)
        var querySnapshot = await coleccion
            .WhereEqualTo("TipoVehiculo", tipoVehiculo)
            .GetSnapshotAsync();

        foreach (var doc in querySnapshot.Documents)
        {
            // Si estamos actualizando, ignoramos el mismo servicio
            if (idActual != null && doc.Id == idActual)
                continue;

            // Comparamos el nombre ignorando mayúsculas/minúsculas y espacios adicionales
            if (doc.GetValue<string>("Nombre").Trim().Equals(nombre.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true; // Ya existe un servicio con el mismo nombre para ese tipo de vehículo
            }
        }

        return false;
    }
}

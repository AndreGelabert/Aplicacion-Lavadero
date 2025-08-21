using Firebase.Models;
using Google.Cloud.Firestore;

public class ServicioService
{
    private readonly FirestoreDb _firestore;

    public ServicioService(FirestoreDb firestore)
    {
        _firestore = firestore;
    }

    public async Task<List<Servicio>> ObtenerServicios(
        List<string> estados,
        List<string> tipos,
        List<string> tiposVehiculo,
        string firstDocId,
        string lastDocId,
        int pageNumber,
        int pageSize)
    {
        estados ??= new List<string>();
        if (!estados.Any())
        {
            estados.Add("Activo");
        }

        Query query = _firestore.Collection("servicios");

        if (estados.Any())
            query = query.WhereIn("Estado", estados);
        else
            query = query.WhereEqualTo("Estado", "Activo");

        if (tipos != null && tipos.Any())
        {
            query = query.WhereIn("Tipo", tipos);
        }

        query = query.OrderBy("Estado").OrderBy("Nombre");
        // Elimina el Limit aquí

        var snapshot = await query.GetSnapshotAsync();
        var servicios = snapshot.Documents.Select(MapearDocumentoAServicio).ToList();

        // Filtrar por tipo de vehículo si es necesario
        if (tiposVehiculo != null && tiposVehiculo.Any())
        {
            servicios = servicios.Where(s => tiposVehiculo.Contains(s.TipoVehiculo)).ToList();
        }

        // Aplicar paginación después del filtrado
        return servicios.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
    }

    public async Task<int> ObtenerTotalPaginas(
        List<string> estados,
        List<string> tipos,
        List<string> tiposVehiculo,
        int pageSize)
    {
        estados ??= new List<string>();
        if (!estados.Any())
        {
            estados.Add("Activo");
        }

        Query query = _firestore.Collection("servicios");
        if (estados.Any())
            query = query.WhereIn("Estado", estados);
        if (tipos != null && tipos.Any())
            query = query.WhereIn("Tipo", tipos);

        var snapshot = await query.GetSnapshotAsync();
        var servicios = snapshot.Documents.Select(doc => new Servicio
        {
            Id = doc.Id,
            Nombre = doc.GetValue<string>("Nombre"),
            Tipo = doc.GetValue<string>("Tipo"),
            TipoVehiculo = doc.ContainsField("TipoVehiculo") ?
                doc.GetValue<string>("TipoVehiculo") : "General",
            Descripcion = "N/A",
            Estado = doc.ContainsField("Estado") ?
                doc.GetValue<string>("Estado") : "Activo"
        }).ToList();

        // Filtrar por tipo de vehículo si es necesario
        if (tiposVehiculo != null && tiposVehiculo.Any())
        {
            servicios = servicios.Where(s => tiposVehiculo.Contains(s.TipoVehiculo)).ToList();
        }

        return (int)Math.Ceiling(servicios.Count / (double)pageSize);
    }

    public async Task<Servicio> ObtenerServicio(string id)
    {
        var docRef = _firestore.Collection("servicios").Document(id);
        var snapshot = await docRef.GetSnapshotAsync();

        return !snapshot.Exists ? null : MapearDocumentoAServicio(snapshot);
    }

    public async Task<List<Servicio>> ObtenerServiciosPorTipo(string tipo)
        => await ObtenerServiciosPorCampo("Tipo", tipo);

    public async Task<List<Servicio>> ObtenerServiciosPorTipoVehiculo(string tipoVehiculo)
        => await ObtenerServiciosPorCampo("TipoVehiculo", tipoVehiculo);

    public async Task CrearServicio(Servicio servicio)
    {
        // Validar si ya existe un servicio con el mismo nombre para el mismo tipo de vehículo
        if (await ExisteServicioConNombreTipoVehiculo(servicio.Nombre, servicio.TipoVehiculo))
            throw new ArgumentException($"Ya existe un servicio con el nombre '{servicio.Nombre}' para vehículos tipo '{servicio.TipoVehiculo}'");

        var servicioRef = _firestore.Collection("servicios").Document();
        servicio.Id = servicioRef.Id;

        // Validaciones
        ValidarServicio(servicio);

        // Convertir el decimal a double para Firestore
        var servicioData = new Dictionary<string, object>
        {
            { "Nombre", servicio.Nombre },
            { "Precio", (double)servicio.Precio },
            { "Tipo", servicio.Tipo },
            { "TipoVehiculo", servicio.TipoVehiculo },
            { "TiempoEstimado", servicio.TiempoEstimado },
            { "Descripcion", servicio.Descripcion },
            { "Estado", servicio.Estado }
        };

        await servicioRef.SetAsync(servicioData);
    }

    public async Task ActualizarServicio(Servicio servicio)
    {
        // Validar si ya existe un servicio con el mismo nombre para el mismo tipo de vehículo (excluyendo el actual)
        if (await ExisteServicioConNombreTipoVehiculo(servicio.Nombre, servicio.TipoVehiculo, servicio.Id))
            throw new ArgumentException($"Ya existe un servicio con el nombre '{servicio.Nombre}' para vehículos tipo '{servicio.TipoVehiculo}'");

        // Validaciones
        ValidarServicio(servicio);

        var servicioRef = _firestore.Collection("servicios").Document(servicio.Id);

        // Convertir decimal a double para Firestore
        var servicioData = new Dictionary<string, object>
        {
            { "Nombre", servicio.Nombre },
            { "Precio", (double)servicio.Precio },
            { "Tipo", servicio.Tipo },
            { "TipoVehiculo", servicio.TipoVehiculo },
            { "TiempoEstimado", servicio.TiempoEstimado },
            { "Descripcion", servicio.Descripcion },
            { "Estado", servicio.Estado }
        };

        await servicioRef.SetAsync(servicioData, SetOptions.Overwrite);
    }

    public async Task CambiarEstadoServicio(string id, string nuevoEstado)
    {
        var servicioRef = _firestore.Collection("servicios").Document(id);
        await servicioRef.UpdateAsync("Estado", nuevoEstado);
    }

    public async Task<bool> ExisteServicioConNombreTipoVehiculo(string nombre, string tipoVehiculo, string idActual = null)
    {
        var coleccion = _firestore.Collection("servicios");
        var querySnapshot = await coleccion
            .WhereEqualTo("TipoVehiculo", tipoVehiculo)
            .GetSnapshotAsync();

        return querySnapshot.Documents
            .Where(doc => idActual == null || doc.Id != idActual)
            .Any(doc => doc.GetValue<string>("Nombre").Trim().Equals(nombre.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    #region Métodos privados

    private async Task<List<Servicio>> ObtenerServiciosPorCampo(string campo, string valor)
    {
        var coleccion = _firestore.Collection("servicios");
        var querySnapshot = await coleccion.WhereEqualTo(campo, valor).GetSnapshotAsync();
        return querySnapshot.Documents.Select(MapearDocumentoAServicio).ToList();
    }

    private Servicio MapearDocumentoAServicio(DocumentSnapshot documento)
    {
        return new Servicio
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
    }

    private void ValidarServicio(Servicio servicio)
    {
        if (string.IsNullOrEmpty(servicio.Nombre))
            throw new ArgumentException("El nombre del servicio no puede estar vacío");

        if (string.IsNullOrEmpty(servicio.Tipo))
            throw new ArgumentException("El tipo de servicio no puede estar vacío");

        if (string.IsNullOrEmpty(servicio.TipoVehiculo))
            throw new ArgumentException("El tipo de vehículo no puede estar vacío");

        if (string.IsNullOrEmpty(servicio.Descripcion))
            throw new ArgumentException("La descripción no puede estar vacía");

        if (!System.Text.RegularExpressions.Regex.IsMatch(servicio.Nombre, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$"))
            throw new ArgumentException("El nombre solo puede contener letras y espacios");

        if (servicio.Precio < 0)
            throw new ArgumentException("El precio debe ser igual o mayor a 0");

        if (servicio.TiempoEstimado <= 0)
            throw new ArgumentException("El tiempo estimado debe ser mayor a 0");
    }

    #endregion
}
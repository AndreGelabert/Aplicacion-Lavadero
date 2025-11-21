using Firebase.Models;
using Google.Cloud.Firestore;

/// <summary>
/// Servicio para la gestión de vehículos en Firestore.
/// Proporciona operaciones CRUD, filtrado, paginación y búsqueda de vehículos.
/// </summary>
public class VehiculoService
{
    private const string COLLECTION_NAME = "vehiculos";
    private const string ESTADO_DEFECTO = "Activo";
    private readonly FirestoreDb _firestore;

    public VehiculoService(FirestoreDb firestore)
    {
        _firestore = firestore ?? throw new ArgumentNullException(nameof(firestore));
    }

    #region Operaciones de Consulta

    /// <summary>
    /// Obtiene una lista paginada de vehículos aplicando filtros y ordenamiento.
    /// </summary>
    public async Task<List<Vehiculo>> ObtenerVehiculos(
        List<string> estados = null,
        List<string> tiposVehiculo = null,
        string clienteId = null,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = null,
        string sortOrder = null)
    {
        ValidarParametrosPaginacion(pageNumber, pageSize);

        sortBy ??= "Placa";
        sortOrder ??= "asc";

        var vehiculos = await ObtenerVehiculosFiltrados(estados, tiposVehiculo, clienteId, sortBy, sortOrder);

        return AplicarPaginacion(vehiculos, pageNumber, pageSize);
    }

    /// <summary>
    /// Calcula el número total de páginas para los vehículos filtrados.
    /// </summary>
    public async Task<int> ObtenerTotalPaginas(
        List<string> estados,
        List<string> tiposVehiculo,
        string clienteId,
        int pageSize)
    {
        if (pageSize <= 0)
            throw new ArgumentException("El tamaño de página debe ser mayor a 0", nameof(pageSize));

        var totalVehiculos = await ObtenerTotalVehiculos(estados, tiposVehiculo, clienteId);
        return (int)Math.Ceiling(totalVehiculos / (double)pageSize);
    }

    /// <summary>
    /// Obtiene un vehículo específico por su ID.
    /// </summary>
    public async Task<Vehiculo> ObtenerVehiculo(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("El ID del vehículo no puede estar vacío", nameof(id));

        var docRef = _firestore.Collection(COLLECTION_NAME).Document(id);
        var snapshot = await docRef.GetSnapshotAsync();

        return snapshot.Exists ? MapearDocumentoAVehiculo(snapshot) : null;
    }

    /// <summary>
    /// Busca vehículos por término de búsqueda.
    /// </summary>
    public async Task<List<Vehiculo>> BuscarVehiculos(
        string searchTerm,
        List<string> estados = null,
        List<string> tiposVehiculo = null,
        string clienteId = null,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = null,
        string sortOrder = null)
    {
        var baseFiltrada = await ObtenerVehiculosFiltrados(estados, tiposVehiculo, clienteId, sortBy, sortOrder);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            baseFiltrada = AplicarBusqueda(baseFiltrada, searchTerm);
        }

        var ordenados = AplicarOrdenamiento(baseFiltrada, sortBy, sortOrder);
        return AplicarPaginacion(ordenados, Math.Max(pageNumber, 1), Math.Max(pageSize, 1));
    }

    /// <summary>
    /// Obtiene el total de vehículos que coinciden con la búsqueda.
    /// </summary>
    public async Task<int> ObtenerTotalVehiculosBusqueda(
        string searchTerm,
        List<string> estados,
        List<string> tiposVehiculo,
        string clienteId)
    {
        estados = ConfigurarEstadosDefecto(estados);
        var baseFiltrada = await ObtenerVehiculosFiltrados(estados, tiposVehiculo, clienteId, "Placa", "asc");

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            baseFiltrada = AplicarBusqueda(baseFiltrada, searchTerm);
        }

        return baseFiltrada.Count;
    }

    /// <summary>
    /// Obtiene todos los vehículos por tipo.
    /// </summary>
    public async Task<List<Vehiculo>> ObtenerVehiculosPorTipo(string tipoVehiculo)
    {
        if (string.IsNullOrWhiteSpace(tipoVehiculo))
            return new List<Vehiculo>();

        var snapshot = await _firestore.Collection(COLLECTION_NAME)
            .WhereEqualTo("TipoVehiculo", tipoVehiculo)
            .GetSnapshotAsync();

        return snapshot.Documents.Select(MapearDocumentoAVehiculo).ToList();
    }

    /// <summary>
    /// Obtiene todos los vehículos de un cliente específico.
    /// </summary>
    public async Task<List<Vehiculo>> ObtenerVehiculosPorCliente(string clienteId)
    {
        if (string.IsNullOrWhiteSpace(clienteId))
            return new List<Vehiculo>();

        var snapshot = await _firestore.Collection(COLLECTION_NAME)
            .WhereEqualTo("ClienteId", clienteId)
            .GetSnapshotAsync();

        return snapshot.Documents.Select(MapearDocumentoAVehiculo).ToList();
    }

    /// <summary>
    /// Obtiene todos los vehículos activos (para dropdowns).
    /// </summary>
    public async Task<List<Vehiculo>> ObtenerVehiculosActivos()
    {
        var snapshot = await _firestore.Collection(COLLECTION_NAME)
            .WhereEqualTo("Estado", "Activo")
            .OrderBy("Placa")
            .GetSnapshotAsync();

        return snapshot.Documents.Select(MapearDocumentoAVehiculo).ToList();
    }

    #endregion

    #region Operaciones CRUD

    /// <summary>
    /// Crea un nuevo vehículo en la base de datos.
    /// </summary>
    public async Task CrearVehiculo(Vehiculo vehiculo)
    {
        if (vehiculo == null)
            throw new ArgumentNullException(nameof(vehiculo));

        if (await ExisteVehiculoConPlaca(vehiculo.Placa))
        {
            throw new ArgumentException($"Ya existe un vehículo con la placa {vehiculo.Placa}");
        }

        ValidarVehiculo(vehiculo);

        var vehiculoRef = _firestore.Collection(COLLECTION_NAME).Document();
        vehiculo.Id = vehiculoRef.Id;

        var vehiculoData = CrearDiccionarioVehiculo(vehiculo);
        await vehiculoRef.SetAsync(vehiculoData);
    }

    /// <summary>
    /// Actualiza un vehículo existente en la base de datos.
    /// </summary>
    public async Task ActualizarVehiculo(Vehiculo vehiculo)
    {
        if (vehiculo == null)
            throw new ArgumentNullException(nameof(vehiculo));

        if (string.IsNullOrWhiteSpace(vehiculo.Id))
            throw new ArgumentException("El ID del vehículo es obligatorio para actualizar", nameof(vehiculo));

        ValidarVehiculo(vehiculo);

        var vehiculoRef = _firestore.Collection(COLLECTION_NAME).Document(vehiculo.Id);
        var vehiculoData = CrearDiccionarioVehiculo(vehiculo);
        await vehiculoRef.SetAsync(vehiculoData, SetOptions.Overwrite);
    }

    /// <summary>
    /// Cambia el estado de un vehículo específico.
    /// </summary>
    public async Task CambiarEstadoVehiculo(string id, string nuevoEstado)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("El ID del vehículo no puede estar vacío", nameof(id));

        if (string.IsNullOrWhiteSpace(nuevoEstado))
            throw new ArgumentException("El nuevo estado no puede estar vacío", nameof(nuevoEstado));

        var vehiculoRef = _firestore.Collection(COLLECTION_NAME).Document(id);
        await vehiculoRef.UpdateAsync("Estado", nuevoEstado);
    }

    #endregion

    #region Operaciones de Verificación

    /// <summary>
    /// Verifica si existe un vehículo con la placa especificada.
    /// </summary>
    public async Task<bool> ExisteVehiculoConPlaca(string placa, string idActual = null)
    {
        if (string.IsNullOrWhiteSpace(placa)) return false;

        var snapshot = await _firestore.Collection(COLLECTION_NAME)
            .WhereEqualTo("Placa", placa.Trim().ToUpperInvariant())
            .GetSnapshotAsync();

        return snapshot.Documents
            .Where(doc => idActual == null || doc.Id != idActual)
            .Any();
    }

    #endregion

    #region Métodos Privados - Consultas Base

    private async Task<List<Vehiculo>> ObtenerVehiculosFiltrados(
        List<string> estados,
        List<string> tiposVehiculo,
        string clienteId,
        string sortBy,
        string sortOrder)
    {
        estados = ConfigurarEstadosDefecto(estados);

        var query = ConstruirQueryFiltros(estados, tiposVehiculo, clienteId);

        var snapshot = await query.GetSnapshotAsync();
        var vehiculos = snapshot.Documents
            .Select(MapearDocumentoAVehiculo)
            .ToList();

        return AplicarOrdenamiento(vehiculos, sortBy, sortOrder);
    }

    private async Task<int> ObtenerTotalVehiculos(
        List<string> estados,
        List<string> tiposVehiculo,
        string clienteId)
    {
        estados = ConfigurarEstadosDefecto(estados);

        var query = ConstruirQueryFiltros(estados, tiposVehiculo, clienteId);
        var snapshot = await query.GetSnapshotAsync();

        return snapshot.Count;
    }

    private Query ConstruirQueryFiltros(List<string> estados, List<string> tiposVehiculo, string clienteId)
    {
        Query query = _firestore.Collection(COLLECTION_NAME);

        if (estados != null && estados.Any())
        {
            query = query.WhereIn("Estado", estados);
        }

        if (tiposVehiculo != null && tiposVehiculo.Any())
        {
            query = query.WhereIn("TipoVehiculo", tiposVehiculo);
        }

        if (!string.IsNullOrWhiteSpace(clienteId))
        {
            query = query.WhereEqualTo("ClienteId", clienteId);
        }

        return query;
    }

    #endregion

    #region Métodos Privados - Búsqueda y Filtrado

    private List<Vehiculo> AplicarBusqueda(List<Vehiculo> vehiculos, string searchTerm)
    {
        var termLower = searchTerm.ToLowerInvariant().Trim();

        return vehiculos.Where(v =>
            v.Placa.ToLowerInvariant().Contains(termLower) ||
            v.Marca.ToLowerInvariant().Contains(termLower) ||
            v.Modelo.ToLowerInvariant().Contains(termLower) ||
            v.Color.ToLowerInvariant().Contains(termLower)
        ).ToList();
    }

    #endregion

    #region Métodos Privados - Ordenamiento y Paginación

    private List<Vehiculo> AplicarOrdenamiento(List<Vehiculo> vehiculos, string sortBy, string sortOrder)
    {
        sortBy ??= "Placa";
        sortOrder ??= "asc";

        var ordenados = sortBy.ToLowerInvariant() switch
        {
            "placa" => sortOrder == "desc"
                ? vehiculos.OrderByDescending(v => v.Placa)
                : vehiculos.OrderBy(v => v.Placa),
            "tipovehiculo" => sortOrder == "desc"
                ? vehiculos.OrderByDescending(v => v.TipoVehiculo)
                : vehiculos.OrderBy(v => v.TipoVehiculo),
            "marca" => sortOrder == "desc"
                ? vehiculos.OrderByDescending(v => v.Marca)
                : vehiculos.OrderBy(v => v.Marca),
            "modelo" => sortOrder == "desc"
                ? vehiculos.OrderByDescending(v => v.Modelo)
                : vehiculos.OrderBy(v => v.Modelo),
            "color" => sortOrder == "desc"
                ? vehiculos.OrderByDescending(v => v.Color)
                : vehiculos.OrderBy(v => v.Color),
            "estado" => sortOrder == "desc"
                ? vehiculos.OrderByDescending(v => v.Estado)
                : vehiculos.OrderBy(v => v.Estado),
            _ => vehiculos.OrderBy(v => v.Placa)
        };

        return ordenados.ToList();
    }

    private List<Vehiculo> AplicarPaginacion(List<Vehiculo> vehiculos, int pageNumber, int pageSize)
    {
        var skip = (pageNumber - 1) * pageSize;
        return vehiculos.Skip(skip).Take(pageSize).ToList();
    }

    #endregion

    #region Métodos Privados - Mapeo y Validación

    private Vehiculo MapearDocumentoAVehiculo(DocumentSnapshot doc)
    {
        return new Vehiculo
        {
            Id = doc.Id,
            Placa = doc.GetValue<string>("Placa"),
            TipoVehiculo = doc.GetValue<string>("TipoVehiculo"),
            Marca = doc.GetValue<string>("Marca"),
            Modelo = doc.GetValue<string>("Modelo"),
            Color = doc.GetValue<string>("Color"),
            ClienteId = doc.GetValue<string>("ClienteId"),
            Estado = doc.GetValue<string>("Estado")
        };
    }

    private Dictionary<string, object> CrearDiccionarioVehiculo(Vehiculo vehiculo)
    {
        return new Dictionary<string, object>
        {
            { "Id", vehiculo.Id },
            { "Placa", vehiculo.Placa.Trim().ToUpperInvariant() },
            { "TipoVehiculo", vehiculo.TipoVehiculo },
            { "Marca", vehiculo.Marca.Trim() },
            { "Modelo", vehiculo.Modelo.Trim() },
            { "Color", vehiculo.Color.Trim() },
            { "ClienteId", vehiculo.ClienteId },
            { "Estado", vehiculo.Estado }
        };
    }

    private void ValidarVehiculo(Vehiculo vehiculo)
    {
        if (string.IsNullOrWhiteSpace(vehiculo.Placa))
            throw new ArgumentException("La placa es obligatoria");

        if (string.IsNullOrWhiteSpace(vehiculo.TipoVehiculo))
            throw new ArgumentException("El tipo de vehículo es obligatorio");

        if (string.IsNullOrWhiteSpace(vehiculo.Marca))
            throw new ArgumentException("La marca es obligatoria");

        if (string.IsNullOrWhiteSpace(vehiculo.Modelo))
            throw new ArgumentException("El modelo es obligatorio");

        if (string.IsNullOrWhiteSpace(vehiculo.Color))
            throw new ArgumentException("El color es obligatorio");

        if (string.IsNullOrWhiteSpace(vehiculo.ClienteId))
            throw new ArgumentException("El cliente es obligatorio");

        if (string.IsNullOrWhiteSpace(vehiculo.Estado))
            throw new ArgumentException("El estado es obligatorio");
    }

    #endregion

    #region Métodos Privados - Utilidades

    private List<string> ConfigurarEstadosDefecto(List<string> estados)
    {
        estados ??= new List<string>();
        if (!estados.Any())
            estados.Add(ESTADO_DEFECTO);
        return estados;
    }

    private void ValidarParametrosPaginacion(int pageNumber, int pageSize)
    {
        if (pageNumber < 1)
            throw new ArgumentException("El número de página debe ser mayor o igual a 1", nameof(pageNumber));

        if (pageSize < 1)
            throw new ArgumentException("El tamaño de página debe ser mayor o igual a 1", nameof(pageSize));
    }

    #endregion
}

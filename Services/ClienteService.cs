using Firebase.Models;
using Google.Cloud.Firestore;

/// <summary>
/// Servicio para la gestión de clientes en Firestore.
/// Proporciona operaciones CRUD, filtrado, paginación y búsqueda de clientes.
/// </summary>
public class ClienteService
{
    private const string COLLECTION_NAME = "clientes";
    private const string ESTADO_DEFECTO = "Activo";
    private readonly FirestoreDb _firestore;

    public ClienteService(FirestoreDb firestore)
    {
        _firestore = firestore ?? throw new ArgumentNullException(nameof(firestore));
    }

    #region Operaciones de Consulta

    /// <summary>
    /// Obtiene una lista paginada de clientes aplicando filtros y ordenamiento.
    /// </summary>
    public async Task<List<Cliente>> ObtenerClientes(
        List<string> estados = null,
        List<string> tiposDocumento = null,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = null,
        string sortOrder = null)
    {
        ValidarParametrosPaginacion(pageNumber, pageSize);

        sortBy ??= "NombreCompleto";
        sortOrder ??= "asc";

        var clientes = await ObtenerClientesFiltrados(estados, tiposDocumento, sortBy, sortOrder);

        return AplicarPaginacion(clientes, pageNumber, pageSize);
    }

    /// <summary>
    /// Calcula el número total de páginas para los clientes filtrados.
    /// </summary>
    public async Task<int> ObtenerTotalPaginas(
        List<string> estados,
        List<string> tiposDocumento,
        int pageSize)
    {
        if (pageSize <= 0)
            throw new ArgumentException("El tamaño de página debe ser mayor a 0", nameof(pageSize));

        var totalClientes = await ObtenerTotalClientes(estados, tiposDocumento);
        return (int)Math.Ceiling(totalClientes / (double)pageSize);
    }

    /// <summary>
    /// Obtiene un cliente específico por su ID.
    /// </summary>
    public async Task<Cliente> ObtenerCliente(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("El ID del cliente no puede estar vacío", nameof(id));

        var docRef = _firestore.Collection(COLLECTION_NAME).Document(id);
        var snapshot = await docRef.GetSnapshotAsync();

        return snapshot.Exists ? MapearDocumentoACliente(snapshot) : null;
    }

    /// <summary>
    /// Busca clientes por término de búsqueda.
    /// </summary>
    public async Task<List<Cliente>> BuscarClientes(
        string searchTerm,
        List<string> estados = null,
        List<string> tiposDocumento = null,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = null,
        string sortOrder = null)
    {
        var baseFiltrada = await ObtenerClientesFiltrados(estados, tiposDocumento, sortBy, sortOrder);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            baseFiltrada = AplicarBusqueda(baseFiltrada, searchTerm);
        }

        var ordenados = AplicarOrdenamiento(baseFiltrada, sortBy, sortOrder);
        return AplicarPaginacion(ordenados, Math.Max(pageNumber, 1), Math.Max(pageSize, 1));
    }

    /// <summary>
    /// Obtiene el total de clientes que coinciden con la búsqueda.
    /// </summary>
    public async Task<int> ObtenerTotalClientesBusqueda(
        string searchTerm,
        List<string> estados,
        List<string> tiposDocumento)
    {
        estados = ConfigurarEstadosDefecto(estados);
        var baseFiltrada = await ObtenerClientesFiltrados(estados, tiposDocumento, "NombreCompleto", "asc");

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            baseFiltrada = AplicarBusqueda(baseFiltrada, searchTerm);
        }

        return baseFiltrada.Count;
    }

    /// <summary>
    /// Obtiene todos los clientes por tipo de documento.
    /// </summary>
    public async Task<List<Cliente>> ObtenerClientesPorTipoDocumento(string tipoDocumento)
    {
        if (string.IsNullOrWhiteSpace(tipoDocumento))
            return new List<Cliente>();

        var snapshot = await _firestore.Collection(COLLECTION_NAME)
            .WhereEqualTo("TipoDocumento", tipoDocumento)
            .GetSnapshotAsync();

        return snapshot.Documents.Select(MapearDocumentoACliente).ToList();
    }

    #endregion

    #region Operaciones CRUD

    /// <summary>
    /// Crea un nuevo cliente en la base de datos.
    /// </summary>
    public async Task CrearCliente(Cliente cliente)
    {
        if (cliente == null)
            throw new ArgumentNullException(nameof(cliente));

        if (await ExisteClienteConDocumento(cliente.TipoDocumento, cliente.NumeroDocumento))
        {
            throw new ArgumentException(
                $"Ya existe un cliente con el documento {cliente.TipoDocumento}: {cliente.NumeroDocumento}");
        }

        ValidarCliente(cliente);

        var clienteRef = _firestore.Collection(COLLECTION_NAME).Document();
        cliente.Id = clienteRef.Id;

        var clienteData = CrearDiccionarioCliente(cliente);
        await clienteRef.SetAsync(clienteData);
    }

    /// <summary>
    /// Actualiza un cliente existente en la base de datos.
    /// </summary>
    public async Task ActualizarCliente(Cliente cliente)
    {
        if (cliente == null)
            throw new ArgumentNullException(nameof(cliente));

        if (string.IsNullOrWhiteSpace(cliente.Id))
            throw new ArgumentException("El ID del cliente es obligatorio para actualizar", nameof(cliente));

        ValidarCliente(cliente);

        var clienteRef = _firestore.Collection(COLLECTION_NAME).Document(cliente.Id);
        var clienteData = CrearDiccionarioCliente(cliente);
        await clienteRef.SetAsync(clienteData, SetOptions.Overwrite);
    }

    /// <summary>
    /// Cambia el estado de un cliente específico.
    /// </summary>
    public async Task CambiarEstadoCliente(string id, string nuevoEstado)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("El ID del cliente no puede estar vacío", nameof(id));

        if (string.IsNullOrWhiteSpace(nuevoEstado))
            throw new ArgumentException("El nuevo estado no puede estar vacío", nameof(nuevoEstado));

        var clienteRef = _firestore.Collection(COLLECTION_NAME).Document(id);
        await clienteRef.UpdateAsync("Estado", nuevoEstado);
    }

    /// <summary>
    /// Agrega un vehículo a la lista de vehículos del cliente.
    /// </summary>
    public async Task AgregarVehiculoACliente(string clienteId, string vehiculoId)
    {
        if (string.IsNullOrWhiteSpace(clienteId))
            throw new ArgumentException("El ID del cliente no puede estar vacío", nameof(clienteId));

        if (string.IsNullOrWhiteSpace(vehiculoId))
            throw new ArgumentException("El ID del vehículo no puede estar vacío", nameof(vehiculoId));

        var clienteRef = _firestore.Collection(COLLECTION_NAME).Document(clienteId);
        await clienteRef.UpdateAsync("VehiculosIds", FieldValue.ArrayUnion(vehiculoId));
    }

    /// <summary>
    /// Remueve un vehículo de la lista de vehículos del cliente.
    /// </summary>
    public async Task RemoverVehiculoDeCliente(string clienteId, string vehiculoId)
    {
        if (string.IsNullOrWhiteSpace(clienteId))
            throw new ArgumentException("El ID del cliente no puede estar vacío", nameof(clienteId));

        if (string.IsNullOrWhiteSpace(vehiculoId))
            throw new ArgumentException("El ID del vehículo no puede estar vacío", nameof(vehiculoId));

        var clienteRef = _firestore.Collection(COLLECTION_NAME).Document(clienteId);
        await clienteRef.UpdateAsync("VehiculosIds", FieldValue.ArrayRemove(vehiculoId));
    }

    #endregion

    #region Operaciones de Verificación

    /// <summary>
    /// Verifica si existe un cliente con el documento especificado.
    /// </summary>
    public async Task<bool> ExisteClienteConDocumento(string tipoDocumento, string numeroDocumento, string idActual = null)
    {
        if (string.IsNullOrWhiteSpace(tipoDocumento)) return false;
        if (string.IsNullOrWhiteSpace(numeroDocumento)) return false;

        var snapshot = await _firestore.Collection(COLLECTION_NAME)
            .WhereEqualTo("TipoDocumento", tipoDocumento)
            .WhereEqualTo("NumeroDocumento", numeroDocumento.Trim())
            .GetSnapshotAsync();

        return snapshot.Documents
            .Where(doc => idActual == null || doc.Id != idActual)
            .Any();
    }

    #endregion

    #region Métodos Privados - Consultas Base

    private async Task<List<Cliente>> ObtenerClientesFiltrados(
        List<string> estados,
        List<string> tiposDocumento,
        string sortBy,
        string sortOrder)
    {
        estados = ConfigurarEstadosDefecto(estados);

        var query = ConstruirQueryFiltros(estados, tiposDocumento);

        var snapshot = await query.GetSnapshotAsync();
        var clientes = snapshot.Documents
            .Select(MapearDocumentoACliente)
            .ToList();

        return AplicarOrdenamiento(clientes, sortBy, sortOrder);
    }

    private async Task<int> ObtenerTotalClientes(
        List<string> estados,
        List<string> tiposDocumento)
    {
        estados = ConfigurarEstadosDefecto(estados);

        var query = ConstruirQueryFiltros(estados, tiposDocumento);
        var snapshot = await query.GetSnapshotAsync();

        return snapshot.Count;
    }

    private Query ConstruirQueryFiltros(List<string> estados, List<string> tiposDocumento)
    {
        Query query = _firestore.Collection(COLLECTION_NAME);

        if (estados != null && estados.Any())
        {
            query = query.WhereIn("Estado", estados);
        }

        if (tiposDocumento != null && tiposDocumento.Any())
        {
            query = query.WhereIn("TipoDocumento", tiposDocumento);
        }

        return query;
    }

    #endregion

    #region Métodos Privados - Búsqueda y Filtrado

    private List<Cliente> AplicarBusqueda(List<Cliente> clientes, string searchTerm)
    {
        var termLower = searchTerm.ToLowerInvariant().Trim();

        return clientes.Where(c =>
            c.NombreCompleto.ToLowerInvariant().Contains(termLower) ||
            c.NumeroDocumento.ToLowerInvariant().Contains(termLower) ||
            c.Email.ToLowerInvariant().Contains(termLower) ||
            c.Telefono.Contains(termLower)
        ).ToList();
    }

    #endregion

    #region Métodos Privados - Ordenamiento y Paginación

    private List<Cliente> AplicarOrdenamiento(List<Cliente> clientes, string sortBy, string sortOrder)
    {
        sortBy ??= "NombreCompleto";
        sortOrder ??= "asc";

        var ordenados = sortBy.ToLowerInvariant() switch
        {
            "nombrecompleto" => sortOrder == "desc"
                ? clientes.OrderByDescending(c => c.NombreCompleto)
                : clientes.OrderBy(c => c.NombreCompleto),
            "tipodocumento" => sortOrder == "desc"
                ? clientes.OrderByDescending(c => c.TipoDocumento)
                : clientes.OrderBy(c => c.TipoDocumento),
            "numerodocumento" => sortOrder == "desc"
                ? clientes.OrderByDescending(c => c.NumeroDocumento)
                : clientes.OrderBy(c => c.NumeroDocumento),
            "email" => sortOrder == "desc"
                ? clientes.OrderByDescending(c => c.Email)
                : clientes.OrderBy(c => c.Email),
            "telefono" => sortOrder == "desc"
                ? clientes.OrderByDescending(c => c.Telefono)
                : clientes.OrderBy(c => c.Telefono),
            "estado" => sortOrder == "desc"
                ? clientes.OrderByDescending(c => c.Estado)
                : clientes.OrderBy(c => c.Estado),
            _ => clientes.OrderBy(c => c.NombreCompleto)
        };

        return ordenados.ToList();
    }

    private List<Cliente> AplicarPaginacion(List<Cliente> clientes, int pageNumber, int pageSize)
    {
        var skip = (pageNumber - 1) * pageSize;
        return clientes.Skip(skip).Take(pageSize).ToList();
    }

    #endregion

    #region Métodos Privados - Mapeo y Validación

    private Cliente MapearDocumentoACliente(DocumentSnapshot doc)
    {
        return new Cliente
        {
            Id = doc.Id,
            TipoDocumento = doc.GetValue<string>("TipoDocumento"),
            NumeroDocumento = doc.GetValue<string>("NumeroDocumento"),
            NombreCompleto = doc.GetValue<string>("NombreCompleto"),
            Telefono = doc.GetValue<string>("Telefono"),
            Email = doc.GetValue<string>("Email"),
            VehiculosIds = doc.ContainsField("VehiculosIds")
                ? doc.GetValue<List<string>>("VehiculosIds") ?? new List<string>()
                : new List<string>(),
            Estado = doc.GetValue<string>("Estado")
        };
    }

    private Dictionary<string, object> CrearDiccionarioCliente(Cliente cliente)
    {
        return new Dictionary<string, object>
        {
            { "Id", cliente.Id },
            { "TipoDocumento", cliente.TipoDocumento },
            { "NumeroDocumento", cliente.NumeroDocumento.Trim() },
            { "NombreCompleto", cliente.NombreCompleto.Trim() },
            { "Telefono", cliente.Telefono.Trim() },
            { "Email", cliente.Email.Trim() },
            { "VehiculosIds", cliente.VehiculosIds ?? new List<string>() },
            { "Estado", cliente.Estado }
        };
    }

    private void ValidarCliente(Cliente cliente)
    {
        if (string.IsNullOrWhiteSpace(cliente.TipoDocumento))
            throw new ArgumentException("El tipo de documento es obligatorio");

        if (string.IsNullOrWhiteSpace(cliente.NumeroDocumento))
            throw new ArgumentException("El número de documento es obligatorio");

        if (string.IsNullOrWhiteSpace(cliente.NombreCompleto))
            throw new ArgumentException("El nombre completo es obligatorio");

        if (string.IsNullOrWhiteSpace(cliente.Telefono))
            throw new ArgumentException("El teléfono es obligatorio");

        if (string.IsNullOrWhiteSpace(cliente.Email))
            throw new ArgumentException("El email es obligatorio");

        if (string.IsNullOrWhiteSpace(cliente.Estado))
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

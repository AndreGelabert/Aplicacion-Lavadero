using Firebase.Models;
using Google.Cloud.Firestore;

/// <summary>
/// Servicio para la gestión de servicios del lavadero en Firestore.
/// Proporciona operaciones CRUD, filtrado, paginación y validación de servicios.
/// </summary>
public class ServicioService
{
    #region Constantes
    private const string COLLECTION_NAME = "servicios";
    private const string ESTADO_DEFECTO = "Activo";
    private const string TIPO_VEHICULO_DEFECTO = "General";
    private const string ORDEN_DEFECTO = "Nombre";
    private const string DIRECCION_DEFECTO = "asc";
    #endregion

    #region Dependencias
    private readonly FirestoreDb _firestore;

    /// <summary>
    /// Inicializa una nueva instancia del servicio de servicios
    /// </summary>
    /// <param name="firestore">Instancia de la base de datos Firestore</param>
    public ServicioService(FirestoreDb firestore)
    {
        _firestore = firestore ?? throw new ArgumentNullException(nameof(firestore));
    }
    #endregion

    #region Operaciones de Consulta

    /// <summary>
    /// Obtiene una lista paginada de servicios aplicando filtros y ordenamiento
    /// </summary>
    /// <param name="estados">Lista de estados a filtrar (null para solo activos)</param>
    /// <param name="tipos">Lista de tipos de servicio a filtrar (null para todos)</param>
    /// <param name="tiposVehiculo">Lista de tipos de vehículo a filtrar (null para todos)</param>
    /// <param name="pageNumber">Número de página (1-based)</param>
    /// <param name="pageSize">Cantidad de elementos por página</param>
    /// <param name="sortBy">Campo por el cual ordenar</param>
    /// <param name="sortOrder">Dirección del ordenamiento (asc/desc)</param>
    /// <returns>Lista de servicios filtrados, ordenados y paginados</returns>
    public async Task<List<Servicio>> ObtenerServicios(
        List<string> estados = null,
        List<string> tipos = null,
        List<string> tiposVehiculo = null,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = null,
        string sortOrder = null)
    {
        // Validar parámetros de paginación
        ValidarParametrosPaginacion(pageNumber, pageSize);

        // Configurar ordenamiento por defecto
        sortBy ??= ORDEN_DEFECTO;
        sortOrder ??= DIRECCION_DEFECTO;

        // Obtener servicios aplicando filtros y ordenamiento
        var servicios = await ObtenerServiciosFiltrados(estados, tipos, tiposVehiculo, sortBy, sortOrder);

        // Aplicar paginación
        return servicios
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();
    }

    /// <summary>
    /// Obtiene una lista paginada de servicios (versión sobrecargada para compatibilidad)
    /// </summary>
    public async Task<List<Servicio>> ObtenerServicios(
        List<string> estados,
        List<string> tipos,
        List<string> tiposVehiculo,
        string firstDocId,
        string lastDocId,
        int pageNumber,
        int pageSize)
    {
        // Los parámetros firstDocId y lastDocId son ignorados (legacy)
        return await ObtenerServicios(estados, tipos, tiposVehiculo, pageNumber, pageSize);
    }

    /// <summary>
    /// Calcula el número total de páginas para los servicios filtrados
    /// </summary>
    /// <param name="estados">Lista de estados a filtrar</param>
    /// <param name="tipos">Lista de tipos de servicio a filtrar</param>
    /// <param name="tiposVehiculo">Lista de tipos de vehículo a filtrar</param>
    /// <param name="pageSize">Cantidad de elementos por página</param>
    /// <returns>Número total de páginas</returns>
    public async Task<int> ObtenerTotalPaginas(
        List<string> estados,
        List<string> tipos,
        List<string> tiposVehiculo,
        int pageSize)
    {
        if (pageSize <= 0)
            throw new ArgumentException("El tamaño de página debe ser mayor a 0", nameof(pageSize));

        var totalServicios = await ObtenerTotalServicios(estados, tipos, tiposVehiculo);
        return (int)Math.Ceiling(totalServicios / (double)pageSize);
    }

    /// <summary>
    /// Obtiene un servicio específico por su ID
    /// </summary>
    /// <param name="id">ID del servicio</param>
    /// <returns>El servicio encontrado o null si no existe</returns>
    public async Task<Servicio> ObtenerServicio(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("El ID del servicio no puede estar vacío", nameof(id));

        var docRef = _firestore.Collection(COLLECTION_NAME).Document(id);
        var snapshot = await docRef.GetSnapshotAsync();

        return snapshot.Exists ? MapearDocumentoAServicio(snapshot) : null;
    }

    /// <summary>
    /// Obtiene todos los servicios de un tipo específico
    /// </summary>
    /// <param name="tipo">Tipo de servicio a buscar</param>
    /// <returns>Lista de servicios del tipo especificado</returns>
    public async Task<List<Servicio>> ObtenerServiciosPorTipo(string tipo)
        => await ObtenerServiciosPorCampo("Tipo", tipo);

    /// <summary>
    /// Obtiene todos los servicios para un tipo de vehículo específico
    /// </summary>
    /// <param name="tipoVehiculo">Tipo de vehículo a buscar</param>
    /// <returns>Lista de servicios para el tipo de vehículo especificado</returns>
    public async Task<List<Servicio>> ObtenerServiciosPorTipoVehiculo(string tipoVehiculo)
        => await ObtenerServiciosPorCampo("TipoVehiculo", tipoVehiculo);
    #endregion

    #region Operaciones CRUD

    /// <summary>
    /// Crea un nuevo servicio en la base de datos
    /// </summary>
    /// <param name="servicio">Servicio a crear</param>
    /// <exception cref="ArgumentException">Si ya existe un servicio con el mismo nombre y tipo de vehículo</exception>
    public async Task CrearServicio(Servicio servicio)
    {
        if (servicio == null)
            throw new ArgumentNullException(nameof(servicio));

        // Validar duplicados
        if (await ExisteServicioConNombreTipoVehiculo(servicio.Nombre, servicio.TipoVehiculo))
        {
            throw new ArgumentException(
                $"Ya existe un servicio con el nombre '{servicio.Nombre}' para vehículos tipo '{servicio.TipoVehiculo}'");
        }

        // Validar datos del servicio
        ValidarServicio(servicio);

        // Generar ID y guardar
        var servicioRef = _firestore.Collection(COLLECTION_NAME).Document();
        servicio.Id = servicioRef.Id;

        var servicioData = CrearDiccionarioServicio(servicio);
        await servicioRef.SetAsync(servicioData);
    }

    /// <summary>
    /// Actualiza un servicio existente en la base de datos
    /// </summary>
    /// <param name="servicio">Servicio con los datos actualizados</param>
    /// <exception cref="ArgumentException">Si ya existe otro servicio con el mismo nombre y tipo de vehículo</exception>
    public async Task ActualizarServicio(Servicio servicio)
    {
        if (servicio == null)
            throw new ArgumentNullException(nameof(servicio));

        if (string.IsNullOrWhiteSpace(servicio.Id))
            throw new ArgumentException("El ID del servicio es obligatorio para actualizar", nameof(servicio));

        // Validar duplicados (excluyendo el servicio actual)
        if (await ExisteServicioConNombreTipoVehiculo(servicio.Nombre, servicio.TipoVehiculo, servicio.Id))
        {
            throw new ArgumentException(
                $"Ya existe un servicio con el nombre '{servicio.Nombre}' para vehículos tipo '{servicio.TipoVehiculo}'");
        }

        // Validar datos del servicio
        ValidarServicio(servicio);

        // Actualizar en Firestore
        var servicioRef = _firestore.Collection(COLLECTION_NAME).Document(servicio.Id);
        var servicioData = CrearDiccionarioServicio(servicio);
        await servicioRef.SetAsync(servicioData, SetOptions.Overwrite);
    }

    /// <summary>
    /// Cambia el estado de un servicio específico
    /// </summary>
    /// <param name="id">ID del servicio</param>
    /// <param name="nuevoEstado">Nuevo estado del servicio</param>
    public async Task CambiarEstadoServicio(string id, string nuevoEstado)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("El ID del servicio no puede estar vacío", nameof(id));

        if (string.IsNullOrWhiteSpace(nuevoEstado))
            throw new ArgumentException("El nuevo estado no puede estar vacío", nameof(nuevoEstado));

        var servicioRef = _firestore.Collection(COLLECTION_NAME).Document(id);
        await servicioRef.UpdateAsync("Estado", nuevoEstado);
    }
    #endregion

    #region Operaciones de Verificación

    /// <summary>
    /// Verifica si existe un servicio con el nombre y tipo de vehículo especificados
    /// </summary>
    /// <param name="nombre">Nombre del servicio</param>
    /// <param name="tipoVehiculo">Tipo de vehículo</param>
    /// <param name="idActual">ID del servicio actual (para excluir en actualizaciones)</param>
    /// <returns>True si existe un servicio con esos criterios</returns>
    public async Task<bool> ExisteServicioConNombreTipoVehiculo(string nombre, string tipoVehiculo, string idActual = null)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return false;

        if (string.IsNullOrWhiteSpace(tipoVehiculo))
            return false;

        var coleccion = _firestore.Collection(COLLECTION_NAME);
        var querySnapshot = await coleccion
            .WhereEqualTo("TipoVehiculo", tipoVehiculo)
            .GetSnapshotAsync();

        return querySnapshot.Documents
            .Where(doc => idActual == null || doc.Id != idActual)
            .Any(doc => doc.GetValue<string>("Nombre")
                .Trim()
                .Equals(nombre.Trim(), StringComparison.OrdinalIgnoreCase));
    }
    #endregion

    #region Métodos Privados - Consultas Base

    /// <summary>
    /// Obtiene todos los servicios aplicando los filtros y ordenamiento especificados
    /// </summary>
    private async Task<List<Servicio>> ObtenerServiciosFiltrados(
        List<string> estados,
        List<string> tipos,
        List<string> tiposVehiculo,
        string sortBy,
        string sortOrder)
    {
        // Configurar estados por defecto
        estados = ConfigurarEstadosDefecto(estados);

        // Construir query base
        var query = ConstruirQueryFiltros(estados, tipos, incluirOrdenamiento: false);

        // Ejecutar query
        var snapshot = await query.GetSnapshotAsync();
        var servicios = snapshot.Documents
            .Select(MapearDocumentoAServicio)
            .ToList();

        // Aplicar filtro de tipo de vehículo (post-procesamiento debido a limitaciones de Firestore)
        servicios = AplicarFiltroTipoVehiculo(servicios, tiposVehiculo);

        // Aplicar ordenamiento en memoria
        return AplicarOrdenamiento(servicios, sortBy, sortOrder);
    }

    /// <summary>
    /// Obtiene el total de servicios que cumplen con los filtros especificados
    /// </summary>
    private async Task<int> ObtenerTotalServicios(
        List<string> estados,
        List<string> tipos,
        List<string> tiposVehiculo)
    {
        // Configurar estados por defecto
        estados = ConfigurarEstadosDefecto(estados);

        // Construir query base (sin ordenamiento para mejor performance)
        var query = ConstruirQueryFiltros(estados, tipos, incluirOrdenamiento: false);

        // Ejecutar query con mapeo ligero para contar
        var snapshot = await query.GetSnapshotAsync();
        var servicios = snapshot.Documents
            .Select(doc => new ServicioConteo
            {
                TipoVehiculo = doc.ContainsField("TipoVehiculo")
                    ? doc.GetValue<string>("TipoVehiculo")
                    : TIPO_VEHICULO_DEFECTO
            })
            .ToList();

        // Aplicar filtro de tipo de vehículo
        if (tiposVehiculo != null && tiposVehiculo.Any())
        {
            servicios = servicios
                .Where(s => tiposVehiculo.Contains(s.TipoVehiculo))
                .ToList();
        }

        return servicios.Count;
    }

    /// <summary>
    /// Obtiene servicios filtrados por un campo específico
    /// </summary>
    private async Task<List<Servicio>> ObtenerServiciosPorCampo(string campo, string valor)
    {
        if (string.IsNullOrWhiteSpace(campo))
            throw new ArgumentException("El campo no puede estar vacío", nameof(campo));

        if (string.IsNullOrWhiteSpace(valor))
            return new List<Servicio>();

        var coleccion = _firestore.Collection(COLLECTION_NAME);
        var querySnapshot = await coleccion
            .WhereEqualTo(campo, valor)
            .GetSnapshotAsync();

        return querySnapshot.Documents
            .Select(MapearDocumentoAServicio)
            .ToList();
    }
    #endregion

    #region Métodos Privados - Utilidades

    /// <summary>
    /// Configura la lista de estados por defecto si no se especificó ninguno
    /// </summary>
    private static List<string> ConfigurarEstadosDefecto(List<string> estados)
    {
        estados ??= new List<string>();
        if (!estados.Any())
        {
            estados.Add(ESTADO_DEFECTO);
        }
        return estados;
    }

    /// <summary>
    /// Construye un query de Firestore aplicando los filtros especificados
    /// </summary>
    private Query ConstruirQueryFiltros(
        List<string> estados, 
        List<string> tipos,
        bool incluirOrdenamiento = true)
    {
        Query query = _firestore.Collection(COLLECTION_NAME);

        // Aplicar filtro de estados
        if (estados?.Any() == true)
        {
            query = query.WhereIn("Estado", estados);
        }

        // Aplicar filtro de tipos de servicio
        if (tipos?.Any() == true)
        {
            query = query.WhereIn("Tipo", tipos);
        }

        return query;
    }

    /// <summary>
    /// Aplica filtro de tipo de vehículo a una lista de servicios
    /// </summary>
    private static List<Servicio> AplicarFiltroTipoVehiculo(List<Servicio> servicios, List<string> tiposVehiculo)
    {
        if (tiposVehiculo?.Any() == true)
        {
            return servicios
                .Where(s => tiposVehiculo.Contains(s.TipoVehiculo))
                .ToList();
        }
        return servicios;
    }

    /// <summary>
    /// Aplica ordenamiento a una lista de servicios
    /// </summary>
    private static List<Servicio> AplicarOrdenamiento(List<Servicio> servicios, string sortBy, string sortOrder)
    {
        var descending = sortOrder?.ToLower() == "desc";

        return sortBy?.ToLower() switch
        {
            "nombre" => descending 
                ? servicios.OrderByDescending(s => s.Nombre).ToList()
                : servicios.OrderBy(s => s.Nombre).ToList(),
                
            "precio" => descending 
                ? servicios.OrderByDescending(s => s.Precio).ToList()
                : servicios.OrderBy(s => s.Precio).ToList(),
                
            "tipo" => descending 
                ? servicios.OrderByDescending(s => s.Tipo).ToList()
                : servicios.OrderBy(s => s.Tipo).ToList(),
                
            "tipovehiculo" => descending 
                ? servicios.OrderByDescending(s => s.TipoVehiculo).ToList()
                : servicios.OrderBy(s => s.TipoVehiculo).ToList(),
                
            "tiempoestimado" => descending 
                ? servicios.OrderByDescending(s => s.TiempoEstimado).ToList()
                : servicios.OrderBy(s => s.TiempoEstimado).ToList(),
                
            "estado" => descending 
                ? servicios.OrderByDescending(s => s.Estado).ToList()
                : servicios.OrderBy(s => s.Estado).ToList(),
                
            _ => servicios.OrderBy(s => s.Nombre).ToList() // Default por nombre ascendente
        };
    }

    /// <summary>
    /// Valida los parámetros de paginación
    /// </summary>
    private static void ValidarParametrosPaginacion(int pageNumber, int pageSize)
    {
        if (pageNumber <= 0)
            throw new ArgumentException("El número de página debe ser mayor a 0", nameof(pageNumber));

        if (pageSize <= 0)
            throw new ArgumentException("El tamaño de página debe ser mayor a 0", nameof(pageSize));
    }

    /// <summary>
    /// Crea el diccionario de datos para guardar/actualizar un servicio en Firestore
    /// </summary>
    private static Dictionary<string, object> CrearDiccionarioServicio(Servicio servicio)
    {
        return new Dictionary<string, object>
        {
            { "Nombre", servicio.Nombre },
            { "Precio", (double)servicio.Precio }, // Firestore requiere double
            { "Tipo", servicio.Tipo },
            { "TipoVehiculo", servicio.TipoVehiculo },
            { "TiempoEstimado", servicio.TiempoEstimado },
            { "Descripcion", servicio.Descripcion },
            { "Estado", servicio.Estado }
        };
    }

    /// <summary>
    /// Mapea un documento de Firestore a un objeto Servicio
    /// </summary>
    private static Servicio MapearDocumentoAServicio(DocumentSnapshot documento)
    {
        return new Servicio
        {
            Id = documento.Id,
            Nombre = documento.GetValue<string>("Nombre"),
            Precio = documento.ContainsField("Precio")
                ? (decimal)Convert.ToDouble(documento.GetValue<object>("Precio"))
                : 0m,
            Tipo = documento.GetValue<string>("Tipo"),
            TipoVehiculo = documento.ContainsField("TipoVehiculo")
                ? documento.GetValue<string>("TipoVehiculo")
                : TIPO_VEHICULO_DEFECTO,
            TiempoEstimado = documento.ContainsField("TiempoEstimado")
                ? documento.GetValue<int>("TiempoEstimado")
                : 0,
            Descripcion = documento.GetValue<string>("Descripcion"),
            Estado = documento.GetValue<string>("Estado")
        };
    }

    /// <summary>
    /// Valida los datos de un servicio antes de guardarlo
    /// </summary>
    private static void ValidarServicio(Servicio servicio)
    {
        var errores = new List<string>();

        // Validaciones de campos requeridos
        if (string.IsNullOrWhiteSpace(servicio.Nombre))
            errores.Add("El nombre del servicio no puede estar vacío");

        if (string.IsNullOrWhiteSpace(servicio.Tipo))
            errores.Add("El tipo de servicio no puede estar vacío");

        if (string.IsNullOrWhiteSpace(servicio.TipoVehiculo))
            errores.Add("El tipo de vehículo no puede estar vacío");

        if (string.IsNullOrWhiteSpace(servicio.Descripcion))
            errores.Add("La descripción no puede estar vacía");

        // Validaciones de formato y rango
        if (!string.IsNullOrWhiteSpace(servicio.Nombre) &&
            !System.Text.RegularExpressions.Regex.IsMatch(servicio.Nombre, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$"))
        {
            errores.Add("El nombre solo puede contener letras y espacios");
        }

        if (servicio.Precio < 0)
            errores.Add("El precio debe ser igual o mayor a 0");

        if (servicio.TiempoEstimado <= 0)
            errores.Add("El tiempo estimado debe ser mayor a 0");

        // Lanzar excepción con todos los errores si existen
        if (errores.Any())
        {
            throw new ArgumentException($"Errores de validación: {string.Join(", ", errores)}");
        }
    }
    #endregion

    #region Clases Auxiliares

    /// <summary>
    /// Clase auxiliar para conteo eficiente de servicios
    /// </summary>
    private class ServicioConteo
    {
        public string TipoVehiculo { get; set; }
    }
    #endregion
}
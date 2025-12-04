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
    /// Inicializa una nueva instancia del servicio de servicios.
    /// </summary>
    /// <param name="firestore">Instancia de la base de datos Firestore.</param>
    public ServicioService(FirestoreDb firestore)
    {
        _firestore = firestore ?? throw new ArgumentNullException(nameof(firestore));
    }
    #endregion

    #region Operaciones de Consulta

    /// <summary>
    /// Obtiene una lista paginada de servicios aplicando filtros y ordenamiento.
    /// </summary>
    /// <param name="estados">Lista de estados a filtrar (null para solo activos por defecto).</param>
    /// <param name="tipos">Lista de tipos de servicio a filtrar (null para todos).</param>
    /// <param name="tiposVehiculo">Lista de tipos de vehículo a filtrar (null para todos).</param>
    /// <param name="pageNumber">Número de página (1-based).</param>
    /// <param name="pageSize">Cantidad de elementos por página.</param>
    /// <param name="sortBy">Campo por el cual ordenar.</param>
    /// <param name="sortOrder">Dirección del ordenamiento (asc/desc).</param>
    /// <returns>Lista de servicios filtrados, ordenados y paginados.</returns>
    public async Task<List<Servicio>> ObtenerServicios(
        List<string> estados = null,
        List<string> tipos = null,
        List<string> tiposVehiculo = null,
        decimal? precioDesde = null,
        decimal? precioHasta = null,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = null,
        string sortOrder = null)
    {
        ValidarParametrosPaginacion(pageNumber, pageSize);

        sortBy ??= ORDEN_DEFECTO;
        sortOrder ??= DIRECCION_DEFECTO;

        var servicios = await ObtenerServiciosFiltrados(estados, tipos, tiposVehiculo, precioDesde, precioHasta, sortBy, sortOrder);

        return AplicarPaginacion(servicios, pageNumber, pageSize);
    }

    /// <summary>
    /// Calcula el número total de páginas para los servicios filtrados.
    /// </summary>
    /// <param name="estados">Lista de estados a filtrar.</param>
    /// <param name="tipos">Lista de tipos de servicio a filtrar.</param>
    /// <param name="tiposVehiculo">Lista de tipos de vehículo a filtrar.</param>
    /// <param name="precioDesde">Precio mínimo a filtrar.</param>
    /// <param name="precioHasta">Precio máximo a filtrar.</param>
    /// <param name="pageSize">Cantidad de elementos por página.</param>
    /// <returns>Número total de páginas.</returns>
    public async Task<int> ObtenerTotalPaginas(
        List<string> estados,
        List<string> tipos,
        List<string> tiposVehiculo,
        decimal? precioDesde,
        decimal? precioHasta,
        int pageSize)
    {
        if (pageSize <= 0)
            throw new ArgumentException("El tamaño de página debe ser mayor a 0", nameof(pageSize));

        var totalServicios = await ObtenerTotalServicios(estados, tipos, tiposVehiculo, precioDesde, precioHasta);
        return (int)Math.Ceiling(totalServicios / (double)pageSize);
    }

    /// <summary>
    /// Obtiene un servicio específico por su ID.
    /// </summary>
    /// <param name="id">ID del servicio.</param>
    /// <returns>El servicio encontrado o null si no existe.</returns>
    public async Task<Servicio> ObtenerServicio(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("El ID del servicio no puede estar vacío", nameof(id));

        var docRef = _firestore.Collection(COLLECTION_NAME).Document(id);
        var snapshot = await docRef.GetSnapshotAsync();

        return snapshot.Exists ? MapearDocumentoAServicio(snapshot) : null;
    }

    /// <summary>
    /// Busca servicios por término de búsqueda (texto/número/"X min").
    /// Incluye coincidencias por cantidad de etapas.
    /// </summary>
    /// <param name="searchTerm">Término de búsqueda.</param>
    /// <param name="estados">Lista de estados a filtrar.</param>
    /// <param name="tipos">Lista de tipos de servicio a filtrar.</param>
    /// <param name="tiposVehiculo">Lista de tipos de vehículo a filtrar.</param>
    /// <param name="precioDesde">Precio mínimo a filtrar.</param>
    /// <param name="precioHasta">Precio máximo a filtrar.</param>
    /// <param name="pageNumber">Número de página actual.</param>
    /// <param name="pageSize">Tamaño de página.</param>
    /// <param name="sortBy">Campo por el cual ordenar.</param>
    /// <param name="sortOrder">Dirección del ordenamiento.</param>
    /// <returns>Lista de servicios que coinciden con la búsqueda.</returns>
    public async Task<List<Servicio>> BuscarServicios(
        string searchTerm,
        List<string> estados = null,
        List<string> tipos = null,
        List<string> tiposVehiculo = null,
        decimal? precioDesde = null,
        decimal? precioHasta = null,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = null,
        string sortOrder = null)
    {
        var baseFiltrada = await ObtenerServiciosFiltrados(estados, tipos, tiposVehiculo, precioDesde, precioHasta, sortBy, sortOrder);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            baseFiltrada = AplicarBusqueda(baseFiltrada, searchTerm);
        }

        var ordenados = AplicarOrdenamiento(baseFiltrada, sortBy, sortOrder);
        return AplicarPaginacion(ordenados, Math.Max(pageNumber, 1), Math.Max(pageSize, 1));
    }

    /// <summary>
    /// Obtiene el total de servicios que coinciden con la búsqueda.
    /// Usa la misma lógica de <see cref="BuscarServicios"/>.
    /// </summary>
    /// <param name="searchTerm">Término de búsqueda.</param>
    /// <param name="estados">Lista de estados a filtrar.</param>
    /// <param name="tipos">Lista de tipos de servicio a filtrar.</param>
    /// <param name="tiposVehiculo">Lista de tipos de vehículo a filtrar.</param>
    /// <param name="precioDesde">Precio mínimo a filtrar.</param>
    /// <param name="precioHasta">Precio máximo a filtrar.</param>
    /// <returns>Número total de servicios que coinciden.</returns>
    public async Task<int> ObtenerTotalServiciosBusqueda(
        string searchTerm,
        List<string> estados,
        List<string> tipos,
        List<string> tiposVehiculo,
        decimal? precioDesde,
        decimal? precioHasta)
    {
        estados = ConfigurarEstadosDefecto(estados);
        var baseFiltrada = await ObtenerServiciosFiltrados(estados, tipos, tiposVehiculo, precioDesde, precioHasta, ORDEN_DEFECTO, DIRECCION_DEFECTO);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            baseFiltrada = AplicarBusqueda(baseFiltrada, searchTerm);
        }

        return baseFiltrada.Count;
    }

    /// <summary>
    /// Obtiene todos los servicios de un tipo específico.
    /// </summary>
    /// <param name="tipo">Tipo de servicio.</param>
    /// <returns>Lista de servicios del tipo especificado.</returns>
    public async Task<List<Servicio>> ObtenerServiciosPorTipo(string tipo)
        => await ObtenerServiciosPorCampo("Tipo", tipo);

    /// <summary>
    /// Obtiene todos los servicios para un tipo de vehículo específico.
    /// </summary>
    /// <param name="tipoVehiculo">Tipo de vehículo.</param>
    /// <returns>Lista de servicios para el tipo de vehículo especificado.</returns>
    public async Task<List<Servicio>> ObtenerServiciosPorTipoVehiculo(string tipoVehiculo)
        => await ObtenerServiciosPorCampo("TipoVehiculo", tipoVehiculo);
    #endregion

    #region Operaciones CRUD

    /// <summary>
    /// Crea un nuevo servicio en la base de datos.
    /// Valida duplicados y datos de entrada.
    /// </summary>
    /// <param name="servicio">Servicio a crear.</param>
    public async Task CrearServicio(Servicio servicio)
    {
        if (servicio == null)
            throw new ArgumentNullException(nameof(servicio));

        if (await ExisteServicioConNombreTipoVehiculo(servicio.Nombre, servicio.TipoVehiculo))
        {
            throw new ArgumentException(
                $"Ya existe un servicio con el nombre '{servicio.Nombre}' para vehículos tipo '{servicio.TipoVehiculo}'");
        }

        ValidarServicio(servicio);

        var servicioRef = _firestore.Collection(COLLECTION_NAME).Document();
        servicio.Id = servicioRef.Id;

        var servicioData = CrearDiccionarioServicio(servicio);
        await servicioRef.SetAsync(servicioData);
    }

    /// <summary>
    /// Actualiza un servicio existente en la base de datos.
    /// Valida duplicados y datos de entrada.
    /// </summary>
    /// <param name="servicio">Servicio a actualizar.</param>
    public async Task ActualizarServicio(Servicio servicio)
    {
        if (servicio == null)
            throw new ArgumentNullException(nameof(servicio));

        if (string.IsNullOrWhiteSpace(servicio.Id))
            throw new ArgumentException("El ID del servicio es obligatorio para actualizar", nameof(servicio));

        if (await ExisteServicioConNombreTipoVehiculo(servicio.Nombre, servicio.TipoVehiculo, servicio.Id))
        {
            throw new ArgumentException(
                $"Ya existe un servicio con el nombre '{servicio.Nombre}' para vehículos tipo '{servicio.TipoVehiculo}'");
        }

        ValidarServicio(servicio);

        var servicioRef = _firestore.Collection(COLLECTION_NAME).Document(servicio.Id);
        var servicioData = CrearDiccionarioServicio(servicio);
        await servicioRef.SetAsync(servicioData, SetOptions.Overwrite);
    }

    /// <summary>
    /// Cambia el estado de un servicio específico.
    /// </summary>
    /// <param name="id">ID del servicio.</param>
    /// <param name="nuevoEstado">Nuevo estado.</param>
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
    /// Verifica si existe un servicio con el nombre y tipo de vehículo especificados.
    /// </summary>
    /// <param name="nombre">Nombre del servicio.</param>
    /// <param name="tipoVehiculo">Tipo de vehículo.</param>
    /// <param name="idActual">ID actual (para excluir en actualización).</param>
    /// <returns>True si existe.</returns>
    public async Task<bool> ExisteServicioConNombreTipoVehiculo(string nombre, string tipoVehiculo, string idActual = null)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return false;
        if (string.IsNullOrWhiteSpace(tipoVehiculo)) return false;

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
    /// Obtiene todos los servicios aplicando filtros y ordenamiento.
    /// </summary>
    /// <param name="estados">Lista de estados a filtrar.</param>
    /// <param name="tipos">Lista de tipos de servicio a filtrar.</param>
    /// <param name="tiposVehiculo">Lista de tipos de vehículo a filtrar.</param>
    /// <param name="precioDesde">Precio mínimo a filtrar.</param>
    /// <param name="precioHasta">Precio máximo a filtrar.</param>
    /// <param name="sortBy">Campo por el cual ordenar.</param>
    /// <param name="sortOrder">Dirección del ordenamiento.</param>
    /// <returns>Lista de servicios filtrados y ordenados.</returns>
    private async Task<List<Servicio>> ObtenerServiciosFiltrados(
        List<string> estados,
        List<string> tipos,
        List<string> tiposVehiculo,
        decimal? precioDesde,
        decimal? precioHasta,
        string sortBy,
        string sortOrder)
    {
        estados = ConfigurarEstadosDefecto(estados);

        var query = ConstruirQueryFiltros(estados, tipos);

        var snapshot = await query.GetSnapshotAsync();
        var servicios = snapshot.Documents
            .Select(MapearDocumentoAServicio)
            .ToList();

        servicios = AplicarFiltroTipoVehiculo(servicios, tiposVehiculo);
        servicios = AplicarFiltroPrecio(servicios, precioDesde, precioHasta);

        return AplicarOrdenamiento(servicios, sortBy, sortOrder);
    }

    /// <summary>
    /// Obtiene el total de servicios que cumplen con los filtros.
    /// </summary>
    /// <param name="estados">Lista de estados a filtrar.</param>
    /// <param name="tipos">Lista de tipos de servicio a filtrar.</param>
    /// <param name="tiposVehiculo">Lista de tipos de vehículo a filtrar.</param>
    /// <param name="precioDesde">Precio mínimo a filtrar.</param>
    /// <param name="precioHasta">Precio máximo a filtrar.</param>
    /// <returns>Número total de servicios.</returns>
    private async Task<int> ObtenerTotalServicios(
        List<string> estados,
        List<string> tipos,
        List<string> tiposVehiculo,
        decimal? precioDesde,
        decimal? precioHasta)
    {
        estados = ConfigurarEstadosDefecto(estados);

        var query = ConstruirQueryFiltros(estados, tipos);

        var snapshot = await query.GetSnapshotAsync();
        var servicios = snapshot.Documents
            .Select(doc => new ServicioConteo
            {
                TipoVehiculo = doc.ContainsField("TipoVehiculo")
                    ? doc.GetValue<string>("TipoVehiculo")
                    : TIPO_VEHICULO_DEFECTO,
                Precio = doc.ContainsField("Precio")
                    ? (decimal)Convert.ToDouble(doc.GetValue<object>("Precio"))
                    : 0m
            })
            .ToList();

        if (tiposVehiculo != null && tiposVehiculo.Any())
        {
            servicios = servicios
                .Where(s => tiposVehiculo.Contains(s.TipoVehiculo))
                .ToList();
        }

        if (precioDesde.HasValue)
        {
            servicios = servicios.Where(s => s.Precio >= precioDesde.Value).ToList();
        }

        if (precioHasta.HasValue)
        {
            servicios = servicios.Where(s => s.Precio <= precioHasta.Value).ToList();
        }

        return servicios.Count;
    }

    /// <summary>
    /// Obtiene servicios filtrados por un campo específico.
    /// </summary>
    /// <param name="campo">Campo por el cual filtrar.</param>
    /// <param name="valor">Valor del campo.</param>
    /// <returns>Lista de servicios filtrados.</returns>
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
    /// Configura la lista de estados por defecto si no se especificó ninguno.
    /// </summary>
    /// <param name="estados">Lista de estados seleccionados por el usuario.</param>
    /// <returns>Lista con al menos un estado; si estaba vacía, contiene "Activo".</returns>
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
    /// Construye un query de Firestore aplicando filtros de estado y tipo.
    /// </summary>
    /// <param name="estados">Lista de estados a filtrar.</param>
    /// <param name="tipos">Lista de tipos de servicio a filtrar.</param>
    /// <returns>Query de Firestore configurado.</returns>
    private Query ConstruirQueryFiltros(
        List<string> estados,
        List<string> tipos)
    {
        Query query = _firestore.Collection(COLLECTION_NAME);

        if (estados?.Any() == true)
        {
            query = query.WhereIn("Estado", estados);
        }

        if (tipos?.Any() == true)
        {
            query = query.WhereIn("Tipo", tipos);
        }

        return query;
    }

    /// <summary>
    /// Aplica filtro de tipo de vehículo (post-proceso).
    /// </summary>
    /// <param name="servicios">Lista de servicios.</param>
    /// <param name="tiposVehiculo">Tipos de vehículo a filtrar.</param>
    /// <returns>Lista filtrada.</returns>
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
    /// Aplica filtro de rango de precios (post-proceso).
    /// </summary>
    /// <param name="servicios">Lista de servicios.</param>
    /// <param name="precioDesde">Precio mínimo.</param>
    /// <param name="precioHasta">Precio máximo.</param>
    /// <returns>Lista filtrada.</returns>
    private static List<Servicio> AplicarFiltroPrecio(List<Servicio> servicios, decimal? precioDesde, decimal? precioHasta)
    {
        if (precioDesde.HasValue)
        {
            servicios = servicios.Where(s => s.Precio >= precioDesde.Value).ToList();
        }

        if (precioHasta.HasValue)
        {
            servicios = servicios.Where(s => s.Precio <= precioHasta.Value).ToList();
        }

        return servicios;
    }

    /// <summary>
    /// Aplica ordenamiento a una lista de servicios.
    /// </summary>
    /// <param name="servicios">Lista de servicios a ordenar.</param>
    /// <param name="sortBy">Campo por el cual ordenar.</param>
    /// <param name="sortOrder">Dirección del ordenamiento.</param>
    /// <returns>Lista ordenada.</returns>
    private static List<Servicio> AplicarOrdenamiento(List<Servicio> servicios, string sortBy, string sortOrder)
    {
        sortBy ??= ORDEN_DEFECTO;
        sortOrder = (sortOrder ?? DIRECCION_DEFECTO).Trim().ToLowerInvariant();

        Func<Servicio, object> keySelector = sortBy switch
        {
            "Nombre" => s => s.Nombre,
            "Precio" => s => s.Precio,
            "Tipo" => s => s.Tipo,
            "TipoVehiculo" => s => s.TipoVehiculo,
            "TiempoEstimado" => s => s.TiempoEstimado,
            "Estado" => s => s.Estado,
            "EtapasCount" => s => (s.Etapas?.Count ?? 0),
            _ => s => s.Nombre
        };

        var ordered = sortOrder == "desc"
            ? servicios.OrderByDescending(keySelector)
            : servicios.OrderBy(keySelector);

        return ordered.ToList();
    }

    /// <summary>
    /// Aplica paginación a una lista en memoria.
    /// </summary>
    /// <param name="lista">Lista a paginar.</param>
    /// <param name="pageNumber">Número de página.</param>
    /// <param name="pageSize">Tamaño de página.</param>
    /// <returns>Lista paginada.</returns>
    private static List<Servicio> AplicarPaginacion(List<Servicio> lista, int pageNumber, int pageSize)
        => lista.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

    /// <summary>
    /// Aplica la lógica de búsqueda sobre una lista previamente filtrada.
    /// </summary>
    /// <param name="baseFiltrada">Lista base filtrada.</param>
    /// <param name="searchTerm">Término de búsqueda.</param>
    /// <returns>Lista filtrada por búsqueda.</returns>
    private static List<Servicio> AplicarBusqueda(List<Servicio> baseFiltrada, string searchTerm)
    {
        var term = searchTerm?.Trim() ?? string.Empty;
        if (term.Length == 0) return baseFiltrada;

        var termUpper = term.ToUpperInvariant();

        // "X min"
        if (termUpper.EndsWith("MIN"))
        {
            var timeText = term.Substring(0, term.Length - 3).Trim();
            if (int.TryParse(timeText, out var minutos))
            {
                return baseFiltrada.Where(s => s.TiempoEstimado == minutos).ToList();
            }
            return new List<Servicio>();
        }

        // Numérico (precio/tiempo/etapas)
        if (decimal.TryParse(term, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var numeroDecimal))
        {
            var numeroEntero = (int)numeroDecimal;
            return baseFiltrada.Where(s =>
                Math.Abs(s.Precio - numeroDecimal) < 0.0001m ||
                s.TiempoEstimado == numeroEntero ||
                (s.Etapas?.Count ?? 0) == numeroEntero
            ).ToList();
        }

        // Textual
        return baseFiltrada.Where(s =>
            (s.Nombre?.ToUpperInvariant().Contains(termUpper) ?? false) ||
            (s.Descripcion?.ToUpperInvariant().Contains(termUpper) ?? false) ||
            (s.Tipo?.ToUpperInvariant().Contains(termUpper) ?? false) ||
            (s.TipoVehiculo?.ToUpperInvariant().Contains(termUpper) ?? false) ||
            (s.Estado?.ToUpperInvariant().Contains(termUpper) ?? false)
        ).ToList();
    }

    /// <summary>
    /// Crea el diccionario de datos para guardar/actualizar un servicio en Firestore.
    /// </summary>
    /// <param name="servicio">Servicio a convertir.</param>
    /// <returns>Diccionario de datos.</returns>
    private static Dictionary<string, object> CrearDiccionarioServicio(Servicio servicio)
    {
        var etapasList = (servicio.Etapas ?? new List<Etapa>())
            .Where(e => e != null && !string.IsNullOrWhiteSpace(e.Nombre))
            .Select(e => new Dictionary<string, object>
            {
                ["Id"] = string.IsNullOrWhiteSpace(e.Id) ? Guid.NewGuid().ToString() : e.Id,
                ["Nombre"] = e.Nombre.Trim()
            })
            .ToList();

        return new Dictionary<string, object>
        {
            { "Nombre", servicio.Nombre },
            { "Precio", (double)servicio.Precio },
            { "Tipo", servicio.Tipo },
            { "TipoVehiculo", servicio.TipoVehiculo },
            { "TiempoEstimado", servicio.TiempoEstimado },
            { "Descripcion", servicio.Descripcion },
            { "Estado", servicio.Estado },
            { "Etapas", etapasList }
        };
    }

    /// <summary>
    /// Mapea un documento de Firestore a un objeto Servicio.
    /// </summary>
    /// <param name="documento">Documento de Firestore.</param>
    /// <returns>Objeto Servicio mapeado.</returns>
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
            Estado = documento.GetValue<string>("Estado"),
            Etapas = documento.ContainsField("Etapas")
                ? MapearEtapas(documento)
                : new List<Etapa>()
        };
    }

    /// <summary>
    /// Mapea la colección embebida de etapas.
    /// </summary>
    /// <param name="documento">Documento de Firestore.</param>
    /// <returns>Lista de etapas.</returns>
    private static List<Etapa> MapearEtapas(DocumentSnapshot documento)
    {
        var etapas = new List<Etapa>();
        var raw = documento.GetValue<IList<object>>("Etapas");
        foreach (var item in raw)
        {
            if (item is IDictionary<string, object> map)
            {
                map.TryGetValue("Id", out var idVal);
                map.TryGetValue("Nombre", out var nombreVal);

                etapas.Add(new Etapa
                {
                    Id = idVal?.ToString() ?? string.Empty,
                    Nombre = nombreVal?.ToString() ?? string.Empty
                });
            }
        }
        return etapas;
    }

    /// <summary>
    /// Valida los datos de un servicio antes de guardarlo.
    /// </summary>
    /// <param name="servicio">Servicio a validar.</param>
    private static void ValidarServicio(Servicio servicio)
    {
        var errores = new List<string>();

        if (string.IsNullOrWhiteSpace(servicio.Nombre))
            errores.Add("El nombre del servicio no puede estar vacío");

        if (string.IsNullOrWhiteSpace(servicio.Tipo))
            errores.Add("El tipo de servicio no puede estar vacío");

        if (string.IsNullOrWhiteSpace(servicio.TipoVehiculo))
            errores.Add("El tipo de vehículo no puede estar vacío");

        if (string.IsNullOrWhiteSpace(servicio.Descripcion))
            errores.Add("La descripción no puede estar vacía");

        if (!string.IsNullOrWhiteSpace(servicio.Nombre) &&
            !System.Text.RegularExpressions.Regex.IsMatch(servicio.Nombre, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$"))
            errores.Add("El nombre solo puede contener letras y espacios");

        if (servicio.Precio < 0)
            errores.Add("El precio debe ser igual o mayor a 0");

        if (servicio.TiempoEstimado <= 0)
            errores.Add("El tiempo estimado debe ser mayor a 0");

        if (servicio.Etapas != null && servicio.Etapas.Any())
        {
            foreach (var etapa in servicio.Etapas)
            {
                if (etapa == null) continue;
                if (string.IsNullOrWhiteSpace(etapa.Nombre))
                {
                    errores.Add("El nombre de la etapa no puede estar vacío");
                    continue;
                }

                if (!System.Text.RegularExpressions.Regex.IsMatch(etapa.Nombre, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$"))
                    errores.Add($"La etapa '{etapa.Nombre}' solo puede contener letras y espacios");
            }
        }

        if (errores.Any())
            throw new ArgumentException($"Errores de validación: {string.Join(", ", errores)}");
    }

    /// <summary>
    /// Valida los parámetros de paginación.
    /// </summary>
    /// <param name="pageNumber">Número de página.</param>
    /// <param name="pageSize">Tamaño de página.</param>
    private static void ValidarParametrosPaginacion(int pageNumber, int pageSize)
    {
        if (pageNumber <= 0)
            throw new ArgumentException("El número de página debe ser mayor a 0", nameof(pageNumber));

        if (pageSize <= 0)
            throw new ArgumentException("El tamaño de página debe ser mayor a 0", nameof(pageSize));
    }
    #endregion

    #region Clases Auxiliares

    /// <summary>
    /// Obtiene el precio mínimo de los servicios activos según los filtros aplicados.
    /// </summary>
    /// <param name="estados">Lista de estados a filtrar.</param>
    /// <param name="tipos">Lista de tipos de servicio a filtrar.</param>
    /// <param name="tiposVehiculo">Lista de tipos de vehículo a filtrar.</param>
    /// <returns>Precio mínimo encontrado o 0 si no hay servicios.</returns>
    public async Task<decimal> ObtenerPrecioMinimo(
        List<string> estados = null,
        List<string> tipos = null,
        List<string> tiposVehiculo = null)
    {
        estados = ConfigurarEstadosDefecto(estados);
        var query = ConstruirQueryFiltros(estados, tipos);
        var snapshot = await query.GetSnapshotAsync();
        
        var servicios = snapshot.Documents
            .Select(MapearDocumentoAServicio)
            .ToList();

        servicios = AplicarFiltroTipoVehiculo(servicios, tiposVehiculo);

        return servicios.Any() ? servicios.Min(s => s.Precio) : 0m;
    }

    /// <summary>
    /// Obtiene el precio máximo de los servicios activos según los filtros aplicados.
    /// </summary>
    /// <param name="estados">Lista de estados a filtrar.</param>
    /// <param name="tipos">Lista de tipos de servicio a filtrar.</param>
    /// <param name="tiposVehiculo">Lista de tipos de vehículo a filtrar.</param>
    /// <returns>Precio máximo encontrado o 0 si no hay servicios.</returns>
    public async Task<decimal> ObtenerPrecioMaximo(
        List<string> estados = null,
        List<string> tipos = null,
        List<string> tiposVehiculo = null)
    {
        estados = ConfigurarEstadosDefecto(estados);
        var query = ConstruirQueryFiltros(estados, tipos);
        var snapshot = await query.GetSnapshotAsync();
        
        var servicios = snapshot.Documents
            .Select(MapearDocumentoAServicio)
            .ToList();

        servicios = AplicarFiltroTipoVehiculo(servicios, tiposVehiculo);

        return servicios.Any() ? servicios.Max(s => s.Precio) : 0m;
    }

    /// <summary>
    /// Clase auxiliar para conteo eficiente de servicios.
    /// </summary>
    private class ServicioConteo
    {
        /// <summary>Tipo de vehículo del servicio.</summary>
        public string TipoVehiculo { get; set; }
        /// <summary>Precio del servicio.</summary>
        public decimal Precio { get; set; }
    }
    #endregion
}
using Firebase.Models;
using Firebase.Services;
using Google.Cloud.Firestore;

/// <summary>
/// Servicio para la gesti�n de servicios del lavadero en Firestore.
/// Proporciona operaciones CRUD, filtrado, paginaci�n y validaci�n de servicios.
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
    /// <param name="tiposVehiculo">Lista de tipos de veh�culo a filtrar (null para todos).</param>
    /// <param name="pageNumber">N�mero de p�gina (1-based).</param>
    /// <param name="pageSize">Cantidad de elementos por p�gina.</param>
    /// <param name="sortBy">Campo por el cual ordenar.</param>
    /// <param name="sortOrder">Direcci�n del ordenamiento (asc/desc).</param>
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
    /// Calcula el n�mero total de p�ginas para los servicios filtrados.
    /// </summary>
    /// <param name="estados">Lista de estados a filtrar.</param>
    /// <param name="tipos">Lista de tipos de servicio a filtrar.</param>
    /// <param name="tiposVehiculo">Lista de tipos de veh�culo a filtrar.</param>
    /// <param name="precioDesde">Precio m�nimo a filtrar.</param>
    /// <param name="precioHasta">Precio m�ximo a filtrar.</param>
    /// <param name="pageSize">Cantidad de elementos por p�gina.</param>
    /// <returns>N�mero total de p�ginas.</returns>
    public async Task<int> ObtenerTotalPaginas(
        List<string> estados,
        List<string> tipos,
        List<string> tiposVehiculo,
        decimal? precioDesde,
        decimal? precioHasta,
        int pageSize)
    {
        if (pageSize <= 0)
            throw new ArgumentException("El tama�o de p�gina debe ser mayor a 0", nameof(pageSize));

        var totalServicios = await ObtenerTotalServicios(estados, tipos, tiposVehiculo, precioDesde, precioHasta);
        return (int)Math.Ceiling(totalServicios / (double)pageSize);
    }

    /// <summary>
    /// Obtiene un servicio espec�fico por su ID.
    /// </summary>
    /// <param name="id">ID del servicio.</param>
    /// <returns>El servicio encontrado o null si no existe.</returns>
    public async Task<Servicio> ObtenerServicio(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("El ID del servicio no puede estar vac�o", nameof(id));

        var docRef = _firestore.Collection(COLLECTION_NAME).Document(id);
        var snapshot = await docRef.GetSnapshotAsync();

        return snapshot.Exists ? MapearDocumentoAServicio(snapshot) : null;
    }

    /// <summary>
    /// Busca servicios por t�rmino de b�squeda (texto/n�mero/"X min").
    /// Incluye coincidencias por cantidad de etapas.
    /// </summary>
    /// <param name="searchTerm">T�rmino de b�squeda.</param>
    /// <param name="estados">Lista de estados a filtrar.</param>
    /// <param name="tipos">Lista de tipos de servicio a filtrar.</param>
    /// <param name="tiposVehiculo">Lista de tipos de veh�culo a filtrar.</param>
    /// <param name="precioDesde">Precio m�nimo a filtrar.</param>
    /// <param name="precioHasta">Precio m�ximo a filtrar.</param>
    /// <param name="pageNumber">N�mero de p�gina actual.</param>
    /// <param name="pageSize">Tama�o de p�gina.</param>
    /// <param name="sortBy">Campo por el cual ordenar.</param>
    /// <param name="sortOrder">Direcci�n del ordenamiento.</param>
    /// <returns>Lista de servicios que coinciden con la b�squeda.</returns>
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
    /// Obtiene el total de servicios que coinciden con la b�squeda.
    /// Usa la misma l�gica de <see cref="BuscarServicios"/>.
    /// </summary>
    /// <param name="searchTerm">T�rmino de b�squeda.</param>
    /// <param name="estados">Lista de estados a filtrar.</param>
    /// <param name="tipos">Lista de tipos de servicio a filtrar.</param>
    /// <param name="tiposVehiculo">Lista de tipos de veh�culo a filtrar.</param>
    /// <param name="precioDesde">Precio m�nimo a filtrar.</param>
    /// <param name="precioHasta">Precio m�ximo a filtrar.</param>
    /// <returns>N�mero total de servicios que coinciden.</returns>
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
    /// Obtiene todos los servicios de un tipo espec�fico.
    /// </summary>
    /// <param name="tipo">Tipo de servicio.</param>
    /// <returns>Lista de servicios del tipo especificado.</returns>
    public async Task<List<Servicio>> ObtenerServiciosPorTipo(string tipo)
        => await ObtenerServiciosPorCampo("Tipo", tipo);

    /// <summary>
    /// Obtiene todos los servicios para un tipo de veh�culo espec�fico.
    /// </summary>
    /// <param name="tipoVehiculo">Tipo de veh�culo.</param>
    /// <returns>Lista de servicios para el tipo de veh�culo especificado.</returns>
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
                $"Ya existe un servicio con el nombre '{servicio.Nombre}' para veh�culos tipo '{servicio.TipoVehiculo}'");
        }

        ValidarServicio(servicio);
        ValidarUbicacionRecetas(servicio, null);
        await ValidarRecetas(servicio);

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
                $"Ya existe un servicio con el nombre '{servicio.Nombre}' para veh�culos tipo '{servicio.TipoVehiculo}'");
        }

        ValidarServicio(servicio);
        var anterior = await ObtenerServicio(servicio.Id);
        ValidarUbicacionRecetas(servicio, anterior);
        await ValidarRecetas(servicio, anterior);

        var servicioRef = _firestore.Collection(COLLECTION_NAME).Document(servicio.Id);
        var servicioData = CrearDiccionarioServicio(servicio);
        await servicioRef.SetAsync(servicioData, SetOptions.Overwrite);
    }

    /// <summary>
    /// Cambia el estado de un servicio espec�fico.
    /// </summary>
    /// <param name="id">ID del servicio.</param>
    /// <param name="nuevoEstado">Nuevo estado.</param>
    public async Task CambiarEstadoServicio(string id, string nuevoEstado)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("El ID del servicio no puede estar vac�o", nameof(id));

        if (string.IsNullOrWhiteSpace(nuevoEstado))
            throw new ArgumentException("El nuevo estado no puede estar vac�o", nameof(nuevoEstado));

        var servicioRef = _firestore.Collection(COLLECTION_NAME).Document(id);
        await servicioRef.UpdateAsync("Estado", nuevoEstado);
    }
    #endregion

    #region Operaciones de Verificaci�n

    /// <summary>
    /// Verifica si existe un servicio con el nombre y tipo de veh�culo especificados.
    /// </summary>
    /// <param name="nombre">Nombre del servicio.</param>
    /// <param name="tipoVehiculo">Tipo de veh�culo.</param>
    /// <param name="idActual">ID actual (para excluir en actualizaci�n).</param>
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

    #region M�todos Privados - Consultas Base

    /// <summary>
    /// Obtiene todos los servicios aplicando filtros y ordenamiento.
    /// </summary>
    /// <param name="estados">Lista de estados a filtrar.</param>
    /// <param name="tipos">Lista de tipos de servicio a filtrar.</param>
    /// <param name="tiposVehiculo">Lista de tipos de veh�culo a filtrar.</param>
    /// <param name="precioDesde">Precio m�nimo a filtrar.</param>
    /// <param name="precioHasta">Precio m�ximo a filtrar.</param>
    /// <param name="sortBy">Campo por el cual ordenar.</param>
    /// <param name="sortOrder">Direcci�n del ordenamiento.</param>
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
    /// <param name="tiposVehiculo">Lista de tipos de veh�culo a filtrar.</param>
    /// <param name="precioDesde">Precio m�nimo a filtrar.</param>
    /// <param name="precioHasta">Precio m�ximo a filtrar.</param>
    /// <returns>N�mero total de servicios.</returns>
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
    /// Obtiene servicios filtrados por un campo espec�fico.
    /// </summary>
    /// <param name="campo">Campo por el cual filtrar.</param>
    /// <param name="valor">Valor del campo.</param>
    /// <returns>Lista de servicios filtrados.</returns>
    private async Task<List<Servicio>> ObtenerServiciosPorCampo(string campo, string valor)
    {
        if (string.IsNullOrWhiteSpace(campo))
            throw new ArgumentException("El campo no puede estar vac�o", nameof(campo));

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

    #region M�todos Privados - Utilidades

    /// <summary>
    /// Configura la lista de estados por defecto si no se especific� ninguno.
    /// </summary>
    /// <param name="estados">Lista de estados seleccionados por el usuario.</param>
    /// <returns>Lista con al menos un estado; si estaba vac�a, contiene "Activo".</returns>
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
    /// Aplica filtro de tipo de veh�culo (post-proceso).
    /// </summary>
    /// <param name="servicios">Lista de servicios.</param>
    /// <param name="tiposVehiculo">Tipos de veh�culo a filtrar.</param>
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
    /// <param name="precioDesde">Precio m�nimo.</param>
    /// <param name="precioHasta">Precio m�ximo.</param>
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
    /// <param name="sortOrder">Direcci�n del ordenamiento.</param>
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
    /// Aplica paginaci�n a una lista en memoria.
    /// </summary>
    /// <param name="lista">Lista a paginar.</param>
    /// <param name="pageNumber">N�mero de p�gina.</param>
    /// <param name="pageSize">Tama�o de p�gina.</param>
    /// <returns>Lista paginada.</returns>
    private static List<Servicio> AplicarPaginacion(List<Servicio> lista, int pageNumber, int pageSize)
        => lista.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

    /// <summary>
    /// Aplica la l�gica de b�squeda sobre una lista previamente filtrada.
    /// </summary>
    /// <param name="baseFiltrada">Lista base filtrada.</param>
    /// <param name="searchTerm">T�rmino de b�squeda.</param>
    /// <returns>Lista filtrada por b�squeda.</returns>
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

        // Num�rico (precio/tiempo/etapas)
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
                ["Nombre"] = e.Nombre.Trim(),
                ["Receta"] = CrearDatosReceta(e.Receta)
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
            { "Etapas", etapasList },
            { "Receta", CrearDatosReceta(servicio.Receta) }
        };
    }

    private static List<Dictionary<string, object>> CrearDatosReceta(List<RecetaInsumo> receta)
        => (receta ?? new List<RecetaInsumo>()).Select(item => new Dictionary<string, object>
        {
            ["Modalidad"] = item.Modalidad,
            ["TipoId"] = item.TipoId,
            ["NombreTipo"] = item.NombreTipo,
            ["Categoria"] = item.Categoria,
            ["InsumoId"] = item.InsumoId,
            ["NombreInsumo"] = item.NombreInsumo,
            ["UnidadMedida"] = item.UnidadMedida,
            ["DosisTeorica"] = (double)item.DosisTeorica,
            ["EsHerramienta"] = item.EsHerramienta
        }).ToList();

    private static List<RecetaInsumo> MapearReceta(object raw)
    {
        if (raw is not IList<object> items) return new List<RecetaInsumo>();
        return items.OfType<IDictionary<string, object>>().Select(item => new RecetaInsumo
        {
            Modalidad = item.TryGetValue("Modalidad", out var modalidad) ? modalidad?.ToString() ?? "Producto" : "Producto",
            TipoId = item.TryGetValue("TipoId", out var tipoId) ? tipoId?.ToString() ?? string.Empty : string.Empty,
            NombreTipo = item.TryGetValue("NombreTipo", out var nombreTipo) ? nombreTipo?.ToString() ?? string.Empty : string.Empty,
            Categoria = item.TryGetValue("Categoria", out var categoria) ? categoria?.ToString() ?? string.Empty : string.Empty,
            InsumoId = item.TryGetValue("InsumoId", out var id) ? id?.ToString() ?? string.Empty : string.Empty,
            NombreInsumo = item.TryGetValue("NombreInsumo", out var nombre) ? nombre?.ToString() ?? string.Empty : string.Empty,
            UnidadMedida = item.TryGetValue("UnidadMedida", out var unidad) ? unidad?.ToString() ?? string.Empty : string.Empty,
            DosisTeorica = item.TryGetValue("DosisTeorica", out var dosis) && dosis != null ? Convert.ToDecimal(dosis) : 0m,
            EsHerramienta = item.TryGetValue("EsHerramienta", out var eh) && eh is bool b && b
        }).ToList();
    }

    private static void ValidarUbicacionRecetas(Servicio servicio, Servicio anterior)
    {
        var etapas = servicio.Etapas ?? new();
        var receta = servicio.Receta ?? new();
        if (etapas.Count > 0 && receta.Count > 0)
        {
            // Permitir la lectura y edición de datos legacy ambiguos solo si la receta del servicio permanece intacta.
            var previa = anterior?.Receta ?? new();
            bool legacy = anterior?.Etapas?.Count > 0 && previa.Count == receta.Count &&
                receta.Zip(previa).All(p => RecetaIgual(p.First, p.Second));
            if (!legacy) throw new ArgumentException("Un servicio con etapas no puede tener receta propia; quite esa receta antes de agregar etapas.");
        }
        foreach (var etapa in anterior?.Etapas ?? new())
            if ((etapa.Receta?.Count ?? 0) > 0 && !etapas.Any(e => e != null && e.Id == etapa.Id))
                throw new ArgumentException("Quite la receta de la etapa antes de eliminarla.");
    }

    private static bool RecetaIgual(RecetaInsumo x, RecetaInsumo y) => x != null && y != null &&
        x.Modalidad == y.Modalidad && x.InsumoId == y.InsumoId && x.TipoId == y.TipoId &&
        x.UnidadMedida == y.UnidadMedida && x.DosisTeorica == y.DosisTeorica;

    private async Task ValidarRecetas(Servicio servicio, Servicio anterior = null)
    {
        await ValidarListaReceta(servicio.Receta, anterior?.Receta);
        foreach (var etapa in servicio.Etapas ?? new())
        {
            if (etapa == null) continue;
            var anteriores = (anterior?.Etapas ?? new()).Where(e => e.Id == etapa.Id && !string.IsNullOrWhiteSpace(etapa.Id)).ToList();
            await ValidarListaReceta(etapa.Receta, anteriores.Count == 1 ? anteriores[0].Receta : null);
        }
    }

    private async Task ValidarListaReceta(List<RecetaInsumo> receta, List<RecetaInsumo> anterior)
    {
        var conversion = new ConversionUnidadesService();
        foreach (var item in receta ?? new())
        {
            if (item == null || item.DosisTeorica <= 0) throw new ArgumentException("La dosis debe ser mayor a cero.");
            var coincidencias = (anterior ?? new()).Where(x => x != null &&
                x.Modalidad == item.Modalidad && x.InsumoId == item.InsumoId && x.TipoId == item.TipoId &&
                x.UnidadMedida == item.UnidadMedida && x.DosisTeorica == item.DosisTeorica).ToList();
            if (coincidencias.Count == 1)
            {
                var snapshot = coincidencias[0];
                item.NombreInsumo = snapshot.NombreInsumo;
                item.NombreTipo = snapshot.NombreTipo;
                item.Categoria = snapshot.Categoria;
                continue;
            }
            if (item.Modalidad == "Producto" && !string.IsNullOrWhiteSpace(item.InsumoId) && string.IsNullOrWhiteSpace(item.TipoId))
            {
                var doc = await _firestore.Collection("insumos").Document(item.InsumoId).GetSnapshotAsync();
                if (!doc.Exists) throw new ArgumentException("El insumo seleccionado no existe.");
                var insumo = doc.ConvertTo<Insumo>();
                if (insumo.Estado != "Activo" || !conversion.SonCompatibles(insumo.UnidadMedida, item.UnidadMedida))
                    throw new ArgumentException("El insumo está inactivo o la unidad es incompatible.");
                item.NombreInsumo = insumo.Nombre;
                item.Categoria = conversion.ObtenerCategoria(item.UnidadMedida) ?? string.Empty;
            }
            else if (item.Modalidad == "Tipo" && !string.IsNullOrWhiteSpace(item.TipoId) && string.IsNullOrWhiteSpace(item.InsumoId))
            {
                var doc = await _firestore.Collection(TipoInsumoService.Collection).Document(item.TipoId).GetSnapshotAsync();
                if (!doc.Exists) throw new ArgumentException("El tipo seleccionado no existe.");
                var tipo = doc.ConvertTo<TipoInsumo>();
                if (tipo.Estado != "Activo" || !TipoInsumo.EsUnidadValidaParaCategoria(tipo.Categoria, item.UnidadMedida))
                    throw new ArgumentException("El tipo está inactivo o la unidad es incompatible.");
                item.NombreTipo = tipo.Nombre;
                item.Categoria = tipo.Categoria;
            }
            else throw new ArgumentException("Seleccione un producto o un tipo de insumo.");
            item.UnidadMedida = conversion.NormalizarUnidad(item.UnidadMedida);
        }
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
            Receta = documento.ContainsField("Receta") ? MapearReceta(documento.GetValue<object>("Receta")) : new List<RecetaInsumo>(),
            Etapas = documento.ContainsField("Etapas")
                ? MapearEtapas(documento)
                : new List<Etapa>()
        };
    }

    /// <summary>
    /// Mapea la colecci�n embebida de etapas.
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
                    Nombre = nombreVal?.ToString() ?? string.Empty,
                    Receta = map.TryGetValue("Receta", out var receta) ? MapearReceta(receta) : new List<RecetaInsumo>()
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
            errores.Add("El nombre del servicio no puede estar vac�o");

        if (string.IsNullOrWhiteSpace(servicio.Tipo))
            errores.Add("El tipo de servicio no puede estar vac�o");

        if (string.IsNullOrWhiteSpace(servicio.TipoVehiculo))
            errores.Add("El tipo de veh�culo no puede estar vac�o");

        if (string.IsNullOrWhiteSpace(servicio.Descripcion))
            errores.Add("La descripci�n no puede estar vac�a");

        if (!string.IsNullOrWhiteSpace(servicio.Nombre) &&
            !System.Text.RegularExpressions.Regex.IsMatch(servicio.Nombre, @"^[a-zA-Z������������\s]+$"))
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
                    errores.Add("El nombre de la etapa no puede estar vac�o");
                    continue;
                }

                if (!System.Text.RegularExpressions.Regex.IsMatch(etapa.Nombre, @"^[a-zA-Z������������\s]+$"))
                    errores.Add($"La etapa '{etapa.Nombre}' solo puede contener letras y espacios");
            }
        }

        if (errores.Any())
            throw new ArgumentException($"Errores de validaci�n: {string.Join(", ", errores)}");
    }

    /// <summary>
    /// Valida los par�metros de paginaci�n.
    /// </summary>
    /// <param name="pageNumber">N�mero de p�gina.</param>
    /// <param name="pageSize">Tama�o de p�gina.</param>
    private static void ValidarParametrosPaginacion(int pageNumber, int pageSize)
    {
        if (pageNumber <= 0)
            throw new ArgumentException("El n�mero de p�gina debe ser mayor a 0", nameof(pageNumber));

        if (pageSize <= 0)
            throw new ArgumentException("El tama�o de p�gina debe ser mayor a 0", nameof(pageSize));
    }
    #endregion

    #region Clases Auxiliares

    /// <summary>
    /// Obtiene el precio m�nimo de los servicios activos seg�n los filtros aplicados.
    /// </summary>
    /// <param name="estados">Lista de estados a filtrar.</param>
    /// <param name="tipos">Lista de tipos de servicio a filtrar.</param>
    /// <param name="tiposVehiculo">Lista de tipos de veh�culo a filtrar.</param>
    /// <returns>Precio m�nimo encontrado o 0 si no hay servicios.</returns>
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
    /// Obtiene el precio m�ximo de los servicios activos seg�n los filtros aplicados.
    /// </summary>
    /// <param name="estados">Lista de estados a filtrar.</param>
    /// <param name="tipos">Lista de tipos de servicio a filtrar.</param>
    /// <param name="tiposVehiculo">Lista de tipos de veh�culo a filtrar.</param>
    /// <returns>Precio m�ximo encontrado o 0 si no hay servicios.</returns>
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
        /// <summary>Tipo de veh�culo del servicio.</summary>
        public string TipoVehiculo { get; set; }
        /// <summary>Precio del servicio.</summary>
        public decimal Precio { get; set; }
    }
    #endregion
}
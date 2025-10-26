using Firebase.Models;
using Google.Cloud.Firestore;

/// <summary>
/// Servicio para la gestión de paquetes de servicios en Firestore.
/// Proporciona operaciones CRUD, filtrado, paginación y validación de paquetes.
/// </summary>
public class PaqueteServicioService
{
    #region Constantes
    private const string COLLECTION_NAME = "paquetes_servicios";
    private const string ESTADO_DEFECTO = "Activo";
    private const string ORDEN_DEFECTO = "Nombre";
    private const string DIRECCION_DEFECTO = "asc";
    #endregion

    #region Dependencias
    private readonly FirestoreDb _firestore;
    private readonly ServicioService _servicioService;

    /// <summary>
    /// Inicializa una nueva instancia del servicio de paquetes.
    /// </summary>
    /// <param name="firestore">Instancia de la base de datos Firestore.</param>
    /// <param name="servicioService">Servicio para validar servicios incluidos en el paquete.</param>
    public PaqueteServicioService(FirestoreDb firestore, ServicioService servicioService)
    {
        _firestore = firestore ?? throw new ArgumentNullException(nameof(firestore));
        _servicioService = servicioService ?? throw new ArgumentNullException(nameof(servicioService));
    }
    #endregion

    #region Operaciones de Consulta

    /// <summary>
    /// Obtiene una lista paginada de paquetes aplicando filtros y ordenamiento.
    /// </summary>
    public async Task<List<PaqueteServicio>> ObtenerPaquetes(
        List<string> estados = null,
        List<string> tiposVehiculo = null,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = null,
        string sortOrder = null)
    {
        ValidarParametrosPaginacion(pageNumber, pageSize);

        sortBy ??= ORDEN_DEFECTO;
        sortOrder ??= DIRECCION_DEFECTO;

        var paquetes = await ObtenerPaquetesFiltrados(estados, tiposVehiculo, sortBy, sortOrder);

        return AplicarPaginacion(paquetes, pageNumber, pageSize);
    }

    /// <summary>
    /// Calcula el número total de páginas para los paquetes filtrados.
    /// </summary>
    public async Task<int> ObtenerTotalPaginas(
        List<string> estados,
        List<string> tiposVehiculo,
        int pageSize)
    {
        if (pageSize <= 0)
            throw new ArgumentException("El tamaño de página debe ser mayor a 0", nameof(pageSize));

        var totalPaquetes = await ObtenerTotalPaquetes(estados, tiposVehiculo);
        return (int)Math.Ceiling(totalPaquetes / (double)pageSize);
    }

    /// <summary>
    /// Obtiene un paquete específico por su ID.
    /// </summary>
    public async Task<PaqueteServicio> ObtenerPaquete(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("El ID del paquete no puede estar vacío", nameof(id));

        var docRef = _firestore.Collection(COLLECTION_NAME).Document(id);
        var snapshot = await docRef.GetSnapshotAsync();

        return snapshot.Exists ? MapearDocumentoAPaquete(snapshot) : null;
    }

    /// <summary>
    /// Busca paquetes por término de búsqueda.
    /// </summary>
    public async Task<List<PaqueteServicio>> BuscarPaquetes(
        string searchTerm,
        List<string> estados = null,
        List<string> tiposVehiculo = null,
        int pageNumber = 1,
        int pageSize = 10,
        string sortBy = null,
        string sortOrder = null)
    {
        var baseFiltrada = await ObtenerPaquetesFiltrados(estados, tiposVehiculo, sortBy, sortOrder);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            baseFiltrada = AplicarBusqueda(baseFiltrada, searchTerm);
        }

        var ordenados = AplicarOrdenamiento(baseFiltrada, sortBy, sortOrder);
        return AplicarPaginacion(ordenados, Math.Max(pageNumber, 1), Math.Max(pageSize, 1));
    }

    /// <summary>
    /// Obtiene el total de paquetes que coinciden con la búsqueda.
    /// </summary>
    public async Task<int> ObtenerTotalPaquetesBusqueda(
        string searchTerm,
        List<string> estados,
        List<string> tiposVehiculo)
    {
        estados = ConfigurarEstadosDefecto(estados);
        var baseFiltrada = await ObtenerPaquetesFiltrados(estados, tiposVehiculo, ORDEN_DEFECTO, DIRECCION_DEFECTO);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            baseFiltrada = AplicarBusqueda(baseFiltrada, searchTerm);
        }

        return baseFiltrada.Count;
    }
    #endregion

    #region Operaciones CRUD

    /// <summary>
    /// Crea un nuevo paquete en la base de datos.
    /// Valida duplicados, servicios y calcula precio/tiempo.
    /// </summary>
    public async Task CrearPaquete(PaqueteServicio paquete)
    {
        if (paquete == null)
            throw new ArgumentNullException(nameof(paquete));

        if (await ExistePaqueteConNombre(paquete.Nombre))
        {
            throw new ArgumentException($"Ya existe un paquete con el nombre '{paquete.Nombre}'");
        }

        await ValidarPaquete(paquete);
        await CalcularPrecioYTiempo(paquete);

        var paqueteRef = _firestore.Collection(COLLECTION_NAME).Document();
        paquete.Id = paqueteRef.Id;

        var paqueteData = CrearDiccionarioPaquete(paquete);
        await paqueteRef.SetAsync(paqueteData);
    }

    /// <summary>
    /// Actualiza un paquete existente en la base de datos.
    /// </summary>
    public async Task ActualizarPaquete(PaqueteServicio paquete)
    {
        if (paquete == null)
            throw new ArgumentNullException(nameof(paquete));

        if (string.IsNullOrWhiteSpace(paquete.Id))
            throw new ArgumentException("El ID del paquete es obligatorio para actualizar", nameof(paquete));

        if (await ExistePaqueteConNombre(paquete.Nombre, paquete.Id))
        {
            throw new ArgumentException($"Ya existe un paquete con el nombre '{paquete.Nombre}'");
        }

        await ValidarPaquete(paquete);
        await CalcularPrecioYTiempo(paquete);

        var paqueteRef = _firestore.Collection(COLLECTION_NAME).Document(paquete.Id);
        var paqueteData = CrearDiccionarioPaquete(paquete);
        await paqueteRef.SetAsync(paqueteData, SetOptions.Overwrite);
    }

    /// <summary>
    /// Cambia el estado de un paquete específico.
    /// </summary>
    public async Task CambiarEstadoPaquete(string id, string nuevoEstado)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("El ID del paquete no puede estar vacío", nameof(id));

        if (string.IsNullOrWhiteSpace(nuevoEstado))
            throw new ArgumentException("El nuevo estado no puede estar vacío", nameof(nuevoEstado));

        var paqueteRef = _firestore.Collection(COLLECTION_NAME).Document(id);
        await paqueteRef.UpdateAsync("Estado", nuevoEstado);
    }
    #endregion

    #region Operaciones de Verificación

    /// <summary>
    /// Verifica si existe un paquete con el nombre especificado.
    /// </summary>
    public async Task<bool> ExistePaqueteConNombre(string nombre, string idActual = null)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return false;

        var coleccion = _firestore.Collection(COLLECTION_NAME);
        var querySnapshot = await coleccion.GetSnapshotAsync();

        return querySnapshot.Documents
            .Where(doc => idActual == null || doc.Id != idActual)
            .Any(doc => doc.GetValue<string>("Nombre")
                .Trim()
                .Equals(nombre.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Obtiene los servicios completos de un paquete dado sus IDs.
    /// </summary>
    public async Task<List<Servicio>> ObtenerServiciosDePaquete(List<string> serviciosIds)
    {
        var servicios = new List<Servicio>();
        
        if (serviciosIds == null || !serviciosIds.Any())
            return servicios;

        foreach (var id in serviciosIds)
        {
            var servicio = await _servicioService.ObtenerServicio(id);
            if (servicio != null)
            {
                servicios.Add(servicio);
            }
        }

        return servicios;
    }
    #endregion

    #region Métodos Privados - Consultas Base

    /// <summary>
    /// Obtiene todos los paquetes aplicando filtros y ordenamiento.
    /// </summary>
    private async Task<List<PaqueteServicio>> ObtenerPaquetesFiltrados(
        List<string> estados,
        List<string> tiposVehiculo,
        string sortBy,
        string sortOrder)
    {
        estados = ConfigurarEstadosDefecto(estados);

        var query = ConstruirQueryFiltros(estados);

        var snapshot = await query.GetSnapshotAsync();
        var paquetes = snapshot.Documents
            .Select(MapearDocumentoAPaquete)
            .ToList();

        paquetes = AplicarFiltroTipoVehiculo(paquetes, tiposVehiculo);

        return AplicarOrdenamiento(paquetes, sortBy, sortOrder);
    }

    /// <summary>
    /// Obtiene el total de paquetes que cumplen con los filtros.
    /// </summary>
    private async Task<int> ObtenerTotalPaquetes(
        List<string> estados,
        List<string> tiposVehiculo)
    {
        estados = ConfigurarEstadosDefecto(estados);

        var query = ConstruirQueryFiltros(estados);

        var snapshot = await query.GetSnapshotAsync();
        var paquetes = snapshot.Documents
            .Select(doc => new PaqueteConteo
            {
                TipoVehiculo = doc.ContainsField("TipoVehiculo")
                    ? doc.GetValue<string>("TipoVehiculo")
                    : ""
            })
            .ToList();

        if (tiposVehiculo != null && tiposVehiculo.Any())
        {
            paquetes = paquetes
                .Where(p => tiposVehiculo.Contains(p.TipoVehiculo))
                .ToList();
        }

        return paquetes.Count;
    }
    #endregion

    #region Métodos Privados - Utilidades

    /// <summary>
    /// Configura la lista de estados por defecto si no se especificó ninguno.
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
    /// Construye un query de Firestore aplicando filtros de estado.
    /// </summary>
    private Query ConstruirQueryFiltros(List<string> estados)
    {
        Query query = _firestore.Collection(COLLECTION_NAME);

        if (estados?.Any() == true)
        {
            query = query.WhereIn("Estado", estados);
        }

        return query;
    }

    /// <summary>
    /// Aplica filtro de tipo de vehículo (post-proceso).
    /// </summary>
    private static List<PaqueteServicio> AplicarFiltroTipoVehiculo(List<PaqueteServicio> paquetes, List<string> tiposVehiculo)
    {
        if (tiposVehiculo?.Any() == true)
        {
            return paquetes
                .Where(p => tiposVehiculo.Contains(p.TipoVehiculo))
                .ToList();
        }
        return paquetes;
    }

    /// <summary>
    /// Aplica ordenamiento a una lista de paquetes.
    /// </summary>
    private static List<PaqueteServicio> AplicarOrdenamiento(List<PaqueteServicio> paquetes, string sortBy, string sortOrder)
    {
        sortBy ??= ORDEN_DEFECTO;
        sortOrder = (sortOrder ?? DIRECCION_DEFECTO).Trim().ToLowerInvariant();

        Func<PaqueteServicio, object> keySelector = sortBy switch
        {
            "Nombre" => p => p.Nombre,
            "Precio" => p => p.Precio,
            "TipoVehiculo" => p => p.TipoVehiculo,
            "TiempoEstimado" => p => p.TiempoEstimado,
            "Estado" => p => p.Estado,
            "PorcentajeDescuento" => p => p.PorcentajeDescuento,
            _ => p => p.Nombre
        };

        var ordered = sortOrder == "desc"
            ? paquetes.OrderByDescending(keySelector)
            : paquetes.OrderBy(keySelector);

        return ordered.ToList();
    }

    /// <summary>
    /// Aplica paginación a una lista en memoria.
    /// </summary>
    private static List<PaqueteServicio> AplicarPaginacion(List<PaqueteServicio> lista, int pageNumber, int pageSize)
        => lista.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

    /// <summary>
    /// Aplica la lógica de búsqueda sobre una lista previamente filtrada.
    /// </summary>
    private static List<PaqueteServicio> AplicarBusqueda(List<PaqueteServicio> baseFiltrada, string searchTerm)
    {
        var term = searchTerm?.Trim() ?? string.Empty;
        if (term.Length == 0) return baseFiltrada;

        var termUpper = term.ToUpperInvariant();

        // Numérico (precio/tiempo)
        if (decimal.TryParse(term, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var numeroDecimal))
        {
            var numeroEntero = (int)numeroDecimal;
            return baseFiltrada.Where(p =>
                Math.Abs(p.Precio - numeroDecimal) < 0.0001m ||
                p.TiempoEstimado == numeroEntero ||
                Math.Abs(p.PorcentajeDescuento - numeroDecimal) < 0.0001m
            ).ToList();
        }

        // Textual
        return baseFiltrada.Where(p =>
            (p.Nombre?.ToUpperInvariant().Contains(termUpper) ?? false) ||
            (p.TipoVehiculo?.ToUpperInvariant().Contains(termUpper) ?? false) ||
            (p.Estado?.ToUpperInvariant().Contains(termUpper) ?? false)
        ).ToList();
    }

    /// <summary>
    /// Valida un paquete antes de guardarlo.
    /// Verifica que los servicios sean válidos y cumplan las reglas.
    /// </summary>
    private async Task ValidarPaquete(PaqueteServicio paquete)
    {
        var errores = new List<string>();

        if (string.IsNullOrWhiteSpace(paquete.Nombre))
            errores.Add("El nombre del paquete no puede estar vacío");

        if (!string.IsNullOrWhiteSpace(paquete.Nombre) &&
            !System.Text.RegularExpressions.Regex.IsMatch(paquete.Nombre, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$"))
            errores.Add("El nombre solo puede contener letras y espacios");

        if (string.IsNullOrWhiteSpace(paquete.TipoVehiculo))
            errores.Add("El tipo de vehículo no puede estar vacío");

        if (paquete.PorcentajeDescuento < 0 || paquete.PorcentajeDescuento > 100)
            errores.Add("El porcentaje de descuento debe estar entre 0 y 100");

        if (paquete.ServiciosIds == null || paquete.ServiciosIds.Count < 2)
            errores.Add("El paquete debe incluir al menos 2 servicios");

        // Validar servicios
        if (paquete.ServiciosIds != null && paquete.ServiciosIds.Count >= 2)
        {
            var servicios = await ObtenerServiciosDePaquete(paquete.ServiciosIds);

            if (servicios.Count != paquete.ServiciosIds.Count)
                errores.Add("Algunos servicios seleccionados no existen");

            // Verificar que todos sean del mismo tipo de vehículo
            var tiposVehiculo = servicios.Select(s => s.TipoVehiculo).Distinct().ToList();
            if (tiposVehiculo.Count > 1)
                errores.Add("Todos los servicios deben ser para el mismo tipo de vehículo");

            if (tiposVehiculo.Count == 1 && tiposVehiculo[0] != paquete.TipoVehiculo)
                errores.Add("El tipo de vehículo del paquete debe coincidir con el de los servicios");

            // Verificar que no haya dos servicios del mismo tipo
            var tiposServicio = servicios.Select(s => s.Tipo).ToList();
            if (tiposServicio.Count != tiposServicio.Distinct().Count())
                errores.Add("No puede haber más de un servicio del mismo tipo en el paquete");

            // Verificar que todos los servicios estén activos
            var serviciosInactivos = servicios.Where(s => s.Estado != "Activo").ToList();
            if (serviciosInactivos.Any())
                errores.Add("Todos los servicios del paquete deben estar activos");
        }

        if (errores.Any())
            throw new ArgumentException($"Errores de validación: {string.Join(", ", errores)}");
    }

    /// <summary>
    /// Calcula el precio con descuento y el tiempo estimado del paquete.
    /// </summary>
    private async Task CalcularPrecioYTiempo(PaqueteServicio paquete)
    {
        var servicios = await ObtenerServiciosDePaquete(paquete.ServiciosIds);

        // Calcular suma de precios
        var precioTotal = servicios.Sum(s => s.Precio);

        // Aplicar descuento
        var descuento = precioTotal * (paquete.PorcentajeDescuento / 100);
        paquete.Precio = precioTotal - descuento;

        // Calcular tiempo total
        paquete.TiempoEstimado = servicios.Sum(s => s.TiempoEstimado);
    }

    /// <summary>
    /// Crea el diccionario de datos para guardar/actualizar un paquete en Firestore.
    /// </summary>
    private static Dictionary<string, object> CrearDiccionarioPaquete(PaqueteServicio paquete)
    {
        return new Dictionary<string, object>
        {
            { "Nombre", paquete.Nombre },
            { "Estado", paquete.Estado },
            { "Precio", (double)paquete.Precio },
            { "PorcentajeDescuento", (double)paquete.PorcentajeDescuento },
            { "TiempoEstimado", paquete.TiempoEstimado },
            { "TipoVehiculo", paquete.TipoVehiculo },
            { "ServiciosIds", paquete.ServiciosIds ?? new List<string>() }
        };
    }

    /// <summary>
    /// Mapea un documento de Firestore a un objeto PaqueteServicio.
    /// </summary>
    private static PaqueteServicio MapearDocumentoAPaquete(DocumentSnapshot documento)
    {
        return new PaqueteServicio
        {
            Id = documento.Id,
            Nombre = documento.GetValue<string>("Nombre"),
            Estado = documento.GetValue<string>("Estado"),
            Precio = documento.ContainsField("Precio")
                ? (decimal)Convert.ToDouble(documento.GetValue<object>("Precio"))
                : 0m,
            PorcentajeDescuento = documento.ContainsField("PorcentajeDescuento")
                ? (decimal)Convert.ToDouble(documento.GetValue<object>("PorcentajeDescuento"))
                : 0m,
            TiempoEstimado = documento.ContainsField("TiempoEstimado")
                ? documento.GetValue<int>("TiempoEstimado")
                : 0,
            TipoVehiculo = documento.GetValue<string>("TipoVehiculo"),
            ServiciosIds = documento.ContainsField("ServiciosIds")
                ? documento.GetValue<List<object>>("ServiciosIds").Select(o => o.ToString()).ToList()
                : new List<string>()
        };
    }

    /// <summary>
    /// Valida los parámetros de paginación.
    /// </summary>
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
    /// Clase auxiliar para conteo eficiente de paquetes.
    /// </summary>
    private class PaqueteConteo
    {
        /// <summary>Tipo de vehículo del paquete.</summary>
        public string TipoVehiculo { get; set; }
    }
    #endregion
}

using Firebase.Models;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Caching.Memory;

/// <summary>
/// Servicio para la gestión de paquetes de servicios en Firestore.
/// Proporciona operaciones CRUD, filtrado, paginación y validación de paquetes.
/// OPTIMIZADO con caché en memoria para reducir consultas a Firestore.
/// </summary>
public class PaqueteServicioService
{
    #region Constantes
    private const string COLLECTION_NAME = "paquetes_servicios";
    private const string ESTADO_DEFECTO = "Activo";
    private const string ORDEN_DEFECTO = "Nombre";
    private const string DIRECCION_DEFECTO = "asc";
    
    // NUEVO: Claves de caché
    private const string CACHE_KEY_SERVICIOS = "all_servicios_activos";
    private const int CACHE_DURATION_MINUTES = 10; // Caché por 10 minutos
    #endregion

    #region Dependencias
    private readonly FirestoreDb _firestore;
    private readonly ServicioService _servicioService;
    private readonly IMemoryCache _cache; // NUEVO

    /// <summary>
    /// Inicializa una nueva instancia del servicio de paquetes.
    /// </summary>
    /// <param name="firestore">Instancia de la base de datos Firestore.</param>
    /// <param name="servicioService">Servicio para validar servicios incluidos en el paquete.</param>
    /// <param name="cache">Instancia de caché en memoria.</param> // NUEVO parámetro
    public PaqueteServicioService(
        FirestoreDb firestore, 
      ServicioService servicioService,
        IMemoryCache cache) // NUEVO parámetro
    {
        _firestore = firestore ?? throw new ArgumentNullException(nameof(firestore));
        _servicioService = servicioService ?? throw new ArgumentNullException(nameof(servicioService));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache)); // NUEVO
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
        string sortOrder = null,
        decimal? precioMin = null,
        decimal? precioMax = null,
        int? tiempoMin = null,
        int? tiempoMax = null,
        decimal? descuentoMin = null,
        decimal? descuentoMax = null,
        int? serviciosMin = null,
        int? serviciosMax = null)
    {
        ValidarParametrosPaginacion(pageNumber, pageSize);

        sortBy ??= ORDEN_DEFECTO;
        sortOrder ??= DIRECCION_DEFECTO;

        var paquetes = await ObtenerPaquetesFiltrados(estados, tiposVehiculo, sortBy, sortOrder,
            precioMin, precioMax, tiempoMin, tiempoMax, descuentoMin, descuentoMax, serviciosMin, serviciosMax);

        return AplicarPaginacion(paquetes, pageNumber, pageSize);
    }

    /// <summary>
    /// Calcula el número total de páginas para los paquetes filtrados.
    /// </summary>
    public async Task<int> ObtenerTotalPaginas(
        List<string> estados,
        List<string> tiposVehiculo,
        int pageSize,
        decimal? precioMin = null,
        decimal? precioMax = null,
        int? tiempoMin = null,
        int? tiempoMax = null,
        decimal? descuentoMin = null,
        decimal? descuentoMax = null,
        int? serviciosMin = null,
        int? serviciosMax = null)
    {
        if (pageSize <= 0)
            throw new ArgumentException("El tamaño de página debe ser mayor a 0", nameof(pageSize));

        var totalPaquetes = await ObtenerTotalPaquetes(estados, tiposVehiculo,
            precioMin, precioMax, tiempoMin, tiempoMax, descuentoMin, descuentoMax, serviciosMin, serviciosMax);
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
        string sortOrder = null,
        decimal? precioMin = null,
        decimal? precioMax = null,
        int? tiempoMin = null,
        int? tiempoMax = null,
        decimal? descuentoMin = null,
        decimal? descuentoMax = null,
        int? serviciosMin = null,
        int? serviciosMax = null)
    {
        var baseFiltrada = await ObtenerPaquetesFiltrados(estados, tiposVehiculo, sortBy, sortOrder,
            precioMin, precioMax, tiempoMin, tiempoMax, descuentoMin, descuentoMax, serviciosMin, serviciosMax);

        // Búsqueda con sufijos especiales
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();

            // Búsqueda por tiempo exacto con sufijo "min" (ej: "35 min" o "35min")
            if (term.EndsWith("min", StringComparison.OrdinalIgnoreCase))
            {
                var numStr = term.Substring(0, term.Length - 3).Trim();
                if (int.TryParse(numStr, out var tiempoExacto))
                {
                    var tiempos = await CalcularTiemposAsync(baseFiltrada);
                    baseFiltrada = baseFiltrada.Where(p =>
                        tiempos.TryGetValue(p.Id, out var tiempo) && tiempo == tiempoExacto
                    ).ToList();
                }
                else
                {
                    // Si no se puede parsear, no hay coincidencias
                    baseFiltrada = new List<PaqueteServicio>();
                }
            }
            // Búsqueda por descuento exacto con sufijo "%" (ej: "15%" o "15 %")
            else if (term.EndsWith("%"))
            {
                var numStr = term.Substring(0, term.Length - 1).Trim();
                if (decimal.TryParse(numStr, System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out var descuentoExacto))
                {
                    baseFiltrada = baseFiltrada.Where(p =>
                        Math.Abs(p.PorcentajeDescuento - descuentoExacto) < 0.0001m
                    ).ToList();
                }
                else
                {
                    // Si no se puede parsear, no hay coincidencias
                    baseFiltrada = new List<PaqueteServicio>();
                }
            }
            // Búsqueda numérica general (precio/tiempo/descuento sin sufijo)
            else if (decimal.TryParse(term, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var numeroDecimal))
            {
                var precios = await CalcularPreciosAsync(baseFiltrada);
                var tiempos = await CalcularTiemposAsync(baseFiltrada);
                var numeroEntero = (int)numeroDecimal;
                baseFiltrada = baseFiltrada.Where(p =>
                    (precios.TryGetValue(p.Id, out var pr) && Math.Abs(pr - numeroDecimal) < 0.0001m) ||
                    (tiempos.TryGetValue(p.Id, out var tt) && tt == numeroEntero) ||
                    Math.Abs(p.PorcentajeDescuento - numeroDecimal) < 0.0001m
                ).ToList();
            }
            // Búsqueda textual
            else
            {
                baseFiltrada = AplicarBusqueda(baseFiltrada, term);
            }
        }

        var ordenados = await AplicarOrdenamiento(baseFiltrada, sortBy, sortOrder);
        return AplicarPaginacion(ordenados, Math.Max(pageNumber, 1), Math.Max(pageSize, 1));
    }

    /// <summary>
    /// Obtiene el total de paquetes que coinciden con la búsqueda.
    /// </summary>
    public async Task<int> ObtenerTotalPaquetesBusqueda(
        string searchTerm,
        List<string> estados,
        List<string> tiposVehiculo,
        decimal? precioMin = null,
        decimal? precioMax = null,
        int? tiempoMin = null,
        int? tiempoMax = null,
        decimal? descuentoMin = null,
        decimal? descuentoMax = null,
        int? serviciosMin = null,
        int? serviciosMax = null)
    {
        estados = ConfigurarEstadosDefecto(estados);
        var baseFiltrada = await ObtenerPaquetesFiltrados(estados, tiposVehiculo, ORDEN_DEFECTO, DIRECCION_DEFECTO,
            precioMin, precioMax, tiempoMin, tiempoMax, descuentoMin, descuentoMax, serviciosMin, serviciosMax);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();

            // Búsqueda por tiempo exacto con sufijo "min"
            if (term.EndsWith("min", StringComparison.OrdinalIgnoreCase))
            {
                var numStr = term.Substring(0, term.Length - 3).Trim();
                if (int.TryParse(numStr, out var tiempoExacto))
                {
                    var tiempos = await CalcularTiemposAsync(baseFiltrada);
                    baseFiltrada = baseFiltrada.Where(p =>
                        tiempos.TryGetValue(p.Id, out var tiempo) && tiempo == tiempoExacto
                    ).ToList();
                }
                else
                {
                    baseFiltrada = new List<PaqueteServicio>();
                }
            }
            // Búsqueda por descuento exacto con sufijo "%"
            else if (term.EndsWith("%"))
            {
                var numStr = term.Substring(0, term.Length - 1).Trim();
                if (decimal.TryParse(numStr, System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out var descuentoExacto))
                {
                    baseFiltrada = baseFiltrada.Where(p =>
                        Math.Abs(p.PorcentajeDescuento - descuentoExacto) < 0.0001m
                    ).ToList();
                }
                else
                {
                    baseFiltrada = new List<PaqueteServicio>();
                }
            }
            // Búsqueda numérica general
            else if (decimal.TryParse(term, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var numeroDecimal))
            {
                var precios = await CalcularPreciosAsync(baseFiltrada);
                var tiempos = await CalcularTiemposAsync(baseFiltrada);
                var numeroEntero = (int)numeroDecimal;
                baseFiltrada = baseFiltrada.Where(p =>
                    (precios.TryGetValue(p.Id, out var pr) && Math.Abs(pr - numeroDecimal) < 0.0001m) ||
                    (tiempos.TryGetValue(p.Id, out var tt) && tt == numeroEntero) ||
                    Math.Abs(p.PorcentajeDescuento - numeroDecimal) < 0.0001m
                ).ToList();
            }
            // Búsqueda textual
            else
            {
                baseFiltrada = AplicarBusqueda(baseFiltrada, term);
            }
        }

        return baseFiltrada.Count;
    }

    /// <summary>
    /// Obtiene el rango de precio (min y max) para los paquetes que cumplen con los filtros actuales
    /// excluyendo el propio filtro de precio.
    /// </summary>
    public async Task<(decimal? min, decimal? max)> ObtenerRangoPrecio(
        List<string> estados,
        List<string> tiposVehiculo,
        string searchTerm,
        int? tiempoMin = null,
        int? tiempoMax = null,
        decimal? descuentoMin = null,
        decimal? descuentoMax = null,
        int? serviciosMin = null,
        int? serviciosMax = null)
    {
        estados = ConfigurarEstadosDefecto(estados);
        // No aplicar precioMin/precioMax aquí
        var paquetes = await ObtenerPaquetesFiltrados(estados, tiposVehiculo, ORDEN_DEFECTO, DIRECCION_DEFECTO,
            null, null, tiempoMin, tiempoMax, descuentoMin, descuentoMax, serviciosMin, serviciosMax);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            paquetes = AplicarBusqueda(paquetes, searchTerm);
        }

        if (paquetes == null || paquetes.Count == 0)
            return (null, null);

        var precios = await CalcularPreciosAsync(paquetes);
        if (precios.Count == 0) return (null, null);
        var min = precios.Min(kv => kv.Value);
        var max = precios.Max(kv => kv.Value);
        return (min, max);
    }

    /// <summary>
    /// Obtiene el rango de descuento (min y max) para los paquetes que cumplen con los filtros actuales
  /// excluyendo el propio filtro de descuento.
    /// </summary>
    public async Task<(decimal? min, decimal? max)> ObtenerRangoDescuento(
        List<string> estados,
        List<string> tiposVehiculo,
     string searchTerm,
      decimal? precioMin = null,
  decimal? precioMax = null,
        int? tiempoMin = null,
        int? tiempoMax = null,
        int? serviciosMin = null,
        int? serviciosMax = null)
    {
        estados = ConfigurarEstadosDefecto(estados);
        // No aplicar descuentoMin/descuentoMax aquí
        var paquetes = await ObtenerPaquetesFiltrados(estados, tiposVehiculo, ORDEN_DEFECTO, DIRECCION_DEFECTO,
     precioMin, precioMax, tiempoMin, tiempoMax, null, null, serviciosMin, serviciosMax);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            paquetes = AplicarBusqueda(paquetes, searchTerm);
        }

    if (paquetes == null || paquetes.Count == 0)
            return (null, null);

        var descuentos = paquetes.Select(p => p.PorcentajeDescuento).ToList();
        if (descuentos.Count == 0) return (null, null);

        var min = descuentos.Min();
        var max = descuentos.Max();
        return (min, max);
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
        // Ya no se persisten valores calculados (Precio/TiempoEstimado)

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
        // Ya no se persisten valores calculados (Precio/TiempoEstimado)

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
    /// OPTIMIZADO con batch loading.
    /// </summary>
    public async Task<List<Servicio>> ObtenerServiciosDePaquete(List<string> serviciosIds)
    {
        if (serviciosIds == null || !serviciosIds.Any())
            return new List<Servicio>();

        var serviciosDict = await ObtenerServiciosBatch(serviciosIds);
    
        return serviciosIds
            .Where(id => serviciosDict.ContainsKey(id))
    .Select(id => serviciosDict[id])
      .ToList();
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
        string sortOrder,
        decimal? precioMin = null,
        decimal? precioMax = null,
        int? tiempoMin = null,
        int? tiempoMax = null,
        decimal? descuentoMin = null,
        decimal? descuentoMax = null,
        int? serviciosMin = null,
        int? serviciosMax = null)
    {
        estados = ConfigurarEstadosDefecto(estados);

        var query = ConstruirQueryFiltros(estados);

        var snapshot = await query.GetSnapshotAsync();
        var paquetes = snapshot.Documents
            .Select(MapearDocumentoAPaquete)
            .ToList();

        paquetes = AplicarFiltroTipoVehiculo(paquetes, tiposVehiculo);

        // Calcular valores dinámicos de precio y tiempo
        var precios = await CalcularPreciosAsync(paquetes);
        var tiempos = await CalcularTiemposAsync(paquetes);

        // Filtros por rango usando valores calculados
        IEnumerable<PaqueteServicio> q = paquetes;
        if (precioMin.HasValue) q = q.Where(p => precios.TryGetValue(p.Id, out var pr) && pr >= precioMin.Value);
        if (precioMax.HasValue) q = q.Where(p => precios.TryGetValue(p.Id, out var pr) && pr <= precioMax.Value);
        if (tiempoMin.HasValue) q = q.Where(p => tiempos.TryGetValue(p.Id, out var tt) && tt >= tiempoMin.Value);
        if (tiempoMax.HasValue) q = q.Where(p => tiempos.TryGetValue(p.Id, out var tt) && tt <= tiempoMax.Value);

        // Otros filtros (descuento y cantidad de servicios)
        if (descuentoMin.HasValue) q = q.Where(p => p.PorcentajeDescuento >= descuentoMin.Value);
        if (descuentoMax.HasValue) q = q.Where(p => p.PorcentajeDescuento <= descuentoMax.Value);
        if (serviciosMin.HasValue) q = q.Where(p => (p.ServiciosIds?.Count ??0) >= serviciosMin.Value);
        if (serviciosMax.HasValue) q = q.Where(p => (p.ServiciosIds?.Count ??0) <= serviciosMax.Value);

        var listFiltrada = q.ToList();

        // Ordenamiento considerando valores calculados
        return await AplicarOrdenamiento(listFiltrada, sortBy, sortOrder, precios, tiempos);
    }

    /// <summary>
    /// Obtiene el total de paquetes que cumplen con los filtros.
    /// </summary>
    private async Task<int> ObtenerTotalPaquetes(
        List<string> estados,
        List<string> tiposVehiculo,
        decimal? precioMin = null,
        decimal? precioMax = null,
        int? tiempoMin = null,
        int? tiempoMax = null,
        decimal? descuentoMin = null,
        decimal? descuentoMax = null,
        int? serviciosMin = null,
        int? serviciosMax = null)
    {
        estados = ConfigurarEstadosDefecto(estados);

        var query = ConstruirQueryFiltros(estados);

        var snapshot = await query.GetSnapshotAsync();
        var paquetes = snapshot.Documents
            .Select(MapearDocumentoAPaquete)
            .ToList();

        paquetes = AplicarFiltroTipoVehiculo(paquetes, tiposVehiculo);

        var precios = await CalcularPreciosAsync(paquetes);
        var tiempos = await CalcularTiemposAsync(paquetes);

        IEnumerable<PaqueteServicio> q = paquetes;
        if (precioMin.HasValue) q = q.Where(p => precios.TryGetValue(p.Id, out var pr) && pr >= precioMin.Value);
        if (precioMax.HasValue) q = q.Where(p => precios.TryGetValue(p.Id, out var pr) && pr <= precioMax.Value);
        if (tiempoMin.HasValue) q = q.Where(p => tiempos.TryGetValue(p.Id, out var tt) && tt >= tiempoMin.Value);
        if (tiempoMax.HasValue) q = q.Where(p => tiempos.TryGetValue(p.Id, out var tt) && tt <= tiempoMax.Value);
        if (descuentoMin.HasValue) q = q.Where(p => p.PorcentajeDescuento >= descuentoMin.Value);
        if (descuentoMax.HasValue) q = q.Where(p => p.PorcentajeDescuento <= descuentoMax.Value);
        if (serviciosMin.HasValue) q = q.Where(p => (p.ServiciosIds?.Count ??0) >= serviciosMin.Value);
        if (serviciosMax.HasValue) q = q.Where(p => (p.ServiciosIds?.Count ??0) <= serviciosMax.Value);

        return q.Count();
    }
    #endregion

    #region Métodos Públicos - Cálculos Optimizados

    /// <summary>
    /// OPTIMIZADO: Calcula precios usando caché de servicios.
    /// Carga todos los servicios necesarios en una sola consulta batch.
    /// </summary>
    public async Task<Dictionary<string, decimal>> CalcularPreciosAsync(IEnumerable<PaqueteServicio> paquetes)
    {
      var list = paquetes?.ToList() ?? new List<PaqueteServicio>();
        var result = new Dictionary<string, decimal>();
   
        if (!list.Any()) return result;

   // OPTIMIZACIÓN: Obtener todos los servicios únicos en una sola consulta
      var todosLosServiciosIds = list
       .SelectMany(p => p.ServiciosIds ?? new List<string>())
    .Distinct()
    .ToList();

        if (!todosLosServiciosIds.Any())
      {
       foreach (var p in list) result[p.Id] = 0m;
  return result;
   }

        // Cargar servicios en batch con caché
        var serviciosDict = await ObtenerServiciosBatch(todosLosServiciosIds);

  // Calcular precios
    foreach (var p in list)
  {
            try
          {
     var suma = 0m;
    if (p.ServiciosIds != null)
        {
   foreach (var id in p.ServiciosIds)
      {
    if (serviciosDict.TryGetValue(id, out var servicio))
  {
    suma += servicio.Precio;
            }
    }
      }
      var descuento = suma * (p.PorcentajeDescuento / 100m);
  result[p.Id] = suma - descuento;
       }
        catch { result[p.Id] = 0m; }
        }
        
  return result;
    }

    /// <summary>
    /// OPTIMIZADO: Calcula tiempos usando caché de servicios.
    /// Carga todos los servicios necesarios en una sola consulta batch.
    /// </summary>
    public async Task<Dictionary<string, int>> CalcularTiemposAsync(IEnumerable<PaqueteServicio> paquetes)
    {
      var list = paquetes?.ToList() ?? new List<PaqueteServicio>();
    var result = new Dictionary<string, int>();
        
        if (!list.Any()) return result;

     // OPTIMIZACIÓN: Obtener todos los servicios únicos en una sola consulta
        var todosLosServiciosIds = list
        .SelectMany(p => p.ServiciosIds ?? new List<string>())
       .Distinct()
  .ToList();

     if (!todosLosServiciosIds.Any())
        {
 foreach (var p in list) result[p.Id] = 0;
       return result;
     }

   // Cargar servicios en batch con caché
        var serviciosDict = await ObtenerServiciosBatch(todosLosServiciosIds);

        // Calcular tiempos
      foreach (var p in list)
        {
            try
{
         var suma = 0;
         if (p.ServiciosIds != null)
 {
       foreach (var id in p.ServiciosIds)
          {
   if (serviciosDict.TryGetValue(id, out var servicio))
          {
       suma += servicio.TiempoEstimado;
}
      }
     }
      result[p.Id] = suma;
  }
         catch { result[p.Id] = 0; }
  }
  
     return result;
  }

    #endregion

    #region Métodos Privados - Utilidades y Helpers

    /// <summary>
    /// NUEVO: Obtiene múltiples servicios en batch con caché.
    /// Reduce consultas a Firestore de N a 1 (o 0 si está en caché).
    /// </summary>
    private async Task<Dictionary<string, Servicio>> ObtenerServiciosBatch(List<string> serviciosIds)
    {
     if (serviciosIds == null || !serviciosIds.Any())
     return new Dictionary<string, Servicio>();

  // Intentar obtener del caché primero
var cacheKey = $"servicios_batch_{string.Join("_", serviciosIds.OrderBy(x => x))}";
      
        if (_cache.TryGetValue<Dictionary<string, Servicio>>(cacheKey, out var cached))
        {
        return cached;
   }

      var result = new Dictionary<string, Servicio>();

        // Firestore limita WhereIn a 10 elementos, dividir en chunks
     const int chunkSize = 10;
        var chunks = serviciosIds
            .Select((id, index) => new { id, index })
        .GroupBy(x => x.index / chunkSize)
            .Select(g => g.Select(x => x.id).ToList())
       .ToList();

     foreach (var chunk in chunks)
     {
   var query = _firestore.Collection("servicios")
          .WhereIn(FieldPath.DocumentId, chunk);
   
   var snapshot = await query.GetSnapshotAsync();

 foreach (var doc in snapshot.Documents)
     {
       if (doc.Exists)
 {
        var servicio = MapearDocumentoAServicio(doc);
     result[doc.Id] = servicio;
}
  }
      }

    // Guardar en caché por 10 minutos
        var cacheOptions = new MemoryCacheEntryOptions()
     .SetAbsoluteExpiration(TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));
        
   _cache.Set(cacheKey, result, cacheOptions);

   return result;
    }

    /// <summary>
    /// NUEVO: Mapea documento de Firestore a Servicio.
    /// </summary>
    private static Servicio MapearDocumentoAServicio(DocumentSnapshot doc)
    {
        return new Servicio
        {
     Id = doc.Id,
            Nombre = doc.GetValue<string>("Nombre"),
 Precio = doc.ContainsField("Precio") 
        ? (decimal)Convert.ToDouble(doc.GetValue<object>("Precio")) 
         : 0m,
     TiempoEstimado = doc.ContainsField("TiempoEstimado") 
? doc.GetValue<int>("TiempoEstimado") 
         : 0,
        Tipo = doc.GetValue<string>("Tipo"),
       TipoVehiculo = doc.GetValue<string>("TipoVehiculo"),
Descripcion = doc.GetValue<string>("Descripcion"),
 Estado = doc.GetValue<string>("Estado")
   };
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

        // Validación ajustada:5..95
     if (paquete.PorcentajeDescuento <5 || paquete.PorcentajeDescuento >95)
            errores.Add("El porcentaje de descuento debe estar entre 5 y 95");

        if (paquete.ServiciosIds == null || paquete.ServiciosIds.Count <2)
            errores.Add("El paquete debe incluir al menos 2 servicios");

        // Validar servicios
        if (paquete.ServiciosIds != null && paquete.ServiciosIds.Count >=2)
    {
 var servicios = await ObtenerServiciosDePaquete(paquete.ServiciosIds);

            if (servicios.Count != paquete.ServiciosIds.Count)
          errores.Add("Algunos servicios seleccionados no existen");

   var tiposVehiculo = servicios.Select(s => s.TipoVehiculo).Distinct().ToList();
     if (tiposVehiculo.Count >1)
                errores.Add("Todos los servicios deben ser para el mismo tipo de vehículo");

            if (tiposVehiculo.Count ==1 && tiposVehiculo[0] != paquete.TipoVehiculo)
        errores.Add("El tipo de vehículo del paquete debe coincidir con el de los servicios");

  var tiposServicio = servicios.Select(s => s.Tipo).ToList();
            if (tiposServicio.Count != tiposServicio.Distinct().Count())
      errores.Add("No puede haber más de un servicio del mismo tipo en el paquete");

      var serviciosInactivos = servicios.Where(s => s.Estado != "Activo").ToList();
            if (serviciosInactivos.Any())
              errores.Add("Todos los servicios del paquete deben estar activos");
        }

        if (errores.Any())
  throw new ArgumentException($"Errores de validación: {string.Join(", ", errores)}");
    }

    /// <summary>
    /// Crea el diccionario de datos para guardar/actualizar un paquete en Firestore.
    /// (Sin campos calculados Precio/TiempoEstimado)
    /// </summary>
    private static Dictionary<string, object> CrearDiccionarioPaquete(PaqueteServicio paquete)
    {
  return new Dictionary<string, object>
        {
       { "Nombre", paquete.Nombre },
   { "Estado", paquete.Estado },
            { "PorcentajeDescuento", (double)paquete.PorcentajeDescuento },
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
    /// Aplica ordenamiento a una lista de paquetes. Usa valores calculados si corresponden.
  /// </summary>
    private async Task<List<PaqueteServicio>> AplicarOrdenamiento(
        List<PaqueteServicio> paquetes, 
      string sortBy, 
   string sortOrder, 
     IDictionary<string, decimal> precios = null, 
  IDictionary<string, int> tiempos = null)
    {
  sortBy ??= ORDEN_DEFECTO;
        sortOrder = (sortOrder ?? DIRECCION_DEFECTO).Trim().ToLowerInvariant();

        IOrderedEnumerable<PaqueteServicio> ordered;
        bool desc = sortOrder == "desc";

    switch (sortBy)
        {
      case "Precio":
     precios ??= await CalcularPreciosAsync(paquetes);
ordered = desc 
      ? paquetes.OrderByDescending(p => precios.TryGetValue(p.Id, out var val) ? val : 0m)
      : paquetes.OrderBy(p => precios.TryGetValue(p.Id, out var val) ? val : 0m);
         break;
     case "TiempoEstimado":
     tiempos ??= await CalcularTiemposAsync(paquetes);
   ordered = desc 
     ? paquetes.OrderByDescending(p => tiempos.TryGetValue(p.Id, out var val) ? val : 0)
        : paquetes.OrderBy(p => tiempos.TryGetValue(p.Id, out var val) ? val : 0);
    break;
  case "TipoVehiculo":
      ordered = desc 
          ? paquetes.OrderByDescending(p => p.TipoVehiculo)
   : paquetes.OrderBy(p => p.TipoVehiculo);
   break;
          case "Estado":
   ordered = desc 
         ? paquetes.OrderByDescending(p => p.Estado)
: paquetes.OrderBy(p => p.Estado);
break;
       case "PorcentajeDescuento":
  ordered = desc 
   ? paquetes.OrderByDescending(p => p.PorcentajeDescuento)
     : paquetes.OrderBy(p => p.PorcentajeDescuento);
       break;
      case "CantidadServicios":
      ordered = desc 
        ? paquetes.OrderByDescending(p => (p.ServiciosIds?.Count ?? 0))
    : paquetes.OrderBy(p => (p.ServiciosIds?.Count ?? 0));
       break;
case "Nombre":
  default:
ordered = desc 
      ? paquetes.OrderByDescending(p => p.Nombre)
   : paquetes.OrderBy(p => p.Nombre);
 break;
    }

     return ordered.ToList();
  }

    /// <summary>
    /// Aplica paginación a una lista en memoria.
    /// </summary>
    private static List<PaqueteServicio> AplicarPaginacion(List<PaqueteServicio> lista, int pageNumber, int pageSize)
    {
   return lista.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
    }

  /// <summary>
  /// Aplica la lógica de búsqueda sobre una lista previamente filtrada (textual).
    /// </summary>
    private static List<PaqueteServicio> AplicarBusqueda(List<PaqueteServicio> baseFiltrada, string searchTerm)
    {
   var term = searchTerm?.Trim() ?? string.Empty;
        if (term.Length == 0) return baseFiltrada;

  var termUpper = term.ToUpperInvariant();

        return baseFiltrada.Where(p =>
(p.Nombre?.ToUpperInvariant().Contains(termUpper) ?? false) ||
   (p.TipoVehiculo?.ToUpperInvariant().Contains(termUpper) ?? false) ||
      (p.Estado?.ToUpperInvariant().Contains(termUpper) ?? false)
        ).ToList();
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

    /// <summary>
    /// Devuelve los valores distintos de cantidad de servicios presentes en todos los paquetes.
    /// </summary>
    public async Task<List<int>> ObtenerValoresCantidadServicios()
    {
        var snapshot = await _firestore.Collection(COLLECTION_NAME).GetSnapshotAsync();
        var counts = snapshot.Documents
            .Select(d =>
            {
                try
                {
                    if (d.ContainsField("ServiciosIds"))
                    {
                        var arr = d.GetValue<List<object>>("ServiciosIds");
                        return arr?.Count ??0;
                    }
                    return 0;
                }
                catch { return 0; }
            })
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        // Filtrar 0 ya que un paquete válido debe tener 2+ servicios
        counts = counts.Where(c => c > 0).ToList();
        return counts;
    }
}

using Firebase.Models.Dtos;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Threading;

namespace Firebase.Services
{
    /// <summary>
    /// Servicio para interactuar con la API de CarQuery
    /// </summary>
    public class CarQueryService : ICarQueryService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<CarQueryService> _logger;
        private readonly string _baseUrl = "https://www.carqueryapi.com/api/0.3/";
        private readonly JsonSerializerOptions _jsonOptions;

        public CarQueryService(
            HttpClient httpClient,
            IMemoryCache cache,
            ILogger<CarQueryService> logger)
        {
            _httpClient = httpClient;
            _cache = cache;
            _logger = logger;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        /// <summary>
        /// Obtiene todas las marcas disponibles
        /// </summary>
        public async Task<List<MarcaSimpleDto>> GetMarcasAsync()
        {
            const string cacheKey = "marcas_all";

            // 1. Verificar caché
            if (_cache.TryGetValue(cacheKey, out List<MarcaSimpleDto>? cachedMarcas) && cachedMarcas != null)
            {
                _logger.LogInformation("? {Count} marcas obtenidas desde caché", cachedMarcas.Count);
                return cachedMarcas;
            }

            // 2. Caché vacío, consultar API
            _logger.LogInformation("?? Caché vacío, consultando API CarQuery...");

            try
            {
                var url = $"{_baseUrl}?cmd=getMakes";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("?? Error HTTP {StatusCode}", response.StatusCode);
                    return new List<MarcaSimpleDto>();
                }

                var json = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<CarQueryMakesResponse>(json, _jsonOptions);

                if (apiResponse?.Makes == null || apiResponse.Makes.Count == 0)
                {
                    _logger.LogWarning("?? API retornó 0 marcas");
                    return new List<MarcaSimpleDto>();
                }

                // Mapear y ordenar
                var marcas = apiResponse.Makes
                    .Select(m => new MarcaSimpleDto(m.make_id, m.make_display))
                    .OrderBy(m => m.Nombre)
                    .ToList();

                _logger.LogInformation("? {Count} marcas obtenidas desde API", marcas.Count);

                // Guardar en caché (24 horas)
                _cache.Set(cacheKey, marcas, TimeSpan.FromHours(24));

                return marcas;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "?? Error al consultar marcas");
                return new List<MarcaSimpleDto>();
            }
        }

        /// <summary>
        /// Obtiene modelos por marca consultando múltiples años (2010-actualidad)
        /// </summary>
        public async Task<List<ModeloSimpleDto>> GetModelosAsync(string marcaId, int? year = null)
        {
            var cacheKey = $"modelos_{marcaId}_{year}";

            // 1. Verificar caché
            if (_cache.TryGetValue(cacheKey, out List<ModeloSimpleDto>? cachedModelos) && cachedModelos != null)
            {
                _logger.LogInformation("? {Count} modelos de '{MarcaId}' obtenidos desde caché", cachedModelos.Count, marcaId);
                return cachedModelos;
            }

            // 2. Caché vacío, consultar API
            _logger.LogInformation("?? Consultando modelos para '{MarcaId}'...", marcaId);

            try
            {
                List<ModeloSimpleDto> modelos;

                // Si no se especifica año, consultar rango 2010-actualidad
                if (!year.HasValue)
                {
                    modelos = await GetModelosPorRangoDeAños(marcaId);
                }
                else
                {
                    // Consulta simple por año específico
                    modelos = await GetModelosPorAño(marcaId, year.Value);
                }

                // Guardar en caché (1 hora)
                _cache.Set(cacheKey, modelos, TimeSpan.FromHours(1));

                return modelos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "?? Error al consultar modelos para '{MarcaId}'", marcaId);
                return new List<ModeloSimpleDto>();
            }
        }

        /// <summary>
        /// Consulta modelos para un rango de años (2010-actualidad) y devuelve lista única
        /// </summary>
        private async Task<List<ModeloSimpleDto>> GetModelosPorRangoDeAños(string marcaId)
        {
            int añoInicio = 2010;
            int añoFin = DateTime.Now.Year;

            _logger.LogInformation("?? Consultando años {Inicio}-{Fin} para '{MarcaId}'", añoInicio, añoFin, marcaId);

            // Diccionario para consolidar modelos únicos (key = nombre normalizado)
            var modelosUnicos = new Dictionary<string, ModeloSimpleDto>(StringComparer.OrdinalIgnoreCase);

            // Consultar año por año con límite de concurrencia
            using var semaphore = new SemaphoreSlim(3); // Máximo 3 peticiones simultáneas
            var tasks = new List<Task>();

            for (int año = añoInicio; año <= añoFin; año++)
            {
                int añoActual = año; // Capturar para el closure
                
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var url = $"{_baseUrl}?cmd=getModels&make={Uri.EscapeDataString(marcaId)}&year={añoActual}";
                        var response = await _httpClient.GetAsync(url);
                        
                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadAsStringAsync();
                            var apiResponse = JsonSerializer.Deserialize<CarQueryModelsResponse>(json, _jsonOptions);

                            if (apiResponse?.Models != null)
                            {
                                lock (modelosUnicos)
                                {
                                    foreach (var modelo in apiResponse.Models)
                                    {
                                        if (!string.IsNullOrWhiteSpace(modelo.model_name))
                                        {
                                            var key = modelo.model_name.Trim();
                                            if (!modelosUnicos.ContainsKey(key))
                                            {
                                                modelosUnicos[key] = new ModeloSimpleDto(key, marcaId);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("?? Error consultando año {Año}: {Message}", añoActual, ex.Message);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            // Esperar a que todas las peticiones terminen
            await Task.WhenAll(tasks);

            // Convertir a lista ordenada
            var modelos = modelosUnicos.Values
                .OrderBy(m => m.Nombre)
                .ToList();

            _logger.LogInformation("? {Count} modelos ÚNICOS encontrados para '{MarcaId}' (años {Inicio}-{Fin})", 
                modelos.Count, marcaId, añoInicio, añoFin);

            return modelos;
        }

        /// <summary>
        /// Consulta modelos para un año específico
        /// </summary>
        private async Task<List<ModeloSimpleDto>> GetModelosPorAño(string marcaId, int year)
        {
            var url = $"{_baseUrl}?cmd=getModels&make={Uri.EscapeDataString(marcaId)}&year={year}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                return new List<ModeloSimpleDto>();
            }

            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<CarQueryModelsResponse>(json, _jsonOptions);

            if (apiResponse?.Models == null)
            {
                return new List<ModeloSimpleDto>();
            }

            var modelos = apiResponse.Models
                .Where(m => !string.IsNullOrWhiteSpace(m.model_name))
                .Select(m => new ModeloSimpleDto(m.model_name.Trim(), marcaId))
                .OrderBy(m => m.Nombre)
                .ToList();

            _logger.LogInformation("? {Count} modelos para '{MarcaId}' año {Year}", modelos.Count, marcaId, year);

            return modelos;
        }

        /// <summary>
        /// Obtiene el rango de años disponibles para una marca
        /// </summary>
        public async Task<(int minYear, int maxYear)> GetYearsAsync(string marcaId)
        {
            const string cacheKey = "years_{0}";
            var key = string.Format(cacheKey, marcaId);

            if (_cache.TryGetValue(key, out (int min, int max) cachedYears))
            {
                return cachedYears;
            }

            try
            {
                var url = $"{_baseUrl}?cmd=getYears&make={Uri.EscapeDataString(marcaId)}";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    // Valores por defecto
                    return (2010, DateTime.Now.Year);
                }

                var json = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<CarQueryYearsDto>(json, _jsonOptions);

                if (apiResponse?.Years == null)
                {
                    return (2010, DateTime.Now.Year);
                }

                var years = (apiResponse.Years.min_year, apiResponse.Years.max_year);
                _cache.Set(key, years, TimeSpan.FromHours(24));

                return years;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar años para {MarcaId}", marcaId);
                return (2010, DateTime.Now.Year);
            }
        }

        /// <summary>
        /// Obtiene colores comunes de vehículos
        /// </summary>
        public Task<List<string>> GetColoresComunes()
        {
            var colores = new List<string>
            {
                "Blanco",
                "Negro",
                "Gris",
                "Plata",
                "Rojo",
                "Azul",
                "Verde",
                "Amarillo",
                "Naranja",
                "Marrón",
                "Beige",
                "Dorado",
                "Violeta",
                "Rosa"
            };

            return Task.FromResult(colores);
        }
    }

    #region DTOs Internos de CarQuery
    internal class CarQueryMakesResponse
    {
        public List<CarQueryMakeDto> Makes { get; set; } = new();
    }

    internal class CarQueryModelsResponse
    {
        public List<CarQueryModelDto> Models { get; set; } = new();
    }
    #endregion
}

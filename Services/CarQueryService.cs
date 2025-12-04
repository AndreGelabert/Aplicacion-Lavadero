using Firebase.Models.Dtos;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Threading;

namespace Firebase.Services
{
    /// <summary>
    /// Servicio para interactuar con la API de NHTSA (https://vpic.nhtsa.dot.gov)
    /// </summary>
    public class CarQueryService : ICarQueryService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<CarQueryService> _logger;
        private readonly string _baseUrl = "https://vpic.nhtsa.dot.gov/api/vehicles";
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
            _logger.LogInformation("?? Caché vacío, consultando NHTSA...");

            try
            {
                // NHTSA endpoint: GET /getallmakes?format=json
                var url = $"{_baseUrl}/getallmakes?format=json";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("?? Error HTTP {StatusCode}", response.StatusCode);
                    return new List<MarcaSimpleDto>();
                }

                var json = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<NhtsaMakesResponse>(json, _jsonOptions);

                if (apiResponse?.Results == null || apiResponse.Results.Count == 0)
                {
                    _logger.LogWarning("?? API retornó 0 marcas");
                    return new List<MarcaSimpleDto>();
                }

                // Mapear y ordenar
                var marcas = apiResponse.Results
                    .Select(m => new MarcaSimpleDto(m.Make_ID.ToString(), m.Make_Name))
                    .OrderBy(m => m.Nombre)
                    .ToList();

                _logger.LogInformation("? {Count} marcas obtenidas desde NHTSA", marcas.Count);

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
        /// Obtiene modelos por marca
        /// </summary>
        public async Task<List<ModeloSimpleDto>> GetModelosAsync(string marcaId, int? year = null)
        {
            var cacheKey = $"modelos_{marcaId}";

            // 1. Verificar caché
            if (_cache.TryGetValue(cacheKey, out List<ModeloSimpleDto>? cachedModelos) && cachedModelos != null)
            {
                _logger.LogInformation("? {Count} modelos de marca '{MarcaId}' obtenidos desde caché", cachedModelos.Count, marcaId);
                return cachedModelos;
            }

            // 2. Caché vacío, consultar API
            _logger.LogInformation("?? Consultando modelos para marca '{MarcaId}'...", marcaId);

            try
            {
                // NHTSA endpoint: GET /GetModelsForMakeId/{makeId}?format=json
                var url = $"{_baseUrl}/GetModelsForMakeId/{marcaId}?format=json";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("?? Error HTTP {StatusCode}", response.StatusCode);
                    return new List<ModeloSimpleDto>();
                }

                var json = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<NhtsaModelsResponse>(json, _jsonOptions);

                if (apiResponse?.Results == null || apiResponse.Results.Count == 0)
                {
                    _logger.LogWarning("?? 0 modelos para marca '{MarcaId}'", marcaId);
                    return new List<ModeloSimpleDto>();
                }

                var modelos = apiResponse.Results
                    .Where(m => !string.IsNullOrWhiteSpace(m.Model_Name))
                    .Select(m => new ModeloSimpleDto(m.Model_Name.Trim(), marcaId))
                    .Distinct()
                    .OrderBy(m => m.Nombre)
                    .ToList();

                _logger.LogInformation("? {Count} modelos para marca '{MarcaId}'", modelos.Count, marcaId);

                // Guardar en caché (1 hora)
                _cache.Set(cacheKey, modelos, TimeSpan.FromHours(1));

                return modelos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "?? Error al consultar modelos para marca '{MarcaId}'", marcaId);
                return new List<ModeloSimpleDto>();
            }
        }

        /// <summary>
        /// Obtiene el rango de años disponibles para una marca
        /// </summary>
        public Task<(int minYear, int maxYear)> GetYearsAsync(string marcaId)
        {
            // NHTSA tiene datos desde 1980 hasta el presente
            return Task.FromResult((1980, DateTime.Now.Year));
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

    #region DTOs Internos de NHTSA
    internal class NhtsaMakesResponse
    {
        public List<NhtsaMake> Results { get; set; } = new();
    }

    internal class NhtsaMake
    {
        public int Make_ID { get; set; }
        public string Make_Name { get; set; } = string.Empty;
    }

    internal class NhtsaModelsResponse
    {
        public List<NhtsaModel> Results { get; set; } = new();
    }

    internal class NhtsaModel
    {
        public int Make_ID { get; set; }
        public string Make_Name { get; set; } = string.Empty;
        public int Model_ID { get; set; }
        public string Model_Name { get; set; } = string.Empty;
    }
    #endregion
}

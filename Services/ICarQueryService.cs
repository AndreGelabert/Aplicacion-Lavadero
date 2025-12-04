using Firebase.Models.Dtos;

namespace Firebase.Services
{
    /// <summary>
    /// Servicio para consultar la API de CarQuery
    /// </summary>
    public interface ICarQueryService
    {
        /// <summary>
        /// Obtiene la lista de todas las marcas de vehículos
        /// </summary>
        Task<List<MarcaSimpleDto>> GetMarcasAsync();

        /// <summary>
        /// Obtiene los modelos de una marca específica
        /// </summary>
        /// <param name="marcaId">ID de la marca (ej: "toyota")</param>
        /// <param name="year">Año opcional para filtrar modelos</param>
        Task<List<ModeloSimpleDto>> GetModelosAsync(string marcaId, int? year = null);

        /// <summary>
        /// Obtiene el rango de años disponibles para una marca
        /// </summary>
        /// <param name="marcaId">ID de la marca</param>
        Task<(int minYear, int maxYear)> GetYearsAsync(string marcaId);

        /// <summary>
        /// Obtiene lista de colores comunes para vehículos
        /// </summary>
        Task<List<string>> GetColoresComunes();
    }
}
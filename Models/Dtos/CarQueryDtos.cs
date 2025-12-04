namespace Firebase.Models.Dtos
{
    /// <summary>
    /// DTO para marca de vehículo desde CarQuery API
    /// </summary>
    public record CarQueryMakeDto(
        string make_id,
        string make_display,
        string? make_country
    );

    /// <summary>
    /// DTO para modelo de vehículo desde CarQuery API
    /// </summary>
    public record CarQueryModelDto(
        string model_name,
        string model_make_id
    );

    /// <summary>
    /// DTO para versión/trim de vehículo desde CarQuery API
    /// </summary>
    public record CarQueryTrimDto(
        int model_id,
        string model_name,
        string model_make_id,
        int model_year,
        string? model_engine_type,
        string? model_body
    );

    /// <summary>
    /// DTO para rango de años desde CarQuery API
    /// </summary>
    public record CarQueryYearsDto(
        YearsRange Years
    );

    public record YearsRange(
        int min_year,
        int max_year
    );

    /// <summary>
    /// DTOs simplificados para frontend
    /// </summary>
    public record MarcaSimpleDto(string Id, string Nombre);
    
    public record ModeloSimpleDto(string Nombre, string MarcaId);

    /// <summary>
    /// Clase auxiliar para deserializar respuesta de CarQuery API
    /// </summary>
    public class CarQueryApiResponse<T>
    {
        public List<T>? Makes { get; set; }
        public List<T>? Models { get; set; }
        public List<T>? Trims { get; set; }
    }
}

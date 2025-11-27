using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// Servicio para enviar eventos de seguimiento a Google Analytics 4.
/// Utiliza la API de Measurement Protocol para enviar eventos desde el servidor.
/// </summary>
/// <remarks>
/// Este servicio se utiliza para rastrear eventos importantes como:
/// - Inicios de sesión
/// - Registros de usuarios
/// - Acciones de negocio relevantes
/// 
/// La configuración requiere un Measurement ID y un API Secret de Google Analytics.
/// </remarks>
public class GoogleAnalyticsService
{
    #region Dependencias

    private readonly HttpClient _httpClient;
    private readonly string _measurementId;
    private readonly string _apiSecret;

    /// <summary>
    /// Inicializa una nueva instancia del servicio de Google Analytics.
    /// </summary>
    /// <param name="httpClient">Cliente HTTP para realizar las peticiones.</param>
    /// <param name="measurementId">ID de medición de Google Analytics (formato: G-XXXXXXXXXX).</param>
    /// <param name="apiSecret">Clave secreta de la API de Measurement Protocol.</param>
    public GoogleAnalyticsService(HttpClient httpClient, string measurementId, string apiSecret)
    {
        _httpClient = httpClient;
        _measurementId = measurementId;
        _apiSecret = apiSecret;
    }

    #endregion

    #region Métodos Públicos

    /// <summary>
    /// Envía un evento de seguimiento a Google Analytics.
    /// </summary>
    /// <param name="eventName">Nombre del evento a registrar (ej: "login", "signup", "purchase").</param>
    /// <param name="clientId">Identificador único del cliente/usuario.</param>
    /// <param name="email">Correo electrónico del usuario (opcional, para identificación adicional).</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    /// <exception cref="HttpRequestException">Se produce cuando la solicitud HTTP falla.</exception>
    public async Task TrackEvent(string eventName, string clientId, string email = null)
    {
        var payload = new
        {
            client_id = clientId,
            events = new[]
            {
                new
                {
                    name = eventName,
                    parameters = new Dictionary<string, object>
                    {
                        { "email", email ?? string.Empty }
                    }
                }
            }
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(
            $"https://www.google-analytics.com/mp/collect?measurement_id={_measurementId}&api_secret={_apiSecret}",
            content
        );

        response.EnsureSuccessStatusCode();
    }

    #endregion
}

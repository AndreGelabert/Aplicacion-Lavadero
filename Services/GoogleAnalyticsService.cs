using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

public class GoogleAnalyticsService
{
    private readonly HttpClient _httpClient;
    private readonly string _measurementId;
    private readonly string _apiSecret;

    public GoogleAnalyticsService(HttpClient httpClient, string measurementId, string apiSecret)
    {
        _httpClient = httpClient;
        _measurementId = measurementId;
        _apiSecret = apiSecret;
    }

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
}

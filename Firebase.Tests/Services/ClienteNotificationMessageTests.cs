using Firebase.Models;
using Firebase.Services;

namespace Firebase.Tests.Services;

public class ClienteNotificationMessageTests
{
    [Theory]
    [InlineData("finalizacion_servicio", "finalizó correctamente")]
    [InlineData("cancelacion_servicio", "fue cancelado")]
    public void ServiceEvent_UsesFriendlyContextAndTruthfulOutcome(string kind, string outcome)
    {
        var lavado = new Lavado { Id = "wash", ClienteId = "client", VehiculoId = "vehicle", Estado = "EnProceso",
            Servicios = new() { new ServicioEnLavado { ServicioId = "s", ServicioNombre = "Lavado completo", Estado = "Realizado" } } };
        var message = ClienteNotificationService.BuildMessage(lavado, null, kind, "s", null, null);
        Assert.Contains("queremos avisarte", message);
        Assert.Contains("Lavado completo", message);
        Assert.Contains(outcome, message);
        Assert.Contains("Fecha y hora:", message);
    }

    [Fact]
    public async Task WhatsAppSequentialAcceptedRequests_DoNotEstablishDelivery()
    {
        var handler = new AcceptedHandler();
        var settings = Microsoft.Extensions.Options.Options.Create(new WhatsAppSettings
        { AccessToken = "fake", VerifyToken = "fake", PhoneNumberId = "fake" });
        var meta = new MetaWhatsAppService(new HttpClient(handler), settings,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<MetaWhatsAppService>.Instance);
        Assert.True(await meta.SendTextMessage("543751000000", "primera"));
        Assert.True(await meta.SendTextMessage("543751000000", "segunda"));
        Assert.Equal(2, handler.Bodies.Count);
        Assert.Contains("primera", handler.Bodies[0]);
        Assert.Contains("segunda", handler.Bodies[1]);
        Assert.Contains("aceptadas por el proveedor", new NotificationDispatchResult(2, 0, 0, 0, 0).ToEmployeeMessage());
    }

    private sealed class AcceptedHandler : HttpMessageHandler
    {
        public List<string> Bodies { get; } = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        }
    }

    [Fact]
    public void CompletedStage_ReportsNextStageRatherThanCurrentService()
    {
        var lavado = new Lavado
        {
            Id = "lavado", ClienteId = "cliente", VehiculoId = "vehiculo", Estado = "EnProceso",
            Servicios = new() { new ServicioEnLavado
            {
                ServicioId = "servicio", ServicioNombre = "Lavado complejo", Estado = "EnProceso",
                Etapas = new() {
                    new EtapaEnLavado { EtapaId = "uno", Nombre = "Mojado inicial", Estado = "Realizado" },
                    new EtapaEnLavado { EtapaId = "dos", Nombre = "Enjuague", Estado = "Pendiente" }
                }
            } }
        };

        var message = ClienteNotificationService.BuildMessage(lavado, null, "finalizacion_etapa", "servicio", "uno", null);

        Assert.Contains("Sigue: Enjuague.", message);
        Assert.DoesNotContain("Sigue: Lavado complejo.", message);
    }
}

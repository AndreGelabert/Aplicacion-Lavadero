using System.Net;
using Firebase.Models;
using Firebase.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Firebase.Tests.Services;

public class ResendEmailSenderTests
{
    private const string Rechazado = "El destinatario no está permitido por la allowlist de desarrollo.";

    [Fact]
    public async Task DestinatarioNoPermitidoSeRechazaSinTransporte()
    {
        var handler = new FakeHandler();
        var sender = CrearSender(handler, "permitido@example.test");

        var result = await sender.EnviarAsync(new EmailMessage("otro@example.test", "asunto", "contenido"));

        Assert.False(result.Enviado);
        Assert.Equal(Rechazado, result.Mensaje);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task AllowlistVaciaSeRechazaSinTransporte()
    {
        var handler = new FakeHandler();
        var sender = CrearSender(handler, "");

        var result = await sender.EnviarAsync(new EmailMessage("permitido@example.test", "asunto", "contenido"));

        Assert.False(result.Enviado);
        Assert.Equal(Rechazado, result.Mensaje);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task DestinatarioPermitidoSinDistinguirMayusculasInvocaTransporteUnaVez()
    {
        var handler = new FakeHandler();
        var sender = CrearSender(handler, "Permitido@Example.Test");

        var result = await sender.EnviarAsync(new EmailMessage("permitido@example.test", "asunto", "contenido"));

        Assert.True(result.Enviado);
        Assert.Equal(1, handler.Calls);
    }

    private static ResendEmailSender CrearSender(FakeHandler handler, string allowed) => new(
        new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") },
        Options.Create(new EmailSettings { ApiKey = "synthetic-key", From = "sender@example.test", DevelopmentAllowedRecipient = allowed }),
        new DevelopmentEnvironment(), NullLogger<ResendEmailSender>.Instance);

    private sealed class FakeHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"synthetic\"}") });
        }
    }

    private sealed class DevelopmentEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Tests";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

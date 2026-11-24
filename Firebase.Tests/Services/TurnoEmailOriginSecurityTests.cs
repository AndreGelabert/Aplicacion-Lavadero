using System.Net;
using Firebase.Models;
using Firebase.Services;

namespace Firebase.Tests.Services;

public class TurnoEmailOriginSecurityTests
{
    private const string Secret = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
    private static readonly EmailMessage Message = new("client@example.com", "Oferta", "<p>Oferta</p>");

    [Theory]
    [InlineData("https://user:password@example.com")]
    [InlineData("https://example.com/path")]
    [InlineData("https://example.com/?query=value")]
    [InlineData("https://example.com/#fragment")]
    [InlineData("http://example.com")]
    public void InvalidOriginsAreRejected(string origin)
    {
        var sender = new Sender();
        var resolver = new TurnoOfertaEmail(sender, origin);
        Assert.False(resolver.HasOrigin);
        Assert.Equal(0, sender.Count);
    }

    [Theory]
    [InlineData("https://user:password@example.com")]
    [InlineData("https://example.com/path")]
    [InlineData("https://example.com/?query=value")]
    [InlineData("https://example.com/#fragment")]
    [InlineData("http://example.com")]
    public void InvalidOriginCannotProduceMessageContainingSecret(string origin)
    {
        var sender = new Sender();
        var resolver = new TurnoOfertaEmail(sender, origin);
        var error = Assert.Throws<InvalidOperationException>(() => resolver.AddLink(Message, "cascade", "offer", "client", Secret));
        Assert.DoesNotContain(Secret, error.ToString());
        Assert.DoesNotContain(Secret, Message.Html);
        Assert.Equal(0, sender.Count);
    }

    [Fact]
    public void ValidHttpsOriginKeepsSecretInFragmentOfTrustedLink()
    {
        var sender = new Sender();
        var resolver = new TurnoOfertaEmail(sender, "https://example.com:8443");
        Assert.True(resolver.HasOrigin);
        var message = resolver.AddLink(Message, "cascade", "offer", "client", Secret);
        var html = WebUtility.HtmlDecode(message.Html);
        Assert.Contains("href=\"https://example.com:8443/turno-oferta-email/responder#CascadeId=cascade&OfferId=offer&ClientId=client&Token=" + Secret + "\"", html);
        Assert.DoesNotContain(Secret, message.To);
        Assert.DoesNotContain(Secret, message.Subject);
        Assert.Equal(0, sender.Count);
    }

    [Theory]
    [InlineData("http://localhost:7059")]
    [InlineData("http://127.0.0.1:5000")]
    public void LocalhostLoopbackOriginsAreAcceptedForDevelopment(string origin)
    {
        var sender = new Sender();
        var resolver = new TurnoOfertaEmail(sender, origin);
        Assert.True(resolver.HasOrigin);
        var message = resolver.AddLink(Message, "cascade", "offer", "client", Secret);
        var html = WebUtility.HtmlDecode(message.Html);
        Assert.Contains("href=\"" + origin + "/turno-oferta-email/responder#CascadeId=cascade&OfferId=offer&ClientId=client&Token=" + Secret + "\"", html);
        Assert.Equal(0, sender.Count);
    }

    private sealed class Sender : IEmailSender
    {
        public int Count;
        public Task<ResultadoEnvioEmail> EnviarAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            Count++;
            return Task.FromResult(new ResultadoEnvioEmail(true, "accepted"));
        }
    }
}

using Google.Apis.Http;

namespace Firebase.Tests.Helpers;

// Fail closed: no request may escape to the application emulator or the network.
internal sealed class LocalAuthEmulatorTransport : DelegatingHandler
{
    public LocalAuthEmulatorTransport() : base(new HttpClientHandler { UseProxy = false }) { }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri;
        if (uri == null || uri.Scheme != "http" || uri.Host != "127.0.0.1" || uri.Port != 9099)
            throw new InvalidOperationException("Unexpected Auth endpoint; refusing request.");
        request.RequestUri = new UriBuilder(uri) { Port = 9199 }.Uri;
        return base.SendAsync(request, cancellationToken);
    }
}

internal sealed class LocalAuthClientFactory : Google.Apis.Http.HttpClientFactory
{
    protected override HttpMessageHandler CreateHandler(CreateHttpClientArgs args) => new LocalAuthEmulatorTransport();
}

using System.Runtime.CompilerServices;
using Firebase.BackgroundServices;
using Firebase.Models;
using Firebase.Services;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Firebase.Tests.Services;

public class TurnoOfferHostRegistrationTests
{
    private sealed class Sender : IEmailSender
    {
        public Task<ResultadoEnvioEmail> EnviarAsync(EmailMessage message, CancellationToken cancellationToken = default) => throw new Exception("Unexpected send");
    }

    private static IServiceCollection Services() => new ServiceCollection()
        .AddLogging()
        .AddSingleton((FirestoreDb)RuntimeHelpers.GetUninitializedObject(typeof(FirestoreDb)))
        .AddSingleton((MetaWhatsAppService)RuntimeHelpers.GetUninitializedObject(typeof(MetaWhatsAppService)))
        .AddSingleton<IEmailSender, Sender>();

    private static IConfiguration Config(string? enabled, string? origin, string? lease) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> {
            ["TurnoOffer:Enabled"] = enabled, ["TurnoOffer:Origin"] = origin, ["TurnoOffer:Lease"] = lease
        }).Build();

    [Theory]
    [InlineData(null, "https://example.com", "00:01:00")]
    [InlineData("false", "https://example.com", "00:01:00")]
    [InlineData("invalid", "https://example.com", "00:01:00")]
    [InlineData("true", "https://example.com", null)]
    [InlineData("true", "https://example.com", "invalid")]
    [InlineData("true", "https://example.com", "00:00:00")]
    [InlineData("true", "https://example.com", "-00:01:00")]
    public void InvalidOrDisabledConfigurationDoesNotRegister(string? enabled, string? origin, string? lease)
    {
        var services = Services();
        TurnoOfferHostRegistration.Add(services, Config(enabled, origin, lease), false);
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(TurnoOfferComposition));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IHostedService));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("http://example.com")]
    [InlineData("https://example.com/path")]
    public void MissingOrInvalidEmailOriginDoesNotDisableWhatsAppHosts(string? origin)
    {
        var services = Services();
        TurnoOfferHostRegistration.Add(services, Config("true", origin, "00:01:00"), false);
        using var provider = services.BuildServiceProvider();
        Assert.Equal(2, provider.GetServices<IHostedService>().Count());
    }

    [Fact]
    public void CompositionDoesNotResolveTransientSendersOnStartup()
    {
        var services = new ServiceCollection().AddLogging()
            .AddSingleton((FirestoreDb)RuntimeHelpers.GetUninitializedObject(typeof(FirestoreDb)))
            .AddTransient<MetaWhatsAppService>(_ => throw new Exception("Unexpected WhatsApp resolution"))
            .AddTransient<IEmailSender>(_ => throw new Exception("Unexpected email resolution"));
        TurnoOfferHostRegistration.Add(services, Config("true", "https://example.com", "00:01:00"), false);
        using var provider = services.BuildServiceProvider();
        Assert.Equal(2, provider.GetServices<IHostedService>().Count());
    }

    [Fact]
    public void ValidConfigurationRegistersBothCompositionHostsWithoutStartingOrSending()
    {
        var services = Services();
        TurnoOfferHostRegistration.Add(services, Config("true", "https://example.com:8443", "00:01:00"), false);
        using var provider = services.BuildServiceProvider();
        var composition = provider.GetRequiredService<TurnoOfferComposition>();
        var hosts = provider.GetServices<IHostedService>().ToArray();
        Assert.Equal(2, hosts.Length);
        Assert.Same(composition.OfferHost, hosts[0]);
        Assert.Same(composition.OutboxHost, hosts[1]);
        Assert.Null(composition.OfferHost.ExecuteTask);
        Assert.Null(composition.OutboxHost.ExecuteTask);
    }

    [Fact]
    public void EmulatorGuardPreservesExistingBackgroundServicesAndDoesNotRegisterOffers()
    {
        var services = Services();
        TurnoOfferHostRegistration.Add(services, Config("true", "https://example.com", "00:01:00"), true);
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IHostedService));
        TurnoHostRegistrations.AddBackgroundServices(services);
        using var provider = services.BuildServiceProvider();
        Assert.Contains(services, d => d.ImplementationType == typeof(WhatsAppSessionCleanupService));
        Assert.Contains(services, d => d.ImplementationType == typeof(TurnoNoShowService));
    }
}

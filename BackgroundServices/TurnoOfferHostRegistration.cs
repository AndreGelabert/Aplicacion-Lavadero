using System.Globalization;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Hosting;

namespace Firebase.BackgroundServices;

internal static class TurnoOfferHostRegistration
{
    // TurnoOffer:Enabled must be explicitly true; Origin and Lease have no defaults.
    internal static void Add(IServiceCollection services, IConfiguration configuration, bool localEmulators)
    {
        var section = configuration.GetSection("TurnoOffer");
        var origin = section["Origin"];
        if (localEmulators || !bool.TryParse(section["Enabled"], out var enabled) || !enabled ||
            !TimeSpan.TryParse(section["Lease"], CultureInfo.InvariantCulture, out var lease) || lease <= TimeSpan.Zero)
            return;

        services.AddSingleton(provider => TurnoOfferComposition.Create(
            provider.GetRequiredService<FirestoreDb>(), provider.GetRequiredService<IServiceScopeFactory>(),
            origin, lease, () => DateTime.UtcNow,
            provider.GetRequiredService<ILoggerFactory>()));
        services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<TurnoOfferComposition>().OfferHost);
        services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<TurnoOfferComposition>().OutboxHost);
    }

}

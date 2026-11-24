using System.Runtime.CompilerServices;
using Firebase.BackgroundServices;
using Firebase.Models;
using Firebase.Services;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Firebase.Tests.Services;

public class TurnoOfferCompositionTests
{
    private static readonly FirestoreDb Db = (FirestoreDb)RuntimeHelpers.GetUninitializedObject(typeof(FirestoreDb));
    private static readonly MetaWhatsAppService WhatsApp = (MetaWhatsAppService)RuntimeHelpers.GetUninitializedObject(typeof(MetaWhatsAppService));
    private static readonly Func<DateTime> Clock = () => new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private sealed class Sender : IEmailSender
    {
        internal int Calls;
        public Task<ResultadoEnvioEmail> EnviarAsync(EmailMessage message, CancellationToken cancellationToken = default)
        { Calls++; return Task.FromResult(new ResultadoEnvioEmail(true, "ok")); }
    }

    [Fact]
    public void CompositionRequiresExplicitValidInputsAndDoesNotSend()
    {
        var sender = new Sender();
        Assert.Throws<ArgumentNullException>(() => TurnoOfferComposition.Create(null!, WhatsApp, sender, "https://example.com", TimeSpan.FromMinutes(1), Clock, NullLoggerFactory.Instance));
        Assert.Throws<ArgumentNullException>(() => TurnoOfferComposition.Create(Db, null!, sender, "https://example.com", TimeSpan.FromMinutes(1), Clock, NullLoggerFactory.Instance));
        Assert.Throws<ArgumentNullException>(() => TurnoOfferComposition.Create(Db, WhatsApp, null!, "https://example.com", TimeSpan.FromMinutes(1), Clock, NullLoggerFactory.Instance));
        Assert.Throws<ArgumentNullException>(() => TurnoOfferComposition.Create(Db, WhatsApp, sender, "https://example.com", TimeSpan.FromMinutes(1), null!, NullLoggerFactory.Instance));
        Assert.Throws<ArgumentOutOfRangeException>(() => TurnoOfferComposition.Create(Db, WhatsApp, sender, "https://example.com", TimeSpan.Zero, Clock, NullLoggerFactory.Instance));
        foreach (var origin in new string?[] { null, "http://example.com", "https://example.com/path", "https://u:p@example.com" })
            Assert.Throws<ArgumentException>(() => TurnoOfferComposition.Create(Db, WhatsApp, sender, origin!, TimeSpan.FromMinutes(1), Clock, NullLoggerFactory.Instance));
        Assert.Throws<ArgumentException>(() => TurnoOfferComposition.Create(Db, WhatsApp, sender, "https://example.com", TimeSpan.FromMinutes(1), () => DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified), NullLoggerFactory.Instance));
        var result = TurnoOfferComposition.Create(Db, WhatsApp, sender, "https://example.com", TimeSpan.FromMinutes(1), Clock, NullLoggerFactory.Instance);
        Assert.NotNull(result.OfferHost);
        Assert.NotNull(result.OutboxHost);
        Assert.Equal(0, sender.Calls);
    }

    [Fact]
    public async Task ChannelDispatchResolvesOnlyItsOwnSenderInFreshScopes()
    {
        var waCount = 0;
        var emailCount = 0;
        var services = new ServiceCollection()
            .AddScoped<MetaWhatsAppService>(_ => { waCount++; return WhatsApp; })
            .AddScoped<IEmailSender>(_ => { emailCount++; return new Sender(); });
        using var provider = services.BuildServiceProvider();
        var scopes = provider.GetRequiredService<IServiceScopeFactory>();
        for (var i = 0; i < 2; i++)
            await Assert.ThrowsAsync<InvalidOperationException>(() => TurnoOfferComposition.Route(scopes, Db, null, TimeSpan.FromMinutes(1), Clock, "id", "Email", default));
        Assert.Equal(0, waCount);
        Assert.Equal(2, emailCount);

        var whatsappOnly = new ServiceCollection()
            .AddScoped<MetaWhatsAppService>(_ => { waCount++; return WhatsApp; })
            .AddScoped<IEmailSender>(_ => throw new Exception("Email must not resolve"));
        using var whatsappProvider = whatsappOnly.BuildServiceProvider();
        for (var i = 0; i < 2; i++)
        {
            var error = await Record.ExceptionAsync(() => TurnoOfferComposition.Route(whatsappProvider.GetRequiredService<IServiceScopeFactory>(), Db, null, TimeSpan.FromMinutes(1), Clock, "id", "WhatsApp", default));
            Assert.NotNull(error); // Inert Firestore cannot execute a claim.
            Assert.DoesNotContain("Email must not resolve", error.ToString());
        }
        Assert.Equal(2, waCount);

        var emailOnly = new ServiceCollection()
            .AddScoped<MetaWhatsAppService>(_ => throw new Exception("WhatsApp must not resolve"))
            .AddScoped<IEmailSender>(_ => new Sender());
        using var emailProvider = emailOnly.BuildServiceProvider();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => TurnoOfferComposition.Route(emailProvider.GetRequiredService<IServiceScopeFactory>(), Db, null, TimeSpan.FromMinutes(1), Clock, "id", "Email", default));
        Assert.Equal("Invalid trusted HTTPS origin for email offer.", ex.Message);
    }

    [Fact]
    public async Task OutboxUsesInjectedClockAndStopsOnCancellation()
    {
        using var stop = new CancellationTokenSource();
        var routed = new List<string>();
        var host = new TurnoOutboxHost((size, cursor, token) => Task.FromResult(new TurnoOutboxHost.Page(new[] {
            new TurnoOutboxHost.Item("expired", "Offer", "Claimed", "Email", Clock().AddSeconds(-1)),
            new TurnoOutboxHost.Item("future", "Offer", "Claimed", "Email", Clock().AddSeconds(1)) }, null)),
            (id, channel, token) => { routed.Add(id); return Task.CompletedTask; },
            (delay, token) => { stop.Cancel(); return Task.CompletedTask; }, NullLogger<TurnoOutboxHost>.Instance, Clock);
        await host.RunAsync(stop.Token);
        Assert.Equal(new[] { "expired" }, routed);
    }

    [Fact]
    public async Task PrecancelledWhatsAppDoesNotClaim()
    {
        var claims = 0;
        var dispatcher = new TurnoWhatsAppOutboxDispatcher((id, token, now, duration) => { claims++; return Task.FromResult<TurnoOutboxPersistence.BoundClaim?>(null); },
            (id, token, binding, now) => Task.FromResult<TurnoWhatsAppOutboxDispatcher.Delivery?>(null),
            (id, token, binding, now) => Task.FromResult(true), (id, token, generation) => Task.FromResult(true),
            (recipient, message) => Task.FromResult(true), Clock, TimeSpan.FromMinutes(1));
        using var stop = new CancellationTokenSource(); stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dispatcher.Dispatch("id", "token", stop.Token));
        Assert.Equal(0, claims);
    }
}

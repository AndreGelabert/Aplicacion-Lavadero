using Firebase.BackgroundServices;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Firebase.Models;
using Moq;

namespace Firebase.Tests.Integration;

public class TurnoNoShowHostRegistrationTests
{
    [LocalFirestoreFact]
    public async Task ActualRegistrationsGateBookingOnRunningHostedTask()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-host-di-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("configuracion").Document("system_config").SetAsync(new { ToleranciaLlegadaTurnosMinutos = 15 });
        using var host = new HostBuilder().ConfigureServices(services =>
        {
            services.AddSingleton(db);
            services.AddScoped<TurnoService>();
            services.AddScoped<TurnoNoShowSweep>();
            services.AddScoped(provider => new TurnoWhatsAppBookingAdapter(provider.GetRequiredService<TurnoService>()));
            services.AddSingleton(new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance));
            services.AddSingleton(new ClienteService(db));
            services.AddSingleton(new VehiculoService(db));
            services.AddSingleton(new TipoDocumentoService(db));
            services.AddSingleton(new TipoVehiculoService(db));
            services.AddSingleton(new LavaderoInfoService(null!, null!, NullLogger<LavaderoInfoService>.Instance));
            services.AddSingleton(Mock.Of<ICarQueryService>());
            services.AddSingleton(new MetaWhatsAppService(new HttpClient(new NoNetworkHandler()), Options.Create(new WhatsAppSettings { VerifyToken = "synthetic", PhoneNumberId = "synthetic", AccessToken = "synthetic" }), NullLogger<MetaWhatsAppService>.Instance));
            TurnoHostRegistrations.AddWhatsAppFlow(services);
            TurnoHostRegistrations.AddBackgroundServices(services);
        }).Build();
        using var scope = host.Services.CreateScope();
        var flow = scope.ServiceProvider.GetRequiredService<WhatsAppFlowService>();
        var scheduler = Assert.Single(host.Services.GetServices<IHostedService>().OfType<TurnoNoShowService>());
        Assert.False(flow.BookingSchedulerEnabled());
        await host.StartAsync();
        try
        {
            Assert.NotNull(scheduler.ExecuteTask);
            Assert.False(scheduler.ExecuteTask!.IsCompleted);
            Assert.True(flow.BookingSchedulerEnabled());
        }
        finally { await host.StopAsync(); }
        Assert.True(scheduler.ExecuteTask!.IsCompleted);
        Assert.False(flow.BookingSchedulerEnabled());
    }

    private sealed class NoNetworkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Network access is forbidden in this test.");
    }
}

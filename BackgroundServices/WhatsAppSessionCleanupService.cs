using Firebase.Services;

namespace Firebase.BackgroundServices;

/// <summary>
/// Servicio en segundo plano que limpia sesiones inactivas de WhatsApp periódicamente
/// </summary>
public class WhatsAppSessionCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WhatsAppSessionCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(15); // Ejecutar cada 15 minutos

    public WhatsAppSessionCleanupService(
        IServiceProvider serviceProvider,
        ILogger<WhatsAppSessionCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🧹 Servicio de limpieza de sesiones WhatsApp iniciado");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_cleanupInterval, stoppingToken);

                using var scope = _serviceProvider.CreateScope();
                var sessionService = scope.ServiceProvider.GetRequiredService<WhatsAppSessionService>();

                _logger.LogInformation("🧹 Ejecutando limpieza de sesiones inactivas...");
                await sessionService.CleanupInactiveSessions();
                _logger.LogInformation("✅ Limpieza de sesiones completada");
            }
            catch (OperationCanceledException)
            {
                // Servicio detenido intencionalmente
                _logger.LogInformation("🛑 Servicio de limpieza detenido");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error durante la limpieza de sesiones");
                // Continuar ejecutando a pesar del error
            }
        }
    }
}

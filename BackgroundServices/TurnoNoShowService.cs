using Firebase.Services;

namespace Firebase.BackgroundServices;

public sealed class TurnoNoShowService(IServiceProvider services, ILogger<TurnoNoShowService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string? cursor = null;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                cursor = await scope.ServiceProvider.GetRequiredService<TurnoNoShowSweep>()
                    .RunBatchAsync(() => DateTime.UtcNow, 100, cursor, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Error al buscar turnos ausentes"); }
            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}

using Firebase.Services;

namespace Firebase.BackgroundServices;

// Isolated until explicitly registered and its scoped dependencies are configured.
internal sealed class TurnoOfertaHost : BackgroundService
{
    private const int PageSize = 100;
    private static readonly TimeSpan Backoff = TimeSpan.FromMinutes(1);
    private readonly Func<int, string?, CancellationToken, Task<string?>> runBatch;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;
    private readonly ILogger<TurnoOfertaHost> logger;

    internal TurnoOfertaHost(IServiceProvider services, ILogger<TurnoOfertaHost> logger)
        : this(async (size, cursor, token) =>
        {
            using var scope = services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<TurnoOfertaSweep>()
                .RunBatchAsync(size, cursor, token);
        }, Task.Delay, logger) { }

    internal TurnoOfertaHost(Func<int, string?, CancellationToken, Task<string?>> runBatch,
        Func<TimeSpan, CancellationToken, Task> delay, ILogger<TurnoOfertaHost> logger)
    {
        this.runBatch = runBatch;
        this.delay = delay;
        this.logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => RunAsync(stoppingToken);

    internal async Task RunAsync(CancellationToken stoppingToken)
    {
        string? cursor = null;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                cursor = await runBatch(PageSize, cursor, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (TurnoOfertaSweep.BatchFailedException ex)
            {
                // Pending failures remain discoverable on the next full scan or restart.
                cursor = ex.Cursor;
                logger.LogError(ex, "No se pudieron continuar algunas cascadas de ofertas");
            }
            catch (Exception ex) { logger.LogError(ex, "Error al continuar cascadas de ofertas"); }

            try { await delay(Backoff, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}

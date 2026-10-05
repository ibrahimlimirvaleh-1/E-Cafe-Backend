using ECafe.Infrastructure.Services;

namespace ECafe.Api.BackgroundServices;

public sealed class MobilePushWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<MobilePushWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, configuration.GetValue("MobileApp:WorkerIntervalSeconds", 15)));
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await ProcessAsync(stoppingToken);
                await timer.WaitForNextTickAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<MobilePushDeliveryProcessor>();
            var batchSize = Math.Clamp(configuration.GetValue("MobileApp:WorkerBatchSize", 50), 1, 100);
            await processor.ExpandOutboxAsync(batchSize, cancellationToken);
            await processor.SendPendingAsync(batchSize, cancellationToken);
            await processor.CheckReceiptsAsync(batchSize, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError("Mobile push processing failed: {ErrorType}", ex.GetType().Name);
        }
    }
}

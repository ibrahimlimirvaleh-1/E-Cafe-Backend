using ECafe.Infrastructure.Services.Realtime;

namespace ECafe.Api.BackgroundServices;

public sealed class RealtimeOutboxWorker(IServiceScopeFactory scopes, ILogger<RealtimeOutboxWorker> logger) : BackgroundService
{
    /// <summary>Yeni scope ilə saniyəlik etibarlı invalidation çatdırır; dayanmada transaction ləğv edilir və siqnallar bazada qalır.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            do
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<RealtimeOutboxProcessor>().ProcessAsync(25, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception error) { logger.LogError(error, "Realtime outbox processing failed."); }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}

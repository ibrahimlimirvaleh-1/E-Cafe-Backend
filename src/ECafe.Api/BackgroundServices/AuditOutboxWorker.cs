using ECafe.Application.Services.AuditLog.Abstract;

namespace ECafe.Api.BackgroundServices
{
    public sealed class AuditOutboxWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AuditOutboxWorker> _logger;
        private readonly TimeSpan _interval;
        private readonly int _batchSize;

        public AuditOutboxWorker(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<AuditOutboxWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _interval = TimeSpan.FromSeconds(
                configuration.GetValue("AuditOutbox:IntervalSeconds", 10));
            _batchSize = configuration.GetValue("AuditOutbox:BatchSize", 50);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var timer = new PeriodicTimer(_interval);

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

        private async Task ProcessAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IAuditOutboxProcessor>();
                var processedCount = await processor.ProcessPendingAsync(_batchSize, stoppingToken);

                if (processedCount > 0)
                    _logger.LogInformation("Processed {ProcessedCount} audit outbox event(s).", processedCount);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audit outbox processing failed.");
            }
        }
    }
}

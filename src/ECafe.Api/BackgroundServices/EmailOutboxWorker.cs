using ECafe.Application.Services;

namespace ECafe.Api.BackgroundServices
{
    public sealed class EmailOutboxWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EmailOutboxWorker> _logger;
        private readonly TimeSpan _interval;
        private readonly int _batchSize;

        public EmailOutboxWorker(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<EmailOutboxWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _interval = TimeSpan.FromSeconds(
                configuration.GetValue("EmailOutbox:IntervalSeconds", 15));
            _batchSize = configuration.GetValue("EmailOutbox:BatchSize", 50);
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
                var processor = scope.ServiceProvider.GetRequiredService<IEmailOutboxProcessor>();
                var processedCount = await processor.ProcessPendingAsync(_batchSize, stoppingToken);

                if (processedCount > 0)
                    _logger.LogInformation("Processed {ProcessedCount} email outbox event(s).", processedCount);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email outbox processing failed.");
            }
        }
    }
}

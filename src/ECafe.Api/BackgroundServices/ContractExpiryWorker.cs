using ECafe.Application.Services.RestaurantContract.Abstract;

namespace ECafe.Api.BackgroundServices
{
    public sealed class ContractExpiryWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ContractExpiryWorker> _logger;
        private readonly TimeSpan _interval;
        private readonly int _batchSize;

        public ContractExpiryWorker(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<ContractExpiryWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _interval = TimeSpan.FromMinutes(
                configuration.GetValue("ContractExpiry:IntervalMinutes", 60));
            _batchSize = configuration.GetValue("ContractExpiry:BatchSize", 100);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await ProcessAsync(stoppingToken);

                using var timer = new PeriodicTimer(_interval);
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await ProcessAsync(stoppingToken);
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
                var contractService = scope.ServiceProvider.GetRequiredService<IRestaurantContractService>();
                var reminderCount = await contractService.SendExpiryRemindersAsync(_batchSize);
                var expiredCount = await contractService.ExpireActiveContractsAsync(_batchSize);
                var activatedCount = await contractService.ActivateDueScheduledContractsAsync(_batchSize);

                if (reminderCount > 0)
                    _logger.LogInformation("Sent expiry reminder for {ReminderCount} restaurant contract(s).", reminderCount);

                if (expiredCount > 0)
                    _logger.LogInformation("Expired {ExpiredCount} restaurant contract(s).", expiredCount);

                if (activatedCount > 0)
                    _logger.LogInformation("Activated {ActivatedCount} scheduled restaurant contract(s).", activatedCount);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Contract status processing failed.");
            }
        }
    }
}

using ECafe.Application.Repositories.ReservationPaymentInstruction;
using ECafe.Application.Services.PaymentInstructionDetails.Abstract;

namespace ECafe.Api.BackgroundServices;

public sealed class PaymentInstructionEncryptionBackfillWorker : BackgroundService
{
    private const int BatchSize = 100;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PaymentInstructionEncryptionBackfillWorker> _logger;

    public PaymentInstructionEncryptionBackfillWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<PaymentInstructionEncryptionBackfillWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EncryptLegacyInstructionsAsync(stoppingToken);

        using var timer = new PeriodicTimer(Interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await EncryptLegacyInstructionsAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Payment instruction encryption backfill stopped.");
        }
    }

    private async Task EncryptLegacyInstructionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var encryptedCount = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider
                    .GetRequiredService<IReservationPaymentInstructionRepository>();
                var protector = scope.ServiceProvider
                    .GetRequiredService<IPaymentInstructionDetailsProtector>();

                var instructions = await repository.GetLegacyUnencryptedAsync(
                    BatchSize,
                    cancellationToken);

                if (instructions.Count == 0)
                    break;

                foreach (var instruction in instructions)
                {
                    var details = instruction.LegacyDisplayText!;
                    instruction.EncryptedDetails = protector.Protect(details);
                    instruction.MaskedDetails = protector.CreateMaskedDetails(details);
                    instruction.LegacyDisplayText = null;
                }

                await repository.SaveChangesAsync();
                encryptedCount += instructions.Count;
            }

            if (encryptedCount > 0)
            {
                _logger.LogInformation(
                    "Encrypted {Count} legacy reservation payment instruction(s).",
                    encryptedCount);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to encrypt legacy reservation payment instructions.");
        }
    }
}

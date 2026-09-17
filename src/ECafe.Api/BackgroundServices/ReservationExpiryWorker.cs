using ECafe.Application.Services.Reservation.Abstract;

namespace ECafe.Api.BackgroundServices;

public sealed class ReservationExpiryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReservationExpiryWorker> _logger;

    private const int BatchSize = 100;
    private static readonly TimeSpan Interval =
        TimeSpan.FromMinutes(1);

    public ReservationExpiryWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ReservationExpiryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Reservation expiry worker started.");

        using var timer = new PeriodicTimer(Interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ExpireReservationsAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Reservation expiry worker cancellation requested.");
        }

        _logger.LogInformation(
            "Reservation expiry worker stopped.");
    }

    private async Task ExpireReservationsAsync(
        CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();

            var reservationService = scope.ServiceProvider
                .GetRequiredService<IReservationService>();

            var expiredCount = await reservationService
                .ExpirePendingReservationsAsync(
                    BatchSize,
                    stoppingToken);

            if (expiredCount > 0)
            {
                _logger.LogInformation(
                    "{Count} reservation(s) expired.",
                    expiredCount);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Error while expiring reservations.");
        }
    }
}

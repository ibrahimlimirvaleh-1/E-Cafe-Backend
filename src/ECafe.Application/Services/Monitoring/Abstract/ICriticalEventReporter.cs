namespace ECafe.Application.Services.Monitoring.Abstract;

public interface ICriticalEventReporter
{
    Task CaptureAsync(CriticalEvent criticalEvent, CancellationToken cancellationToken = default);
}

using ECafe.Application.Services.Monitoring.Abstract;

namespace ECafe.Application.Services.Monitoring.Concrete;

public sealed class NoopCriticalEventReporter : ICriticalEventReporter
{
    public Task CaptureAsync(CriticalEvent criticalEvent, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

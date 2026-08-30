using ECafe.Application.Services.Monitoring.Abstract;
using Sentry;

namespace ECafe.Api.Monitoring;

public sealed class SentryCriticalEventReporter : ICriticalEventReporter
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<SentryCriticalEventReporter> _logger;

    public SentryCriticalEventReporter(
        IHttpContextAccessor httpContextAccessor,
        ILogger<SentryCriticalEventReporter> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public Task CaptureAsync(CriticalEvent criticalEvent, CancellationToken cancellationToken = default)
    {
        if (criticalEvent is null)
            return Task.CompletedTask;

        try
        {
            var context = _httpContextAccessor.HttpContext;

            using (SentrySdk.PushScope())
            {
                SentrySdk.ConfigureScope(scope =>
                {
                    scope.SetTag("category", NormalizeTag(criticalEvent.Category));
                    scope.SetTag("event", NormalizeTag(criticalEvent.Name));
                    scope.SetTag("severity", criticalEvent.Severity.ToString().ToLowerInvariant());

                    if (context is not null)
                    {
                        scope.SetTag("traceId", context.TraceIdentifier);
                        scope.SetTag("path", context.Request.Path);
                        scope.SetTag("method", context.Request.Method);
                    }

                    if (criticalEvent.Properties is null)
                        return;

                    foreach (var (key, value) in criticalEvent.Properties)
                    {
                        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                            continue;

                        scope.SetExtra(key, value);
                    }
                });

                if (criticalEvent.Exception is not null)
                {
                    SentrySdk.CaptureException(criticalEvent.Exception);
                    return Task.CompletedTask;
                }

                SentrySdk.CaptureMessage(
                    $"{criticalEvent.Category}.{criticalEvent.Name}",
                    ToSentryLevel(criticalEvent.Severity));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Critical event could not be reported to Sentry. Event: {Event}", criticalEvent.Name);
        }

        return Task.CompletedTask;
    }

    private static SentryLevel ToSentryLevel(CriticalEventSeverity severity)
        => severity switch
        {
            CriticalEventSeverity.Critical => SentryLevel.Fatal,
            CriticalEventSeverity.Error => SentryLevel.Error,
            CriticalEventSeverity.Warning => SentryLevel.Warning,
            _ => SentryLevel.Info
        };

    private static string NormalizeTag(string value)
        => value.Trim().Replace(' ', '_').ToLowerInvariant();
}

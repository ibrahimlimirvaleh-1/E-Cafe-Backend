namespace ECafe.Application.Services.Monitoring.Abstract;

public sealed record CriticalEvent(
    string Category,
    string Name,
    CriticalEventSeverity Severity,
    IReadOnlyDictionary<string, string?>? Properties = null,
    Exception? Exception = null);

namespace ECafe.Api.Realtime.Connections;

public sealed class RealtimeConnectionOptions
{
    public const string SectionName = "SignalR:ConnectionLimits";
    public int MaxConnectionsPerUser { get; set; } = 5;
    public int LeaseSeconds { get; set; } = 90;
    public int RenewalSeconds { get; set; } = 20;
}

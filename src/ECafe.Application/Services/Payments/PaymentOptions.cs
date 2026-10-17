namespace ECafe.Application.Services.Payments;

public sealed record PaymentEnvironment(string Name);

public sealed class PaymentOptions
{
    public const string SectionName = "Payments";
    public string Mode { get; set; } = "Disabled";
    public bool EnableSimulation { get; set; }
}

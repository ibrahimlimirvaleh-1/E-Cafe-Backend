namespace ECafe.Application.Services.Payments;

public sealed record PaymentEnvironment(string Name);

public sealed class PaymentOptions
{
    public const string SectionName = "Payments";
    public string Mode { get; set; } = "Simulation";
    public bool EnableSimulation { get; set; } = true;
}

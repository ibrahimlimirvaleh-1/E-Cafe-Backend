namespace ECafe.Application.Services.Reservation.Concrete;

public sealed class ReservationTimingOptions
{
    public const string SectionName = "ReservationTiming";
    public int MaximumLateArrivalMinutes { get; set; } = 45;
    public int ArrivalDecisionMinutes { get; set; } = 3;
    public int ArrivalChoiceStepMinutes { get; set; } = 5;
    public int CancellationGraceMinutes { get; set; } = 10;
}

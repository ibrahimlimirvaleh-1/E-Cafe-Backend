namespace ECafe.Application.Services.TableSessions.Concrete;

public sealed class TableSessionReviewOptions
{
    public const string SectionName = "TableSessions:CloseReview";
    public int MaxContinuationHours { get; set; } = 24;
    public int MaxReasonLength { get; set; } = 500;
}

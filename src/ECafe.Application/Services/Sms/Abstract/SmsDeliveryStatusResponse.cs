namespace ECafe.Application.Services.Sms.Abstract;

public sealed class SmsDeliveryStatusResponse
{
    public string MessageId { get; set; } = null!;

    public int StatusCode { get; set; }

    public string StatusText { get; set; } = null!;

    public DateTime? Date { get; set; }

    public bool IsFinal { get; set; }

    public bool IsDelivered { get; set; }
}

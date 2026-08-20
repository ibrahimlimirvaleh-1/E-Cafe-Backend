namespace ECafe.Application.Services.Sms.Abstract;

public sealed class SmsBalanceResponse
{
    public decimal Balance { get; set; }

    public SmsApiPermissionsResponse Apis { get; set; } = new();

    public bool TestMode { get; set; }

    public string? TestModePhone { get; set; }

    public SmsApiPermissionsResponse Available { get; set; } = new();

    public bool LimitedSms { get; set; }
}

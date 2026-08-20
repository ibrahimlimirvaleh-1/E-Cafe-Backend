namespace ECafe.Application.Services.Sms.Abstract;

public sealed class SmsApiPermissionsResponse
{
    public bool Otp { get; set; }

    public bool Bulk { get; set; }

    public bool Advertising { get; set; }
}

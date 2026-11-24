namespace ECafe.Api.Security;

public static class RateLimitPolicyNames
{
    public const string AuthLogin = "auth-login";
    public const string AuthRefresh = "auth-refresh";
    public const string AuthPasswordReset = "auth-password-reset";
    public const string EmailChangeRequest = "email-change-request";
    public const string EmailChangeConfirm = "email-change-confirm";
    public const string CustomerAccountDeletion = "customer-account-deletion";
    public const string ProductAnnouncements = "product-announcements";
    public const string PaymentMutation = "payment-mutation";
    public const string PaymentRefresh = "payment-refresh";
    public const string PaymentRead = "payment-read";
    public const string FileUpload = "file-upload";
    public const string FileDownload = "file-download";
    public const string MobileRelease = "mobile-release";
    public const string PublicRead = "public-read";
}

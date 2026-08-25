namespace ECafe.Api.Security;

public static class RateLimitPolicyNames
{
    public const string AuthLogin = "auth-login";
    public const string AuthRefresh = "auth-refresh";
    public const string AuthPasswordReset = "auth-password-reset";
    public const string FileUpload = "file-upload";
    public const string FileDownload = "file-download";
    public const string PublicRead = "public-read";
}

namespace ECafe.Application.Common.Configuration;

public static class AuthenticationConfigurationKeys
{
    public const string FrontendBaseUrl = "Frontend:BaseUrl";

    public static class Jwt
    {
        public const string Key = "Jwt:Key";
        public const string Issuer = "Jwt:Issuer";
        public const string Audience = "Jwt:Audience";
        public const string AccessTokenLifetimeMinutes = "Jwt:AccessTokenLifetimeMinutes";
    }
}

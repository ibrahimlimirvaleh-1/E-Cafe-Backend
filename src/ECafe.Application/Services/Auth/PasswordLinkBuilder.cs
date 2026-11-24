using Microsoft.Extensions.Configuration;


namespace ECafe.Application.Services.Auth;

public static class PasswordLinkBuilder
{
    /// <summary>Bərpa və ilkin parol linkində mövcud frontend/fallback ünvanını saxlayır; tokeni HTTP query-sinə deyil fragment-ə yazır.</summary>
    public static string Build(IConfiguration configuration, string path, string token)
    {
        var frontendBaseUrl = configuration["Frontend:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(frontendBaseUrl))
            frontendBaseUrl = "http://localhost:5173";

        return $"{frontendBaseUrl}{path}#token={Uri.EscapeDataString(token)}";
    }
}

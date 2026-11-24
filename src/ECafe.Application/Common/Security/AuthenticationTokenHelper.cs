using System.Security.Cryptography;
using System.Text;

namespace ECafe.Application.Common.Security;

public static class AuthenticationTokenHelper
{
    /// <summary>Parol bərpası və ilkin təyini üçün 64 təsadüfi baytdan əvvəlki formatda URL-safe token yaradır.</summary>
    public static string GenerateUrlSafeToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", string.Empty);

    /// <summary>Tokenin UTF-8 mətnini SHA-256 ilə hash-ləyir; bazadakı mövcud böyük hərfli hex formatını saxlayır.</summary>
    public static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>İlkin parol təyin edilməmiş işçi hesabına məlum olmayan təsadüfi dəyərin BCrypt hash-ini verir.</summary>
    public static string CreateUnusablePasswordHash()
        => BCrypt.Net.BCrypt.HashPassword(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
}

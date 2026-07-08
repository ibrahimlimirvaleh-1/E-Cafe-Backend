namespace ECafe.Application.DTOs.Auth
{
    public class AuthTokenMapData
    {
        public string AccessToken { get; set; } = null!;

        public string RefreshToken { get; set; } = null!;
    }
}

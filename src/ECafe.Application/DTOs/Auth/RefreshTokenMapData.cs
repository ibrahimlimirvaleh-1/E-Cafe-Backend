namespace ECafe.Application.DTOs.Auth
{
    public class RefreshTokenMapData
    {
        public int UserId { get; set; }

        public string TokenHash { get; set; } = null!;

        public string SessionId { get; set; } = null!;

        public DateTime ExpiresAt { get; set; }

        public string? CreatedByIp { get; set; }

        public string? UserAgent { get; set; }
    }
}

namespace ECafe.Application.DTOs.Auth;

public sealed class UserSessionResponseDto
{
    public string SessionId { get; set; } = null!;

    public string Device { get; set; } = null!;

    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime LastSeenAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public bool IsCurrent { get; set; }
}

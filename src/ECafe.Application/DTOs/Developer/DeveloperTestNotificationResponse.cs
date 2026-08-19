namespace ECafe.Application.DTOs.Developer;

public sealed class DeveloperTestNotificationResponse
{
    public bool Sent { get; set; }

    public string Channel { get; set; } = null!;

    public string Recipient { get; set; } = null!;

    public DateTime SentAt { get; set; }
}

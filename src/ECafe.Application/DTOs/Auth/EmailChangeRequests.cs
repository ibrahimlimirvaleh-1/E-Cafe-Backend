namespace ECafe.Application.DTOs.Auth;

public sealed class RequestEmailChangeRequest
{
    public string NewEmail { get; set; } = null!;
    public string CurrentPassword { get; set; } = null!;
}

public sealed class ConfirmEmailChangeRequest
{
    public string Token { get; set; } = null!;
}

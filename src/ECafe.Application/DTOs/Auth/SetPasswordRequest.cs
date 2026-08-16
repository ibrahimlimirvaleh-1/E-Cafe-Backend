namespace ECafe.Application.DTOs.Auth
{
    public class SetPasswordRequest
    {
        public string Token { get; set; } = null!;

        public string Password { get; set; } = null!;

        public string ConfirmPassword { get; set; } = null!;
    }
}

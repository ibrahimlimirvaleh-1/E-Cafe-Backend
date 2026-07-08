using ECafe.Application.DTOs.Auth;

namespace ECafe.Application.Services.Auth.Abstract
{
    public interface IAuthService
    {
        public Task<AuthResponseDto> LoginAsync(LoginRequestDto request);

        public Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request);

        public Task<AuthResponseDto> RefreshAsync(RefreshTokenRequestDto request);

    }
}

using ECafe.Application.DTOs.Auth;

namespace ECafe.Application.Services.Auth.Abstract;

public interface IUserSessionService
{
    Task<List<UserSessionResponseDto>> GetMySessionsAsync();

    Task RevokeMySessionAsync(string sessionId);
}

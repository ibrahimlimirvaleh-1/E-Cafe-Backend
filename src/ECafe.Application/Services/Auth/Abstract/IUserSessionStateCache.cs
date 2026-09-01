namespace ECafe.Application.Services.Auth.Abstract;

public sealed record UserSessionState(bool IsActive, int SessionVersion);

public interface IUserSessionStateCache
{
    Task<UserSessionState?> GetAsync(int userId);

    Task<bool> IsSessionActiveAsync(int userId, string sessionId);

    Task InvalidateAsync(int userId);

    Task InvalidateSessionAsync(int userId, string sessionId);
}

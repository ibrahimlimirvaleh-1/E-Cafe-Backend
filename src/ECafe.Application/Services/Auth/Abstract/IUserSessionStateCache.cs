namespace ECafe.Application.Services.Auth.Abstract;

public sealed record UserSessionState(bool IsActive, int SessionVersion);

public interface IUserSessionStateCache
{
    Task<UserSessionState?> GetAsync(int userId);

    Task InvalidateAsync(int userId);
}

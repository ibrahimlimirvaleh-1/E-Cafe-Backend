namespace ECafe.Application.Services.Auth.Abstract;

public sealed record UserSessionState(bool IsActive, int SessionVersion);

/// <summary>Adı çağırış uyğunluğu üçün saxlanır; təhlükəsizlik oxunuşları cari bazaya əsaslanır, invalidation yalnız köhnə açarların təmizliyidir.</summary>
public interface IUserSessionStateCache
{
    Task<UserSessionState?> GetAsync(int userId);

    Task<bool> IsSessionActiveAsync(int userId, string sessionId);

    Task InvalidateAsync(int userId);

    Task InvalidateSessionAsync(int userId, string sessionId);
}

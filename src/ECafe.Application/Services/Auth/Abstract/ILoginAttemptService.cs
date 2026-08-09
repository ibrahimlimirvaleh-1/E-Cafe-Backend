namespace ECafe.Application.Services.Auth.Abstract;

public interface ILoginAttemptService
{
    Task EnsureNotLockedOutAsync(string email);
    Task RecordFailureAsync(Domain.Entities.User? user, string email, string failureReason);
    Task RecordSuccessAsync(Domain.Entities.User user, string email);
}

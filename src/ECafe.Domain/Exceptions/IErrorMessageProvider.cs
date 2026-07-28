namespace ECafe.Domain.Exceptions;

public interface IErrorMessageProvider
{
    string GetMessage(BaseException exception);
    string GetMessage(ErrorCode code, object? parameters = null, string? fallbackMessage = null);
}

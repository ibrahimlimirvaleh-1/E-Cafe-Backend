using System.Net;

namespace ECafe.Domain.Exceptions;

public sealed class UnauthorizedException : BaseException
{
    public UnauthorizedException(ErrorCode code, object? parameters = null)
        : base(code, (int)HttpStatusCode.Unauthorized, parameters)
    {
    }

    public UnauthorizedException(string message)
        : base(message, (int)HttpStatusCode.Unauthorized, ErrorCode.InvalidCredentials)
    {
    }
}

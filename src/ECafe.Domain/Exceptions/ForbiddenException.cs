using System.Net;
using ECafe.Domain.Exceptions;

namespace ECafe.Application.Common.Exceptions;

public sealed class ForbiddenException : BaseException
{
    public ForbiddenException(string message)
        : base(message, (int)HttpStatusCode.Forbidden, ErrorCode.Forbidden)
    {
    }

    public ForbiddenException(ErrorCode code, object? parameters = null)
        : base(code, (int)HttpStatusCode.Forbidden, parameters)
    {
    }
}

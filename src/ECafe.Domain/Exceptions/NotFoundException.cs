using System.Net;
using ECafe.Domain.Exceptions;

namespace ECafe.Application.Common.Exceptions;

public sealed class NotFoundException : BaseException
{
    public NotFoundException(string message)
        : base(message, (int)HttpStatusCode.NotFound, ErrorCode.NotFound)
    {
    }

    public NotFoundException(ErrorCode code, object? parameters = null)
        : base(code, (int)HttpStatusCode.NotFound, parameters)
    {
    }
}

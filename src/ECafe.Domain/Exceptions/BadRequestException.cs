using System.Net;
using ECafe.Domain.Exceptions;

namespace ECafe.Application.Common.Exceptions;

public sealed class BadRequestException : BaseException
{
    public BadRequestException(string message)
        : base(message, (int)HttpStatusCode.BadRequest, ErrorCode.BadRequest)
    {
    }

    public BadRequestException(ErrorCode code, object? parameters = null)
        : base(code, (int)HttpStatusCode.BadRequest, parameters)
    {
    }
}

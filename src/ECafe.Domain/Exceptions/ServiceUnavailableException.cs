using System.Net;

namespace ECafe.Domain.Exceptions;

public sealed class ServiceUnavailableException : BaseException
{
    public ServiceUnavailableException(string message)
        : base(message, (int)HttpStatusCode.ServiceUnavailable, ErrorCode.InternalServerError)
    {
    }

    public ServiceUnavailableException(ErrorCode code, object? parameters = null)
        : base(code, (int)HttpStatusCode.ServiceUnavailable, parameters)
    {
    }
}

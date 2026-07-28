using System.Net;

namespace ECafe.Domain.Exceptions;

public sealed class BusinessRuleException : BaseException
{
    public BusinessRuleException(string message)
        : base(message, (int)HttpStatusCode.Conflict)
    {
    }

    public BusinessRuleException(ErrorCode code, object? parameters = null)
        : base(code, (int)HttpStatusCode.Conflict, parameters)
    {
    }
}

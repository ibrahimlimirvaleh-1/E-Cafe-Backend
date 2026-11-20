namespace ECafe.Domain.Exceptions;

public sealed class TooManyRequestsException() : BaseException(ErrorCode.TooManyRequests, 429);

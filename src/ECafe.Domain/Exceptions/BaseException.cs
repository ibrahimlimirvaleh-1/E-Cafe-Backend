namespace ECafe.Domain.Exceptions
{
    public abstract class BaseException : Exception
    {
        protected BaseException(string message, int statusCode)
            : this(message, statusCode, ErrorCode.BusinessRuleViolation)
        {
        }

        protected BaseException(string message, int statusCode, ErrorCode code)
            : base(message)
        {
            StatusCode = statusCode;
            Code = code;
            UsesDynamicMessage = false;
        }

        protected BaseException(ErrorCode code, int statusCode, object? parameters = null)
            : base(code.ToString())
        {
            Code = code;
            StatusCode = statusCode;
            Parameters = parameters;
            UsesDynamicMessage = true;
        }

        public ErrorCode Code { get; }
        public object? Parameters { get; }
        public int StatusCode { get; }
        public bool UsesDynamicMessage { get; }
    }
}

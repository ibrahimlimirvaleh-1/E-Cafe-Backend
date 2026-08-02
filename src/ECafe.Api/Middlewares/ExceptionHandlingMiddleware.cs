using System.Net;
using ECafe.Domain.Exceptions;
using FluentValidation;
using Sentry;

namespace ECafe.Api.Middlewares;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IErrorMessageProvider _errorMessageProvider;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IErrorMessageProvider errorMessageProvider)
    {
        _next = next;
        _logger = logger;
        _errorMessageProvider = errorMessageProvider;
    }

    public async Task Invoke(HttpContext context)
        {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception. TraceId: {TraceId}", context.TraceIdentifier);

            if (ShouldReportToSentry(ex))
            {
                SentrySdk.ConfigureScope(scope =>
                {
                    scope.SetTag("traceId", context.TraceIdentifier);
                    scope.SetTag("path", context.Request.Path);
                    scope.SetTag("method", context.Request.Method);
                });

                SentrySdk.CaptureException(ex);
            }

            await HandleExceptionAsync(context, ex, _errorMessageProvider);
        }
    }

    private static bool ShouldReportToSentry(Exception ex)
        => ex is not ValidationException
           and not BaseException;

    private static async Task HandleExceptionAsync(
        HttpContext context,
        Exception ex,
        IErrorMessageProvider errorMessageProvider)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.ContentType = "application/json";

        if (ex is ValidationException validationException)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;

            await context.Response.WriteAsJsonAsync(new
            {
                statusCode = context.Response.StatusCode,
                code = ErrorCode.ValidationFailed.ToString(),
                message = "Validation failed",
                traceId = context.TraceIdentifier,
                errors = validationException.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage }),
                timestamp = DateTime.UtcNow
            });

            return;
        }

        var statusCode = ex is BaseException baseException
            ? baseException.StatusCode
            : (int)HttpStatusCode.InternalServerError;

        var code = ex is BaseException codedException
            ? codedException.Code.ToString()
            : ErrorCode.InternalServerError.ToString();

        var message = ex is BaseException applicationException
            ? errorMessageProvider.GetMessage(applicationException)
            : "Internal server error";

        context.Response.StatusCode = statusCode;

        await context.Response.WriteAsJsonAsync(new
        {
            statusCode,
            code,
            message,
            traceId = context.TraceIdentifier,
            timestamp = DateTime.UtcNow
        });
    }
}

using System.Net;
using ECafe.Application.Common.Exceptions;
using ECafe.Domain.Exceptions;
using FluentValidation;

namespace ECafe.Api.Middlewares;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            // TraceId ilə log daha faydalı olur
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

            await HandleExceptionAsync(context, ex);
        }
    }

    private static bool ShouldReportToSentry(Exception ex)
        => ex is not ValidationException
           and not NotFoundException
           and not ForbiddenException
           and not BusinessRuleException;
    private static async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.ContentType = "application/json";

        // Validation üçün xüsusi payload
        if (ex is ValidationException vex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;

            await context.Response.WriteAsJsonAsync(new
            {
                statusCode = context.Response.StatusCode,
                message = "Validation failed",
                traceId = context.TraceIdentifier,
                errors = vex.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage }),
                timestamp = DateTime.UtcNow
            });

            return;
        }

        var statusCode = ex switch
        {
            NotFoundException => (int)HttpStatusCode.NotFound,
            ForbiddenException => (int)HttpStatusCode.Forbidden,
            BusinessRuleException => (int)HttpStatusCode.Conflict,
            _ => (int)HttpStatusCode.InternalServerError
        };

        context.Response.StatusCode = statusCode;

        var message = statusCode == (int)HttpStatusCode.InternalServerError
            ? "Internal server error"
            : ex.Message;

        await context.Response.WriteAsJsonAsync(new
        {
            statusCode,
            message,
            traceId = context.TraceIdentifier,
            timestamp = DateTime.UtcNow
        });
    }
}

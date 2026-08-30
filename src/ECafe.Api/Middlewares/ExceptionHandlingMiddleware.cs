using System.Net;
using ECafe.Application.Services.Monitoring.Abstract;
using ECafe.Domain.Exceptions;
using FluentValidation;
using Sentry;

namespace ECafe.Api.Middlewares;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IErrorMessageProvider _errorMessageProvider;
    private readonly ICriticalEventReporter _criticalEventReporter;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IErrorMessageProvider errorMessageProvider,
        ICriticalEventReporter criticalEventReporter)
    {
        _next = next;
        _logger = logger;
        _errorMessageProvider = errorMessageProvider;
        _criticalEventReporter = criticalEventReporter;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            LogException(context, ex);
            await ReportCriticalApplicationExceptionAsync(context, ex);
            ReportUnexpectedException(context, ex);

            await WriteErrorResponseAsync(context, ex);
        }
    }

    private void LogException(HttpContext context, Exception exception)
    {
        if (IsHandledException(exception))
        {
            _logger.LogWarning(exception, "Request failed with handled exception. TraceId: {TraceId}", context.TraceIdentifier);
            return;
        }

        _logger.LogError(exception, "Unhandled exception. TraceId: {TraceId}", context.TraceIdentifier);
    }

    private static bool IsHandledException(Exception exception)
        => exception is ValidationException or BaseException;

    private Task ReportCriticalApplicationExceptionAsync(HttpContext context, Exception exception)
    {
        if (exception is not BaseException baseException || !IsCriticalApplicationError(baseException.Code))
            return Task.CompletedTask;

        return _criticalEventReporter.CaptureAsync(new CriticalEvent(
            Category: ResolveCriticalCategory(baseException.Code),
            Name: baseException.Code.ToString(),
            Severity: ResolveCriticalSeverity(baseException.Code),
            Properties: new Dictionary<string, string?>
            {
                ["statusCode"] = baseException.StatusCode.ToString(),
                ["errorCode"] = baseException.Code.ToString(),
                ["traceId"] = context.TraceIdentifier,
                ["path"] = context.Request.Path,
                ["method"] = context.Request.Method
            }));
    }

    private static bool IsCriticalApplicationError(ErrorCode code)
        => code is ErrorCode.RefreshTokenReuseDetected
            or ErrorCode.AccountTemporarilyLocked
            or ErrorCode.SessionInvalid
            or ErrorCode.UserDeactivated
            or ErrorCode.NotificationProviderUnavailable
            or ErrorCode.FileStorageUnavailable;

    private static string ResolveCriticalCategory(ErrorCode code)
        => code switch
        {
            ErrorCode.RefreshTokenReuseDetected
                or ErrorCode.AccountTemporarilyLocked
                or ErrorCode.SessionInvalid
                or ErrorCode.UserDeactivated => "auth",
            ErrorCode.NotificationProviderUnavailable => "notification",
            ErrorCode.FileStorageUnavailable => "storage",
            _ => "application"
        };

    private static CriticalEventSeverity ResolveCriticalSeverity(ErrorCode code)
        => code switch
        {
            ErrorCode.RefreshTokenReuseDetected => CriticalEventSeverity.Critical,
            ErrorCode.NotificationProviderUnavailable
                or ErrorCode.FileStorageUnavailable => CriticalEventSeverity.Error,
            _ => CriticalEventSeverity.Warning
        };

    private static void ReportUnexpectedException(HttpContext context, Exception exception)
    {
        if (IsHandledException(exception))
            return;

        SentrySdk.ConfigureScope(scope =>
        {
            scope.SetTag("traceId", context.TraceIdentifier);
            scope.SetTag("path", context.Request.Path);
            scope.SetTag("method", context.Request.Method);
        });

        SentrySdk.CaptureException(exception);
    }

    private async Task WriteErrorResponseAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.ContentType = "application/json";

        var response = CreateErrorResponse(context, exception);
        context.Response.StatusCode = response.StatusCode;

        await context.Response.WriteAsJsonAsync(response);
    }

    private ApiErrorResponse CreateErrorResponse(HttpContext context, Exception exception)
        => exception switch
        {
            ValidationException validationException => CreateValidationErrorResponse(context, validationException),
            BaseException baseException => CreateApplicationErrorResponse(context, baseException),
            _ => CreateInternalErrorResponse(context)
        };

    private ApiErrorResponse CreateValidationErrorResponse(
        HttpContext context,
        ValidationException exception)
        => new(
            StatusCode: (int)HttpStatusCode.BadRequest,
            Code: ErrorCode.ValidationFailed.ToString(),
            Message: _errorMessageProvider.GetMessage(ErrorCode.ValidationFailed),
            TraceId: context.TraceIdentifier,
            Timestamp: DateTime.UtcNow,
            Errors: exception.Errors.Select(error => new ValidationErrorResponse(error.PropertyName, error.ErrorMessage)));

    private ApiErrorResponse CreateApplicationErrorResponse(
        HttpContext context,
        BaseException exception)
        => new(
            StatusCode: exception.StatusCode,
            Code: exception.Code.ToString(),
            Message: _errorMessageProvider.GetMessage(exception),
            TraceId: context.TraceIdentifier,
            Timestamp: DateTime.UtcNow);

    private ApiErrorResponse CreateInternalErrorResponse(HttpContext context)
        => new(
            StatusCode: (int)HttpStatusCode.InternalServerError,
            Code: ErrorCode.InternalServerError.ToString(),
            Message: _errorMessageProvider.GetMessage(ErrorCode.InternalServerError),
            TraceId: context.TraceIdentifier,
            Timestamp: DateTime.UtcNow);

    private sealed record ApiErrorResponse(
        int StatusCode,
        string Code,
        string Message,
        string TraceId,
        DateTime Timestamp,
        IEnumerable<ValidationErrorResponse>? Errors = null);

    private sealed record ValidationErrorResponse(string Field, string Message);
}

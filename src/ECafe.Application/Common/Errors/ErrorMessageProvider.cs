using System.Reflection;
using ECafe.Domain.Exceptions;

namespace ECafe.Application.Common.Errors;

public sealed class ErrorMessageProvider : IErrorMessageProvider
{
    private static readonly IReadOnlyDictionary<ErrorCode, string> Messages = new Dictionary<ErrorCode, string>
    {
        [ErrorCode.BusinessRuleViolation] = "Business rule violation.",
        [ErrorCode.ValidationFailed] = "Validation failed.",
        [ErrorCode.BadRequest] = "Bad request.",
        [ErrorCode.Forbidden] = "Access denied.",
        [ErrorCode.NotFound] = "Resource not found.",
        [ErrorCode.InternalServerError] = "Internal server error.",
        [ErrorCode.TooManyRequests] = "Too many requests. Please try again later.",

        [ErrorCode.RequestCannotBeNull] = "Request cannot be null.",
        [ErrorCode.InvalidRestaurantId] = "Invalid restaurant ID.",
        [ErrorCode.InvalidInventoryItemId] = "Invalid inventory item ID.",
        [ErrorCode.RestaurantContextRequired] = "Restaurant context is required.",
        [ErrorCode.AccessDenied] = "You do not have access to this resource.",
        [ErrorCode.InvalidCredentials] = "Email or password is incorrect.",
        [ErrorCode.AccountTemporarilyLocked] = "Account is temporarily locked because of too many failed login attempts. Please try again later.",
        [ErrorCode.RefreshTokenInvalid] = "Refresh token is invalid or expired.",
        [ErrorCode.RefreshTokenReuseDetected] = "Refresh token reuse was detected. All sessions were signed out for security.",

        [ErrorCode.RestaurantNotFound] = "Restaurant not found.",
        [ErrorCode.UnitNotFound] = "Unit not found.",
        [ErrorCode.InventoryItemNotFound] = "Inventory item not found.",
        [ErrorCode.InventoryItemAlreadyExists] = "Inventory item '{name}' already exists in this restaurant.",
        [ErrorCode.InventoryMovementTypeNotFound] = "Inventory movement type not found.",
        [ErrorCode.InvalidInventoryMovementType] = "Invalid inventory movement type.",
        [ErrorCode.InventoryMovementQuantityMustBeGreaterThanZero] = "Quantity must be greater than zero.",
        [ErrorCode.InventoryStockCannotBeNegative] = "Stock cannot be negative.",
        [ErrorCode.InventoryUnitConversionNotAllowed] = "Unit conversion is not allowed between different unit groups.",

        [ErrorCode.InvalidFileToken] = "Invalid file token.",
        [ErrorCode.FileNotFound] = "File not found.",
        [ErrorCode.FileStorageUnavailable] = "File storage is temporarily unavailable.",

        [ErrorCode.OutboxMessageNotFound] = "Outbox message not found.",
        [ErrorCode.OutboxMessageAlreadySent] = "Outbox message has already been sent.",
        [ErrorCode.OutboxMessageRetryNotAllowed] = "Outbox message cannot be retried."
    };

    public string GetMessage(BaseException exception)
    {
        if (!exception.UsesDynamicMessage)
            return exception.Message;

        return GetMessage(exception.Code, exception.Parameters, exception.Message);
    }

    public string GetMessage(ErrorCode code, object? parameters = null, string? fallbackMessage = null)
    {
        var template = Messages.GetValueOrDefault(code, fallbackMessage ?? code.ToString());
        return ApplyParameters(template, parameters);
    }

    private static string ApplyParameters(string template, object? parameters)
    {
        if (parameters is null)
            return template;

        foreach (var parameter in ToParameters(parameters))
            template = template.Replace($"{{{parameter.Key}}}", parameter.Value, StringComparison.OrdinalIgnoreCase);

        return template;
    }

    private static IEnumerable<KeyValuePair<string, string>> ToParameters(object parameters)
    {
        if (parameters is IReadOnlyDictionary<string, object?> readOnlyDictionary)
            return readOnlyDictionary.Select(x => new KeyValuePair<string, string>(x.Key, x.Value?.ToString() ?? string.Empty));

        if (parameters is IDictionary<string, object?> dictionary)
            return dictionary.Select(x => new KeyValuePair<string, string>(x.Key, x.Value?.ToString() ?? string.Empty));

        return parameters
            .GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(x => new KeyValuePair<string, string>(x.Name, x.GetValue(parameters)?.ToString() ?? string.Empty));
    }
}

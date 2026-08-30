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
        [ErrorCode.DuplicateResource] = "This record already exists.",
        [ErrorCode.UserDeactivated] = "Hesabınız deaktiv edilib. Sistemə girişiniz dayandırıldı.",
        [ErrorCode.InvalidUserId] = "Invalid user ID.",
        [ErrorCode.InvalidRoleId] = "Invalid role ID.",
        [ErrorCode.InvalidStaffId] = "Invalid staff ID.",
        [ErrorCode.InvalidCategoryId] = "Invalid category ID.",
        [ErrorCode.InvalidContractId] = "Invalid contract ID.",
        [ErrorCode.InvalidFileId] = "Invalid file ID.",
        [ErrorCode.InvalidRestaurantGroupId] = "Invalid restaurant group ID.",
        [ErrorCode.UserNotFound] = "User not found.",
        [ErrorCode.RoleNotFound] = "Role not found.",
        [ErrorCode.StaffNotFound] = "Staff not found.",
        [ErrorCode.StaffAssignmentNotFound] = "Staff assignment not found.",
        [ErrorCode.ActiveStaffAssignmentNotFound] = "Active staff assignment not found.",
        [ErrorCode.CustomerCannotBeStaff] = "Customer role cannot be assigned as restaurant staff.",
        [ErrorCode.CannotDeactivateOwnAccount] = "You cannot deactivate your own account.",
        [ErrorCode.EmailOrPhoneAlreadyUsed] = "Email or phone already belongs to another user.",
        [ErrorCode.UserEmailAlreadyExists] = "User with this email already exists.",
        [ErrorCode.UserPhoneAlreadyExists] = "User with this phone already exists.",
        [ErrorCode.FileNotFoundOrAlreadyAttached] = "File not found or already attached.",
        [ErrorCode.RestaurantScopedRoleRequiresAssignment] = "Restaurant-scoped role requires an active restaurant assignment.",
        [ErrorCode.RestaurantAlreadyHasActiveOwner] = "Restaurant already has an active owner.",
        [ErrorCode.PasswordResetTokenInvalidOrExpired] = "Password reset link is invalid or expired.",
        [ErrorCode.PasswordResetNewPasswordMustBeDifferent] = "New password must be different from the current password.",
        [ErrorCode.SessionInvalid] = "Sessiya məlumatları yenilənib. Zəhmət olmasa yenidən daxil olun.",

        [ErrorCode.RestaurantNotFound] = "Restaurant not found.",
        [ErrorCode.UnitNotFound] = "Unit not found.",
        [ErrorCode.InventoryItemNotFound] = "Inventory item not found.",
        [ErrorCode.InventoryItemAlreadyExists] = "Inventory item '{name}' already exists in this restaurant.",
        [ErrorCode.InventoryMovementTypeNotFound] = "Inventory movement type not found.",
        [ErrorCode.InvalidInventoryMovementType] = "Invalid inventory movement type.",
        [ErrorCode.InventoryMovementQuantityMustBeGreaterThanZero] = "Quantity must be greater than zero.",
        [ErrorCode.InventoryStockCannotBeNegative] = "Stock cannot be negative.",
        [ErrorCode.InventoryUnitConversionNotAllowed] = "Unit conversion is not allowed between different unit groups.",
        [ErrorCode.TableAlreadyExists] = "Bu restoran üçün {tableNo} nömrəli masa artıq mövcuddur.",
        [ErrorCode.TableNotFound] = "Table not found.",
        [ErrorCode.CategoryNotFound] = "Category not found.",
        [ErrorCode.CategoryIsEmpty] = "Category is empty.",
        [ErrorCode.CategoryDoesNotBelongToRestaurant] = "Category does not belong to the selected restaurant.",
        [ErrorCode.ItemAlreadyExistsInCategory] = "Item with the same name already exists in this category.",
        [ErrorCode.PublicRestaurantNotFound] = "Public restaurant not found.",
        [ErrorCode.RestaurantEmailAlreadyExists] = "Restaurant with this email already exists.",
        [ErrorCode.RestaurantNameAlreadyExists] = "Restaurant with this name already exists.",
        [ErrorCode.RestaurantPhoneAlreadyExists] = "Restaurant with this phone already exists.",
        [ErrorCode.RestaurantGroupNotFound] = "Restaurant group not found.",
        [ErrorCode.RestaurantGroupInactive] = "Restaurant group is inactive.",
        [ErrorCode.RestaurantGroupNameAlreadyExists] = "Restaurant group with this name already exists.",
        [ErrorCode.BranchAlreadyExistsInRestaurantGroup] = "Branch with this name already exists in the selected restaurant group.",
        [ErrorCode.BranchNameRequired] = "Branch name is required.",
        [ErrorCode.RestaurantGroupRequired] = "Restaurant group is required.",
        [ErrorCode.RestaurantActiveContractRequired] = "Restaurant does not have an active contract.",
        [ErrorCode.RestaurantAlreadyHasActiveContract] = "Restaurant already has an active contract. Terminate or expire the current contract before creating a new one.",

        [ErrorCode.InvalidFileToken] = "Invalid file token.",
        [ErrorCode.FileNotFound] = "File not found.",
        [ErrorCode.FileStorageUnavailable] = "File storage is temporarily unavailable.",
        [ErrorCode.NotificationProviderUnavailable] = "Notification provider is temporarily unavailable.",
        [ErrorCode.FileTypeNotFound] = "File type not found.",
        [ErrorCode.AttachedFileCannotBeDeleted] = "Attached file cannot be deleted.",

        [ErrorCode.OutboxMessageNotFound] = "Outbox message not found.",
        [ErrorCode.OutboxMessageAlreadySent] = "Outbox message has already been sent.",
        [ErrorCode.OutboxMessageRetryNotAllowed] = "Outbox message cannot be retried.",

        [ErrorCode.ContractRequestRequired] = "Contract request is required.",
        [ErrorCode.ContractNotFound] = "Restaurant contract not found.",
        [ErrorCode.ContractTermsMustBeAccepted] = "Contract terms must be accepted.",
        [ErrorCode.ContractAcceptanceTextRequired] = "Contract acceptance text is required.",
        [ErrorCode.RestaurantOwnerNotAssigned] = "Restorana aktiv sahibkar təyin edilməyib. Müqaviləni təsdiqə göndərmək üçün əvvəl restoran sahibkarı əlavə edin.",
        [ErrorCode.ContractNumberGenerationFailed] = "Could not generate a unique contract number.",
        [ErrorCode.ContractStartDateCannotBeInPast] = "Contract start date cannot be in the past.",
        [ErrorCode.ExpiredContractCannotBeActivated] = "Expired contract cannot be activated.",
        [ErrorCode.ContractStartDateRequired] = "Contract start date is required.",
        [ErrorCode.ContractEndDateRequired] = "Contract end date is required.",
        [ErrorCode.ContractEndDateMustBeAfterStartDate] = "Contract end date must be later than start date.",
        [ErrorCode.ExpiredContractCannotContinueApprovalFlow] = "Expired contract cannot continue in the approval flow.",
        [ErrorCode.ContractAmountMustBeGreaterThanZero] = "Contract amount must be greater than zero.",
        [ErrorCode.ContractExpiryReminderDaysInvalid] = "Expiry reminder days before must be between 1 and 365.",
        [ErrorCode.OnlyRestaurantOwnerCanApproveContract] = "Only the restaurant owner can approve this contract."
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

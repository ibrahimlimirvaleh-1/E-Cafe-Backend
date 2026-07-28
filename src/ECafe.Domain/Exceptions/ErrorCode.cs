namespace ECafe.Domain.Exceptions;

public enum ErrorCode
{
    BusinessRuleViolation = 1000,
    ValidationFailed = 1001,
    BadRequest = 1002,
    Forbidden = 1003,
    NotFound = 1004,
    InternalServerError = 1005,

    RequestCannotBeNull = 1100,
    InvalidRestaurantId = 1101,
    InvalidInventoryItemId = 1102,
    RestaurantContextRequired = 1103,
    AccessDenied = 1104,

    RestaurantNotFound = 2000,
    UnitNotFound = 2001,
    InventoryItemNotFound = 2002,
    InventoryItemAlreadyExists = 2003
}

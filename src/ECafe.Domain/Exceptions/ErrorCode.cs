namespace ECafe.Domain.Exceptions;

public enum ErrorCode
{
    BusinessRuleViolation = 1000,
    ValidationFailed = 1001,
    BadRequest = 1002,
    Forbidden = 1003,
    NotFound = 1004,
    InternalServerError = 1005,
    TooManyRequests = 1006,

    RequestCannotBeNull = 1100,
    InvalidRestaurantId = 1101,
    InvalidInventoryItemId = 1102,
    RestaurantContextRequired = 1103,
    AccessDenied = 1104,
    InvalidCredentials = 1105,
    AccountTemporarilyLocked = 1106,
    RefreshTokenInvalid = 1107,
    RefreshTokenReuseDetected = 1108,

    RestaurantNotFound = 2000,
    UnitNotFound = 2001,
    InventoryItemNotFound = 2002,
    InventoryItemAlreadyExists = 2003,
    InventoryMovementTypeNotFound = 2004,
    InvalidInventoryMovementType = 2005,
    InventoryMovementQuantityMustBeGreaterThanZero = 2006,
    InventoryStockCannotBeNegative = 2007,
    InventoryUnitConversionNotAllowed = 2008,
    InventoryMovementNotFound = 2009,
    TableAlreadyExists = 2010,

    InvalidFileToken = 3000,
    FileNotFound = 3001,
    FileStorageUnavailable = 3002,

    OutboxMessageNotFound = 4000,
    OutboxMessageAlreadySent = 4001,
    OutboxMessageRetryNotAllowed = 4002
}

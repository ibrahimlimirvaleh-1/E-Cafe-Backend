namespace ECafe.Shared.Http;

// Tətbiqə aid header müqaviləsi; standart header-lər üçün framework HeaderNames istifadə olunur.
public static class RequestHeaderNames
{
    public const string IdempotencyKey = "Idempotency-Key";
    public const string ActiveRestaurantId = "X-Active-Restaurant-Id";
    public const string ActiveRoleId = "X-Active-Role-Id";
    public const string CorrelationId = "X-Correlation-Id";
}

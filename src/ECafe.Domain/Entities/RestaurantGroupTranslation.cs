namespace ECafe.Domain.Entities;

public sealed class RestaurantGroupTranslation : EntityTranslation
{
    public int RestaurantGroupId { get; set; }
    public RestaurantGroup RestaurantGroup { get; set; } = null!;
    public string? Name { get; set; }
}

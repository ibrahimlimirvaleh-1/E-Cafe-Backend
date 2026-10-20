namespace ECafe.Domain.Entities;

public sealed class RestaurantTranslation : EntityTranslation
{
    public int RestaurantId { get; set; }
    public Restaurant Restaurant { get; set; } = null!;
    public string? Name { get; set; }
    public string? Location { get; set; }
    public string? BranchName { get; set; }
}

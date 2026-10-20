namespace ECafe.Domain.Entities;

public sealed class ItemTranslation : EntityTranslation
{
    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? UnavailableReason { get; set; }
}

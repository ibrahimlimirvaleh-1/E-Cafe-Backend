namespace ECafe.Domain.Entities;

public sealed class CategoryTranslation : EntityTranslation
{
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public string? Name { get; set; }
}

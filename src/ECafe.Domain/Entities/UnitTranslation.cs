namespace ECafe.Domain.Entities;

public sealed class UnitTranslation : EntityTranslation
{
    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;
    public string? Name { get; set; }
}

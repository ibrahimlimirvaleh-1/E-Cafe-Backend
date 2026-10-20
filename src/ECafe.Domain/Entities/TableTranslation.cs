namespace ECafe.Domain.Entities;

public sealed class TableTranslation : EntityTranslation
{
    public int TableId { get; set; }
    public Table Table { get; set; } = null!;
    public string? Name { get; set; }
}

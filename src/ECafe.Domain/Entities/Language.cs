namespace ECafe.Domain.Entities;

public sealed class Language
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }
}

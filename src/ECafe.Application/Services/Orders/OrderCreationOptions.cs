namespace ECafe.Application.Services.Orders;

public sealed class OrderCreationOptions
{
    public const string SectionName = "Orders:Creation";
    public int MaxLines { get; set; } = 100;
    public int MaxQuantityPerItem { get; set; } = 100;
    public int MaxNoteLength { get; set; } = 1000;
    public int MaxLineNoteLength { get; set; } = 500;
}

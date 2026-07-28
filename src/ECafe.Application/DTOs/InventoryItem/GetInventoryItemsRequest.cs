namespace ECafe.Application.DTOs.InventoryItem
{
    public class GetInventoryItemsRequest
    {
        public string? Search { get; set; }
        public bool OnlyLowStock { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 5;
    }
}

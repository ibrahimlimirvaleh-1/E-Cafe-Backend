namespace ECafe.Application.DTOs.Restaurant
{
    public class ItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;

        public string? Description { get; set; }

        public decimal BasePrice { get; set; }

        public string? UnavailableReason { get; set; }

        public int SalesCount { get; set; }

        public string? FileUrl {get; set; }
    }
}

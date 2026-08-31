namespace ECafe.Application.DTOs.Item
{
    public class ItemDto
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public string? Description { get; set; }

        public int CategoryId { get; set; }

        public string? CategoryName { get; set; }

        public int StatusId { get; set; }

        public string? StatusName { get; set; }

        public decimal BasePrice { get; set; }

        public bool IsActive { get; set; }

        public int SalesCount { get; set; }

        public string? FileUrl { get; set; }

    }
}

namespace ECafe.Application.DTOs.Item
{
    public class ItemDto
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public string? CategoryName { get; set; }

        public decimal BasePrice { get; set; }

        public bool IsActive { get; set; }

        public int SalesCount { get; set; }

    }
}

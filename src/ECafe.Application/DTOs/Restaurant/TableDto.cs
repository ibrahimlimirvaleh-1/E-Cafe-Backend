namespace ECafe.Application.DTOs.Restaurant
{
    public class TableDto
    {
        public int Id { get; set; }
        public int TableNo { get; set; }

        public string? Name { get; set; }

        public int Capacity { get; set; }

        public bool IsActive { get; set; }
        public bool IsEmpty { get; set; }
    }
}

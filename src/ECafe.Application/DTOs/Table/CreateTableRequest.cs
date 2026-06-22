namespace ECafe.Application.DTOs.Table
{
    public class CreateTableRequest
    {
        public int RestaurantId { get; set; }

        public int TableNo { get; set; }

        public string? Name { get; set; }

        public int Capacity { get; set; }
    }
}

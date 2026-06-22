using Microsoft.AspNetCore.Http;

namespace ECafe.Application.DTOs.Item
{
    public class CreateItemRequest
    {
        public int RestaurantId { get; set; }

        public int CategoryId { get; set; }

        public string Name { get; set; } = null!;

        public string? Description { get; set; }

        public decimal BasePrice { get; set; }

        public string? UnavailableReason { get; set; }

        public int SalesCount { get; set; }

        public IFormFile? File { get; set; }


    }
}

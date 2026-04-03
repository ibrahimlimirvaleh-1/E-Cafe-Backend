using Microsoft.AspNetCore.Http;

namespace ECafe.Application.DTOs.Restaurant
{
    public class RegisterRestaurantRequest
    {
        public string Name { get; set; } = null!;

        public string Location { get; set; } = null!;

        public string Phone { get; set; } = null!;
        public string Email { get; set; } = null!;

        public decimal? RatingAverage { get; set; }

        public int? RatingCount { get; set; }

        public List<IFormFile>? Files { get; set; }

    }
}

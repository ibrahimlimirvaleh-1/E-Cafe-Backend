using ECafe.Application.DTOs.File;

namespace ECafe.Application.DTOs.Restaurant
{
    public class GetByIdRestaurantResponse
    {
        public string Name { get; set; } = null!;

        public string Location { get; set; } = null!;

        public string Phone { get; set; } = null!;
        public string Email { get; set; } = null!;

        public decimal? RatingAverage { get; set; }

        public int? RatingCount { get; set; }

        public List<string>? ImageUrls { get; set; }

        public List<TableDto> Tables { get; set; } = new List<TableDto>();

    }
}

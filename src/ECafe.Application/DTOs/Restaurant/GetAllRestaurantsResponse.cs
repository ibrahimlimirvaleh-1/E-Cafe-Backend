namespace ECafe.Application.DTOs.Restaurant
{
    public class GetAllRestaurantsResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;

        public string Location { get; set; } = null!;

        public string Phone { get; set; } = null!;

        public decimal? RatingAverage { get; set; }

        public int? RatingCount { get; set; }

        public List<string>? ImageUrls { get; set; }
    }
}

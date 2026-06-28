namespace ECafe.Application.DTOs.Restaurant
{
    public class GetByIdRestaurantResponse
    {
        public RestaurantDetailDto Restaurant { get; set; } = null!;

        public List<TableDto> Tables { get; set; } = new List<TableDto>();

        public List<CategoryDto> Categories { get; set; } = new List<CategoryDto>();
    }
}

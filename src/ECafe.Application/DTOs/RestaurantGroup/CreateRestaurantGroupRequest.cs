namespace ECafe.Application.DTOs.RestaurantGroup
{
    public class CreateRestaurantGroupRequest
    {
        public string Name { get; set; } = null!;

        public string? LegalName { get; set; }

        public string? Email { get; set; }
    }
}

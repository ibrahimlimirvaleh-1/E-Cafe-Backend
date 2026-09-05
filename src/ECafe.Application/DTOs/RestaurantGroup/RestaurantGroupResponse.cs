namespace ECafe.Application.DTOs.RestaurantGroup
{
    public class RestaurantGroupResponse
    {
        public int Id { get; set; }

        public string Name { get; set; } = null!;

        public string? LegalName { get; set; }

        public string? Email { get; set; }

        public bool IsActive { get; set; }
    }
}

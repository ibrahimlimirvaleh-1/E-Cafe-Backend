namespace ECafe.Application.DTOs.User
{
    public class ProfileResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Surname { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public bool IsActive { get; set; }
        public decimal? Rating { get; set; }
        public string Role { get; set; } = null!;
        public int? RestaurantId { get; set; }
        public string? FileUrl { get; set; }
    }
}

namespace ECafe.Application.DTOs.User
{
    public class CreateUserRequest
    {
        public string Name { get; set; } = null!;

        public string Surname { get; set; } = null!;

        public string Email { get; set; } = null!;

        public string Phone { get; set; } = null!;

        public bool IsActive { get; set; }

        public decimal? Rating { get; set; }

        public int? FileId { get; set; }

        public int RestaurantId { get; set; }

        public int RoleId { get; set; }

        public decimal? ServiceFeePercent { get; set; }
    }
}

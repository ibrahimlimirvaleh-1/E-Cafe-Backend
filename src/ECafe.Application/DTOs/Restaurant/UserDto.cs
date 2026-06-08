namespace ECafe.Application.DTOs.Restaurant
{
    public class UserDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;

        public string Surname { get; set; } = null!;

        public string Email { get; set; } = null!;

        public string Phone { get; set; } = null!;

        public decimal? Rating { get; set; }

        public string FileUrl { get; set; } = null!;

        public RoleDto Role { get; set; } = null!;
    }
}

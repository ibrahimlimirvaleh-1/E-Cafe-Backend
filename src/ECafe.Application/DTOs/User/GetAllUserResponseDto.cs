namespace ECafe.Application.DTOs.User
{
    public class GetAllUserResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;

        public string Surname { get; set; } = null!;

        public bool IsActive { get; set; }

        public decimal? Rating { get; set; }

        public RoleDto Role { get; set; } = null!;

    }
}

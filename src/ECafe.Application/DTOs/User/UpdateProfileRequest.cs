namespace ECafe.Application.DTOs.User
{
    public class UpdateProfileRequest
    {
        public string Name { get; set; } = null!;
        public string Surname { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public int? FileId { get; set; }
    }
}

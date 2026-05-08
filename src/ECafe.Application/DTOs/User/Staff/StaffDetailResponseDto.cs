namespace ECafe.Application.DTOs.User.Staff
{
    public class StaffDetailResponseDto : StaffBaseDto
    {
        public string Email { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public bool IsActive { get; set; }
    }
}

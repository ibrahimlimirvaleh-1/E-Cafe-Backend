namespace ECafe.Application.DTOs.User.Staff
{
    public class UpdateStaffRequest
    {
        public string Name { get; set; } = null!;

        public string Surname { get; set; } = null!;

        public string Email { get; set; } = null!;

        public string Phone { get; set; } = null!;

        public bool IsActive { get; set; }

        public int? FileId { get; set; }

        public decimal? ServiceFeePercent { get; set; }

        public int? MaxActiveTableCount { get; set; }
    }
}

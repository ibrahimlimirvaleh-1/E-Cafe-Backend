namespace ECafe.Application.DTOs.User.Staff
{
    public class StaffBaseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Surname { get; set; } = null!;
        public decimal? Rating { get; set; }
        public string Role { get; set; } = null!;
        public decimal? ServiceFeePercent { get; set; }
        public int? MaxActiveTableCount { get; set; }
        public int? EffectiveMaxActiveTableCount { get; set; }
        public int ActiveTableSessionCount { get; set; }
        public bool CanAcceptMoreTables { get; set; }

        public string? FileUrl { get; set; }

    }
}

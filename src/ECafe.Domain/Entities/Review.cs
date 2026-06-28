using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities
{
    public class Review : AuditableSoftDeletableEntity<int>
    {
        public int UserId { get; set; }

        public int RestaurantId { get; set; }

        public int Rating { get; set; }

        public string? Comment { get; set; }

        public virtual User User { get; set; } = null!;

        public virtual Restaurant Restaurant { get; set; } = null!;
    }
}

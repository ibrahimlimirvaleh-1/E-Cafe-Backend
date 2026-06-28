using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities
{
    public class Notification : AuditableSoftDeletableEntity<int>
    {
        public int UserId { get; set; }

        public string Title { get; set; } = null!;

        public string Message { get; set; } = null!;

        public bool IsRead { get; set; }

        public DateTime? ReadAt { get; set; }

        public virtual User User { get; set; } = null!;
    }
}

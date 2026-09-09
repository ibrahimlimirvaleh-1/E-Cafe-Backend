using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities;

public class RestaurantWorkingHour : AuditableSoftDeletableEntity<int>
{
    public int RestaurantId { get; set; }

    public DayOfWeek DayOfWeek { get; set; }

    public TimeOnly OpensAt { get; set; }

    public TimeOnly ClosesAt { get; set; }

    public bool IsClosed { get; set; }

    public virtual Restaurant Restaurant { get; set; } = null!;
}

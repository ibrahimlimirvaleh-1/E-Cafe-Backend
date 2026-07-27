using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities
{
    public class Unit : AuditableSoftDeletableEntity<int>
    {
        public string Name { get; set; } = null!;
        public string Code { get; set; } = null!;
        public int? BaseUnitId { get; set; }
        public decimal ConversionRateToBase { get; set; }

        public Unit? BaseUnit { get; set; }
    }
}

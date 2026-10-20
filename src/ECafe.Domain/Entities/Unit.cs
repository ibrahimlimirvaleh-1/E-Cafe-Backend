using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities
{
    public class Unit : AuditableSoftDeletableEntity<int>
    {
        public ICollection<UnitTranslation> Translations { get; set; } = new List<UnitTranslation>();

        public string Name { get; set; } = null!;
        public string Code { get; set; } = null!;
        public int? BaseUnitId { get; set; }
        public decimal ConversionRateToBase { get; set; }

        public Unit? BaseUnit { get; set; }
    }
}

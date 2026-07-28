using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities
{
    public class InventoryMovementType : AuditableSoftDeletableEntity<int>
    {
        public string Name { get; set; } = null!;
        public string Code { get; set; } = null!;
        public string Description { get; set; } = null!;
    }
}

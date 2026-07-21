using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities
{
    public partial class File : AuditableSoftDeletableEntity<int>
    {
        public string Token { get; set; } = null!;

        public string Name { get; set; } = null!;

        public string Extension { get; set; } = null!;

        public long Size { get; set; }

        public string Url { get; set; } = null!;

        public virtual ICollection<Item> Items { get; set; } = new List<Item>();

        public virtual User? User { get; set; }

        public virtual Restaurant? Restaurant { get; set; }

        public virtual ICollection<RestaurantContract> RestaurantContracts { get; set; } = new List<RestaurantContract>();
    }
}

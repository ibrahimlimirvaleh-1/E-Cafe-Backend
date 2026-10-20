using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete;

public sealed class InventoryItemTranslationConfiguration : IEntityTypeConfiguration<InventoryItemTranslation>
{
    public void Configure(EntityTypeBuilder<InventoryItemTranslation> builder)
    {
        ContentTranslationConfiguration.Configure(builder, "inventory_item_translations", "inventory", nameof(InventoryItemTranslation.InventoryItemId), 150);
        builder.HasOne(row => row.InventoryItem).WithMany(owner => owner.Translations).HasForeignKey(row => row.InventoryItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasQueryFilter(row => !row.InventoryItem.IsDeleted && !row.InventoryItem.Restaurant.IsDeleted && row.Language.IsActive);
    }
}

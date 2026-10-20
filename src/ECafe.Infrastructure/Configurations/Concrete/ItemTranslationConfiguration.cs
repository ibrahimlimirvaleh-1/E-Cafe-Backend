using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete;

public sealed class ItemTranslationConfiguration : IEntityTypeConfiguration<ItemTranslation>
{
    public void Configure(EntityTypeBuilder<ItemTranslation> builder)
    {
        ContentTranslationConfiguration.Configure(builder, "item_translations", "catalog", nameof(ItemTranslation.ItemId), 150);
        builder.Property(row => row.Description).HasMaxLength(500);
        builder.Property(row => row.UnavailableReason).HasColumnType("text");
        builder.HasOne(row => row.Item).WithMany(owner => owner.Translations).HasForeignKey(row => row.ItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasQueryFilter(row => !row.Item.IsDeleted && !row.Item.Restaurant.IsDeleted && row.Language.IsActive);
    }
}

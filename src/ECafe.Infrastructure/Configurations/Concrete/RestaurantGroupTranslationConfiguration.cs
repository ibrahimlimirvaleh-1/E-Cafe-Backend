using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete;

public sealed class RestaurantGroupTranslationConfiguration : IEntityTypeConfiguration<RestaurantGroupTranslation>
{
    public void Configure(EntityTypeBuilder<RestaurantGroupTranslation> builder)
    {
        ContentTranslationConfiguration.Configure(builder, "restaurant_group_translations", "core", nameof(RestaurantGroupTranslation.RestaurantGroupId), 100);
        builder.HasOne(row => row.RestaurantGroup).WithMany(owner => owner.Translations).HasForeignKey(row => row.RestaurantGroupId).OnDelete(DeleteBehavior.Cascade);
        builder.HasQueryFilter(row => !row.RestaurantGroup.IsDeleted && row.Language.IsActive);
    }
}

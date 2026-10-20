using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete;

public sealed class RestaurantTranslationConfiguration : IEntityTypeConfiguration<RestaurantTranslation>
{
    public void Configure(EntityTypeBuilder<RestaurantTranslation> builder)
    {
        ContentTranslationConfiguration.Configure(builder, "restaurant_translations", "core", nameof(RestaurantTranslation.RestaurantId), 200);
        builder.Property(row => row.Location).HasMaxLength(1000);
        builder.Property(row => row.BranchName).HasMaxLength(100);
        builder.HasOne(row => row.Restaurant).WithMany(owner => owner.Translations).HasForeignKey(row => row.RestaurantId).OnDelete(DeleteBehavior.Cascade);
        builder.HasQueryFilter(row => !row.Restaurant.IsDeleted && row.Language.IsActive);
    }
}

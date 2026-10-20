using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete;

public sealed class CategoryTranslationConfiguration : IEntityTypeConfiguration<CategoryTranslation>
{
    public void Configure(EntityTypeBuilder<CategoryTranslation> builder)
    {
        ContentTranslationConfiguration.Configure(builder, "category_translations", "catalog", nameof(CategoryTranslation.CategoryId), 100);
        builder.HasOne(row => row.Category).WithMany(owner => owner.Translations).HasForeignKey(row => row.CategoryId).OnDelete(DeleteBehavior.Cascade);
        builder.HasQueryFilter(row => !row.Category.IsDeleted && !row.Category.Restaurant.IsDeleted && row.Language.IsActive);
    }
}

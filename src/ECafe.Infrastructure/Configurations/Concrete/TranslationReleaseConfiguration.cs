using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete;

public sealed class TranslationReleaseConfiguration : IEntityTypeConfiguration<TranslationRelease>
{
    public void Configure(EntityTypeBuilder<TranslationRelease> builder)
    {
        builder.ToTable("translation_releases", "core");
        builder.HasKey(release => release.Id);
        builder.HasOne<User>().WithMany().HasForeignKey(release => release.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TranslationRelease>().WithMany().HasForeignKey(release => release.SourceReleaseId).OnDelete(DeleteBehavior.Restrict);
    }
}

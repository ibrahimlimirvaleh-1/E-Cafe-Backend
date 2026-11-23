using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete;

public sealed class ProductAnnouncementConfiguration : IEntityTypeConfiguration<ProductAnnouncement>
{
    public void Configure(EntityTypeBuilder<ProductAnnouncement> builder)
    {
        builder.ToTable("product_announcements", "platform", table => table.HasCheckConstraint("announcement_version_positive", "\"Version\" > 0"));
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).UseIdentityByDefaultColumn().HasIdentityOptions(startValue: 2);
        builder.Property(value => value.Destination).HasMaxLength(40).IsRequired();
        builder.HasIndex(value => new { value.PublishedAtUtc, value.Id });
        builder.HasMany(value => value.Texts).WithOne().HasForeignKey(value => value.AnnouncementId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(value => value.Roles).WithOne().HasForeignKey(value => value.AnnouncementId).OnDelete(DeleteBehavior.Cascade);
        builder.HasData(new ProductAnnouncement { Id = 1, Version = 1, Destination = ProductAnnouncementPolicy.CustomerProfile,
            CreatedAt = new DateTime(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "system" });
    }
}

public sealed class ProductAnnouncementTextConfiguration : IEntityTypeConfiguration<ProductAnnouncementText>
{
    public void Configure(EntityTypeBuilder<ProductAnnouncementText> builder)
    {
        builder.ToTable("product_announcement_texts", "platform");
        builder.HasKey(value => new { value.AnnouncementId, value.Language });
        builder.Property(value => value.Language).HasMaxLength(2);
        builder.Property(value => value.Title).HasMaxLength(ProductAnnouncementPolicy.MaximumTitleLength).IsRequired();
        builder.Property(value => value.Body).HasMaxLength(ProductAnnouncementPolicy.MaximumBodyLength).IsRequired();
        builder.HasData(
            new ProductAnnouncementText { AnnouncementId = 1, Language = "az", Title = "Hesabınızı özünüz silə bilərsiniz",
                Body = "Profilinizin Təhlükəsizlik bölməsindən hesabınızı bağlaya bilərsiniz. Aktiv rezervasiya, açıq masa və ya ödənilməmiş hesab varsa, əvvəl onları tamamlamalısınız. Profil məlumatları anonimləşdirilir, sifariş və maliyyə tarixçəsi saxlanılır." },
            new ProductAnnouncementText { AnnouncementId = 1, Language = "en", Title = "You can now delete your own account",
                Body = "You can close your account in Profile > Security. Complete active reservations, open table sessions and outstanding payments first. Profile details are anonymized; order and financial history is retained." },
            new ProductAnnouncementText { AnnouncementId = 1, Language = "ru", Title = "Теперь вы можете удалить свой аккаунт",
                Body = "Закрыть аккаунт можно в разделе Профиль > Безопасность. Сначала завершите активные бронирования, сеансы за столом и неоплаченные счета. Данные профиля обезличиваются, история заказов и финансовых операций сохраняется." });
    }
}

public sealed class ProductAnnouncementRoleConfiguration : IEntityTypeConfiguration<ProductAnnouncementRole>
{
    public void Configure(EntityTypeBuilder<ProductAnnouncementRole> builder)
    {
        builder.ToTable("product_announcement_roles", "platform");
        builder.HasKey(value => new { value.AnnouncementId, value.RoleId });
        builder.HasIndex(value => new { value.RoleId, value.AnnouncementId });
        builder.HasOne<Role>().WithMany().HasForeignKey(value => value.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasData(new ProductAnnouncementRole { AnnouncementId = 1, RoleId = (int)RoleCode.Customer });
    }
}

public sealed class ProductAnnouncementReceiptConfiguration : IEntityTypeConfiguration<ProductAnnouncementReceipt>
{
    public void Configure(EntityTypeBuilder<ProductAnnouncementReceipt> builder)
    {
        builder.ToTable("product_announcement_receipts", "platform");
        builder.HasKey(value => new { value.UserId, value.RoleId, value.AnnouncementId, value.Version });
        builder.HasOne<ProductAnnouncement>().WithMany().HasForeignKey(value => value.AnnouncementId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(value => value.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Role>().WithMany().HasForeignKey(value => value.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}

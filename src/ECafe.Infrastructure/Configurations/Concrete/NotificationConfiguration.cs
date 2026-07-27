using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete
{
    public class NotificationConfiguration
    : IEntityTypeConfiguration<Notification>
    {
        public void Configure(EntityTypeBuilder<Notification> builder)
        {
            builder.ToTable("notifications", "notification");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Title)
                .HasMaxLength(200);

            builder.Property(x => x.Message)
                .HasMaxLength(1000);

            builder.HasOne(x => x.User)
                .WithMany(x => x.Notifications)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(x => x.PayloadJson)
            .HasColumnType("jsonb");

            builder.Property(x => x.RelatedEntityType)
                .HasMaxLength(100);

            builder.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt });
            builder.HasIndex(x => new { x.RestaurantId, x.CreatedAt });
            builder.HasIndex(x => new { x.RelatedEntityType, x.RelatedEntityId });
        }
    }
}

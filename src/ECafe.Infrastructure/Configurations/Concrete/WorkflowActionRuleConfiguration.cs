using ECafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECafe.Infrastructure.Configurations.Concrete;

public class WorkflowActionRuleConfiguration : DbEntityConfig<WorkflowActionRule>
{
    public override void Configure(EntityTypeBuilder<WorkflowActionRule> builder)
    {
        builder.HasKey(e => e.Id).HasName("workflow_action_rules_pkey");

        builder.ToTable("workflow_action_rules", "core");

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.FlowCode).HasMaxLength(64).HasColumnName("flow_code");
        builder.Property(e => e.StatusId).HasColumnName("status_id");
        builder.Property(e => e.RoleId).HasColumnName("role_id");
        builder.Property(e => e.ActionCode).HasMaxLength(64).HasColumnName("action_code");
        builder.Property(e => e.Label).HasMaxLength(128).HasColumnName("label");
        builder.Property(e => e.HttpMethod).HasMaxLength(16).HasColumnName("http_method");
        builder.Property(e => e.EndpointTemplate).HasMaxLength(256).HasColumnName("endpoint_template");
        builder.Property(e => e.SortOrder).HasColumnName("sort_order");
        builder.Property(e => e.RequiresConfirmation).HasColumnName("requires_confirmation");
        builder.Property(e => e.IsEnabled).HasColumnName("is_enabled").HasDefaultValue(true);

        builder.HasIndex(e => new { e.FlowCode, e.StatusId, e.RoleId, e.ActionCode })
            .IsUnique()
            .HasDatabaseName("workflow_action_rules_flow_status_role_action_key");

        builder.HasOne(e => e.Status)
            .WithMany()
            .HasForeignKey(e => e.StatusId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("workflow_action_rules_status_id_fkey");

        builder.HasOne(e => e.Role)
            .WithMany()
            .HasForeignKey(e => e.RoleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("workflow_action_rules_role_id_fkey");
    }
}

using Abuvi.API.Features.Camps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class CampAuditLogConfiguration : IEntityTypeConfiguration<CampAuditLog>
{
    public void Configure(EntityTypeBuilder<CampAuditLog> builder)
    {
        builder.ToTable("camp_audit_logs");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");

        builder.Property(a => a.CampId).IsRequired().HasColumnName("camp_id");
        builder.HasIndex(a => a.CampId).HasDatabaseName("ix_camp_audit_logs_camp_id");

        builder.Property(a => a.FieldName)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("field_name");

        builder.Property(a => a.OldValue).HasMaxLength(2000).HasColumnName("old_value");
        builder.Property(a => a.NewValue).HasMaxLength(2000).HasColumnName("new_value");

        builder.Property(a => a.ChangedByUserId).IsRequired().HasColumnName("changed_by_user_id");

        builder.Property(a => a.ChangedAt)
            .IsRequired()
            .HasColumnName("changed_at")
            .HasDefaultValueSql("NOW()");

        builder.HasIndex(a => new { a.CampId, a.ChangedAt })
            .HasDatabaseName("ix_camp_audit_logs_camp_id_changed_at");
    }
}

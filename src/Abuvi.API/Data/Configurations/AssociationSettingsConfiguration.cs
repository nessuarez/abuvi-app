using Abuvi.API.Features.Camps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

/// <summary>
/// EF Core configuration for AssociationSettings entity
/// </summary>
public class AssociationSettingsConfiguration : IEntityTypeConfiguration<AssociationSettings>
{
    public void Configure(EntityTypeBuilder<AssociationSettings> builder)
    {
        // Table name (snake_case for PostgreSQL convention)
        builder.ToTable("association_settings");

        // Primary key
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");

        // SettingKey: required, max 100, unique
        builder.Property(s => s.SettingKey)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("setting_key");

        builder.HasIndex(s => s.SettingKey)
            .IsUnique()
            .HasDatabaseName("IX_AssociationSettings_SettingKey");

        // SettingValue: required (JSON or text)
        builder.Property(s => s.SettingValue)
            .IsRequired()
            .HasColumnName("setting_value");

        // UpdatedBy: optional, FK to Users
        builder.Property(s => s.UpdatedBy)
            .HasColumnName("updated_by");

        // Timestamp: required, default NOW()
        builder.Property(s => s.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");
    }
}

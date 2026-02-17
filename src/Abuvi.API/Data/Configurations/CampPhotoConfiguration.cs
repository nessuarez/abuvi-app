using Abuvi.API.Features.Camps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

/// <summary>
/// EF Core configuration for CampPhoto entity
/// </summary>
public class CampPhotoConfiguration : IEntityTypeConfiguration<CampPhoto>
{
    public void Configure(EntityTypeBuilder<CampPhoto> builder)
    {
        builder.ToTable("camp_photos");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");

        builder.Property(p => p.CampId)
            .IsRequired()
            .HasColumnName("camp_id");

        builder.Property(p => p.PhotoReference)
            .HasMaxLength(500)
            .HasColumnName("photo_reference");

        builder.Property(p => p.PhotoUrl)
            .HasMaxLength(1000)
            .HasColumnName("photo_url");

        builder.Property(p => p.Width)
            .IsRequired()
            .HasColumnName("width");

        builder.Property(p => p.Height)
            .IsRequired()
            .HasColumnName("height");

        builder.Property(p => p.AttributionName)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("attribution_name");

        builder.Property(p => p.AttributionUrl)
            .HasMaxLength(500)
            .HasColumnName("attribution_url");

        builder.Property(p => p.IsOriginal)
            .IsRequired()
            .HasDefaultValue(true)
            .HasColumnName("is_original");

        builder.Property(p => p.IsPrimary)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("is_primary");

        builder.Property(p => p.DisplayOrder)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("display_order");

        builder.Property(p => p.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(p => p.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        // Indexes
        builder.HasIndex(p => p.CampId)
            .HasDatabaseName("ix_camp_photos_camp_id");

        builder.HasIndex(p => new { p.CampId, p.IsPrimary })
            .HasDatabaseName("ix_camp_photos_camp_id_is_primary");

        // Relationship: Camp → Photos (cascade delete — photos deleted when camp deleted)
        builder.HasOne(p => p.Camp)
            .WithMany(c => c.Photos)
            .HasForeignKey(p => p.CampId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

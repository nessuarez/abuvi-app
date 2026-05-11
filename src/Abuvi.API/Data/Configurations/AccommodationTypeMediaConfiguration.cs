using Abuvi.API.Features.Camps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class AccommodationTypeMediaConfiguration : IEntityTypeConfiguration<AccommodationTypeMedia>
{
    public void Configure(EntityTypeBuilder<AccommodationTypeMedia> builder)
    {
        builder.ToTable("accommodation_type_media");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(m => m.AccommodationType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasColumnName("accommodation_type");

        builder.Property(m => m.FileUrl)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnName("file_url");

        builder.Property(m => m.ThumbnailUrl)
            .HasMaxLength(500)
            .HasColumnName("thumbnail_url");

        builder.Property(m => m.Description)
            .HasMaxLength(200)
            .HasColumnName("description");

        builder.Property(m => m.DisplayOrder)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("display_order");

        builder.Property(m => m.IsPrimary)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("is_primary");

        builder.Property(m => m.UploadedByUserId)
            .IsRequired()
            .HasColumnName("uploaded_by_user_id");

        builder.Property(m => m.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(m => m.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        builder.HasIndex(m => m.AccommodationType)
            .HasDatabaseName("ix_accommodation_type_media_type");

        builder.HasIndex(m => new { m.AccommodationType, m.IsPrimary })
            .HasDatabaseName("ix_accommodation_type_media_type_primary");
    }
}

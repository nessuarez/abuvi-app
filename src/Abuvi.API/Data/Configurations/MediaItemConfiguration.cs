using Abuvi.API.Features.MediaItems;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class MediaItemConfiguration : IEntityTypeConfiguration<MediaItem>
{
    public void Configure(EntityTypeBuilder<MediaItem> builder)
    {
        builder.ToTable("media_items");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");

        builder.Property(m => m.UploadedByUserId)
            .IsRequired()
            .HasColumnName("uploaded_by_user_id");

        builder.Property(m => m.FileUrl)
            .IsRequired()
            .HasMaxLength(2048)
            .HasColumnName("file_url");

        builder.Property(m => m.ThumbnailUrl)
            .HasMaxLength(2048)
            .HasColumnName("thumbnail_url");

        builder.Property(m => m.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasColumnName("type");

        builder.Property(m => m.Title)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("title");

        builder.Property(m => m.Description)
            .HasMaxLength(1000)
            .HasColumnName("description");

        builder.Property(m => m.Year)
            .HasColumnName("year");

        builder.Property(m => m.Decade)
            .HasMaxLength(10)
            .HasColumnName("decade");

        builder.Property(m => m.MemoryId)
            .HasColumnName("memory_id");

        builder.Property(m => m.CampLocationId)
            .HasColumnName("camp_location_id");

        builder.Property(m => m.IsPublished)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("is_published");

        builder.Property(m => m.IsApproved)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("is_approved");

        builder.Property(m => m.Context)
            .HasMaxLength(50)
            .HasColumnName("context");

        builder.Property(m => m.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(m => m.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        // Indexes
        builder.HasIndex(m => m.UploadedByUserId)
            .HasDatabaseName("ix_media_items_uploaded_by_user_id");

        builder.HasIndex(m => m.Year)
            .HasDatabaseName("ix_media_items_year");

        builder.HasIndex(m => m.Context)
            .HasDatabaseName("ix_media_items_context");

        builder.HasIndex(m => new { m.IsApproved, m.IsPublished })
            .HasDatabaseName("ix_media_items_approved_published");

        builder.HasIndex(m => m.MemoryId)
            .HasDatabaseName("ix_media_items_memory_id");

        // Relationships
        builder.HasOne(m => m.UploadedBy)
            .WithMany()
            .HasForeignKey(m => m.UploadedByUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Memory)
            .WithMany(mem => mem.MediaItems)
            .HasForeignKey(m => m.MemoryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

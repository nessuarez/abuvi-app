using Abuvi.API.Features.MediaItems;
using Abuvi.API.Features.MediaSources;
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

        builder.Property(m => m.DisplayOrder)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("display_order");

        builder.Property(m => m.IsPrimary)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("is_primary");

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

        builder.Property(m => m.AccommodationId)
            .HasColumnName("accommodation_id")
            .IsRequired(false);

        builder.Property(m => m.ZoneId)
            .HasColumnName("zone_id")
            .IsRequired(false);

        // Relationships
        builder.HasOne(m => m.UploadedBy)
            .WithMany()
            .HasForeignKey(m => m.UploadedByUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Memory)
            .WithMany(mem => mem.MediaItems)
            .HasForeignKey(m => m.MemoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(m => m.Accommodation)
            .WithMany(a => a.MediaItems)
            .HasForeignKey(m => m.AccommodationId)
            .OnDelete(DeleteBehavior.SetNull);

        // ── Camp edition album anchor, provenance and dating ──

        builder.Property(m => m.CampEditionId)
            .HasColumnName("camp_edition_id");

        builder.Property(m => m.YearSource)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(MediaItemYearSource.Unknown)
            .HasColumnName("year_source");

        builder.Property(m => m.CommentCount)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("comment_count");

        builder.Property(m => m.MediaSourceId)
            .HasColumnName("media_source_id");

        builder.Property(m => m.SourcePath)
            .HasMaxLength(1024)
            .HasColumnName("source_path");

        builder.HasOne(m => m.CampEdition)
            .WithMany()
            .HasForeignKey(m => m.CampEditionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(m => m.MediaSource)
            .WithMany()
            .HasForeignKey(m => m.MediaSourceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(m => m.CampEditionId)
            .HasDatabaseName("ix_media_items_camp_edition_id");

        // Album grid query: edition + approval state
        builder.HasIndex(m => new { m.CampEditionId, m.IsApproved, m.IsPublished })
            .HasDatabaseName("ix_media_items_edition_approved_published");

        builder.HasIndex(m => m.MediaSourceId)
            .HasDatabaseName("ix_media_items_media_source_id");

        // Note: the partial index for the unplaced pile (WHERE camp_edition_id IS NULL)
        // is declared as raw SQL in the migration — EF cannot express it here.

        // Indexes for primary media lookups
        builder.HasIndex(m => new { m.AccommodationId, m.IsPrimary })
            .HasDatabaseName("ix_media_items_accommodation_primary");

        builder.HasIndex(m => new { m.ZoneId, m.IsPrimary })
            .HasDatabaseName("ix_media_items_zone_primary");
    }
}

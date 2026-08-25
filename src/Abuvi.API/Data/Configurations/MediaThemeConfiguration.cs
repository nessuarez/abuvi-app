using Abuvi.API.Features.MediaThemes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class MediaThemeConfiguration : IEntityTypeConfiguration<MediaTheme>
{
    public void Configure(EntityTypeBuilder<MediaTheme> builder)
    {
        builder.ToTable("media_themes");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("name");

        builder.Property(t => t.Slug)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("slug");

        builder.Property(t => t.Description)
            .HasMaxLength(500)
            .HasColumnName("description");

        builder.Property(t => t.IsActive)
            .IsRequired()
            .HasDefaultValue(true)
            .HasColumnName("is_active");

        builder.Property(t => t.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(t => t.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        // Slug is the URL identity — must be unique.
        builder.HasIndex(t => t.Slug)
            .IsUnique()
            .HasDatabaseName("ux_media_themes_slug");
    }
}

public class MediaItemThemeConfiguration : IEntityTypeConfiguration<MediaItemTheme>
{
    public void Configure(EntityTypeBuilder<MediaItemTheme> builder)
    {
        builder.ToTable("media_item_themes");

        // Composite PK makes duplicate tagging impossible at the database level,
        // so attaching a theme twice is a no-op rather than a data-quality problem.
        builder.HasKey(t => new { t.MediaItemId, t.MediaThemeId });

        builder.Property(t => t.MediaItemId).HasColumnName("media_item_id");
        builder.Property(t => t.MediaThemeId).HasColumnName("media_theme_id");

        builder.Property(t => t.TaggedByUserId)
            .IsRequired()
            .HasColumnName("tagged_by_user_id");

        builder.Property(t => t.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        // Relationships
        builder.HasOne(t => t.MediaItem)
            .WithMany(m => m.Themes)
            .HasForeignKey(t => t.MediaItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.MediaTheme)
            .WithMany(th => th.Items)
            .HasForeignKey(t => t.MediaThemeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.TaggedBy)
            .WithMany()
            .HasForeignKey(t => t.TaggedByUserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Backs the theme-browse query ("all San Abuvino photos across all years").
        builder.HasIndex(t => t.MediaThemeId)
            .HasDatabaseName("ix_media_item_themes_theme_id");
    }
}

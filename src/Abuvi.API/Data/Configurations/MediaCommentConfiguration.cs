using Abuvi.API.Features.MediaComments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class MediaCommentConfiguration : IEntityTypeConfiguration<MediaComment>
{
    public void Configure(EntityTypeBuilder<MediaComment> builder)
    {
        builder.ToTable("media_comments");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");

        builder.Property(c => c.MediaItemId)
            .IsRequired()
            .HasColumnName("media_item_id");

        builder.Property(c => c.AuthorUserId)
            .IsRequired()
            .HasColumnName("author_user_id");

        builder.Property(c => c.Body)
            .IsRequired()
            .HasMaxLength(1000)
            .HasColumnName("body");

        builder.Property(c => c.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(c => c.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(c => c.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Property(c => c.DeletedByUserId)
            .HasColumnName("deleted_by_user_id");

        // Indexes — (media_item_id, created_at) backs the thread query.
        builder.HasIndex(c => new { c.MediaItemId, c.CreatedAt })
            .HasDatabaseName("ix_media_comments_item_created");

        builder.HasIndex(c => c.AuthorUserId)
            .HasDatabaseName("ix_media_comments_author_user_id");

        // Relationships — deleting a user removes their comments (right to erasure).
        builder.HasOne(c => c.MediaItem)
            .WithMany()
            .HasForeignKey(c => c.MediaItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Author)
            .WithMany()
            .HasForeignKey(c => c.AuthorUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class MediaCommentReportConfiguration : IEntityTypeConfiguration<MediaCommentReport>
{
    public void Configure(EntityTypeBuilder<MediaCommentReport> builder)
    {
        builder.ToTable("media_comment_reports");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");

        builder.Property(r => r.MediaCommentId)
            .IsRequired()
            .HasColumnName("media_comment_id");

        builder.Property(r => r.ReportedByUserId)
            .IsRequired()
            .HasColumnName("reported_by_user_id");

        builder.Property(r => r.Reason)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasColumnName("reason");

        builder.Property(r => r.Notes)
            .HasMaxLength(500)
            .HasColumnName("notes");

        builder.Property(r => r.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(MediaCommentReportStatus.Pending)
            .HasColumnName("status");

        builder.Property(r => r.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(r => r.ReviewedAt)
            .HasColumnName("reviewed_at");

        builder.Property(r => r.ReviewedByUserId)
            .HasColumnName("reviewed_by_user_id");

        // One report per user per comment.
        builder.HasIndex(r => new { r.MediaCommentId, r.ReportedByUserId })
            .IsUnique()
            .HasDatabaseName("ux_media_comment_reports_comment_reporter");

        // Backs the moderation queue.
        builder.HasIndex(r => r.Status)
            .HasDatabaseName("ix_media_comment_reports_status");

        // Relationships
        builder.HasOne(r => r.MediaComment)
            .WithMany()
            .HasForeignKey(r => r.MediaCommentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.ReportedBy)
            .WithMany()
            .HasForeignKey(r => r.ReportedByUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

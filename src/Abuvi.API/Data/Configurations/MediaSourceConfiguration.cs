using Abuvi.API.Features.MediaSources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class MediaSourceConfiguration : IEntityTypeConfiguration<MediaSource>
{
    public void Configure(EntityTypeBuilder<MediaSource> builder)
    {
        builder.ToTable("media_sources");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");

        builder.Property(s => s.ContributorName)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("contributor_name");

        builder.Property(s => s.ContributorUserId)
            .HasColumnName("contributor_user_id");

        builder.Property(s => s.ContributorContact)
            .HasMaxLength(200)
            .HasColumnName("contributor_contact");

        builder.Property(s => s.Notes)
            .HasMaxLength(1000)
            .HasColumnName("notes");

        builder.Property(s => s.ReceivedAt)
            .HasColumnName("received_at");

        builder.Property(s => s.RegisteredByUserId)
            .IsRequired()
            .HasColumnName("registered_by_user_id");

        builder.Property(s => s.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(s => s.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        // Indexes
        builder.HasIndex(s => s.ContributorUserId)
            .HasDatabaseName("ix_media_sources_contributor_user_id");

        // Free-text names produce near-duplicates ("Manolo García" / "Manuel García");
        // this index backs the admin merge screen's duplicate detection.
        builder.HasIndex(s => s.ContributorName)
            .HasDatabaseName("ix_media_sources_contributor_name");

        // Relationships
        // Contributor link is SetNull so erasing a member's account does not orphan
        // the donated material — the row survives with the free-text name.
        builder.HasOne(s => s.ContributorUser)
            .WithMany()
            .HasForeignKey(s => s.ContributorUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(s => s.RegisteredBy)
            .WithMany()
            .HasForeignKey(s => s.RegisteredByUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

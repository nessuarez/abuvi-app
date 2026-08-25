using Abuvi.API.Features.MediaDating;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class MediaItemYearProposalConfiguration : IEntityTypeConfiguration<MediaItemYearProposal>
{
    public void Configure(EntityTypeBuilder<MediaItemYearProposal> builder)
    {
        builder.ToTable("media_item_year_proposals");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");

        builder.Property(p => p.MediaItemId)
            .IsRequired()
            .HasColumnName("media_item_id");

        builder.Property(p => p.ProposedByUserId)
            .IsRequired()
            .HasColumnName("proposed_by_user_id");

        builder.Property(p => p.ProposedYear)
            .IsRequired()
            .HasColumnName("proposed_year");

        builder.Property(p => p.ProposedCampEditionId)
            .HasColumnName("proposed_camp_edition_id");

        builder.Property(p => p.Rationale)
            .HasMaxLength(500)
            .HasColumnName("rationale");

        builder.Property(p => p.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(p => p.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        // One vote per user per item — re-proposing updates this row rather than
        // stacking a second vote, which is what keeps the consensus ratio honest.
        builder.HasIndex(p => new { p.MediaItemId, p.ProposedByUserId })
            .IsUnique()
            .HasDatabaseName("ux_media_item_year_proposals_item_user");

        builder.HasIndex(p => p.MediaItemId)
            .HasDatabaseName("ix_media_item_year_proposals_item_id");

        // Relationships
        builder.HasOne(p => p.MediaItem)
            .WithMany()
            .HasForeignKey(p => p.MediaItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.ProposedBy)
            .WithMany()
            .HasForeignKey(p => p.ProposedByUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.ProposedCampEdition)
            .WithMany()
            .HasForeignKey(p => p.ProposedCampEditionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

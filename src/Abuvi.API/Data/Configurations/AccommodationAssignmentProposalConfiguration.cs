using Abuvi.API.Features.Camps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class AccommodationAssignmentProposalConfiguration
    : IEntityTypeConfiguration<AccommodationAssignmentProposal>
{
    public void Configure(EntityTypeBuilder<AccommodationAssignmentProposal> builder)
    {
        builder.ToTable("accommodation_assignment_proposals");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(p => p.CampEditionId)
            .IsRequired()
            .HasColumnName("camp_edition_id");

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("name");

        builder.Property(p => p.Notes)
            .HasMaxLength(500)
            .HasColumnName("notes");

        builder.Property(p => p.IsActive)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("is_active");

        builder.Property(p => p.CreatedByUserId)
            .IsRequired()
            .HasColumnName("created_by_user_id");

        builder.Property(p => p.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(p => p.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        builder.HasOne(p => p.CampEdition)
            .WithMany()
            .HasForeignKey(p => p.CampEditionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

using Abuvi.API.Features.Camps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class AccommodationAssignmentConfiguration
    : IEntityTypeConfiguration<AccommodationAssignment>
{
    public void Configure(EntityTypeBuilder<AccommodationAssignment> builder)
    {
        builder.ToTable("accommodation_assignments");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(a => a.ProposalId)
            .IsRequired()
            .HasColumnName("proposal_id");

        builder.Property(a => a.RegistrationId)
            .IsRequired()
            .HasColumnName("registration_id");

        builder.Property(a => a.AccommodationId)
            .IsRequired()
            .HasColumnName("accommodation_id");

        builder.Property(a => a.UnitIndex)
            .HasColumnName("unit_index");

        builder.Property(a => a.AssignedByUserId)
            .IsRequired()
            .HasColumnName("assigned_by_user_id");

        builder.Property(a => a.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(a => a.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        // One registration per proposal
        builder.HasIndex(a => new { a.ProposalId, a.RegistrationId })
            .IsUnique()
            .HasDatabaseName("IX_AccommodationAssignments_Proposal_Registration");

        // Prevent double-booking the same physical unit within a proposal
        builder.HasIndex(a => new { a.ProposalId, a.AccommodationId, a.UnitIndex })
            .IsUnique()
            .HasFilter("unit_index IS NOT NULL")
            .HasDatabaseName("IX_AccommodationAssignments_Proposal_Accommodation_UnitIndex");

        builder.HasOne(a => a.Proposal)
            .WithMany(p => p.Assignments)
            .HasForeignKey(a => a.ProposalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Registration)
            .WithMany()
            .HasForeignKey(a => a.RegistrationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Accommodation)
            .WithMany()
            .HasForeignKey(a => a.AccommodationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

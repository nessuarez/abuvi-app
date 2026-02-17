using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Abuvi.API.Features.Memberships;

namespace Abuvi.API.Data.Configurations;

public class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasDefaultValueSql("gen_random_uuid()");

        // Unique constraint: one active membership per family member
        builder.HasIndex(m => m.FamilyMemberId).IsUnique();

        builder.Property(m => m.StartDate).IsRequired();
        builder.Property(m => m.IsActive).IsRequired();

        // Relationship to FamilyMember (one-to-one)
        builder.HasOne(m => m.FamilyMember)
            .WithOne()  // No back-reference on FamilyMember
            .HasForeignKey<Membership>(m => m.FamilyMemberId)
            .OnDelete(DeleteBehavior.Restrict);  // Don't cascade delete

        // Relationship to Fees (one-to-many)
        builder.HasMany(m => m.Fees)
            .WithOne(f => f.Membership)
            .HasForeignKey(f => f.MembershipId)
            .OnDelete(DeleteBehavior.Cascade);  // Delete fees when membership deleted

        // Audit fields
        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.UpdatedAt).IsRequired();

        // Table name
        builder.ToTable("memberships");
    }
}

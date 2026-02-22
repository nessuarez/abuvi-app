using Abuvi.API.Features.Registrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class RegistrationMemberConfiguration : IEntityTypeConfiguration<RegistrationMember>
{
    public void Configure(EntityTypeBuilder<RegistrationMember> builder)
    {
        builder.ToTable("registration_members");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
        builder.Property(m => m.RegistrationId).IsRequired().HasColumnName("registration_id");
        builder.Property(m => m.FamilyMemberId).IsRequired().HasColumnName("family_member_id");
        builder.Property(m => m.AgeAtCamp).IsRequired().HasColumnName("age_at_camp");
        builder.Property(m => m.AgeCategory)
            .HasConversion<string>().IsRequired().HasMaxLength(10).HasColumnName("age_category");
        builder.Property(m => m.IndividualAmount)
            .HasPrecision(10, 2).IsRequired().HasColumnName("individual_amount");
        builder.Property(m => m.CreatedAt).IsRequired().HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.HasIndex(m => new { m.RegistrationId, m.FamilyMemberId }).IsUnique()
            .HasDatabaseName("IX_RegistrationMembers_RegistrationId_FamilyMemberId");
        builder.HasIndex(m => m.RegistrationId)
            .HasDatabaseName("IX_RegistrationMembers_RegistrationId");

        builder.HasOne(m => m.FamilyMember).WithMany()
            .HasForeignKey(m => m.FamilyMemberId).OnDelete(DeleteBehavior.Restrict);
    }
}

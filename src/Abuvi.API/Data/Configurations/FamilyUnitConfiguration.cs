using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

/// <summary>
/// EF Core configuration for FamilyUnit entity
/// </summary>
public class FamilyUnitConfiguration : IEntityTypeConfiguration<FamilyUnit>
{
    public void Configure(EntityTypeBuilder<FamilyUnit> builder)
    {
        // Table name (snake_case for PostgreSQL convention)
        builder.ToTable("family_units");

        // Primary key
        builder.HasKey(fu => fu.Id);

        // Name: required, max 200
        builder.Property(fu => fu.Name)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("name");

        // Representative User ID: required FK to users table
        builder.Property(fu => fu.RepresentativeUserId)
            .IsRequired()
            .HasColumnName("representative_user_id");

        // Foreign key relationship to User
        builder.HasOne<User>()
            .WithOne()
            .HasForeignKey<FamilyUnit>(fu => fu.RepresentativeUserId)
            .OnDelete(DeleteBehavior.Restrict); // Don't cascade delete user if family unit is deleted

        // Family number: optional, unique when assigned
        builder.Property(fu => fu.FamilyNumber)
            .HasColumnName("family_number");

        builder.HasIndex(fu => fu.FamilyNumber)
            .IsUnique()
            .HasFilter("family_number IS NOT NULL");

        // Profile photo URL: optional, max 2048
        builder.Property(fu => fu.ProfilePhotoUrl)
            .HasMaxLength(2048)
            .HasColumnName("profile_photo_url");

        // Timestamps: required, default NOW()
        builder.Property(fu => fu.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(fu => fu.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");
    }
}

using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

/// <summary>
/// EF Core configuration for FamilyMember entity
/// </summary>
public class FamilyMemberConfiguration : IEntityTypeConfiguration<FamilyMember>
{
    public void Configure(EntityTypeBuilder<FamilyMember> builder)
    {
        // Table name (snake_case for PostgreSQL convention)
        builder.ToTable("family_members");

        // Primary key
        builder.HasKey(fm => fm.Id);

        // Family unit FK: required
        builder.Property(fm => fm.FamilyUnitId)
            .IsRequired()
            .HasColumnName("family_unit_id");

        // Foreign key relationship to FamilyUnit (cascade delete members when family unit is deleted)
        builder.HasOne<FamilyUnit>()
            .WithMany()
            .HasForeignKey(fm => fm.FamilyUnitId)
            .OnDelete(DeleteBehavior.Cascade);

        // User ID: optional FK to users table (for future self-access)
        builder.Property(fm => fm.UserId)
            .HasColumnName("user_id");

        // Foreign key relationship to User (optional)
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(fm => fm.UserId)
            .OnDelete(DeleteBehavior.SetNull); // Set to null if user is deleted

        // First name: required, max 100
        builder.Property(fm => fm.FirstName)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("first_name");

        // Last name: required, max 100
        builder.Property(fm => fm.LastName)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("last_name");

        // Date of birth: required
        builder.Property(fm => fm.DateOfBirth)
            .IsRequired()
            .HasColumnName("date_of_birth");

        // Relationship: stored as string, max 20
        builder.Property(fm => fm.Relationship)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("relationship");

        // NEW FIELDS

        // Document number: optional, max 50, uppercase alphanumeric
        builder.Property(fm => fm.DocumentNumber)
            .HasMaxLength(50)
            .HasColumnName("document_number");

        // Email: optional, max 255
        builder.Property(fm => fm.Email)
            .HasMaxLength(255)
            .HasColumnName("email");

        // Phone: optional, max 20, E.164 format
        builder.Property(fm => fm.Phone)
            .HasMaxLength(20)
            .HasColumnName("phone");

        // SENSITIVE ENCRYPTED FIELDS

        // Medical notes: optional, max 2000 (encrypted at rest)
        builder.Property(fm => fm.MedicalNotes)
            .HasMaxLength(2000)
            .HasColumnName("medical_notes");

        // Allergies: optional, max 1000 (encrypted at rest)
        builder.Property(fm => fm.Allergies)
            .HasMaxLength(1000)
            .HasColumnName("allergies");

        // Timestamps: required, default NOW()
        builder.Property(fm => fm.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(fm => fm.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");
    }
}

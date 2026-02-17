using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Abuvi.API.Features.Guests;

namespace Abuvi.API.Data.Configurations;

public class GuestConfiguration : IEntityTypeConfiguration<Guest>
{
    public void Configure(EntityTypeBuilder<Guest> builder)
    {
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasDefaultValueSql("gen_random_uuid()");

        // Personal data
        builder.Property(g => g.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(g => g.LastName).IsRequired().HasMaxLength(100);
        builder.Property(g => g.DateOfBirth).IsRequired();
        builder.Property(g => g.DocumentNumber).HasMaxLength(50);
        builder.Property(g => g.Email).HasMaxLength(255);
        builder.Property(g => g.Phone).HasMaxLength(20);

        // Encrypted fields - stored as text
        builder.Property(g => g.MedicalNotes).HasColumnType("text");
        builder.Property(g => g.Allergies).HasColumnType("text");

        builder.Property(g => g.IsActive).IsRequired();

        // Relationship to FamilyUnit - cascade delete
        builder.HasOne(g => g.FamilyUnit)
            .WithMany()
            .HasForeignKey(g => g.FamilyUnitId)
            .OnDelete(DeleteBehavior.Cascade);

        // Audit fields
        builder.Property(g => g.CreatedAt).IsRequired();
        builder.Property(g => g.UpdatedAt).IsRequired();

        // Indexes
        builder.HasIndex(g => g.FamilyUnitId);
        builder.HasIndex(g => g.DocumentNumber);
    }
}

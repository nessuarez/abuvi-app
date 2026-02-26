using Abuvi.API.Features.Registrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class RegistrationConfiguration : IEntityTypeConfiguration<Registration>
{
    public void Configure(EntityTypeBuilder<Registration> builder)
    {
        builder.ToTable("registrations");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");

        builder.Property(r => r.FamilyUnitId).IsRequired().HasColumnName("family_unit_id");
        builder.Property(r => r.CampEditionId).IsRequired().HasColumnName("camp_edition_id");
        builder.Property(r => r.RegisteredByUserId).IsRequired().HasColumnName("registered_by_user_id");

        builder.Property(r => r.BaseTotalAmount)
            .HasPrecision(10, 2).IsRequired().HasColumnName("base_total_amount");
        builder.Property(r => r.ExtrasAmount)
            .HasPrecision(10, 2).IsRequired().HasDefaultValue(0m).HasColumnName("extras_amount");
        builder.Property(r => r.TotalAmount)
            .HasPrecision(10, 2).IsRequired().HasColumnName("total_amount");

        builder.Property(r => r.Status)
            .HasConversion<string>().IsRequired().HasMaxLength(20).HasColumnName("status");
        builder.Property(r => r.Notes).HasMaxLength(1000).HasColumnName("notes");
        builder.Property(r => r.SpecialNeeds)
            .HasMaxLength(2000).HasColumnName("special_needs");
        builder.Property(r => r.CampatesPreference)
            .HasMaxLength(500).HasColumnName("campates_preference");

        builder.Property(r => r.CreatedAt).IsRequired().HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");
        builder.Property(r => r.UpdatedAt).IsRequired().HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        builder.HasIndex(r => new { r.FamilyUnitId, r.CampEditionId }).IsUnique()
            .HasDatabaseName("IX_Registrations_FamilyUnitId_CampEditionId");
        builder.HasIndex(r => r.CampEditionId).HasDatabaseName("IX_Registrations_CampEditionId");
        builder.HasIndex(r => r.Status).HasDatabaseName("IX_Registrations_Status");

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Registrations_TotalAmount",
            "total_amount = base_total_amount + extras_amount"));

        builder.HasOne(r => r.FamilyUnit).WithMany()
            .HasForeignKey(r => r.FamilyUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.CampEdition).WithMany()
            .HasForeignKey(r => r.CampEditionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.RegisteredByUser).WithMany()
            .HasForeignKey(r => r.RegisteredByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(r => r.Members).WithOne(m => m.Registration)
            .HasForeignKey(m => m.RegistrationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(r => r.Extras).WithOne(e => e.Registration)
            .HasForeignKey(e => e.RegistrationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(r => r.Payments).WithOne(p => p.Registration)
            .HasForeignKey(p => p.RegistrationId).OnDelete(DeleteBehavior.Restrict);
    }
}

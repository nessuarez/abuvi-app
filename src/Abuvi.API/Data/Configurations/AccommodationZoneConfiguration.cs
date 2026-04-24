using Abuvi.API.Features.Camps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class AccommodationZoneConfiguration : IEntityTypeConfiguration<AccommodationZone>
{
    public void Configure(EntityTypeBuilder<AccommodationZone> builder)
    {
        builder.ToTable("accommodation_zones");

        builder.HasKey(z => z.Id);
        builder.Property(z => z.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(z => z.CampEditionId)
            .IsRequired()
            .HasColumnName("camp_edition_id");

        builder.Property(z => z.AccommodationType)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("accommodation_type");

        builder.Property(z => z.Name)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("name");

        builder.Property(z => z.MaxCapacity)
            .HasColumnName("max_capacity");

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_AccommodationZones_MaxCapacity",
            "max_capacity IS NULL OR max_capacity > 0"));

        builder.Property(z => z.DistributionNotes)
            .HasMaxLength(500)
            .HasColumnName("distribution_notes");

        builder.Property(z => z.SortOrder)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("sort_order");

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_AccommodationZones_SortOrder",
            "sort_order >= 0"));

        builder.Property(z => z.IsActive)
            .IsRequired()
            .HasDefaultValue(true)
            .HasColumnName("is_active");

        builder.Property(z => z.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(z => z.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        builder.HasOne(z => z.CampEdition)
            .WithMany()
            .HasForeignKey(z => z.CampEditionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

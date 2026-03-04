using Abuvi.API.Features.Camps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class CampEditionAccommodationConfiguration : IEntityTypeConfiguration<CampEditionAccommodation>
{
    public void Configure(EntityTypeBuilder<CampEditionAccommodation> builder)
    {
        builder.ToTable("camp_edition_accommodations");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.CampEditionId)
            .IsRequired()
            .HasColumnName("camp_edition_id");

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("name");

        builder.Property(e => e.AccommodationType)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(30)
            .HasColumnName("accommodation_type");

        builder.Property(e => e.Description)
            .HasMaxLength(1000)
            .HasColumnName("description");

        builder.Property(e => e.Capacity)
            .HasColumnName("capacity");

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_CampEditionAccommodations_Capacity",
            "capacity IS NULL OR capacity > 0"));

        builder.Property(e => e.IsActive)
            .IsRequired()
            .HasDefaultValue(true)
            .HasColumnName("is_active");

        builder.Property(e => e.SortOrder)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("sort_order");

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_CampEditionAccommodations_SortOrder",
            "sort_order >= 0"));

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        builder.HasOne(e => e.CampEdition)
            .WithMany(ce => ce.Accommodations)
            .HasForeignKey(e => e.CampEditionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

using Abuvi.API.Features.Camps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

/// <summary>
/// EF Core configuration for Camp entity
/// </summary>
public class CampConfiguration : IEntityTypeConfiguration<Camp>
{
    public void Configure(EntityTypeBuilder<Camp> builder)
    {
        // Table name (snake_case for PostgreSQL convention)
        builder.ToTable("camps");

        // Primary key
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");

        // Name: required, max 200
        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("name");

        // Description: optional, max 2000
        builder.Property(c => c.Description)
            .HasMaxLength(2000)
            .HasColumnName("description");

        // Location: optional, max 500
        builder.Property(c => c.Location)
            .HasMaxLength(500)
            .HasColumnName("location");

        // Latitude: optional, range -90 to 90
        builder.Property(c => c.Latitude)
            .HasPrecision(9, 6)
            .HasColumnName("latitude");

        // Longitude: optional, range -180 to 180
        builder.Property(c => c.Longitude)
            .HasPrecision(9, 6)
            .HasColumnName("longitude");

        // Age-based pricing (must be >= 0)
        builder.Property(c => c.PricePerAdult)
            .HasPrecision(10, 2)
            .IsRequired()
            .HasColumnName("price_per_adult");

        builder.Property(c => c.PricePerChild)
            .HasPrecision(10, 2)
            .IsRequired()
            .HasColumnName("price_per_child");

        builder.Property(c => c.PricePerBaby)
            .HasPrecision(10, 2)
            .IsRequired()
            .HasColumnName("price_per_baby");

        // Check constraints for pricing (>= 0)
        builder.ToTable(t => t.HasCheckConstraint("CK_Camps_PricePerAdult", "price_per_adult >= 0"));
        builder.ToTable(t => t.HasCheckConstraint("CK_Camps_PricePerChild", "price_per_child >= 0"));
        builder.ToTable(t => t.HasCheckConstraint("CK_Camps_PricePerBaby", "price_per_baby >= 0"));

        // Check constraints for coordinates
        builder.ToTable(t => t.HasCheckConstraint("CK_Camps_Latitude", "latitude IS NULL OR (latitude >= -90 AND latitude <= 90)"));
        builder.ToTable(t => t.HasCheckConstraint("CK_Camps_Longitude", "longitude IS NULL OR (longitude >= -180 AND longitude <= 180)"));

        // IsActive: required, default true
        builder.Property(c => c.IsActive)
            .IsRequired()
            .HasDefaultValue(true)
            .HasColumnName("is_active");

        // Timestamps: required, default NOW()
        builder.Property(c => c.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(c => c.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        // Relationships
        builder.HasMany(c => c.Editions)
            .WithOne(e => e.Camp)
            .HasForeignKey(e => e.CampId)
            .OnDelete(DeleteBehavior.Restrict); // Cannot delete camp if editions exist
    }
}

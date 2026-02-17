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

        // Google Place ID: optional, max 255, indexed
        builder.Property(c => c.GooglePlaceId)
            .HasMaxLength(255)
            .HasColumnName("google_place_id");

        builder.HasIndex(c => c.GooglePlaceId)
            .HasDatabaseName("ix_camps_google_place_id");

        // Extended Contact Information (all nullable)
        builder.Property(c => c.FormattedAddress)
            .HasMaxLength(500)
            .HasColumnName("formatted_address");

        builder.Property(c => c.StreetAddress)
            .HasMaxLength(200)
            .HasColumnName("street_address");

        builder.Property(c => c.Locality)
            .HasMaxLength(100)
            .HasColumnName("locality");

        builder.Property(c => c.AdministrativeArea)
            .HasMaxLength(100)
            .HasColumnName("administrative_area");

        builder.Property(c => c.PostalCode)
            .HasMaxLength(20)
            .HasColumnName("postal_code");

        builder.Property(c => c.Country)
            .HasMaxLength(100)
            .HasColumnName("country");

        builder.Property(c => c.PhoneNumber)
            .HasMaxLength(30)
            .HasColumnName("phone_number");

        builder.Property(c => c.NationalPhoneNumber)
            .HasMaxLength(30)
            .HasColumnName("national_phone_number");

        builder.Property(c => c.WebsiteUrl)
            .HasMaxLength(500)
            .HasColumnName("website_url");

        builder.Property(c => c.GoogleMapsUrl)
            .HasMaxLength(500)
            .HasColumnName("google_maps_url");

        // Google Metadata (all nullable)
        builder.Property(c => c.GoogleRating)
            .HasPrecision(3, 1)
            .HasColumnName("google_rating");

        builder.Property(c => c.GoogleRatingCount)
            .HasColumnName("google_rating_count");

        builder.Property(c => c.LastGoogleSyncAt)
            .HasColumnName("last_google_sync_at");

        builder.Property(c => c.BusinessStatus)
            .HasMaxLength(50)
            .HasColumnName("business_status");

        builder.Property(c => c.PlaceTypes)
            .HasMaxLength(500)
            .HasColumnName("place_types");

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

        // Accommodation capacity JSON (nullable text column)
        builder.Property(c => c.AccommodationCapacityJson)
            .HasColumnType("text")
            .HasColumnName("accommodation_capacity_json");

        // Relationships
        builder.HasMany(c => c.Editions)
            .WithOne(e => e.Camp)
            .HasForeignKey(e => e.CampId)
            .OnDelete(DeleteBehavior.Restrict); // Cannot delete camp if editions exist
    }
}

using Abuvi.API.Features.Registrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class RegistrationAccommodationPreferenceConfiguration
    : IEntityTypeConfiguration<RegistrationAccommodationPreference>
{
    public void Configure(EntityTypeBuilder<RegistrationAccommodationPreference> builder)
    {
        builder.ToTable("registration_accommodation_preferences");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasDefaultValueSql("gen_random_uuid()")
            .HasColumnName("id");

        builder.Property(e => e.RegistrationId)
            .IsRequired()
            .HasColumnName("registration_id");

        builder.Property(e => e.CampEditionAccommodationId)
            .IsRequired()
            .HasColumnName("camp_edition_accommodation_id");

        builder.Property(e => e.PreferenceOrder)
            .IsRequired()
            .HasColumnName("preference_order");

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_RegAccommPrefs_PreferenceOrder",
            "preference_order >= 1 AND preference_order <= 3"));

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        // Unique: one accommodation per registration
        builder.HasIndex(e => new { e.RegistrationId, e.CampEditionAccommodationId })
            .IsUnique()
            .HasDatabaseName("IX_RegAccommPrefs_RegistrationId_AccommodationId");

        // Unique: one preference order per registration
        builder.HasIndex(e => new { e.RegistrationId, e.PreferenceOrder })
            .IsUnique()
            .HasDatabaseName("IX_RegAccommPrefs_RegistrationId_PreferenceOrder");

        // Relationships
        builder.HasOne(e => e.Registration)
            .WithMany(r => r.AccommodationPreferences)
            .HasForeignKey(e => e.RegistrationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.CampEditionAccommodation)
            .WithMany()
            .HasForeignKey(e => e.CampEditionAccommodationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

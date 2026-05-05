using Abuvi.API.Features.Registrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class RegistrationAccommodationNeedConfiguration
    : IEntityTypeConfiguration<RegistrationAccommodationNeed>
{
    public void Configure(EntityTypeBuilder<RegistrationAccommodationNeed> builder)
    {
        builder.ToTable("registration_accommodation_needs");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");
        builder.Property(n => n.RegistrationId)
            .IsRequired().HasColumnName("registration_id");
        builder.Property(n => n.AccommodationFeatureId)
            .IsRequired().HasColumnName("accommodation_feature_id");
        builder.Property(n => n.TaggedByUserId)
            .HasColumnName("tagged_by_user_id");
        builder.Property(n => n.CreatedAt)
            .IsRequired().HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.HasIndex(n => new { n.RegistrationId, n.AccommodationFeatureId })
            .IsUnique()
            .HasDatabaseName("IX_RegistrationAccommodationNeeds_RegistrationId_FeatureId");
        builder.HasIndex(n => n.RegistrationId)
            .HasDatabaseName("IX_RegistrationAccommodationNeeds_RegistrationId");

        builder.HasOne(n => n.Registration)
            .WithMany(r => r.AccommodationNeeds)
            .HasForeignKey(n => n.RegistrationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(n => n.AccommodationFeature)
            .WithMany()
            .HasForeignKey(n => n.AccommodationFeatureId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

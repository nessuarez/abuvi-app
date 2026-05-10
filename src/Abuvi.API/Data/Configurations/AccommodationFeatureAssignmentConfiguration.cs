using Abuvi.API.Features.Camps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class AccommodationFeatureAssignmentConfiguration
    : IEntityTypeConfiguration<AccommodationFeatureAssignment>
{
    public void Configure(EntityTypeBuilder<AccommodationFeatureAssignment> builder)
    {
        builder.ToTable("accommodation_feature_assignments");
        builder.HasKey(a => new { a.AccommodationId, a.FeatureId });
        builder.Property(a => a.AccommodationId).HasColumnName("accommodation_id");
        builder.Property(a => a.FeatureId).HasColumnName("feature_id");
        builder.Property(a => a.CreatedAt).IsRequired().HasColumnName("created_at").HasDefaultValueSql("NOW()");

        builder.HasOne(a => a.Accommodation)
            .WithMany(acc => acc.FeatureAssignments)
            .HasForeignKey(a => a.AccommodationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Feature)
            .WithMany(f => f.AccommodationAssignments)
            .HasForeignKey(a => a.FeatureId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

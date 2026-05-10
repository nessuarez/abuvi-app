using Abuvi.API.Features.Camps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class ZoneFeatureAssignmentConfiguration : IEntityTypeConfiguration<ZoneFeatureAssignment>
{
    public void Configure(EntityTypeBuilder<ZoneFeatureAssignment> builder)
    {
        builder.ToTable("zone_feature_assignments");
        builder.HasKey(a => new { a.ZoneId, a.FeatureId });
        builder.Property(a => a.ZoneId).HasColumnName("zone_id");
        builder.Property(a => a.FeatureId).HasColumnName("feature_id");
        builder.Property(a => a.CreatedAt).IsRequired().HasColumnName("created_at").HasDefaultValueSql("NOW()");

        builder.HasOne(a => a.Zone)
            .WithMany(z => z.FeatureAssignments)
            .HasForeignKey(a => a.ZoneId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Feature)
            .WithMany(f => f.ZoneAssignments)
            .HasForeignKey(a => a.FeatureId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

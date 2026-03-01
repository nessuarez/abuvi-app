using Abuvi.API.Features.Camps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class CampObservationConfiguration : IEntityTypeConfiguration<CampObservation>
{
    public void Configure(EntityTypeBuilder<CampObservation> builder)
    {
        builder.ToTable("camp_observations");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("id");

        builder.Property(o => o.CampId).IsRequired().HasColumnName("camp_id");
        builder.HasIndex(o => o.CampId).HasDatabaseName("ix_camp_observations_camp_id");

        builder.Property(o => o.Text)
            .IsRequired()
            .HasMaxLength(4000)
            .HasColumnName("text");

        builder.Property(o => o.Season).HasMaxLength(20).HasColumnName("season");
        builder.Property(o => o.CreatedByUserId).HasColumnName("created_by_user_id");

        builder.Property(o => o.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");
    }
}

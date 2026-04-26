using Abuvi.API.Features.Camps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class AccommodationFeatureConfiguration : IEntityTypeConfiguration<AccommodationFeature>
{
    public void Configure(EntityTypeBuilder<AccommodationFeature> builder)
    {
        builder.ToTable("accommodation_features");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(f => f.Name).IsRequired().HasMaxLength(100).HasColumnName("name");
        builder.HasIndex(f => f.Name).IsUnique();
        builder.Property(f => f.Icon).IsRequired().HasMaxLength(100).HasColumnName("icon");
        builder.Property(f => f.Description).HasColumnType("text").HasColumnName("description");
        builder.Property(f => f.ApplicabilityLevel).IsRequired()
            .HasConversion<string>().HasColumnName("applicability_level");
        builder.Property(f => f.IsActive).IsRequired().HasDefaultValue(true).HasColumnName("is_active");
        builder.Property(f => f.SortOrder).IsRequired().HasDefaultValue(0).HasColumnName("sort_order");
        builder.ToTable(t => t.HasCheckConstraint("CK_AccommodationFeatures_SortOrder", "sort_order >= 0"));
        builder.Property(f => f.CreatedAt).IsRequired().HasColumnName("created_at").HasDefaultValueSql("NOW()");
        builder.Property(f => f.UpdatedAt).IsRequired().HasColumnName("updated_at").HasDefaultValueSql("NOW()");
    }
}

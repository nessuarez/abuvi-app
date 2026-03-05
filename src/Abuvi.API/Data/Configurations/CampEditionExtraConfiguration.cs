using Abuvi.API.Features.Camps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

/// <summary>
/// EF Core configuration for CampEditionExtra entity
/// </summary>
public class CampEditionExtraConfiguration : IEntityTypeConfiguration<CampEditionExtra>
{
    public void Configure(EntityTypeBuilder<CampEditionExtra> builder)
    {
        // Table name (snake_case for PostgreSQL convention)
        builder.ToTable("camp_edition_extras");

        // Primary key
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        // Foreign key
        builder.Property(e => e.CampEditionId)
            .IsRequired()
            .HasColumnName("camp_edition_id");

        // Name: required, max 200
        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("name");

        // Description: optional, max 1000
        builder.Property(e => e.Description)
            .HasMaxLength(1000)
            .HasColumnName("description");

        // Price: required, must be >= 0
        builder.Property(e => e.Price)
            .HasPrecision(10, 2)
            .IsRequired()
            .HasColumnName("price");

        builder.ToTable(t => t.HasCheckConstraint("CK_CampEditionExtras_Price", "price >= 0"));

        // Pricing configuration
        builder.Property(e => e.PricingType)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("pricing_type");

        builder.Property(e => e.PricingPeriod)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("pricing_period");

        // IsRequired: required, default false
        builder.Property(e => e.IsRequired)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("is_required");

        // IsActive: required, default true
        builder.Property(e => e.IsActive)
            .IsRequired()
            .HasDefaultValue(true)
            .HasColumnName("is_active");

        // MaxQuantity: optional
        builder.Property(e => e.MaxQuantity)
            .HasColumnName("max_quantity");

        builder.ToTable(t => t.HasCheckConstraint("CK_CampEditionExtras_MaxQuantity", "max_quantity IS NULL OR max_quantity > 0"));

        // User input configuration
        builder.Property(e => e.RequiresUserInput)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("requires_user_input");

        builder.Property(e => e.UserInputLabel)
            .HasMaxLength(200)
            .HasColumnName("user_input_label");

        // Timestamps: required, default NOW()
        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        // Relationships
        builder.HasOne(e => e.CampEdition)
            .WithMany(ce => ce.Extras)
            .HasForeignKey(e => e.CampEditionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

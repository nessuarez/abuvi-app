using Abuvi.API.Features.Camps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

/// <summary>
/// EF Core configuration for CampEdition entity
/// </summary>
public class CampEditionConfiguration : IEntityTypeConfiguration<CampEdition>
{
    public void Configure(EntityTypeBuilder<CampEdition> builder)
    {
        // Table name (snake_case for PostgreSQL convention)
        builder.ToTable("camp_editions");

        // Primary key
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        // Foreign key
        builder.Property(e => e.CampId)
            .IsRequired()
            .HasColumnName("camp_id");

        // Year: required
        builder.Property(e => e.Year)
            .IsRequired()
            .HasColumnName("year");

        // Unique constraint: one edition per camp per year (except for Proposed status)
        builder.HasIndex(e => new { e.CampId, e.Year })
            .HasDatabaseName("IX_CampEditions_CampId_Year");

        // Dates
        builder.Property(e => e.StartDate)
            .IsRequired()
            .HasColumnName("start_date");

        builder.Property(e => e.EndDate)
            .IsRequired()
            .HasColumnName("end_date");

        // Age-based pricing (must be >= 0)
        builder.Property(e => e.PricePerAdult)
            .HasPrecision(10, 2)
            .IsRequired()
            .HasColumnName("price_per_adult");

        builder.Property(e => e.PricePerChild)
            .HasPrecision(10, 2)
            .IsRequired()
            .HasColumnName("price_per_child");

        builder.Property(e => e.PricePerBaby)
            .HasPrecision(10, 2)
            .IsRequired()
            .HasColumnName("price_per_baby");

        // Check constraints for pricing (>= 0)
        builder.ToTable(t => t.HasCheckConstraint("CK_CampEditions_PricePerAdult", "price_per_adult >= 0"));
        builder.ToTable(t => t.HasCheckConstraint("CK_CampEditions_PricePerChild", "price_per_child >= 0"));
        builder.ToTable(t => t.HasCheckConstraint("CK_CampEditions_PricePerBaby", "price_per_baby >= 0"));

        // Age ranges configuration
        builder.Property(e => e.UseCustomAgeRanges)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("use_custom_age_ranges");

        builder.Property(e => e.CustomBabyMaxAge)
            .HasColumnName("custom_baby_max_age");

        builder.Property(e => e.CustomChildMinAge)
            .HasColumnName("custom_child_min_age");

        builder.Property(e => e.CustomChildMaxAge)
            .HasColumnName("custom_child_max_age");

        builder.Property(e => e.CustomAdultMinAge)
            .HasColumnName("custom_adult_min_age");

        // Check constraint: if custom age ranges enabled, all must be provided
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_CampEditions_CustomAgeRanges",
            "NOT use_custom_age_ranges OR (custom_baby_max_age IS NOT NULL AND custom_child_min_age IS NOT NULL AND custom_child_max_age IS NOT NULL AND custom_adult_min_age IS NOT NULL)"));

        // Status: stored as string
        builder.Property(e => e.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("status");

        // Max capacity: optional
        builder.Property(e => e.MaxCapacity)
            .HasColumnName("max_capacity");

        // Notes: optional, max 2000
        builder.Property(e => e.Notes)
            .HasMaxLength(2000)
            .HasColumnName("notes");

        // Description: optional, long text (no max length → PostgreSQL text)
        builder.Property(e => e.Description)
            .HasColumnType("text")
            .HasColumnName("description");

        // ProposalReason: optional, max 1000 (stored for board review)
        builder.Property(e => e.ProposalReason)
            .HasMaxLength(1000)
            .HasColumnName("proposal_reason");

        // ProposalNotes: optional, max 2000 (additional context at proposal time)
        builder.Property(e => e.ProposalNotes)
            .HasMaxLength(2000)
            .HasColumnName("proposal_notes");

        // IsArchived: required, default false
        builder.Property(e => e.IsArchived)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("is_archived");

        // Timestamps: required, default NOW()
        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        // Period split point
        builder.Property(e => e.HalfDate)
            .HasColumnName("half_date")
            .HasColumnType("date");

        // Per-period (week) pricing
        builder.Property(e => e.PricePerAdultWeek)
            .HasPrecision(10, 2)
            .HasColumnName("price_per_adult_week");

        builder.Property(e => e.PricePerChildWeek)
            .HasPrecision(10, 2)
            .HasColumnName("price_per_child_week");

        builder.Property(e => e.PricePerBabyWeek)
            .HasPrecision(10, 2)
            .HasColumnName("price_per_baby_week");

        // Weekend visit window
        builder.Property(e => e.WeekendStartDate)
            .HasColumnName("weekend_start_date")
            .HasColumnType("date");

        builder.Property(e => e.WeekendEndDate)
            .HasColumnName("weekend_end_date")
            .HasColumnType("date");

        // Weekend visit pricing
        builder.Property(e => e.PricePerAdultWeekend)
            .HasPrecision(10, 2)
            .HasColumnName("price_per_adult_weekend");

        builder.Property(e => e.PricePerChildWeekend)
            .HasPrecision(10, 2)
            .HasColumnName("price_per_child_weekend");

        builder.Property(e => e.PricePerBabyWeekend)
            .HasPrecision(10, 2)
            .HasColumnName("price_per_baby_weekend");

        // Weekend visit capacity
        builder.Property(e => e.MaxWeekendCapacity)
            .HasColumnName("max_weekend_capacity");

        // Accommodation capacity JSON (nullable text column)
        builder.Property(e => e.AccommodationCapacityJson)
            .HasColumnType("text")
            .HasColumnName("accommodation_capacity_json");

        // Relationships
        builder.HasOne(e => e.Camp)
            .WithMany(c => c.Editions)
            .HasForeignKey(e => e.CampId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Extras)
            .WithOne(ex => ex.CampEdition)
            .HasForeignKey(ex => ex.CampEditionId)
            .OnDelete(DeleteBehavior.Cascade); // Delete extras when edition is deleted
    }
}

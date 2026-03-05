using Abuvi.API.Features.Registrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class RegistrationExtraConfiguration : IEntityTypeConfiguration<RegistrationExtra>
{
    public void Configure(EntityTypeBuilder<RegistrationExtra> builder)
    {
        builder.ToTable("registration_extras");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
        builder.Property(e => e.RegistrationId).IsRequired().HasColumnName("registration_id");
        builder.Property(e => e.CampEditionExtraId).IsRequired().HasColumnName("camp_edition_extra_id");
        builder.Property(e => e.Quantity).IsRequired().HasColumnName("quantity");
        builder.Property(e => e.UnitPrice).HasPrecision(10, 2).IsRequired().HasColumnName("unit_price");
        builder.Property(e => e.CampDurationDays).IsRequired().HasColumnName("camp_duration_days");
        builder.Property(e => e.TotalAmount)
            .HasPrecision(10, 2).IsRequired().HasColumnName("total_amount");
        builder.Property(e => e.UserInput)
            .HasMaxLength(500).HasColumnName("user_input");
        builder.Property(e => e.CreatedAt).IsRequired().HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.HasIndex(e => new { e.RegistrationId, e.CampEditionExtraId }).IsUnique()
            .HasDatabaseName("IX_RegistrationExtras_RegistrationId_CampEditionExtraId");

        builder.HasOne(e => e.CampEditionExtra).WithMany()
            .HasForeignKey(e => e.CampEditionExtraId).OnDelete(DeleteBehavior.Restrict);
    }
}

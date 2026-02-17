using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Abuvi.API.Features.Memberships;

namespace Abuvi.API.Data.Configurations;

public class MembershipFeeConfiguration : IEntityTypeConfiguration<MembershipFee>
{
    public void Configure(EntityTypeBuilder<MembershipFee> builder)
    {
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasDefaultValueSql("gen_random_uuid()");

        // Unique constraint: one fee per membership per year
        builder.HasIndex(f => new { f.MembershipId, f.Year }).IsUnique();

        builder.Property(f => f.Year).IsRequired();

        builder.Property(f => f.Amount)
            .HasPrecision(10, 2)  // Max 10 digits, 2 decimal places (e.g., 99999999.99)
            .IsRequired();

        builder.Property(f => f.Status)
            .HasConversion<string>()  // Store enum as string
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(f => f.PaymentReference)
            .HasMaxLength(100);  // Optional external payment reference

        // Audit fields
        builder.Property(f => f.CreatedAt).IsRequired();
        builder.Property(f => f.UpdatedAt).IsRequired();

        // Table name
        builder.ToTable("membership_fees");
    }
}

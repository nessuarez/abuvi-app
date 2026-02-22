using Abuvi.API.Features.Registrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
        builder.Property(p => p.RegistrationId).IsRequired().HasColumnName("registration_id");
        builder.Property(p => p.Amount).HasPrecision(10, 2).IsRequired().HasColumnName("amount");
        builder.ToTable(t => t.HasCheckConstraint("CK_Payments_Amount", "amount > 0"));
        builder.Property(p => p.PaymentDate).IsRequired().HasColumnName("payment_date");
        builder.Property(p => p.Method)
            .HasConversion<string>().IsRequired().HasMaxLength(20).HasColumnName("method");
        builder.Property(p => p.Status)
            .HasConversion<string>().IsRequired().HasMaxLength(20).HasColumnName("status");
        builder.Property(p => p.ExternalReference).HasMaxLength(255).HasColumnName("external_reference");
        builder.Property(p => p.CreatedAt).IsRequired().HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");
        builder.Property(p => p.UpdatedAt).IsRequired().HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        builder.HasIndex(p => p.RegistrationId).HasDatabaseName("IX_Payments_RegistrationId");
        builder.HasIndex(p => p.Status).HasDatabaseName("IX_Payments_Status");
    }
}

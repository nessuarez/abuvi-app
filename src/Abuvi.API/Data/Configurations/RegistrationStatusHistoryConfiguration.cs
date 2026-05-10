using Abuvi.API.Features.Registrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class RegistrationStatusHistoryConfiguration : IEntityTypeConfiguration<RegistrationStatusHistory>
{
    public void Configure(EntityTypeBuilder<RegistrationStatusHistory> builder)
    {
        builder.ToTable("registration_status_history");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id)
            .HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");

        builder.Property(h => h.RegistrationId).IsRequired().HasColumnName("registration_id");
        builder.Property(h => h.PreviousStatus)
            .HasConversion<string>().IsRequired().HasMaxLength(30).HasColumnName("previous_status");
        builder.Property(h => h.NewStatus)
            .HasConversion<string>().IsRequired().HasMaxLength(30).HasColumnName("new_status");
        builder.Property(h => h.ChangedByUserId).HasColumnName("changed_by_user_id");
        builder.Property(h => h.ChangedAt).IsRequired().HasColumnName("changed_at");
        builder.Property(h => h.Trigger)
            .HasConversion<string>().IsRequired().HasMaxLength(20).HasColumnName("trigger");
        builder.Property(h => h.Notes).HasMaxLength(1000).HasColumnName("notes");

        builder.HasIndex(h => h.RegistrationId)
            .HasDatabaseName("IX_RegistrationStatusHistory_RegistrationId");

        builder.HasOne(h => h.Registration).WithMany(r => r.StatusHistory)
            .HasForeignKey(h => h.RegistrationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(h => h.ChangedByUser).WithMany()
            .HasForeignKey(h => h.ChangedByUserId).OnDelete(DeleteBehavior.SetNull);
    }
}

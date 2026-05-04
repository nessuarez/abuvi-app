using Abuvi.API.Features.Registrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class RegistrationFriendLinkConfiguration
    : IEntityTypeConfiguration<RegistrationFriendLink>
{
    public void Configure(EntityTypeBuilder<RegistrationFriendLink> builder)
    {
        builder.ToTable("registration_friend_links");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");
        builder.Property(l => l.RegistrationId)
            .IsRequired().HasColumnName("registration_id");
        builder.Property(l => l.LinkedRegistrationId)
            .IsRequired().HasColumnName("linked_registration_id");
        builder.Property(l => l.CreatedByUserId)
            .HasColumnName("created_by_user_id");
        builder.Property(l => l.CreatedAt)
            .IsRequired().HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.HasIndex(l => new { l.RegistrationId, l.LinkedRegistrationId })
            .IsUnique()
            .HasDatabaseName("IX_RegistrationFriendLinks_RegistrationId_LinkedId");
        builder.HasIndex(l => l.RegistrationId)
            .HasDatabaseName("IX_RegistrationFriendLinks_RegistrationId");

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_RegistrationFriendLinks_NoSelfLink",
            "registration_id <> linked_registration_id"));

        builder.HasOne(l => l.Registration)
            .WithMany(r => r.FriendLinks)
            .HasForeignKey(l => l.RegistrationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(l => l.LinkedRegistration)
            .WithMany()
            .HasForeignKey(l => l.LinkedRegistrationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

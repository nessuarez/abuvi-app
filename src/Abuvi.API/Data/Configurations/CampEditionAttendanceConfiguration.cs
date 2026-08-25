using Abuvi.API.Features.Camps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class CampEditionAttendanceConfiguration : IEntityTypeConfiguration<CampEditionAttendance>
{
    public void Configure(EntityTypeBuilder<CampEditionAttendance> builder)
    {
        builder.ToTable("camp_edition_attendances");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");

        builder.Property(a => a.CampEditionId)
            .IsRequired()
            .HasColumnName("camp_edition_id");

        builder.Property(a => a.UserId)
            .IsRequired()
            .HasColumnName("user_id");

        builder.Property(a => a.FamilyMemberId)
            .HasColumnName("family_member_id");

        builder.Property(a => a.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        // Guards duplicates when declaring on behalf of a family member.
        //
        // NOTE: in PostgreSQL a NULL does not collide in a unique index, so this does NOT
        // prevent (edition, user, NULL) being inserted twice. Self-declarations are guarded
        // by a partial unique index declared as raw SQL in the migration.
        builder.HasIndex(a => new { a.CampEditionId, a.UserId, a.FamilyMemberId })
            .IsUnique()
            .HasDatabaseName("ux_camp_edition_attendances_edition_user_member");

        // Backs the personal timeline ("has estado en 14 campamentos").
        builder.HasIndex(a => a.UserId)
            .HasDatabaseName("ix_camp_edition_attendances_user_id");

        // Relationships
        builder.HasOne(a => a.CampEdition)
            .WithMany()
            .HasForeignKey(a => a.CampEditionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.FamilyMember)
            .WithMany()
            .HasForeignKey(a => a.FamilyMemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

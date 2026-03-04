using Abuvi.API.Features.Memories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

public class MemoryConfiguration : IEntityTypeConfiguration<Memory>
{
    public void Configure(EntityTypeBuilder<Memory> builder)
    {
        builder.ToTable("memories");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");

        builder.Property(m => m.AuthorUserId)
            .IsRequired()
            .HasColumnName("author_user_id");

        builder.Property(m => m.Title)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("title");

        builder.Property(m => m.Content)
            .IsRequired()
            .HasColumnName("content");

        builder.Property(m => m.Year)
            .HasColumnName("year");

        builder.Property(m => m.CampLocationId)
            .HasColumnName("camp_location_id");

        builder.Property(m => m.IsPublished)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("is_published");

        builder.Property(m => m.IsApproved)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("is_approved");

        builder.Property(m => m.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(m => m.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        // Indexes
        builder.HasIndex(m => m.AuthorUserId)
            .HasDatabaseName("ix_memories_author_user_id");

        builder.HasIndex(m => m.Year)
            .HasDatabaseName("ix_memories_year");

        builder.HasIndex(m => new { m.IsApproved, m.IsPublished })
            .HasDatabaseName("ix_memories_approved_published");

        // Relationships
        builder.HasOne(m => m.Author)
            .WithMany()
            .HasForeignKey(m => m.AuthorUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
